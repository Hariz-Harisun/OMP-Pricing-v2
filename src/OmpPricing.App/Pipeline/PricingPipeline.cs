using OmpPricing.App.Excel;
using OmpPricing.App.Pricing;
using OmpPricing.App.Providers;
using OmpPricing.App.Rules;
using Serilog;

namespace OmpPricing.App.Pipeline;

public sealed record PipelineResult(
    int LineCount,
    IReadOnlyDictionary<LineStatus, int> StatusCounts,
    string OutputPath,
    int LookupCount);

/// <summary>
/// The one entry point (AGENTS.md): read a workbook from an <see cref="IInputSource"/>, price every
/// line, write the found offers into the sender's workbook, hand it to an <see cref="IResultSink"/>.
/// It knows nothing about files, mail or any distributor.
/// </summary>
public sealed class PricingPipeline
{
    private readonly FakePricer _pricer;
    private readonly ExcelOptions _excel;
    private readonly PackagingMap _packaging;
    private readonly CountryList _countries;
    private readonly ILogger _logger;

    public PricingPipeline(
        FakePricer pricer,
        ExcelOptions excel,
        PackagingMap packaging,
        CountryList countries,
        ILogger logger)
    {
        _pricer = pricer;
        _excel = excel;
        _packaging = packaging;
        _countries = countries;
        _logger = logger;
    }

    public async Task<PipelineResult> RunAsync(
        IInputSource input,
        IResultSink sink,
        CancellationToken cancellationToken)
    {
        var source = await input.OpenAsync(cancellationToken);

        using var loaded = WorkbookReader.Load(source.Content, _excel);
        var lines = loaded.Lines;

        _logger.Information(
            "Read {Rows} data rows from sheet {Sheet}",
            lines.Count,
            loaded.Sheet.Name);

        var cache = new Dictionary<string, IReadOnlyList<Offer>>(StringComparer.Ordinal);

        foreach (var line in lines)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Generic parametric rows and unusable input never reach a provider (A9.2, A6 rule 1).
            if (line.Status is LineStatus.NeedsReview or LineStatus.InvalidInput)
            {
                continue;
            }

            // One lookup serves every row with the same MFG + MPN (A6 rule 1, A9.10).
            var key = line.Mfg.ToUpperInvariant() + "|" + line.Mpn;
            if (!cache.TryGetValue(key, out var offers))
            {
                offers = await _pricer.GetOffersAsync(line, cancellationToken);
                cache[key] = offers;
            }

            line.Offers.AddRange(offers);
            line.SelectedOffer = offers
                .OrderBy(offer => offer.PriceBreaks.Count > 0 ? offer.PriceBreaks[0].UnitPrice : decimal.MaxValue)
                .FirstOrDefault();

            if (line.SelectedOffer is null)
            {
                line.Status = LineStatus.NotFound;
                line.Remarks = "Not found";
                continue;
            }

            ColumnFiller.Fill(line, _excel, _packaging, _countries);
            line.Status = line.MissingColumns.Count == 0 ? LineStatus.Priced : LineStatus.Incomplete;
        }

        var content = WorkbookWriter.Write(loaded.Workbook, loaded.Sheet, lines, _excel);
        content = ValidationGuard.Ensure(content, _excel, _countries.Count);

        var outputPath = await sink.SaveAsync(new InputWorkbook(source.FileName, content), cancellationToken);

        var statusCounts = lines
            .GroupBy(line => line.Status)
            .ToDictionary(group => group.Key, group => group.Count());

        _logger.Information("Wrote {Path}", outputPath);

        return new PipelineResult(lines.Count, statusCounts, outputPath, _pricer.LookupCount);
    }
}

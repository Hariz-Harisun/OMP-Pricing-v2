using OmpPricing.App.Excel;
using OmpPricing.App.Pricing;
using OmpPricing.App.Providers;
using OmpPricing.App.Rules;
using Serilog;

namespace OmpPricing.App.Pipeline;

public sealed record PipelineResult(
    int LineCount,
    IReadOnlyDictionary<LineStatus, int> StatusCounts,
    IReadOnlyDictionary<string, int> ProviderCalls,
    string OutputPath);

/// <summary>
/// The one entry point (AGENTS.md): read a workbook from an <see cref="IInputSource"/>, ask every
/// enabled provider, match and choose an offer, write the result into the sender's workbook, hand it
/// to an <see cref="IResultSink"/>. It knows nothing about files, mail or any distributor.
/// </summary>
public sealed class PricingPipeline
{
    private readonly IReadOnlyList<IPriceProvider> _providers;
    private readonly ExcelOptions _excel;
    private readonly PackagingMap _packaging;
    private readonly CountryList _countries;
    private readonly PartMatcher _matcher;
    private readonly MatchCache _cache;
    private readonly ILogger _logger;

    public PricingPipeline(
        IReadOnlyList<IPriceProvider> providers,
        ExcelOptions excel,
        PackagingMap packaging,
        CountryList countries,
        PartMatcher matcher,
        MatchCache cache,
        ILogger logger)
    {
        _providers = providers;
        _excel = excel;
        _packaging = packaging;
        _countries = countries;
        _matcher = matcher;
        _cache = cache;
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

        _logger.Information("Read {Rows} data rows from sheet {Sheet}", lines.Count, loaded.Sheet.Name);

        if (_providers.Count == 0)
        {
            _logger.Warning("No price provider is enabled: quoteable lines stay NotFound, no price is invented");
        }

        var providerCalls = new SortedDictionary<string, int>(StringComparer.Ordinal);

        // One lookup serves every row with the same MFG + MPN, across all providers (A6 rule 1, A9.10).
        var resultCache = new Dictionary<string, ProviderResult>(StringComparer.Ordinal);

        foreach (var line in lines)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Generic parametric rows and unusable input never reach a provider (A9.2, A6 rule 1).
            if (line.Status is LineStatus.NeedsReview or LineStatus.InvalidInput)
            {
                continue;
            }

            if (_providers.Count == 0)
            {
                line.Status = LineStatus.NotFound;
                line.Remarks = "No price provider configured";
                continue;
            }

            await PriceLineAsync(line, providerCalls, resultCache, cancellationToken);
        }

        var content = WorkbookWriter.Write(loaded.Workbook, loaded.Sheet, lines, _excel);
        content = ValidationGuard.Ensure(content, _excel, _countries.Count);

        var outputPath = await sink.SaveAsync(new InputWorkbook(source.FileName, content), cancellationToken);

        var statusCounts = lines
            .GroupBy(line => line.Status)
            .ToDictionary(group => group.Key, group => group.Count());

        _logger.Information("Wrote {Path}", outputPath);

        return new PipelineResult(lines.Count, statusCounts, providerCalls, outputPath);
    }

    private async Task PriceLineAsync(
        QuoteLine line,
        SortedDictionary<string, int> providerCalls,
        Dictionary<string, ProviderResult> resultCache,
        CancellationToken cancellationToken)
    {
        var requested = new PartQuery
        {
            Mpn = line.Mpn,
            Mfg = line.Mfg,
            WildcardPrefix = line.WildcardPrefix,
            Qty = line.Qty ?? 1,
        };

        var found = new List<(Offer Offer, MatchLevel Level)>();
        var problems = new List<string>();
        var throttled = false;
        var ambiguous = false;

        foreach (var provider in _providers)
        {
            var cached = _cache.Find(line.Mfg, line.Mpn, provider.Name);
            var query = cached is null ? requested : requested with { ProviderPartNumber = cached.ProviderPartNumber };

            var cacheKey = provider.Name + "|" + line.Mfg.ToUpperInvariant() + "|" + line.Mpn;

            if (!resultCache.TryGetValue(cacheKey, out var result))
            {
                try
                {
                    result = await provider.FindAsync(query, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    result = ProviderResult.Error(exception.Message);
                }

                resultCache[cacheKey] = result;

                var tallyKey = provider.Name + " " + result.Status;
                providerCalls[tallyKey] = providerCalls.TryGetValue(tallyKey, out var count) ? count + 1 : 1;
            }

            switch (result.Status)
            {
                case ProviderStatus.Throttled:
                    throttled = true;
                    break;
                case ProviderStatus.Ambiguous:
                    ambiguous = true;
                    problems.Add(provider.Name + ": " + (result.Message ?? result.Status.ToString()));
                    break;
                case ProviderStatus.Error:
                    problems.Add(provider.Name + ": " + (result.Message ?? result.Status.ToString()));
                    break;
            }

            foreach (var offer in result.Offers)
            {
                if (offer.IsContractPrice)
                {
                    continue;
                }

                var level = _matcher.Classify(requested, offer);
                if (level == MatchLevel.None)
                {
                    continue;
                }

                found.Add((offer, level));

                if (_matcher.IsAccepted(level) && !string.IsNullOrWhiteSpace(offer.ProviderPartNumber))
                {
                    _cache.Record(line.Mfg, line.Mpn, provider.Name, offer.ProviderPartNumber, level);
                }
            }
        }

        line.Offers.AddRange(found.Select(candidate => candidate.Offer));

        var accepted = found
            .Where(candidate => _matcher.IsAccepted(candidate.Level))
            .Select(candidate => candidate.Offer)
            .ToList();

        if (accepted.Count == 0)
        {
            if (found.Count > 0)
            {
                line.Status = LineStatus.NeedsReview;
                line.Remarks = "Needs review: candidate " + string.Join(
                    ", ",
                    found.Select(candidate => candidate.Offer.Mpn).Distinct(StringComparer.Ordinal));
            }
            else if (throttled)
            {
                line.Status = LineStatus.WaitingForQuota;
                line.Remarks = "Waiting for provider quota";
            }
            else if (ambiguous)
            {
                line.Status = LineStatus.NeedsReview;
                line.Remarks = "Needs review: " + string.Join("; ", problems);
            }
            else
            {
                line.Status = LineStatus.NotFound;
                line.Remarks = problems.Count > 0 ? "Not found (" + string.Join("; ", problems) + ")" : "Not found";
            }

            return;
        }

        // Step 3 replaces this with the A6 rules 3 and 4 selection; Step 2 only needs one offer.
        line.SelectedOffer = accepted
            .OrderBy(offer => offer.PriceBreaks.Count > 0
                ? offer.PriceBreaks.Min(priceBreak => priceBreak.UnitPrice)
                : decimal.MaxValue)
            .First();

        ColumnFiller.Fill(line, _excel, _packaging, _countries);
        line.Status = line.MissingColumns.Count == 0 ? LineStatus.Priced : LineStatus.Incomplete;

        if (problems.Count > 0)
        {
            AddRemark(line, string.Join("; ", problems));
        }
    }

    private void AddRemark(QuoteLine line, string note)
    {
        var remarks = string.IsNullOrWhiteSpace(line.Remarks) ? note : line.Remarks + "; " + note;
        line.Remarks = remarks;
        line.Filled[_excel.Columns.Remarks] = remarks;
    }
}

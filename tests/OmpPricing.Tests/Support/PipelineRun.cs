using ClosedXML.Excel;
using OmpPricing.App.Excel;
using OmpPricing.App.Pipeline;
using OmpPricing.App.Pricing;
using OmpPricing.App.Providers;
using OmpPricing.App.Rules;
using Serilog;

namespace OmpPricing.Tests.Support;

/// <summary>Runs the real pipeline on the client sample into a temp folder, with scripted providers.</summary>
public sealed class PipelineRun : IDisposable
{
    private PipelineRun(string folder, PipelineResult result, MatchCache cache, FakeProvider? provider)
    {
        OutputFolder = folder;
        Result = result;
        Cache = cache;
        Provider = provider;
    }

    public string OutputFolder { get; }

    public PipelineResult Result { get; }

    public MatchCache Cache { get; }

    /// <summary>The scripted provider, when one was passed, so tests can assert its calls.</summary>
    public FakeProvider? Provider { get; }

    public IReadOnlyList<PartQuery> Calls() => Provider?.Calls ?? [];

    public byte[] OutputBytes => File.ReadAllBytes(Result.OutputPath);

    public XLWorkbook OpenOutput() => new(new MemoryStream(OutputBytes));

    public static PipelineRun Execute(params IPriceProvider[] providers)
    {
        var folder = Sample.NewTempFolder();
        var countries = Sample.Countries;
        var matcher = new PartMatcher(
            SuffixRules.Load(Sample.RulesFile("suffix-rules.json")),
            MfgAliases.Load(Sample.RulesFile("mfg-aliases.json")));

        var cache = MatchCache.Empty;
        var logger = new LoggerConfiguration().MinimumLevel.Fatal().CreateLogger();

        var pipeline = new PricingPipeline(
            providers,
            new ExcelOptions(),
            new PackagingMap(),
            countries,
            matcher,
            cache,
            logger);

        var result = pipeline
            .RunAsync(new FileInputSource(Sample.SamplePath), new FolderResultSink(folder), CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        var fake = providers.OfType<FakeProvider>().FirstOrDefault();
        return new PipelineRun(folder, result, cache, fake);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(OutputFolder, true);
        }
        catch (IOException)
        {
            // A locked file must not fail the suite.
        }
    }
}

using Microsoft.Extensions.Configuration;
using OmpPricing.App.Excel;
using OmpPricing.App.Pipeline;
using OmpPricing.App.Pricing;
using OmpPricing.App.Providers;
using OmpPricing.App.Rules;
using Serilog;

namespace OmpPricing.App;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        CommandLineOptions options;
        try
        {
            options = CommandLine.Parse(args);
        }
        catch (ArgumentException exception)
        {
            Console.Error.WriteLine(exception.Message);
            Console.Error.WriteLine(CommandLine.Usage);
            return 1;
        }

        if (options.Help)
        {
            Console.WriteLine(CommandLine.Usage);
            return 0;
        }

        if (options.File is null)
        {
            Console.Error.WriteLine("--file <path.xlsx> is required.");
            Console.Error.WriteLine(CommandLine.Usage);
            return 1;
        }

        var configuration = BuildConfiguration();
        var excel = configuration.GetSection("Omp:Excel").Get<ExcelOptions>() ?? new ExcelOptions();
        var rulesFolder = ResolveRulesFolder(configuration["Omp:RulesFolder"] ?? "rules");
        var outputFolder = Path.GetFullPath(configuration["Omp:OutputFolder"] ?? "output");
        var stateFolder = Path.GetFullPath(configuration["Omp:StateFolder"] ?? "state");
        var packagingMap = ReadPackagingMap(configuration);

        Log.Logger = CreateLogger();

        try
        {
            Log.Information("OmpPricing start, input {File}", options.File);

            if (options.ExportCountries)
            {
                var content = await File.ReadAllBytesAsync(options.File);
                var names = CountryListExporter.FromWorkbook(content, excel);
                var target = Path.Combine(rulesFolder, "countries.txt");
                CountryList.Save(names, target);
                Log.Information("Wrote {Count} country names to {Path}", names.Count, target);
                return 0;
            }

            if (options.Packaging is not null)
            {
                Log.Information(
                    "Packaging filter {Packaging} noted; it is applied when the selector lands in Step 3",
                    options.Packaging);
            }

            var countries = CountryList.Load(Path.Combine(rulesFolder, "countries.txt"));
            var suffixes = SuffixRules.Load(Path.Combine(rulesFolder, "suffix-rules.json"));
            var aliases = MfgAliases.Load(Path.Combine(rulesFolder, "mfg-aliases.json"));

            // Loaded and validated at startup: a COO country that is not in countries.txt stops the run
            // here instead of producing a value the dropdown would reject. Columns 15-17 and 21 are
            // filled from these tables in Step 3.
            var supplierTerms = SupplierTermsTable.Load(Path.Combine(rulesFolder, "supplier-terms.json"));
            var cooRules = CooRulesTable.Load(Path.Combine(rulesFolder, "coo-rules.json"), countries);
            Log.Information(
                "Rules: {Countries} countries, {Suffixes} suffixes, {Aliases} manufacturer aliases, {Terms} supplier-term rows, {Coo} COO rows",
                countries.Count,
                suffixes.Count,
                aliases.Count,
                supplierTerms.Count,
                cooRules.Count);

            var providers = ProviderRegistry.FromAssembly().Build(configuration);
            if (providers.Count == 0)
            {
                Log.Warning("No provider is enabled in configuration; quoteable rows will be NotFound");
            }
            else
            {
                Log.Information("Providers enabled: {Providers}", string.Join(", ", providers.Select(provider => provider.Name)));
            }

            var cachePath = Path.Combine(stateFolder, "match-cache.json");
            var cache = MatchCache.Load(cachePath);

            var pipeline = new PricingPipeline(
                providers,
                excel,
                new PackagingMap(packagingMap),
                countries,
                new PartMatcher(suffixes, aliases),
                cache,
                Log.Logger);

            var result = await pipeline.RunAsync(
                new FileInputSource(options.File),
                new FolderResultSink(outputFolder),
                CancellationToken.None);

            cache.Save(cachePath);

            Log.Information("Data rows: {Rows}", result.LineCount);
            foreach (var (status, count) in result.StatusCounts.OrderBy(pair => pair.Key.ToString(), StringComparer.Ordinal))
            {
                Log.Information("  {Status}: {Count}", status, count);
            }

            foreach (var (key, count) in result.ProviderCalls)
            {
                Log.Information("  provider {Provider}: {Count}", key, count);
            }

            Log.Information("Match cache: {Count} decisions in {Path}", cache.Count, cachePath);
            Log.Information("Output: {Output}", result.OutputPath);
            return 0;
        }
        catch (Exception exception) when (exception is InvalidOperationException or FileNotFoundException)
        {
            Log.Error("{Message}", exception.Message);
            return 1;
        }
        catch (Exception exception)
        {
            Log.Error(exception, "OmpPricing failed");
            return 1;
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }

    private static IConfiguration BuildConfiguration()
    {
        return new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddEnvironmentVariables("OMP_")
            .AddUserSecrets(typeof(Program).Assembly, optional: true)
            .Build();
    }

    private static ILogger CreateLogger()
    {
        var logFolder = Path.Combine(AppContext.BaseDirectory, "logs");
        Directory.CreateDirectory(logFolder);

        return new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                Path.Combine(logFolder, "omp-pricing-.log"),
                rollingInterval: RollingInterval.Day,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }

    private static IReadOnlyDictionary<string, string> ReadPackagingMap(IConfiguration configuration)
    {
        return configuration.GetSection("Omp:PackagingMap")
            .GetChildren()
            .Where(child => child.Value is not null)
            .ToDictionary(child => child.Key, child => child.Value!);
    }

    /// <summary>
    /// Rule files live in the repo's rules/ folder. Fall back to the copy next to the app so the
    /// published build still finds them.
    /// </summary>
    private static string ResolveRulesFolder(string folder)
    {
        if (Path.IsPathRooted(folder))
        {
            return folder;
        }

        var fromWorkingDirectory = Path.GetFullPath(folder);
        if (Directory.Exists(fromWorkingDirectory))
        {
            return fromWorkingDirectory;
        }

        var nextToApp = Path.Combine(AppContext.BaseDirectory, folder);
        return Directory.Exists(nextToApp) ? nextToApp : fromWorkingDirectory;
    }
}

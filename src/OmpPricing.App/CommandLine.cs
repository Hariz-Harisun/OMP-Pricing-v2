namespace OmpPricing.App;

public sealed class CommandLineOptions
{
    public string? File { get; init; }

    public string? Packaging { get; init; }

    public bool ExportCountries { get; init; }

    public string? ProbeProvider { get; init; }

    public string? ProbeMpn { get; init; }

    /// <summary>Manufacturer for the probe, so the duplicate-product retry can be exercised.</summary>
    public string? ProbeMfg { get; init; }

    public int Qty { get; init; } = 1;

    public bool NoCache { get; init; }

    public bool Help { get; init; }

    public string? Sheet { get; init; }
}

/// <summary>Parse of OmpPricing.App arguments. No library, no magic: a few options and one dev switch.</summary>
public static class CommandLine
{
    public const string Usage = """
        Usage: OmpPricing.App --file <path.xlsx> [--packaging CT] [--sheet <name>] [--no-cache]
               OmpPricing.App --probe <provider> <MPN> [--mfg <name>] [--qty N]
               OmpPricing.App --file <path.xlsx> --export-countries

          --file <path.xlsx>      parts list to process; the result is written to the output folder
          --packaging <code>      restrict offers to a packaging short code (applied in Step 3)
          --sheet <name>          Excel sheet name to process (overrides appsettings.json)
          --no-cache              do not read or write state/match-cache.json for this run
          --probe <provider> <MPN>
                                  call one real provider for one part, print the normalized offers
                                  and save the raw JSON to state/probe/ (credentials removed)
          --mfg <name>            manufacturer for --probe, which also drives the duplicate-product
                                  retry (DigiKey keys the manufacturerId on it)
          --qty <n>               quantity for --probe (default 1)
          --export-countries      regenerate rules/countries.txt from Sheet1 of the --file workbook
          --help                  show this text
        """;

    public static CommandLineOptions Parse(string[] args)
    {
        string? file = null;
        string? packaging = null;
        string? probeProvider = null;
        string? probeMpn = null;
        string? probeMfg = null;
        var exportCountries = false;
        var noCache = false;
        var qty = 1;
        var help = false;
        string? sheet = null;

        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];
            switch (argument)
            {
                case "--file":
                case "-f":
                    file = RequireValue(args, ref index, argument);
                    break;
                case "--packaging":
                    packaging = RequireValue(args, ref index, argument);
                    break;
                case "--sheet":
                    sheet = RequireValue(args, ref index, argument);
                    break;
                case "--probe":
                    probeProvider = RequireValue(args, ref index, argument);
                    probeMpn = RequireValue(args, ref index, argument);
                    break;
                case "--mfg":
                    probeMfg = RequireValue(args, ref index, argument);
                    break;
                case "--qty":
                    qty = RequireQty(RequireValue(args, ref index, argument));
                    break;
                case "--no-cache":
                    noCache = true;
                    break;
                case "--export-countries":
                    exportCountries = true;
                    break;
                case "--help":
                case "-h":
                    help = true;
                    break;
                default:
                    throw new ArgumentException("Unknown argument: " + argument);
            }
        }

        return new CommandLineOptions
        {
            File = file,
            Packaging = packaging,
            ExportCountries = exportCountries,
            ProbeProvider = probeProvider,
            ProbeMpn = probeMpn,
            ProbeMfg = probeMfg,
            Qty = qty,
            NoCache = noCache,
            Help = help,
            Sheet = sheet,
        };
    }

    private static string RequireValue(string[] args, ref int index, string name)
    {
        if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
        {
            throw new ArgumentException("Missing value for " + name);
        }

        index++;
        return args[index];
    }

    private static int RequireQty(string value)
    {
        if (!int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var qty)
            || qty <= 0)
        {
            throw new ArgumentException("--qty must be a positive whole number");
        }

        return qty;
    }
}

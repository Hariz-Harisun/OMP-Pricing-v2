namespace OmpPricing.App;

public sealed class CommandLineOptions
{
    public string? File { get; init; }

    public string? Packaging { get; init; }

    public bool ExportCountries { get; init; }

    public bool Help { get; init; }
}

/// <summary>Parse of OmpPricing.App arguments. No library, no magic: two options and one dev switch.</summary>
public static class CommandLine
{
    public const string Usage = """
        Usage: OmpPricing.App --file <path.xlsx> [--packaging CT] [--export-countries]

          --file <path.xlsx>   parts list to process; the result is written to the output folder
          --packaging <code>   restrict offers to a packaging short code (applied in Step 3)
          --export-countries   regenerate rules/countries.txt from Sheet1 of the --file workbook
          --help               show this text
        """;

    public static CommandLineOptions Parse(string[] args)
    {
        string? file = null;
        string? packaging = null;
        var exportCountries = false;
        var help = false;

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
            Help = help,
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
}

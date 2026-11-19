namespace OmpPricing.App.Rules;

/// <summary>
/// One row of rules/coo-rules.json: the country of origin for a manufacturer, optionally narrowed to
/// a series. Schema documented in docs/decisions.md.
/// </summary>
public sealed class CooRule
{
    public string Manufacturer { get; set; } = "";

    public string? Series { get; set; }

    public string Country { get; set; } = "";
}

/// <summary>
/// rules/coo-rules.json. The country must exist in countries.txt with exactly that spelling (A9.6),
/// otherwise the app stops at startup instead of writing a value the dropdown would reject.
/// Step 2 loads and validates it; Step 3 fills column 21 from it.
/// </summary>
public sealed class CooRulesTable
{
    private CooRulesTable(List<CooRule> entries) => Entries = entries;

    public IReadOnlyList<CooRule> Entries { get; }

    public int Count => Entries.Count;

    public static CooRulesTable Empty => new([]);

    public static CooRulesTable Load(string path, CountryList countries)
    {
        var entries = RuleFile.Load<CooRule>(path);

        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Manufacturer))
            {
                throw new InvalidOperationException(
                    "rules/coo-rules.json needs a manufacturer on every entry: " + path);
            }

            if (string.IsNullOrWhiteSpace(entry.Country))
            {
                throw new InvalidOperationException(
                    "rules/coo-rules.json needs a country for manufacturer " + entry.Manufacturer + ": " + path);
            }

            if (!countries.Contains(entry.Country))
            {
                throw new InvalidOperationException(
                    "rules/coo-rules.json country '" + entry.Country + "' for manufacturer "
                    + entry.Manufacturer + " is not in rules/countries.txt. Use the exact Sheet1 spelling.");
            }

            entry.Country = countries.Canonical(entry.Country)!;
            entry.Series = string.IsNullOrWhiteSpace(entry.Series) ? null : entry.Series.Trim();
        }

        return new CooRulesTable(entries);
    }
}

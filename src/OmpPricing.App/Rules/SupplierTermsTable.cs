namespace OmpPricing.App.Rules;

/// <summary>
/// One row of rules/supplier-terms.json. Columns 15, 16 and 17 come from here because no distributor
/// API returns them (A6 rule 6). Schema documented in docs/decisions.md.
/// </summary>
public sealed class SupplierTerm
{
    public string Provider { get; set; } = "";

    /// <summary>Null means "the default for this provider"; a manufacturer row wins over it.</summary>
    public string? Manufacturer { get; set; }

    public int CancellationWeeks { get; set; }

    public int RescheduleWeeks { get; set; }

    /// <summary>DB (delivery based) or PB (PO based).</summary>
    public string Basis { get; set; } = "";
}

/// <summary>
/// rules/supplier-terms.json, maintained by purchasing. Step 2 loads and validates it; Step 3 fills
/// columns 15, 16 and 17 from it.
/// </summary>
public sealed class SupplierTermsTable
{
    private SupplierTermsTable(List<SupplierTerm> entries) => Entries = entries;

    public IReadOnlyList<SupplierTerm> Entries { get; }

    public int Count => Entries.Count;

    public static SupplierTermsTable Empty => new([]);

    public static SupplierTermsTable Load(string path)
    {
        var entries = RuleFile.Load<SupplierTerm>(path);

        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Provider))
            {
                throw new InvalidOperationException(
                    "rules/supplier-terms.json needs a provider on every entry: " + path);
            }

            if (entry.CancellationWeeks < 0 || entry.RescheduleWeeks < 0)
            {
                throw new InvalidOperationException(
                    "rules/supplier-terms.json needs non-negative week values for provider " + entry.Provider
                    + ": " + path);
            }

            var basis = (entry.Basis ?? "").Trim().ToUpperInvariant();
            if (basis != "DB" && basis != "PB")
            {
                throw new InvalidOperationException(
                    "rules/supplier-terms.json must use basis DB or PB for provider " + entry.Provider
                    + ", found '" + entry.Basis + "': " + path);
            }

            entry.Basis = basis;
            entry.Manufacturer = string.IsNullOrWhiteSpace(entry.Manufacturer) ? null : entry.Manufacturer.Trim();
        }

        return new SupplierTermsTable(entries);
    }
}

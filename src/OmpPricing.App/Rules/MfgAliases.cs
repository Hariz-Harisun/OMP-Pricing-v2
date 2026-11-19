namespace OmpPricing.App.Rules;

public sealed class MfgAliasEntry
{
    public string Canonical { get; set; } = "";

    public List<string> Aliases { get; set; } = [];
}

/// <summary>
/// Manufacturer aliases from rules/mfg-aliases.json (A6 rule 2): Murata, Murata Electronics and
/// Murata Manufacturing are the same manufacturer for matching.
/// </summary>
public sealed class MfgAliases
{
    private readonly List<MfgAliasEntry> _entries;

    private MfgAliases(List<MfgAliasEntry> entries) => _entries = entries;

    public static MfgAliases Empty => new([]);

    public int Count => _entries.Count;

    public static MfgAliases Load(string path)
    {
        var entries = RuleFile.Load<MfgAliasEntry>(path);

        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Canonical) || entry.Aliases.Count == 0)
            {
                throw new InvalidOperationException(
                    "rules/mfg-aliases.json needs a canonical name and at least one alias on every entry: " + path);
            }
        }

        return new MfgAliases(entries);
    }

    /// <summary>Returns the canonical manufacturer, or the trimmed input when it is not in the table.</summary>
    public string Canonical(string? manufacturer)
    {
        var name = (manufacturer ?? "").Trim();
        if (name.Length == 0)
        {
            return "";
        }

        foreach (var entry in _entries)
        {
            if (entry.Aliases.Any(alias => string.Equals(alias.Trim(), name, StringComparison.OrdinalIgnoreCase)))
            {
                return entry.Canonical.Trim();
            }
        }

        return name;
    }

    public bool SameManufacturer(string? left, string? right)
    {
        var leftCanonical = Canonical(left);
        var rightCanonical = Canonical(right);
        return leftCanonical.Length > 0
            && rightCanonical.Length > 0
            && string.Equals(leftCanonical, rightCanonical, StringComparison.OrdinalIgnoreCase);
    }
}

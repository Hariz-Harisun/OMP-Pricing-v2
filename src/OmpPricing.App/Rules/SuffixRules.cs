namespace OmpPricing.App.Rules;

public sealed class SuffixRule
{
    public string Manufacturer { get; set; } = "";

    public string Suffix { get; set; } = "";

    public string? Description { get; set; }
}

/// <summary>
/// The known packaging suffixes from rules/suffix-rules.json (A6 rule 2). Only these may be accepted
/// automatically; any other tail is a Prefix match and needs a human. Seeded with Murata D.
/// </summary>
public sealed class SuffixRules
{
    private readonly List<SuffixRule> _rules;

    private SuffixRules(List<SuffixRule> rules) => _rules = rules;

    public static SuffixRules Empty => new([]);

    public int Count => _rules.Count;

    public static SuffixRules Load(string path)
    {
        var rules = RuleFile.Load<SuffixRule>(path);

        foreach (var rule in rules)
        {
            if (string.IsNullOrWhiteSpace(rule.Manufacturer) || string.IsNullOrWhiteSpace(rule.Suffix))
            {
                throw new InvalidOperationException(
                    "rules/suffix-rules.json needs a manufacturer and a suffix on every entry: " + path);
            }
        }

        return new SuffixRules(rules);
    }

    /// <summary>True when the candidate is the requested MPN plus a suffix known for this manufacturer.</summary>
    public bool IsKnownSuffix(string? manufacturer, string requestedMpn, string candidateMpn)
    {
        if (string.IsNullOrWhiteSpace(manufacturer))
        {
            return false;
        }

        return _rules.Any(rule =>
            string.Equals(rule.Manufacturer.Trim(), manufacturer.Trim(), StringComparison.OrdinalIgnoreCase)
            && string.Equals(candidateMpn, requestedMpn + rule.Suffix, StringComparison.OrdinalIgnoreCase));
    }
}

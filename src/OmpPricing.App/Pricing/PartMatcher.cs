using OmpPricing.App.Providers;
using OmpPricing.App.Rules;

namespace OmpPricing.App.Pricing;

/// <summary>How a candidate MPN relates to the requested one (A6 rule 2).</summary>
public enum MatchLevel
{
    None,
    /// <summary>The candidate starts with the requested MPN but the tail is unknown: a human decides.</summary>
    Prefix,
    /// <summary>Requested MPN plus a known packaging suffix, or a wildcard prefix with at most 3 extra characters.</summary>
    PackagingVariant,
    Exact,
}

/// <summary>
/// Classifies provider offers against the requested part. Works on <see cref="Offer"/> only, never on
/// a provider type, and uses rules/suffix-rules.json and rules/mfg-aliases.json.
/// </summary>
public sealed class PartMatcher
{
    private readonly SuffixRules _suffixes;
    private readonly MfgAliases _aliases;

    public PartMatcher(SuffixRules suffixes, MfgAliases aliases)
    {
        _suffixes = suffixes;
        _aliases = aliases;
    }

    public static PartMatcher Empty => new(SuffixRules.Empty, MfgAliases.Empty);

    public bool IsAccepted(MatchLevel level) => level is MatchLevel.Exact or MatchLevel.PackagingVariant;

    public MatchLevel Classify(PartQuery query, Offer offer)
    {
        if (!ManufacturerMatches(query.Mfg, offer.Mfg))
        {
            return MatchLevel.None;
        }

        var requested = (query.Mpn ?? "").Trim().ToUpperInvariant();
        var candidate = (offer.Mpn ?? "").Trim().ToUpperInvariant();

        if (requested.Length == 0 || candidate.Length == 0)
        {
            return MatchLevel.None;
        }

        if (candidate == requested)
        {
            return MatchLevel.Exact;
        }

        // A9.3: a trailing wildcard means "this prefix plus at most 3 characters" is a packaging variant.
        if (!string.IsNullOrWhiteSpace(query.WildcardPrefix))
        {
            var prefix = query.WildcardPrefix.Trim().ToUpperInvariant();
            if (prefix.Length > 0
                && candidate.StartsWith(prefix, StringComparison.Ordinal)
                && candidate.Length - prefix.Length is >= 1 and <= 3)
            {
                return MatchLevel.PackagingVariant;
            }
        }

        var manufacturer = _aliases.Canonical(query.Mfg);
        if (_suffixes.IsKnownSuffix(manufacturer, requested, candidate))
        {
            return MatchLevel.PackagingVariant;
        }

        if (candidate.StartsWith(requested, StringComparison.Ordinal))
        {
            return MatchLevel.Prefix;
        }

        return MatchLevel.None;
    }

    /// <summary>An empty manufacturer on either side cannot be checked, so it does not block the match.</summary>
    private bool ManufacturerMatches(string? requested, string? candidate)
    {
        if (string.IsNullOrWhiteSpace(requested) || string.IsNullOrWhiteSpace(candidate))
        {
            return true;
        }

        return _aliases.SameManufacturer(requested, candidate);
    }
}

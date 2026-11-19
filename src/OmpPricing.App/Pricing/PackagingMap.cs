using OmpPricing.App.Excel;
using OmpPricing.App.Rules;

namespace OmpPricing.App.Pricing;

/// <summary>
/// Maps a provider packaging name to the short code used in column 18 (A6 rule 6).
/// The map lives in configuration under Omp:PackagingMap.
/// </summary>
public sealed class PackagingMap
{
    private readonly Dictionary<string, string> _byName;

    public PackagingMap(IReadOnlyDictionary<string, string>? byName = null)
    {
        _byName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Documented examples from A6 rule 6.
        _byName["Tape & Reel (TR)"] = "T&R";
        _byName["Cut Tape (CT)"] = "CT";
        _byName["Tray"] = "Tray";
        _byName["Tube"] = "Tube";
        _byName["Bulk"] = "Bulk";

        if (byName is null)
        {
            return;
        }

        foreach (var (key, value) in byName)
        {
            if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(value))
            {
                _byName[key.Trim()] = value.Trim();
            }
        }
    }

    /// <summary>Maps a provider packaging name; unknown names pass through unchanged (A6 rule 6).</summary>
    public string? Map(string? packaging)
    {
        if (string.IsNullOrWhiteSpace(packaging))
        {
            return null;
        }

        var trimmed = packaging.Trim();
        return _byName.TryGetValue(trimmed, out var shortCode) ? shortCode : trimmed;
    }
}

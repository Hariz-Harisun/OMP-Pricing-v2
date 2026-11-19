using System.Text.Json;
using System.Text.Json.Serialization;

namespace OmpPricing.App.Pricing;

public sealed class MatchCacheEntry
{
    public string Mfg { get; set; } = "";

    public string Mpn { get; set; } = "";

    public string Provider { get; set; } = "";

    public string ProviderPartNumber { get; set; } = "";

    public MatchLevel Level { get; set; }
}

/// <summary>
/// state/match-cache.json: the decision for an input MPN + MFG, per provider, so a later run can look
/// the part up by the provider part number instead of repeating the search (A6 rule 2).
/// </summary>
public sealed class MatchCache
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly Dictionary<string, MatchCacheEntry> _entries = new(StringComparer.Ordinal);

    public int Count => _entries.Count;

    public static MatchCache Empty => new();

    public static MatchCache Load(string path)
    {
        var cache = new MatchCache();

        if (!File.Exists(path))
        {
            return cache;
        }

        var json = File.ReadAllText(path);
        if (string.IsNullOrWhiteSpace(json))
        {
            return cache;
        }

        var entries = JsonSerializer.Deserialize<List<MatchCacheEntry>>(json, JsonOptions) ?? [];
        foreach (var entry in entries)
        {
            cache._entries[Key(entry.Mfg, entry.Mpn, entry.Provider)] = entry;
        }

        return cache;
    }

    public MatchCacheEntry? Find(string? mfg, string? mpn, string provider)
        => _entries.TryGetValue(Key(mfg, mpn, provider), out var entry) ? entry : null;

    public void Record(string? mfg, string? mpn, string provider, string providerPartNumber, MatchLevel level)
    {
        _entries[Key(mfg, mpn, provider)] = new MatchCacheEntry
        {
            Mfg = (mfg ?? "").Trim(),
            Mpn = (mpn ?? "").Trim().ToUpperInvariant(),
            Provider = provider,
            ProviderPartNumber = providerPartNumber,
            Level = level,
        };
    }

    public void Save(string path)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var ordered = _entries.Values
            .OrderBy(entry => entry.Provider, StringComparer.Ordinal)
            .ThenBy(entry => entry.Mfg, StringComparer.Ordinal)
            .ThenBy(entry => entry.Mpn, StringComparer.Ordinal)
            .ToList();

        File.WriteAllText(path, JsonSerializer.Serialize(ordered, JsonOptions));
    }

    private static string Key(string? mfg, string? mpn, string provider)
        => (mfg ?? "").Trim().ToUpperInvariant() + "|" + (mpn ?? "").Trim().ToUpperInvariant() + "|" + provider;
}

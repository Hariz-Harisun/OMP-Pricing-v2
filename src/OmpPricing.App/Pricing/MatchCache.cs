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

    /// <summary>When the decision was made. Entries older than 30 days are ignored.</summary>
    public DateTimeOffset CachedAtUtc { get; set; }
}

/// <summary>
/// state/match-cache.json: the decision for an input MPN + MFG, per provider, so a later run can look
/// the part up by the provider part number instead of repeating the search (A6 rule 2).
///
/// Only Exact and PackagingVariant decisions are kept: a Prefix or None decision says nothing about
/// the part and must be made again. Entries expire after 30 days. `--no-cache` runs with a disabled
/// cache, which neither reads nor writes the file (decision D6 in docs/decisions.md).
/// </summary>
public sealed class MatchCache
{
    public static readonly TimeSpan MaxAge = TimeSpan.FromDays(30);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly Dictionary<string, MatchCacheEntry> _entries = new(StringComparer.Ordinal);
    private readonly bool _enabled;

    public MatchCache(bool enabled = true) => _enabled = enabled;

    public bool Enabled => _enabled;

    public int Count => _entries.Count;

    public static MatchCache Empty => new();

    public static MatchCache Disabled => new(false);

    public static MatchCache Load(string path, DateTimeOffset? now = null, TimeSpan? maxAge = null)
    {
        var cache = new MatchCache();
        return cache.LoadFrom(path, now ?? DateTimeOffset.UtcNow, maxAge ?? MaxAge);
    }

    /// <summary>Reads the file and keeps only entries that are usable today.</summary>
    public MatchCache LoadFrom(string path, DateTimeOffset now, TimeSpan? maxAge = null)
    {
        if (!File.Exists(path))
        {
            return this;
        }

        var json = File.ReadAllText(path);
        if (string.IsNullOrWhiteSpace(json))
        {
            return this;
        }

        var oldest = now - (maxAge ?? MaxAge);
        var entries = JsonSerializer.Deserialize<List<MatchCacheEntry>>(json, JsonOptions) ?? [];

        foreach (var entry in entries)
        {
            if (!IsCacheable(entry.Level))
            {
                continue;
            }

            // No timestamp means the entry predates the expiry rule: make the decision again.
            if (entry.CachedAtUtc == default || entry.CachedAtUtc < oldest)
            {
                continue;
            }

            _entries[Key(entry.Mfg, entry.Mpn, entry.Provider)] = entry;
        }

        return this;
    }

    public MatchCacheEntry? Find(string? mfg, string? mpn, string provider)
        => _enabled && _entries.TryGetValue(Key(mfg, mpn, provider), out var entry) ? entry : null;

    public void Record(
        string? mfg,
        string? mpn,
        string provider,
        string providerPartNumber,
        MatchLevel level,
        DateTimeOffset? capturedAt = null)
    {
        if (!_enabled || !IsCacheable(level) || string.IsNullOrWhiteSpace(providerPartNumber))
        {
            return;
        }

        _entries[Key(mfg, mpn, provider)] = new MatchCacheEntry
        {
            Mfg = (mfg ?? "").Trim(),
            Mpn = (mpn ?? "").Trim().ToUpperInvariant(),
            Provider = provider,
            ProviderPartNumber = providerPartNumber,
            Level = level,
            CachedAtUtc = capturedAt ?? DateTimeOffset.UtcNow,
        };
    }

    public void Save(string path)
    {
        if (!_enabled)
        {
            return;
        }

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

    private static bool IsCacheable(MatchLevel level) => level is MatchLevel.Exact or MatchLevel.PackagingVariant;

    private static string Key(string? mfg, string? mpn, string provider)
        => (mfg ?? "").Trim().ToUpperInvariant() + "|" + (mpn ?? "").Trim().ToUpperInvariant() + "|" + provider;
}

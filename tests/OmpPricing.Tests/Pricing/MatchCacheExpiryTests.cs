using OmpPricing.App.Pricing;
using OmpPricing.Tests.Support;

namespace OmpPricing.Tests.Pricing;

/// <summary>Decision D6: only Exact and PackagingVariant decisions are cached, and they expire in 30 days.</summary>
public sealed class MatchCacheExpiryTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);

    private readonly string _folder = Sample.NewTempFolder();

    private string CachePath => Path.Combine(_folder, "match-cache.json");

    public void Dispose()
    {
        try
        {
            Directory.Delete(_folder, true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public void Exact_and_packaging_variant_decisions_survive_a_reload()
    {
        var cache = new MatchCache();
        cache.Record("Murata", "BLM18HE601SN1", "DigiKey", "490-5214-2-ND", MatchLevel.Exact, Now);
        cache.Record("Murata", "BLM15GG221SZ1", "DigiKey", "490-1234-1-ND", MatchLevel.PackagingVariant, Now);
        cache.Save(CachePath);

        var reloaded = MatchCache.Load(CachePath, Now);

        Assert.Equal(2, reloaded.Count);
        Assert.Equal("490-5214-2-ND", reloaded.Find("Murata", "BLM18HE601SN1", "DigiKey")!.ProviderPartNumber);
        Assert.Equal(MatchLevel.PackagingVariant, reloaded.Find("murata", "BLM15GG221SZ1", "DigiKey")!.Level);
    }

    [Fact]
    public void Prefix_and_none_decisions_are_never_cached()
    {
        var cache = new MatchCache();
        cache.Record("Murata", "BLM18HE601SN1", "DigiKey", "490-5214-2-ND", MatchLevel.Prefix, Now);
        cache.Record("Murata", "BLM18HE601SN2", "DigiKey", "490-5214-3-ND", MatchLevel.None, Now);

        Assert.Equal(0, cache.Count);
        Assert.Null(cache.Find("Murata", "BLM18HE601SN1", "DigiKey"));
    }

    [Fact]
    public void Entries_older_than_thirty_days_are_dropped_on_load()
    {
        var cache = new MatchCache();
        cache.Record("Murata", "BLM18HE601SN1", "DigiKey", "490-5214-2-ND", MatchLevel.Exact, Now.AddDays(-31));
        cache.Record("Murata", "BLM18HE601SN2", "DigiKey", "490-5214-3-ND", MatchLevel.Exact, Now.AddDays(-29));
        cache.Save(CachePath);

        var reloaded = MatchCache.Load(CachePath, Now);

        Assert.Equal(1, reloaded.Count);
        Assert.Null(reloaded.Find("Murata", "BLM18HE601SN1", "DigiKey"));
        Assert.NotNull(reloaded.Find("Murata", "BLM18HE601SN2", "DigiKey"));
    }

    [Fact]
    public void An_entry_without_a_timestamp_is_treated_as_expired()
    {
        File.WriteAllText(
            CachePath,
            """
            [
              {
                "mfg": "Murata",
                "mpn": "BLM18HE601SN1",
                "provider": "DigiKey",
                "providerPartNumber": "490-5214-2-ND",
                "level": "Exact"
              }
            ]
            """);

        var cache = MatchCache.Load(CachePath, Now);

        Assert.Equal(0, cache.Count);
    }

    [Fact]
    public void A_disabled_cache_neither_records_nor_writes()
    {
        var cache = MatchCache.Disabled;
        cache.Record("Murata", "BLM18HE601SN1", "DigiKey", "490-5214-2-ND", MatchLevel.Exact, Now);
        cache.Save(CachePath);

        Assert.False(cache.Enabled);
        Assert.Equal(0, cache.Count);
        Assert.False(File.Exists(CachePath));

        // --no-cache also ignores a file that already holds decisions.
        var existing = new MatchCache();
        existing.Record("Murata", "BLM18HE601SN1", "DigiKey", "490-5214-2-ND", MatchLevel.Exact, Now);
        existing.Save(CachePath);

        Assert.Null(MatchCache.Disabled.LoadFrom(CachePath, Now).Find("Murata", "BLM18HE601SN1", "DigiKey"));
    }
}

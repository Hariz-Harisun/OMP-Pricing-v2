using OmpPricing.App.Pricing;
using OmpPricing.Tests.Support;

namespace OmpPricing.Tests.Pricing;

/// <summary>state/match-cache.json keeps the decision per input MPN + MFG and provider (A6 rule 2).</summary>
public sealed class MatchCacheTests
{
    [Fact]
    public void Missing_file_gives_an_empty_cache()
    {
        var cache = MatchCache.Load(Path.Combine(Sample.NewTempFolder(), "match-cache.json"));

        Assert.Equal(0, cache.Count);
    }

    [Fact]
    public void Recorded_decision_is_found_again_regardless_of_case()
    {
        var cache = MatchCache.Empty;

        cache.Record("Murata", "blm18he601sN1", "DigiKey", "490-5214-2-ND", MatchLevel.PackagingVariant);

        var entry = cache.Find("MURATA", "BLM18HE601SN1", "DigiKey");

        Assert.NotNull(entry);
        Assert.Equal("490-5214-2-ND", entry!.ProviderPartNumber);
        Assert.Equal(MatchLevel.PackagingVariant, entry.Level);
    }

    [Fact]
    public void Different_provider_is_a_different_key()
    {
        var cache = MatchCache.Empty;
        cache.Record("Murata", "BLM18HE601SN1", "DigiKey", "490-5214-2-ND", MatchLevel.Exact);

        Assert.Null(cache.Find("Murata", "BLM18HE601SN1", "Mouser"));
    }

    [Fact]
    public void Cache_round_trips_through_the_state_file()
    {
        var folder = Sample.NewTempFolder();
        var path = Path.Combine(folder, "match-cache.json");

        var cache = MatchCache.Empty;
        cache.Record("NXP", "BAT54CW,115", "DigiKey", "568-1234-1-ND", MatchLevel.Exact);
        cache.Record("Murata", "BLM18HE601SN1", "DigiKey", "490-5214-2-ND", MatchLevel.PackagingVariant);
        cache.Save(path);

        var json = File.ReadAllText(path);
        Assert.Contains("PackagingVariant", json);

        var reloaded = MatchCache.Load(path);

        Assert.Equal(2, reloaded.Count);
        Assert.Equal("568-1234-1-ND", reloaded.Find("NXP", "BAT54CW,115", "DigiKey")!.ProviderPartNumber);
        Assert.Equal("490-5214-2-ND", reloaded.Find("Murata", "BLM18HE601SN1", "DigiKey")!.ProviderPartNumber);
    }

    [Fact]
    public void Empty_file_is_treated_as_an_empty_cache()
    {
        var path = Path.Combine(Sample.NewTempFolder(), "match-cache.json");
        File.WriteAllText(path, "");

        Assert.Equal(0, MatchCache.Load(path).Count);
    }
}

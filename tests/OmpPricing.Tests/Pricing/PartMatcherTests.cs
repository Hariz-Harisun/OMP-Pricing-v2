using OmpPricing.App.Pricing;
using OmpPricing.App.Providers;
using OmpPricing.App.Rules;
using OmpPricing.Tests.Support;

namespace OmpPricing.Tests.Pricing;

/// <summary>Match classification from A6 rule 2 and the wildcard rule A9.3.</summary>
public sealed class PartMatcherTests
{
    private static PartMatcher Matcher() => new(
        SuffixRules.Load(Sample.RulesFile("suffix-rules.json")),
        MfgAliases.Load(Sample.RulesFile("mfg-aliases.json")));

    private static Offer OfferFor(string mfg, string mpn) => new()
    {
        Provider = "FakeProvider",
        Mfg = mfg,
        Mpn = mpn,
        PriceBreaks = [new PriceBreak(1, 0.1m)],
    };

    [Fact]
    public void Same_mpn_and_same_manufacturer_is_exact()
    {
        var query = new PartQuery { Mpn = "BLM18HE601SN1", Mfg = "Murata" };

        Assert.Equal(MatchLevel.Exact, Matcher().Classify(query, OfferFor("Murata", "BLM18HE601SN1")));
    }

    [Fact]
    public void Known_packaging_suffix_is_a_packaging_variant()
    {
        var query = new PartQuery { Mpn = "BLM15GG221SZ1", Mfg = "Murata" };

        var level = Matcher().Classify(query, OfferFor("Murata Electronics", "BLM15GG221SZ1D"));

        Assert.Equal(MatchLevel.PackagingVariant, level);
    }

    [Fact]
    public void Unknown_tail_is_a_prefix_match_that_needs_review()
    {
        var query = new PartQuery { Mpn = "BLM18HE601SN1", Mfg = "Murata" };

        var level = Matcher().Classify(query, OfferFor("Murata", "BLM18HE601SN1XYZ"));

        Assert.Equal(MatchLevel.Prefix, level);
        Assert.False(Matcher().IsAccepted(level));
    }

    [Fact]
    public void Wildcard_prefix_accepts_up_to_three_extra_characters()
    {
        var query = new PartQuery { Mpn = "BLM15GG221SZ1", Mfg = "Murata", WildcardPrefix = "BLM15GG221SZ1" };

        Assert.Equal(MatchLevel.PackagingVariant, Matcher().Classify(query, OfferFor("Murata", "BLM15GG221SZ1D")));
    }

    [Fact]
    public void Wildcard_prefix_rejects_more_than_three_extra_characters()
    {
        var query = new PartQuery { Mpn = "BLM15GG221SZ1", Mfg = "Murata", WildcardPrefix = "BLM15GG221SZ1" };

        Assert.Equal(MatchLevel.Prefix, Matcher().Classify(query, OfferFor("Murata", "BLM15GG221SZ1D4SX")));
    }

    [Fact]
    public void Different_manufacturer_is_not_a_match()
    {
        var query = new PartQuery { Mpn = "BAT54CW,115", Mfg = "NXP" };

        Assert.Equal(MatchLevel.None, Matcher().Classify(query, OfferFor("Diodes Incorporated", "BAT54CW,115")));
    }

    [Fact]
    public void Empty_manufacturer_on_the_row_does_not_block_an_exact_match()
    {
        var query = new PartQuery { Mpn = "BAT54CW,115", Mfg = "" };

        Assert.Equal(MatchLevel.Exact, Matcher().Classify(query, OfferFor("NXP USA Inc.", "BAT54CW,115")));
    }

    [Fact]
    public void Manufacturer_aliases_are_one_manufacturer()
    {
        var aliases = MfgAliases.Load(Sample.RulesFile("mfg-aliases.json"));

        Assert.True(aliases.SameManufacturer("Murata", "Murata Electronics"));
        Assert.True(aliases.SameManufacturer("MURATA MANUFACTURING", "murata"));
        Assert.False(aliases.SameManufacturer("Murata", "TDK"));
    }

    [Fact]
    public void Unrelated_mpn_is_not_a_match()
    {
        var query = new PartQuery { Mpn = "BAT54CW,115", Mfg = "NXP" };

        Assert.Equal(MatchLevel.None, Matcher().Classify(query, OfferFor("NXP", "BAT54C,215")));
    }
}

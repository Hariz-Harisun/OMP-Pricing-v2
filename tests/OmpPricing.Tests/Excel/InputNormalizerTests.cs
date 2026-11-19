using OmpPricing.App.Pricing;

namespace OmpPricing.Tests.Excel;

public sealed class InputNormalizerTests
{
    [Fact]
    public void Generic_parametric_row_is_flagged_by_its_spec_string()
    {
        var result = InputNormalizer.Normalize(
            "UnDefined",
            "SMT;SMD0201;100n;+10 %;-10%;25V;X7R",
            "1",
            "5100");

        Assert.True(result.IsGeneric);
        Assert.Equal(LineStatus.NeedsReview, result.Status);
        Assert.Equal("Generic part: no real MPN", result.Remarks);
        Assert.Equal(1, result.Qty);
    }

    [Fact]
    public void Generic_parametric_row_is_flagged_by_a_placeholder_manufacturer()
    {
        var result = InputNormalizer.Normalize("Common", "SMT;SMD0402;100p;+10%;-10%;25V;X7R", "22", "56100");

        Assert.True(result.IsGeneric);
        Assert.Equal(LineStatus.NeedsReview, result.Status);
    }

    [Fact]
    public void Trailing_wildcard_is_detected_before_uppercasing_and_removed_from_the_lookup_mpn()
    {
        var result = InputNormalizer.Normalize("Murata", "BLM15GG221SZ1x", "4", "20400");

        Assert.True(result.IsWildcard);
        Assert.False(result.IsGeneric);
        Assert.Equal("BLM15GG221SZ1", result.WildcardPrefix);
        Assert.Equal("BLM15GG221SZ1", result.Mpn);
        Assert.Equal(LineStatus.Queued, result.Status);
    }

    [Fact]
    public void Three_character_wildcard_suffix_is_also_a_prefix_wildcard()
    {
        var result = InputNormalizer.Normalize("NXP", "PESD5V0S1UA-xxx", "2", "5100");

        Assert.True(result.IsWildcard);
        Assert.Equal("PESD5V0S1UA-", result.WildcardPrefix);
        Assert.Equal("PESD5V0S1UA-", result.Mpn);
    }

    [Fact]
    public void Wildcard_inside_the_mpn_goes_to_needs_review()
    {
        var result = InputNormalizer.Normalize("TDK", "C1608X7R1H104K080xE", "6", "15300");

        Assert.False(result.IsWildcard);
        Assert.Equal(LineStatus.NeedsReview, result.Status);
        Assert.Equal("Wildcard inside MPN: C1608X7R1H104K080xE", result.Remarks);
    }

    [Fact]
    public void Multi_mpn_cell_keeps_the_first_mpn_and_lists_the_others_as_alternates()
    {
        const string cell = "BAT54CW,115 / BAT54CWT1G (at) /\nBAT54CW (alt) / LBAT54CWT1G (alt) /\nBAT54CW-7-F (alt)";

        var result = InputNormalizer.Normalize("", cell, "2", "9400");

        Assert.Equal("BAT54CW,115", result.Mpn);
        Assert.Equal(new[] { "BAT54CWT1G", "BAT54CW", "LBAT54CWT1G", "BAT54CW-7-F" }, result.Alternates);
    }

    [Fact]
    public void Multi_mpn_cell_split_on_a_line_break_alone_is_supported()
    {
        var result = InputNormalizer.Normalize("TI", "TPA6205A1DGN\nTPA6203A1DGNR", "5", "2000");

        Assert.Equal("TPA6205A1DGN", result.Mpn);
        Assert.Equal(new[] { "TPA6203A1DGNR" }, result.Alternates);
    }

    [Fact]
    public void Trailing_and_repeated_whitespace_is_trimmed()
    {
        var result = InputNormalizer.Normalize("  NXP  ", "BAT54C,215   ", "1", "4700");

        Assert.Equal("NXP", result.Mfg);
        Assert.Equal("BAT54C,215", result.Mpn);
    }

    [Fact]
    public void Comma_inside_the_mpn_is_preserved()
    {
        var result = InputNormalizer.Normalize("NXP", "BAT54CW,115", "2", "800");

        Assert.Equal("BAT54CW,115", result.Mpn);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("-2")]
    [InlineData("1.5")]
    [InlineData("many")]
    public void Quantity_must_be_a_positive_whole_number(string? qty)
    {
        var result = InputNormalizer.Normalize("Murata", "BLM18HE601SN1", qty, "127500");

        Assert.Null(result.Qty);
        Assert.Equal(LineStatus.InvalidInput, result.Status);
    }

    [Fact]
    public void Empty_eau_is_accepted()
    {
        var result = InputNormalizer.Normalize("Murata", "BLM18HE601SN1", "1", "");

        Assert.Null(result.Eau);
        Assert.Equal(LineStatus.Queued, result.Status);
    }
}

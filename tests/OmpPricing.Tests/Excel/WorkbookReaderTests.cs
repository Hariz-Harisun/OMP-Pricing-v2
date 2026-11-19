using OmpPricing.App.Pricing;
using OmpPricing.Tests.Support;

namespace OmpPricing.Tests.Excel;

/// <summary>Reads the client's real file (tests/samples/LT2026100009.xlsx) by column position.</summary>
public sealed class WorkbookReaderTests
{
    [Fact]
    public void Reads_all_145_data_rows()
    {
        using var loaded = Sample.Load();

        Assert.Equal("LT Quote", loaded.Sheet.Name);
        Assert.Equal(145, loaded.Lines.Count);
        Assert.Equal(2, loaded.Lines[0].RowNumber);
        Assert.Equal(146, loaded.Lines[^1].RowNumber);
    }

    [Fact]
    public void Identifies_exactly_67_generic_parametric_rows()
    {
        using var loaded = Sample.Load();

        var generic = loaded.Lines.Where(line => line.IsGeneric).ToList();

        Assert.Equal(67, generic.Count);
        Assert.All(generic, line => Assert.Equal(LineStatus.NeedsReview, line.Status));
        Assert.All(generic, line => Assert.Equal("Generic part: no real MPN", line.Remarks));
    }

    [Fact]
    public void Reads_by_position_even_though_headers_repeat_and_have_trailing_spaces()
    {
        using var loaded = Sample.Load();
        var line = loaded.Lines.Single(candidate => candidate.RowNumber == 40);

        Assert.Equal("NXP", line.RawMfg);
        Assert.Equal("BAT54CW,115", line.Mpn);
        Assert.Equal(2, line.Qty);
        Assert.Equal(800, line.Eau);
        Assert.Contains("DIODE", line.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("xxxxxxxxxx", line.Pn);
    }

    [Fact]
    public void Detects_the_wildcard_row_before_uppercasing()
    {
        using var loaded = Sample.Load();
        var line = loaded.Lines.Single(candidate => candidate.RowNumber == 3);

        Assert.Equal("BLM15GG221SZ1x", line.RawMpn);
        Assert.True(line.IsWildcard);
        Assert.Equal("BLM15GG221SZ1", line.Mpn);
    }

    [Fact]
    public void Reads_the_five_mpn_cell_as_one_primary_mpn_plus_alternates()
    {
        using var loaded = Sample.Load();
        var line = loaded.Lines.Single(candidate => candidate.RowNumber == 140);

        Assert.Equal("BAT54CW,115", line.Mpn);
        Assert.Equal(4, line.Alternates.Count);
        Assert.Equal("BAT54CWT1G", line.Alternates[0]);
        Assert.Equal("BAT54CW-7-F", line.Alternates[3]);
    }

    [Fact]
    public void Rows_with_a_wildcard_inside_the_mpn_are_sent_to_review_without_a_lookup()
    {
        using var loaded = Sample.Load();
        var rows = loaded.Lines
            .Where(line => line.Status == LineStatus.NeedsReview && !line.IsGeneric)
            .Select(line => line.RowNumber)
            .ToList();

        Assert.Equal(new[] { 14, 47, 55, 74 }, rows);
    }

    [Fact]
    public void Trailing_space_in_a_cell_is_trimmed_before_comparison()
    {
        using var input = new ClosedXML.Excel.XLWorkbook(new MemoryStream(Sample.Bytes));
        var senderCell = input.Worksheet("LT Quote").Cell(141, 4).GetString();
        Assert.Equal("BAT54C,215 ", senderCell);

        using var loaded = Sample.Load();
        var line = loaded.Lines.Single(candidate => candidate.RowNumber == 141);

        Assert.Equal("BAT54C,215", line.Mpn);
        Assert.Equal("BAT54C,215", line.RawMpn);
    }

    [Fact]
    public void Every_queued_line_has_a_positive_quantity()
    {
        using var loaded = Sample.Load();

        var queued = loaded.Lines.Where(line => line.Status == LineStatus.Queued).ToList();

        Assert.Equal(74, queued.Count);
        Assert.All(queued, line => Assert.True(line.Qty > 0));
    }
}

using ClosedXML.Excel;
using OmpPricing.App.Pricing;
using OmpPricing.Tests.Support;

namespace OmpPricing.Tests.Pipeline;

/// <summary>The whole path on the client's real file, with a scripted provider.</summary>
public sealed class SamplePipelineTests : IClassFixture<SamplePipelineTests.Fixture>
{
    private static readonly XLColor NotPricedRow = XLColor.FromHtml("#FFF2CC");
    private static readonly XLColor MissingCell = XLColor.FromHtml("#FFC7CE");

    private readonly Fixture _fixture;

    public SamplePipelineTests(Fixture fixture) => _fixture = fixture;

    public sealed class Fixture : IDisposable
    {
        public Fixture() => Run = PipelineRun.Execute(FakeProvider.Echoing());

        public PipelineRun Run { get; }

        public void Dispose() => Run.Dispose();
    }

    private PipelineRun Run => _fixture.Run;

    [Fact]
    public void Output_file_is_written_with_the_timestamped_name()
    {
        Assert.True(File.Exists(Run.Result.OutputPath));
        Assert.Equal(Path.GetFullPath(Run.OutputFolder), Path.GetDirectoryName(Path.GetFullPath(Run.Result.OutputPath)));

        var name = Path.GetFileName(Run.Result.OutputPath);
        Assert.Matches(@"^LT2026100009_\d{8}-\d{6}(-\d+)?\.xlsx$", name);
        Assert.Equal(145, Run.Result.LineCount);
    }

    [Fact]
    public void Sender_columns_1_to_6_are_unchanged()
    {
        using var input = new XLWorkbook(new MemoryStream(Sample.Bytes));
        using var output = Run.OpenOutput();

        var inputSheet = input.Worksheet("LT Quote");
        var outputSheet = output.Worksheet("LT Quote");

        for (var row = 1; row <= 146; row++)
        {
            for (var column = 1; column <= 6; column++)
            {
                Assert.Equal(inputSheet.Cell(row, column).GetString(), outputSheet.Cell(row, column).GetString());
            }
        }
    }

    [Fact]
    public void Country_sheet_with_its_table_and_257_countries_survives()
    {
        using var output = Run.OpenOutput();

        Assert.Contains("Sheet1", output.Worksheets.Select(sheet => sheet.Name));

        var sheet = output.Worksheet("Sheet1");
        Assert.Equal(258, sheet.LastRowUsed()!.RowNumber());
        Assert.Single(sheet.Tables);
        Assert.Equal(257, Enumerable.Range(2, 257).Count(row => !string.IsNullOrWhiteSpace(sheet.Cell(row, 1).GetString())));
    }

    [Fact]
    public void All_three_data_validations_survive_and_appear_exactly_once()
    {
        using var output = Run.OpenOutput();
        var sheet = output.Worksheet("LT Quote");

        var validations = sheet.DataValidations.ToList();
        Assert.Equal(3, validations.Count);
        Assert.Equal(2, validations.Count(validation => validation.ErrorMessage == "Reply Yes or No only"));
        Assert.Single(validations, validation => validation.ErrorMessage == "Please select a country from the list");

        var xml = ReadSheetXml(Run.OutputBytes);
        var countryOccurrences = CountOccurrences(xml, "U1:U1048576") + CountOccurrences(xml, "<xm:sqref>U:U</xm:sqref>");
        Assert.Equal(1, countryOccurrences);
        Assert.Contains("Sheet1!$A$2:$A$258", xml);
    }

    [Fact]
    public void Generic_rows_are_needs_review_and_never_reach_a_provider()
    {
        Assert.DoesNotContain(Run.Calls(), call => call.Mpn.StartsWith("SMT;", StringComparison.Ordinal));
        Assert.Equal(71, Run.Result.StatusCounts[LineStatus.NeedsReview]);
        Assert.Equal(74, Run.Result.StatusCounts[LineStatus.Incomplete]);
        Assert.False(Run.Result.StatusCounts.ContainsKey(LineStatus.InvalidInput));
        Assert.False(Run.Result.StatusCounts.ContainsKey(LineStatus.Priced));
    }

    [Fact]
    public void One_lookup_serves_every_row_with_the_same_mfg_and_mpn()
    {
        // 74 lines are eligible; the pairs BAT54C,215 (rows 49/141) and TPA6205A1DGN (rows 133/145)
        // share MFG + MPN, so 72 provider calls.
        Assert.Equal(72, Run.Calls().Count);
        Assert.Single(Run.Calls(), call => call.Mpn == "BAT54C,215");
        Assert.Single(Run.Calls(), call => call.Mpn == "TPA6205A1DGN");
        Assert.Equal(72, Run.Result.ProviderCalls["FakeProvider Found"]);
    }

    [Fact]
    public void Generic_row_gets_the_generic_remark_and_is_highlighted()
    {
        using var output = Run.OpenOutput();
        var sheet = output.Worksheet("LT Quote");

        Assert.Contains("Generic part: no real MPN", sheet.Cell(2, 22).GetString());
        Assert.Equal(NotPricedRow.Color.ToArgb(), sheet.Cell(2, 1).Style.Fill.BackgroundColor.Color.ToArgb());
    }

    [Fact]
    public void Wildcard_row_is_looked_up_with_its_prefix_and_noted()
    {
        using var output = Run.OpenOutput();
        var sheet = output.Worksheet("LT Quote");

        Assert.Contains(Run.Calls(), call => call.WildcardPrefix == "BLM15GG221SZ1");
        Assert.Equal("BLM15GG221SZ1", sheet.Cell(3, 9).GetString());
        Assert.Equal("FakeProvider", sheet.Cell(3, 7).GetString());
        Assert.Equal("USD", sheet.Cell(3, 10).GetString());
        Assert.Equal("T&R", sheet.Cell(3, 18).GetString());
        Assert.Equal("Yes", sheet.Cell(3, 19).GetString());
        Assert.Equal("Yes", sheet.Cell(3, 20).GetString());
        Assert.Contains("Wildcard", sheet.Cell(3, 22).GetString());
    }

    [Fact]
    public void Five_mpn_cell_writes_one_mpn_and_lists_the_alternates()
    {
        using var output = Run.OpenOutput();
        var sheet = output.Worksheet("LT Quote");

        Assert.Equal("BAT54CW,115", sheet.Cell(140, 9).GetString());
        Assert.Contains(
            "Alternates: BAT54CWT1G; BAT54CW; LBAT54CWT1G; BAT54CW-7-F",
            sheet.Cell(140, 22).GetString());
    }

    [Fact]
    public void Missing_compulsory_cells_are_highlighted_and_named_in_remarks()
    {
        using var output = Run.OpenOutput();
        var sheet = output.Worksheet("LT Quote");

        foreach (var column in new[] { 15, 16, 17, 21 })
        {
            Assert.Equal(MissingCell.Color.ToArgb(), sheet.Cell(40, column).Style.Fill.BackgroundColor.Color.ToArgb());
        }

        Assert.Contains(
            "Missing: Cancellation window (WKS), Reschedule window (WKS), CXL & RES window, COO",
            sheet.Cell(40, 22).GetString());

        Assert.NotEqual(MissingCell.Color.ToArgb(), sheet.Cell(40, 11).Style.Fill.BackgroundColor.Color.ToArgb());
    }

    [Fact]
    public void Offers_sheet_lists_every_offer_and_marks_the_selected_one()
    {
        using var output = Run.OpenOutput();

        Assert.Contains("Offers", output.Worksheets.Select(sheet => sheet.Name));

        var sheet = output.Worksheet("Offers");
        Assert.Equal("Line", sheet.Cell(1, 1).GetString());
        Assert.Equal("Selected", sheet.Cell(1, 3).GetString());

        var rows = sheet.LastRowUsed()!.RowNumber() - 1;
        Assert.Equal(74, Enumerable.Range(2, rows).Count(row => sheet.Cell(row, 3).GetString() == "Yes"));
        Assert.Equal(71, Enumerable.Range(2, rows).Count(row => sheet.Cell(row, 2).GetString() == "NeedsReview"));
    }

    [Fact]
    public void Match_cache_records_the_decisions()
    {
        Assert.Equal(72, Run.Cache.Count);
        var entry = Run.Cache.Find("NXP", "BAT54CW,115", "FakeProvider");
        Assert.NotNull(entry);
        Assert.Equal(MatchLevel.Exact, entry!.Level);
    }

    [Fact]
    public void Every_row_is_written_and_the_row_count_is_unchanged()
    {
        using var output = Run.OpenOutput();
        var sheet = output.Worksheet("LT Quote");

        Assert.Equal(22, sheet.LastColumnUsed()!.ColumnNumber());
        Assert.Equal(146, sheet.LastRowUsed()!.RowNumber());
    }

    private static string ReadSheetXml(byte[] workbook)
    {
        using var stream = new MemoryStream(workbook);
        using var archive = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Read);
        var entry = archive.GetEntry("xl/worksheets/sheet1.xml")!;
        using var reader = new StreamReader(entry.Open());
        return reader.ReadToEnd();
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }
}

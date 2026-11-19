using ClosedXML.Excel;
using OmpPricing.App.Excel;
using OmpPricing.App.Pipeline;
using OmpPricing.App.Pricing;
using OmpPricing.Tests.Support;
using Serilog;

namespace OmpPricing.Tests.Pipeline;

/// <summary>Runs the pipeline once on the client's real file and inspects the produced workbook.</summary>
public sealed class PipelineFixture : IDisposable
{
    public PipelineFixture()
    {
        OutputFolder = Sample.NewTempFolder();
        Excel = new ExcelOptions();
        Pricer = new FakePricer();

        var logger = new LoggerConfiguration().MinimumLevel.Fatal().CreateLogger();
        var pipeline = new PricingPipeline(Pricer, Excel, new PackagingMap(), Sample.Countries, logger);

        Result = pipeline
            .RunAsync(new FileInputSource(Sample.SamplePath), new FolderResultSink(OutputFolder), CancellationToken.None)
            .GetAwaiter()
            .GetResult();
    }

    public string OutputFolder { get; }

    public ExcelOptions Excel { get; }

    public FakePricer Pricer { get; }

    public PipelineResult Result { get; }

    public byte[] OutputBytes => File.ReadAllBytes(Result.OutputPath);

    public XLWorkbook OpenOutput() => new(new MemoryStream(OutputBytes));

    public void Dispose()
    {
        try
        {
            Directory.Delete(OutputFolder, true);
        }
        catch (IOException)
        {
            // The test run is over; a locked file must not fail the suite.
        }
    }
}

public sealed class SamplePipelineTests : IClassFixture<PipelineFixture>
{
    private static readonly XLColor NotPricedRow = XLColor.FromHtml("#FFF2CC");
    private static readonly XLColor MissingCell = XLColor.FromHtml("#FFC7CE");

    private readonly PipelineFixture _fixture;

    public SamplePipelineTests(PipelineFixture fixture) => _fixture = fixture;

    [Fact]
    public void Run_writes_the_workbook_into_the_output_folder_under_the_sender_file_name()
    {
        Assert.True(File.Exists(_fixture.Result.OutputPath));
        Assert.Equal(Sample.FileName, Path.GetFileName(_fixture.Result.OutputPath));
        Assert.Equal(Path.GetFullPath(_fixture.OutputFolder), Path.GetDirectoryName(Path.GetFullPath(_fixture.Result.OutputPath)));
        Assert.Equal(145, _fixture.Result.LineCount);
    }

    [Fact]
    public void Sender_columns_1_to_6_are_unchanged()
    {
        using var input = new XLWorkbook(new MemoryStream(Sample.Bytes));
        using var output = _fixture.OpenOutput();

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
        using var output = _fixture.OpenOutput();

        Assert.Contains("Sheet1", output.Worksheets.Select(sheet => sheet.Name));

        var sheet = output.Worksheet("Sheet1");
        Assert.Equal(258, sheet.LastRowUsed()!.RowNumber());
        Assert.Single(sheet.Tables);
        Assert.Equal("Countries", sheet.Cell(1, 1).GetString());
        Assert.Equal(257, Enumerable.Range(2, 257).Count(row => !string.IsNullOrWhiteSpace(sheet.Cell(row, 1).GetString())));
    }

    [Fact]
    public void All_three_data_validations_survive_and_appear_exactly_once()
    {
        using var output = _fixture.OpenOutput();
        var sheet = output.Worksheet("LT Quote");

        var validations = sheet.DataValidations.ToList();
        Assert.Equal(3, validations.Count);
        Assert.Equal(2, validations.Count(validation => validation.ErrorMessage == "Reply Yes or No only"));
        Assert.Single(validations, validation => validation.ErrorMessage == "Please select a country from the list");

        var xml = ReadSheetXml(_fixture.OutputBytes);
        var countryOccurrences = CountOccurrences(xml, "U1:U1048576") + CountOccurrences(xml, "<xm:sqref>U:U</xm:sqref>");
        Assert.Equal(1, countryOccurrences);
        Assert.Contains("Sheet1!$A$2:$A$258", xml);
    }

    [Fact]
    public void Generic_rows_are_needs_review_and_never_reach_a_provider()
    {
        Assert.DoesNotContain(_fixture.Pricer.LookedUpMpns, mpn => mpn.StartsWith("SMT;", StringComparison.Ordinal));
        Assert.DoesNotContain(_fixture.Pricer.LookedUpMpns, mpn => mpn.Contains("SMD0402", StringComparison.Ordinal));
        Assert.Equal(72, _fixture.Pricer.LookupCount);

        Assert.Equal(71, _fixture.Result.StatusCounts[LineStatus.NeedsReview]);
        Assert.Equal(74, _fixture.Result.StatusCounts[LineStatus.Incomplete]);
        Assert.False(_fixture.Result.StatusCounts.ContainsKey(LineStatus.InvalidInput));
        Assert.False(_fixture.Result.StatusCounts.ContainsKey(LineStatus.Priced));
    }

    [Fact]
    public void One_lookup_serves_every_row_with_the_same_mfg_and_mpn()
    {
        // 74 lines are eligible for a lookup; two pairs share MFG + MPN, so 72 distinct queries.
        Assert.Equal(74, _fixture.Result.StatusCounts[LineStatus.Incomplete]);
        Assert.Equal(72, _fixture.Pricer.LookupCount);

        // BAT54C,215 (rows 49 and 141, both NXP) and TPA6205A1DGN (rows 133 and 145, both TI).
        Assert.Single(_fixture.Pricer.LookedUpMpns, mpn => mpn == "BAT54C,215");
        Assert.Single(_fixture.Pricer.LookedUpMpns, mpn => mpn == "TPA6205A1DGN");
    }

    [Fact]
    public void Generic_row_gets_the_generic_remark_and_is_highlighted()
    {
        using var output = _fixture.OpenOutput();
        var sheet = output.Worksheet("LT Quote");

        Assert.Contains("Generic part: no real MPN", sheet.Cell(2, 22).GetString());
        Assert.Equal(NotPricedRow.Color.ToArgb(), sheet.Cell(2, 1).Style.Fill.BackgroundColor.Color.ToArgb());
    }

    [Fact]
    public void Wildcard_row_is_written_with_the_prefix_mpn_and_a_note()
    {
        using var output = _fixture.OpenOutput();
        var sheet = output.Worksheet("LT Quote");

        Assert.Equal("BLM15GG221SZ1", sheet.Cell(3, 9).GetString());
        Assert.Equal("FakeDistributor", sheet.Cell(3, 7).GetString());
        Assert.Equal("USD", sheet.Cell(3, 10).GetString());
        Assert.Equal("T&R", sheet.Cell(3, 18).GetString());
        Assert.Equal("Yes", sheet.Cell(3, 19).GetString());
        Assert.Equal("Yes", sheet.Cell(3, 20).GetString());
        Assert.Contains("Wildcard", sheet.Cell(3, 22).GetString());
    }

    [Fact]
    public void Five_mpn_cell_writes_one_mpn_and_lists_the_alternates()
    {
        using var output = _fixture.OpenOutput();
        var sheet = output.Worksheet("LT Quote");

        Assert.Equal("BAT54CW,115", sheet.Cell(140, 9).GetString());
        var remarks = sheet.Cell(140, 22).GetString();
        Assert.Contains("Alternates: BAT54CWT1G; BAT54CW; LBAT54CWT1G; BAT54CW-7-F", remarks);
    }

    [Fact]
    public void Missing_compulsory_cells_are_highlighted_and_named_in_remarks()
    {
        using var output = _fixture.OpenOutput();
        var sheet = output.Worksheet("LT Quote");

        // Columns 15, 16, 17 (rules/supplier-terms.json is empty) and 21 (no COO from the fake offer).
        foreach (var column in new[] { 15, 16, 17, 21 })
        {
            Assert.Equal(MissingCell.Color.ToArgb(), sheet.Cell(40, column).Style.Fill.BackgroundColor.Color.ToArgb());
        }

        var remarks = sheet.Cell(40, 22).GetString();
        Assert.Contains("Missing: Cancellation window (WKS), Reschedule window (WKS), CXL & RES window, COO", remarks);

        // A filled compulsory cell is not flagged.
        Assert.NotEqual(MissingCell.Color.ToArgb(), sheet.Cell(40, 11).Style.Fill.BackgroundColor.Color.ToArgb());
    }

    [Fact]
    public void Offers_sheet_lists_every_offer_and_marks_the_selected_one()
    {
        using var output = _fixture.OpenOutput();

        Assert.Contains("Offers", output.Worksheets.Select(sheet => sheet.Name));

        var sheet = output.Worksheet("Offers");
        Assert.Equal("Line", sheet.Cell(1, 1).GetString());
        Assert.Equal("Selected", sheet.Cell(1, 3).GetString());

        var selected = Enumerable.Range(2, sheet.LastRowUsed()!.RowNumber() - 1)
            .Count(row => sheet.Cell(row, 3).GetString() == "Yes");
        Assert.Equal(74, selected);

        var genericRows = Enumerable.Range(2, sheet.LastRowUsed()!.RowNumber() - 1)
            .Count(row => sheet.Cell(row, 2).GetString() == "NeedsReview");
        Assert.Equal(71, genericRows);
    }

    [Fact]
    public void Every_row_is_written_and_the_row_count_is_unchanged()
    {
        using var output = _fixture.OpenOutput();
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

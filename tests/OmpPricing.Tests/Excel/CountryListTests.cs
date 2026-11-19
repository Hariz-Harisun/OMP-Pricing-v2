using ClosedXML.Excel;
using OmpPricing.App.Excel;
using OmpPricing.App.Rules;
using OmpPricing.Tests.Support;

namespace OmpPricing.Tests.Excel;

/// <summary>rules/countries.txt is generated from Sheet1 and must stay identical to it (A9.6).</summary>
public sealed class CountryListTests
{
    [Fact]
    public void Countries_file_holds_the_257_names_from_sheet1_in_the_same_order()
    {
        var fromSheet = CountryListExporter.FromWorkbook(Sample.Bytes, new ExcelOptions());
        var fromFile = File.ReadAllLines(Sample.RulesFile("countries.txt"));

        Assert.Equal(257, fromSheet.Count);
        Assert.Equal(fromSheet, fromFile);
    }

    [Fact]
    public void Sheet1_has_257_countries_under_its_header()
    {
        using var workbook = new XLWorkbook(new MemoryStream(Sample.Bytes));
        var sheet = workbook.Worksheet("Sheet1");

        Assert.Equal("Countries", sheet.Cell(1, 1).GetString());
        Assert.Equal(258, sheet.LastRowUsed()!.RowNumber());

        var names = Enumerable.Range(2, 257).Select(row => sheet.Cell(row, 1).GetString().Trim()).ToList();
        Assert.Equal(257, names.Count);
        Assert.All(names, name => Assert.False(string.IsNullOrWhiteSpace(name)));
    }

    [Fact]
    public void File_contains_malawi_and_the_seven_obsolete_entries()
    {
        var countries = Sample.Countries;

        Assert.Contains("Malawi", countries.Names);
        Assert.Equal(7, countries.Names.Count(name => name.StartsWith("Obsolete", StringComparison.Ordinal)));
    }

    [Fact]
    public void Lookup_returns_the_sheet1_spelling()
    {
        var countries = Sample.Countries;

        Assert.Equal("Korea, Republic of", countries.Canonical("korea, republic of"));
        Assert.Equal("Viet Nam", countries.Canonical("VIET NAM"));
        Assert.Equal("Taiwan", countries.Canonical("Taiwan"));
    }

    [Fact]
    public void Country_outside_the_list_is_not_written()
    {
        var countries = Sample.Countries;

        Assert.False(countries.Contains("Atlantis"));
        Assert.Null(countries.Canonical(""));
        Assert.Null(countries.Canonical(null));
    }
}

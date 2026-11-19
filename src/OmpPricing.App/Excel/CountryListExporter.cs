using ClosedXML.Excel;

namespace OmpPricing.App.Excel;

/// <summary>
/// Produces rules/countries.txt from Sheet1 of a client workbook, so nobody types 257 country names
/// by hand (docs/test-data.md).
/// </summary>
public static class CountryListExporter
{
    public static IReadOnlyList<string> FromWorkbook(byte[] workbook, ExcelOptions options)
    {
        using var spreadsheet = new XLWorkbook(new MemoryStream(workbook));
        var sheet = spreadsheet.Worksheets.FirstOrDefault(candidate => candidate.Name == options.CountrySheetName)
            ?? throw new InvalidOperationException("Country sheet not found: " + options.CountrySheetName);

        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;
        var names = new List<string>();

        for (var row = options.CountryFirstRow; row <= lastRow; row++)
        {
            var name = sheet.Cell(row, 1).GetString().Trim();
            if (name.Length > 0)
            {
                names.Add(name);
            }
        }

        return names;
    }
}

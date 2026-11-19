using System.Globalization;
using ClosedXML.Excel;
using OmpPricing.App.Pricing;

namespace OmpPricing.App.Excel;

/// <summary>The loaded client workbook plus its data lines.</summary>
public sealed class LoadedWorkbook : IDisposable
{
    public LoadedWorkbook(XLWorkbook workbook, IXLWorksheet sheet, IReadOnlyList<QuoteLine> lines)
    {
        Workbook = workbook;
        Sheet = sheet;
        Lines = lines;
    }

    public XLWorkbook Workbook { get; }

    public IXLWorksheet Sheet { get; }

    public IReadOnlyList<QuoteLine> Lines { get; }

    public void Dispose() => Workbook.Dispose();
}

/// <summary>
/// Reads the parts list by column position from configuration, never by header text (A4, A9.8),
/// and applies the input rules through <see cref="InputNormalizer"/>.
/// </summary>
public static class WorkbookReader
{
    public static LoadedWorkbook Load(byte[] content, ExcelOptions options)
    {
        var workbook = new XLWorkbook(new MemoryStream(content));

        var sheet = workbook.Worksheets.FirstOrDefault(candidate => candidate.Name == options.SheetName)
            ?? workbook.Worksheets.First();

        var columns = options.Columns;
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;
        var lines = new List<QuoteLine>();

        for (var row = options.FirstDataRow; row <= lastRow; row++)
        {
            var pn = CellText(sheet.Cell(row, columns.Pn));
            var description = CellText(sheet.Cell(row, columns.Description));
            var mfg = CellText(sheet.Cell(row, columns.Mfg));
            var mpn = CellText(sheet.Cell(row, columns.Mpn));
            var qty = CellText(sheet.Cell(row, columns.Qty));
            var eau = CellText(sheet.Cell(row, columns.Eau));

            if (pn.Length == 0 && description.Length == 0 && mfg.Length == 0 && mpn.Length == 0 && qty.Length == 0)
            {
                continue;
            }

            var normalized = InputNormalizer.Normalize(mfg, mpn, qty, eau);

            lines.Add(new QuoteLine
            {
                RowNumber = row,
                Pn = pn,
                Description = description,
                RawMfg = mfg,
                RawMpn = mpn,
                Mfg = normalized.Mfg,
                Mpn = normalized.Mpn,
                IsGeneric = normalized.IsGeneric,
                IsWildcard = normalized.IsWildcard,
                WildcardPrefix = normalized.WildcardPrefix,
                Alternates = normalized.Alternates,
                Qty = normalized.Qty,
                Eau = normalized.Eau,
                Status = normalized.Status,
                Remarks = normalized.Remarks,
            });
        }

        return new LoadedWorkbook(workbook, sheet, lines);
    }

    private static string CellText(IXLCell cell)
    {
        if (cell.IsEmpty())
        {
            return "";
        }

        return cell.DataType switch
        {
            XLDataType.Number => cell.GetDouble().ToString("G17", CultureInfo.InvariantCulture),
            XLDataType.Boolean => cell.GetBoolean().ToString(),
            _ => cell.GetString().Trim(),
        };
    }
}

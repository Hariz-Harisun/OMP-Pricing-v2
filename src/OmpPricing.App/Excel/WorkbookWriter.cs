using ClosedXML.Excel;
using OmpPricing.App.Pricing;

namespace OmpPricing.App.Excel;

/// <summary>
/// Writes the results into the sender's own workbook (A6 rule 8, A9.5): columns 7 to 21 for every
/// line, Remarks for exceptions, an Offers sheet, and highlighting for rows that are not Priced and
/// for each missing compulsory cell. Columns 1 to 6 are never touched.
/// </summary>
public static class WorkbookWriter
{
    private static readonly XLColor NotPricedRow = XLColor.FromHtml("#FFF2CC");
    private static readonly XLColor MissingCell = XLColor.FromHtml("#FFC7CE");
    private static readonly XLColor OffersHeader = XLColor.FromHtml("#DDEBF7");

    public static byte[] Write(
        XLWorkbook workbook,
        IXLWorksheet sheet,
        IReadOnlyList<QuoteLine> lines,
        ExcelOptions options)
    {
        var columns = options.Columns;

        foreach (var line in lines)
        {
            WriteLine(sheet, line, options);

            if (line.Status != LineStatus.Priced)
            {
                for (var column = 1; column <= columns.Remarks; column++)
                {
                    sheet.Cell(line.RowNumber, column).Style.Fill.BackgroundColor = NotPricedRow;
                }
            }

            foreach (var position in line.MissingColumns)
            {
                sheet.Cell(line.RowNumber, position).Style.Fill.BackgroundColor = MissingCell;
            }
        }

        WriteOffersSheet(workbook, options, lines);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void WriteLine(IXLWorksheet sheet, QuoteLine line, ExcelOptions options)
    {
        var columns = options.Columns;

        foreach (var (position, value) in line.Filled)
        {
            if (position == columns.Remarks || value is null)
            {
                continue;
            }

            var cell = sheet.Cell(line.RowNumber, position);
            switch (value)
            {
                case decimal money:
                    cell.Value = money;
                    cell.Style.NumberFormat.Format = "0.############";
                    break;
                case int number:
                    cell.Value = number;
                    break;
                case string text when text.Length == 0:
                    break;
                case string text:
                    cell.Value = text;
                    break;
                default:
                    cell.Value = value.ToString() ?? "";
                    break;
            }
        }

        var remarks = line.Filled.TryGetValue(columns.Remarks, out var filledRemarks)
            ? filledRemarks as string
            : line.Remarks;

        if (!string.IsNullOrWhiteSpace(remarks))
        {
            sheet.Cell(line.RowNumber, columns.Remarks).Value = remarks;
        }
    }

    private static void WriteOffersSheet(XLWorkbook workbook, ExcelOptions options, IReadOnlyList<QuoteLine> lines)
    {
        var existing = workbook.Worksheets.FirstOrDefault(candidate => candidate.Name == options.OffersSheetName);
        existing?.Delete();

        var sheet = workbook.Worksheets.Add(options.OffersSheetName);

        string[] headers =
        [
            "Line", "Status", "Selected", "Supplier", "MFG", "MPN", "Currency", "Unit Price",
            "MOQ", "SPQ", "Lead time (WKS)", "Packaging", "Stock", "Notes",
        ];

        for (var index = 0; index < headers.Length; index++)
        {
            var cell = sheet.Cell(1, index + 1);
            cell.Value = headers[index];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = OffersHeader;
        }

        var row = 2;
        foreach (var line in lines)
        {
            if (line.Offers.Count == 0)
            {
                sheet.Cell(row, 1).Value = line.RowNumber;
                sheet.Cell(row, 2).Value = line.Status.ToString();
                sheet.Cell(row, 3).Value = "";
                sheet.Cell(row, 14).Value = line.Remarks ?? "";
                row++;
                continue;
            }

            foreach (var offer in line.Offers)
            {
                var unitPrice = offer.PriceBreaks.Count > 0 ? offer.PriceBreaks[0].UnitPrice : (decimal?)null;

                sheet.Cell(row, 1).Value = line.RowNumber;
                sheet.Cell(row, 2).Value = line.Status.ToString();
                sheet.Cell(row, 3).Value = ReferenceEquals(offer, line.SelectedOffer) ? "Yes" : "No";
                sheet.Cell(row, 4).Value = offer.Provider;
                sheet.Cell(row, 5).Value = offer.Mfg;
                sheet.Cell(row, 6).Value = offer.Mpn;
                sheet.Cell(row, 7).Value = offer.Currency;

                if (unitPrice is decimal price)
                {
                    sheet.Cell(row, 8).Value = price;
                    sheet.Cell(row, 8).Style.NumberFormat.Format = "0.############";
                }

                if (offer.Moq is int moq)
                {
                    sheet.Cell(row, 9).Value = moq;
                }

                if (offer.StandardPackage is int standardPackage)
                {
                    sheet.Cell(row, 10).Value = standardPackage;
                }

                if (offer.LeadWeeks is int leadWeeks)
                {
                    sheet.Cell(row, 11).Value = leadWeeks;
                }

                sheet.Cell(row, 12).Value = offer.Packaging ?? "";

                if (offer.Stock is int stock)
                {
                    sheet.Cell(row, 13).Value = stock;
                }

                sheet.Cell(row, 14).Value = line.Remarks ?? "";
                row++;
            }
        }

        sheet.SheetView.FreezeRows(1);
        sheet.Columns().AdjustToContents(1, 60);
    }
}

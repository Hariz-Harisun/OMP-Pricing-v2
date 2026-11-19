using ClosedXML.Excel;

var wb = new XLWorkbook(@"C:\Users\hariz\Desktop\LT2026100007.xlsx");
Console.WriteLine($"Sheets: {string.Join(", ", wb.Worksheets.Select(s => s.Name))}");

foreach (var ws in wb.Worksheets)
{
    Console.WriteLine($"\n=== Sheet: {ws.Name} ===");
    Console.WriteLine($"Rows: {ws.RowsUsed().Count()}, Cols: {ws.ColumnsUsed().Count()}");
    
    var firstRow = ws.Row(1);
    for (int c = 1; c <= Math.Min(25, ws.ColumnsUsed().Count()); c++)
    {
        Console.WriteLine($"  Col {c}: '{firstRow.Cell(c).GetString()}'");
    }
    
    var row2 = ws.Row(2);
    for (int c = 1; c <= Math.Min(25, ws.ColumnsUsed().Count()); c++)
    {
        Console.WriteLine($"  Row2 Col {c}: '{row2.Cell(c).GetString()}'");
    }
}

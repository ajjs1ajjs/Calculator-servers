using OfficeOpenXml;

ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
var file = new FileInfo(@"D:\OpenCode GitHub\Calculator-servers\Калькулятор розрахунку ресурсів 1.xlsx");
using var pkg = new ExcelPackage(file);

foreach (var ws in pkg.Workbook.Worksheets)
{
    if (ws.Dimension == null) { Console.WriteLine($"\n=== Sheet: '{ws.Name}' (EMPTY) ==="); continue; }
    Console.WriteLine($"\n=== Sheet: '{ws.Name}' (Rows: {ws.Dimension.Rows}, Cols: {ws.Dimension.Columns}) ===");
    int maxRows = Math.Min(ws.Dimension.Rows, 80);
    for (int row = 1; row <= maxRows; row++)
    {
        var cells = new List<string>();
        for (int col = 1; col <= Math.Min(ws.Dimension.Columns, 12); col++)
        {
            var val = ws.Cells[row, col].Text?.Trim() ?? "";
            if (!string.IsNullOrEmpty(val))
                cells.Add($"[{col}]{val}");
        }
        if (cells.Count > 0)
            Console.WriteLine($"  R{row,2}: {string.Join(", ", cells)}");
    }
}

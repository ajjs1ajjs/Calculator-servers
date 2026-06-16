#r "D:\OpenCode GitHub\Calculator-servers\AIResourceCalculator\bin\Release\net10.0-windows\win-x64\EPPlus.dll"
using OfficeOpenXml;
ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
var file = new FileInfo(@"D:\OpenCode GitHub\Calculator-servers\Калькулятор розрахунку ресурсів 1.xlsx");
using var pkg = new ExcelPackage(file);
foreach (var ws in pkg.Workbook.Worksheets)
{
    Console.WriteLine($"\n=== Sheet: '{ws.Name}' (Rows: {ws.Dimension?.Rows}, Cols: {ws.Dimension?.Columns}) ===");
    for (int row = 1; row <= Math.Min(ws.Dimension?.Rows ?? 0, 80); row++)
    {
        var cells = new List<string>();
        for (int col = 1; col <= Math.Min(ws.Dimension?.Columns ?? 0, 12); col++)
        {
            var val = ws.Cells[row, col].Text?.Trim() ?? "";
            if (!string.IsNullOrEmpty(val))
                cells.Add($"[{col}]{val}");
        }
        if (cells.Count > 0)
            Console.WriteLine($"  R{row}: {string.Join(", ", cells)}");
    }
}

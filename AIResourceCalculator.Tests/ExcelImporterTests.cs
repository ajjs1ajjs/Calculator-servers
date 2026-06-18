using System.IO;
using OfficeOpenXml;
using AIResourceCalculator.Data;

namespace AIResourceCalculator.Tests;

public class ExcelImporterTests : IDisposable
{
    private readonly string _tempDir;

    public ExcelImporterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "AIResCalcTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    [Fact]
    public void Import_MsSqlSheet_ReturnsRanges()
    {
        var filePath = CreateTestMssqlFile();
        var importer = new ExcelImporter();
        var matrix = importer.Import(filePath);

        Assert.NotEmpty(matrix.MsSqlRanges);
    }

    [Fact]
    public void Import_MsSqlSheet_HasCorrectValues()
    {
        var filePath = CreateTestMssqlFile();
        var importer = new ExcelImporter();
        var matrix = importer.Import(filePath);

        var range = matrix.MsSqlRanges.FirstOrDefault(r => r.MinUsers == 1);
        Assert.NotNull(range);
        Assert.Equal(10, range!.MaxUsers);
        Assert.Equal(2, range.Cpu);
        Assert.Equal(4, range.RamMin);
        Assert.Equal(8, range.RamRec);
        Assert.Equal(200, range.Iops);
        Assert.Equal(8, range.Latency);
    }

    [Fact]
    public void Import_MsSqlSheet_PopulatesPerformanceRanges()
    {
        var filePath = CreateTestMssqlFile();
        var importer = new ExcelImporter();
        var matrix = importer.Import(filePath);

        Assert.NotEmpty(matrix.MsSqlPerformanceRanges);
        var perfRange = matrix.MsSqlPerformanceRanges.FirstOrDefault(r => r.MinUsers == 51);
        Assert.NotNull(perfRange);
        Assert.Equal(100, perfRange!.MaxUsers);
        Assert.Equal(6, perfRange.Cpu);
    }

    [Fact]
    public void Import_K8sSheet_ReturnsModules()
    {
        var filePath = CreateTestK8sFile();
        var importer = new ExcelImporter();
        var matrix = importer.Import(filePath);

        Assert.NotEmpty(matrix.StandardModules);
    }

    [Fact]
    public void Import_K8sSheet_HasAppServerModule()
    {
        var filePath = CreateTestK8sFile();
        var importer = new ExcelImporter();
        var matrix = importer.Import(filePath);

        var appServer = matrix.StandardModules.FirstOrDefault(m => m.Name == "App Server");
        Assert.NotNull(appServer);
        Assert.True(appServer!.IsEnabled);
    }

    [Fact]
    public void Import_K8sSheet_HasInfrastructureNodes()
    {
        var filePath = CreateTestK8sFile();
        var importer = new ExcelImporter();
        var matrix = importer.Import(filePath);

        Assert.NotNull(matrix.DefaultK8sSql);
        Assert.NotNull(matrix.DefaultK8sMaster);
        Assert.NotNull(matrix.DefaultK8sWorker);
    }

    [Fact]
    public void Import_WindowsSheet_ReturnsAppServerRanges()
    {
        var filePath = CreateTestWindowsFile();
        var importer = new ExcelImporter();
        var matrix = importer.Import(filePath);

        Assert.NotEmpty(matrix.AppServerRanges);
    }

    [Fact]
    public void Import_UnknownSheet_ReturnsDefaultMatrix()
    {
        var filePath = CreateUnknownSheetFile();
        var importer = new ExcelImporter();
        var matrix = importer.Import(filePath);

        Assert.NotNull(matrix);
        Assert.NotEmpty(matrix.MsSqlRanges);
        Assert.Equal(12, matrix.MsSqlRanges.Count);
    }

    [Fact]
    public void Import_EmptySheets_ReturnsDefaultMatrix()
    {
        var filePath = CreateEmptyFile();
        var importer = new ExcelImporter();
        var matrix = importer.Import(filePath);

        Assert.NotNull(matrix);
        Assert.NotEmpty(matrix.MsSqlRanges);
        Assert.NotEmpty(matrix.StandardModules);
    }

    private string CreateTestMssqlFile()
    {
        var filePath = Path.Combine(_tempDir, "test_mssql.xlsx");
        using var pkg = new ExcelPackage();
        var ws = pkg.Workbook.Worksheets.Add("MSSQL");

        ws.Cells[1, 1].Value = "Min";
        ws.Cells[1, 2].Value = "Max";
        ws.Cells[1, 3].Value = "CPU";
        ws.Cells[1, 4].Value = "RAM Min";
        ws.Cells[1, 5].Value = "RAM Rec";
        ws.Cells[1, 6].Value = "IOPS";
        ws.Cells[1, 7].Value = "Latency";

        ws.Cells[2, 1].Value = 1;
        ws.Cells[2, 2].Value = 10;
        ws.Cells[2, 3].Value = 2;
        ws.Cells[2, 4].Value = 4;
        ws.Cells[2, 5].Value = 8;
        ws.Cells[2, 6].Value = 200;
        ws.Cells[2, 7].Value = 8;

        ws.Cells[3, 1].Value = 11;
        ws.Cells[3, 2].Value = 25;
        ws.Cells[3, 3].Value = 4;
        ws.Cells[3, 4].Value = 8;
        ws.Cells[3, 5].Value = 12;
        ws.Cells[3, 6].Value = 250;
        ws.Cells[3, 7].Value = 7;

        ws.Cells[4, 1].Value = 26;
        ws.Cells[4, 2].Value = 50;
        ws.Cells[4, 3].Value = 6;
        ws.Cells[4, 4].Value = 16;
        ws.Cells[4, 5].Value = 24;
        ws.Cells[4, 6].Value = 300;
        ws.Cells[4, 7].Value = 5;

        // Performance (DocumentFlow) section — marked by the "Документообіг" label in col 1.
        ws.Cells[5, 1].Value = "Документообіг";
        ws.Cells[6, 1].Value = "Min";
        ws.Cells[6, 2].Value = "Max";
        ws.Cells[6, 3].Value = "CPU";
        ws.Cells[6, 4].Value = "RAM Min";
        ws.Cells[6, 5].Value = "RAM Rec";
        ws.Cells[6, 6].Value = "IOPS";
        ws.Cells[6, 7].Value = "Latency";

        ws.Cells[7, 1].Value = 51;
        ws.Cells[7, 2].Value = 100;
        ws.Cells[7, 3].Value = 6;
        ws.Cells[7, 4].Value = 32;
        ws.Cells[7, 5].Value = 48;
        ws.Cells[7, 6].Value = 500;
        ws.Cells[7, 7].Value = 4;

        pkg.SaveAs(new FileInfo(filePath));
        return filePath;
    }

    private string CreateTestK8sFile()
    {
        var filePath = Path.Combine(_tempDir, "test_k8s.xlsx");
        using var pkg = new ExcelPackage();
        var ws = pkg.Workbook.Worksheets.Add("k8s Standard");

        // Col 1 = header, Col 2 = name, Col 3 = CPU, Col 4 = RAM, Col 5 = Qty/Count
        ws.Cells[1, 1].Value = "Server";
        ws.Cells[1, 2].Value = "Name";
        ws.Cells[1, 3].Value = "CPU";
        ws.Cells[1, 4].Value = "RAM";
        ws.Cells[1, 5].Value = "Qty";

        // SQL Server node
        ws.Cells[2, 2].Value = "SQL Server";
        ws.Cells[2, 3].Value = 4;
        ws.Cells[2, 4].Value = 16;
        ws.Cells[2, 5].Value = 1;

        // Master node
        ws.Cells[3, 2].Value = "Master node";
        ws.Cells[3, 3].Value = 4;
        ws.Cells[3, 4].Value = 6;
        ws.Cells[3, 5].Value = 1;

        // Worker node
        ws.Cells[4, 2].Value = "Worker node";
        ws.Cells[4, 3].Value = 8;
        ws.Cells[4, 4].Value = 32;
        ws.Cells[4, 5].Value = 1;

        // Separator
        ws.Cells[5, 2].Value = "---";

        // Pods header
        ws.Cells[6, 2].Value = "Pods:";

        // App Server section
        ws.Cells[7, 2].Value = "AS (App Server)";
        ws.Cells[7, 3].Value = 1.0;
        ws.Cells[7, 4].Value = 8;
        ws.Cells[7, 5].Value = 4;

        ws.Cells[8, 2].Value = "AS-Local SQL";
        ws.Cells[8, 3].Value = 1.0;
        ws.Cells[8, 4].Value = 3;
        ws.Cells[8, 5].Value = 1;
        ws.Cells[8, 8].Value = "Has local SQL";

        ws.Cells[9, 2].Value = "AS-Redis";
        ws.Cells[9, 3].Value = 0.1;
        ws.Cells[9, 4].Value = 0.1;
        ws.Cells[9, 5].Value = 1;

        pkg.SaveAs(new FileInfo(filePath));
        return filePath;
    }

    private string CreateTestWindowsFile()
    {
        var filePath = Path.Combine(_tempDir, "test_windows.xlsx");
        using var pkg = new ExcelPackage();
        var ws = pkg.Workbook.Worksheets.Add("Windows");

        // AppServers block — marked by the "AppServers" label in col 1 (matches real workbook layout).
        ws.Cells[1, 1].Value = "AppServers";
        ws.Cells[1, 2].Value = "Стандарт";
        ws.Cells[2, 1].Value = "Min";
        ws.Cells[2, 2].Value = "Max";
        ws.Cells[2, 3].Value = "Quantity";
        ws.Cells[2, 4].Value = "GHz";
        ws.Cells[2, 5].Value = "CPU";
        ws.Cells[2, 6].Value = "IOPS";
        ws.Cells[2, 7].Value = "RAM Min";
        ws.Cells[2, 8].Value = "RAM Rec";

        ws.Cells[3, 1].Value = 1;
        ws.Cells[3, 2].Value = 10;
        ws.Cells[3, 3].Value = 1;
        ws.Cells[3, 4].Value = 2.0;
        ws.Cells[3, 5].Value = 4;
        ws.Cells[3, 6].Value = 250;
        ws.Cells[3, 7].Value = 6;
        ws.Cells[3, 8].Value = 8;

        pkg.SaveAs(new FileInfo(filePath));
        return filePath;
    }

    private string CreateEmptyFile()
    {
        var filePath = Path.Combine(_tempDir, "empty.xlsx");
        using var pkg = new ExcelPackage();
        pkg.Workbook.Worksheets.Add("CustomSheet");
        pkg.SaveAs(new FileInfo(filePath));
        return filePath;
    }

    private string CreateUnknownSheetFile()
    {
        var filePath = Path.Combine(_tempDir, "unknown.xlsx");
        using var pkg = new ExcelPackage();
        pkg.Workbook.Worksheets.Add("CustomData");
        pkg.SaveAs(new FileInfo(filePath));
        return filePath;
    }
}
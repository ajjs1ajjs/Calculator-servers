using System.IO;
using System.Linq;
using ResourceCalculator.Models;
using ResourceCalculator.Services;
using OfficeOpenXml;

namespace ResourceCalculator.Tests;

public class ConfigExportServiceTests
{
    private readonly ConfigExportService _svc;
    private readonly ResourceRequirement _req;
    private readonly ProjectConfig _config;

    public ConfigExportServiceTests()
    {
        _svc = new ConfigExportService();
        _req = new ResourceRequirement
        {
            UserCount = 100,
            DeploymentType = DeploymentType.Kubernetes,
            LoadProfile = LoadProfile.Performance,
            TotalCpu = 24,
            TotalRamGb = 96,
            TotalStorageGb = 1500,
            TotalIops = 5000,
            WorkerNodeCount = 3,
            MasterNodeCount = 1
        };
        _req.Components.Add(new ServiceComponent { Name = "Web", Cpu = 4, RamGb = 16, Replicas = 2 });
        _req.Infrastructure.Add(new InfrastructureNode { Name = "SQL Server", Cpu = 4, RamGb = 16, NodeCount = 1, StorageGb = 300, StorageGb2 = 150 });
        _req.Infrastructure.Add(new InfrastructureNode { Name = "Worker Node", Cpu = 8, RamGb = 32, NodeCount = 3, StorageGb = 200 });

        _config = new ProjectConfig
        {
            ProjectName = "TestProject",
            UserCount = 100,
            DeploymentType = DeploymentType.Kubernetes,
            LoadProfile = LoadProfile.Performance
        };
    }

    [Fact]
    public void ExportPdf_ProducesNonEmptyPdf()
    {
        var bytes = _svc.ExportPdf(_req, _config);
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0);
        // PDF починається із сигнатури "%PDF".
        Assert.Equal((byte)'%', bytes[0]);
        Assert.Equal((byte)'P', bytes[1]);
        Assert.Equal((byte)'D', bytes[2]);
        Assert.Equal((byte)'F', bytes[3]);
    }

    [Fact]
    public void ExportPdf_WithEnvironments_ProducesPdf()
    {
        var envs = new List<EnvironmentReport>
        {
            new() { Name = "PROD", UserCount = 100, Requirement = _req },
            new() { Name = "DEV",  UserCount = 10,  Requirement = _req },
            new() { Name = "TEST", UserCount = 25,  Requirement = _req },
        };
        var bytes = _svc.ExportPdf(_req, _config, envs);
        Assert.True(bytes.Length > 0);
        Assert.Equal((byte)'%', bytes[0]);
        Assert.Equal((byte)'P', bytes[1]);
    }

    [Fact]
    public void ExportExcel_ProducesNonEmptyWorkbook()
    {
        var bytes = _svc.ExportExcel(_req, _config);
        Assert.NotNull(bytes);
        // .xlsx — це ZIP, тож починається з сигнатури "PK".
        Assert.True(bytes.Length > 0);
        Assert.Equal((byte)'P', bytes[0]);
        Assert.Equal((byte)'K', bytes[1]);
    }

    [Fact]
    public void ExportExcel_HasNoSeparateDiskRequirementsSheet()
    {
        // Вимоги до дисків тепер колонками в "Інфраструктура", не окремим аркушем.
        var bytes = _svc.ExportExcel(_req, _config);
        using var pkg = new ExcelPackage(new MemoryStream(bytes));
        Assert.Null(pkg.Workbook.Worksheets["Вимоги до дисків"]);
    }

    [Fact]
    public void ExportExcel_InfrastructureSheet_HasDiskBreakdownColumns()
    {
        var bytes = _svc.ExportExcel(_req, _config);
        using var pkg = new ExcelPackage(new MemoryStream(bytes));
        var ws = pkg.Workbook.Worksheets["Інфраструктура"];
        Assert.NotNull(ws);
        var headerRow = Enumerable.Range(1, ws!.Dimension.Rows)
            .First(r => ws.Cells[r, 1].Text == "Сервер (ВМ)");
        var headers = Enumerable.Range(1, ws.Dimension.Columns).Select(cidx => ws.Cells[headerRow, cidx].Text).ToList();
        Assert.Contains("Частота, ГГц", headers);
        Assert.Contains("Диск ОС (ГБ)", headers);
        Assert.Contains("Диск Logs/TempDB (ГБ)", headers);
        Assert.Contains("Диск MainData (ГБ)", headers);
        Assert.Contains("Диск Content (ГБ)", headers);
    }

    [Fact]
    public void ExportExcel_WithEnvironments_ProducesWorkbook()
    {
        // Кілька середовищ → задіюються зведені аркуші (ВМ та Компоненти по середовищах).
        var envs = new List<EnvironmentReport>
        {
            new() { Name = "PROD", UserCount = 100, Requirement = _req },
            new() { Name = "DEV",  UserCount = 10,  Requirement = _req },
            new() { Name = "TEST", UserCount = 25,  Requirement = _req },
        };
        var bytes = _svc.ExportExcel(_req, _config, envs);
        Assert.True(bytes.Length > 0);
        Assert.Equal((byte)'P', bytes[0]);
        Assert.Equal((byte)'K', bytes[1]);
    }

    // Регресія: аркуш «Інфраструктура» має містити ВСІ середовища (не лише PROD).
    [Fact]
    public void ExportExcel_InfrastructureSheet_CoversAllEnvironments()
    {
        var envs = new List<EnvironmentReport>
        {
            new() { Name = "PROD", UserCount = 100, Requirement = _req },
            new() { Name = "DEV",  UserCount = 10,  Requirement = _req },
            new() { Name = "TEST", UserCount = 25,  Requirement = _req },
        };
        var bytes = _svc.ExportExcel(_req, _config, envs);

        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        using var pkg = new ExcelPackage(new MemoryStream(bytes));
        var ws = pkg.Workbook.Worksheets["Інфраструктура"];
        Assert.NotNull(ws);
        var text = string.Join("\n", ws!.Cells[ws.Dimension.Address]
            .Where(c => c.Value is string).Select(c => c.Text));
        Assert.Contains("середовище PROD", text);
        Assert.Contains("середовище DEV", text);
        Assert.Contains("середовище TEST", text);
    }

}

using System.IO;
using OfficeOpenXml;
using AIResourceCalculator.Data;
using AIResourceCalculator.Models;
using AIResourceCalculator.Services;

namespace AIResourceCalculator.Tests;

// Integration tests that import the REAL knowledge-base workbook (Калькулятор.xlsx) shipped
// in the repo root, and assert the parsed matrix faithfully reproduces it. These lock the
// importer against the actual file layout (Ukrainian sheet names, header rows, range tables).
public class RealExcelImportTests
{
    private static string? FindWorkbook()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (int i = 0; i < 8 && dir != null; i++, dir = dir.Parent)
        {
            var path = Path.Combine(dir.FullName, "Калькулятор.xlsx");
            if (File.Exists(path)) return path;
        }
        return null;
    }

    private static SizingMatrix Import()
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        var wb = FindWorkbook();
        Assert.True(wb != null, "Калькулятор.xlsx not found in repo tree");
        return new ExcelImporter().Import(wb!);
    }

    [Fact]
    public void Import_MsSql_IncludesFirstRange_1to10()
    {
        var m = Import();
        var first = m.MsSqlRanges.FirstOrDefault(r => r.MinUsers == 1 && r.MaxUsers == 10);
        Assert.NotNull(first);                 // regression: the 1-10 range used to be dropped
        Assert.Equal(2, first!.Cpu);
        Assert.Equal(8, first.RamRec);
    }

    [Fact]
    public void Import_MsSql_HasAll12StandardRanges()
    {
        var m = Import();
        Assert.Equal(12, m.MsSqlRanges.Count);
        var r = m.MsSqlRanges.First(x => x.MinUsers == 51 && x.MaxUsers == 100);
        Assert.Equal(8, r.Cpu);
        Assert.Equal(48, r.RamRec);
    }

    [Fact]
    public void Import_MsSqlPerformance_DocumentFlowTable_IsSeparate()
    {
        var m = Import();
        Assert.Equal(12, m.MsSqlPerformanceRanges.Count);
        // DocumentFlow 26-50 has Cpu=4 (vs 6 in Standard) — confirms the perf block is read distinctly.
        var r = m.MsSqlPerformanceRanges.First(x => x.MinUsers == 26 && x.MaxUsers == 50);
        Assert.Equal(4, r.Cpu);
        Assert.Equal(24, r.RamRec);
    }

    [Fact]
    public void Import_AppServers_IncludeFirstRange_1to10()
    {
        var m = Import();
        var first = m.AppServerRanges.FirstOrDefault(r => r.MinUsers == 1 && r.MaxUsers == 10);
        Assert.NotNull(first);
        Assert.Equal(1, first!.InstanceCount);
        Assert.Equal(8, first.RamRec);
        // 51-100 has 2 instances per the table.
        Assert.Equal(2, m.AppServerRanges.First(r => r.MinUsers == 51).InstanceCount);
    }

    [Fact]
    public void Import_WebServers_IncludeFirstRange_1to50()
    {
        var m = Import();
        var first = m.WebServerRanges.FirstOrDefault(r => r.MinUsers == 1 && r.MaxUsers == 50);
        Assert.NotNull(first);
        Assert.Equal(6, first!.RamRec);
    }

    [Fact]
    public void Import_AppServersPerformance_IsPopulated()
    {
        var m = Import();
        Assert.NotEmpty(m.AppServerPerformanceRanges);
        // DocumentFlow app servers jump to 1200 IOPS at 26-50.
        var r = m.AppServerPerformanceRanges.First(x => x.MinUsers == 26 && x.MaxUsers == 50);
        Assert.Equal(1200, r.Iops);
    }

    [Fact]
    public void Import_K8sStandardModules_ArePopulated()
    {
        var m = Import();
        Assert.NotEmpty(m.StandardModules);
        Assert.Contains(m.StandardModules.SelectMany(x => x.Components), c => c.Name.Contains("AS"));
    }

    [Fact]
    public void ImportedMatrix_DoesNotIngestInfraNodesAsComponents()
    {
        var m = Import();
        // Regression: the SQL/Master/Worker infrastructure rows must NOT become pod components
        // (that bug inflated 100-user sizing to ~152 vCPU / 20 worker nodes).
        var comps = m.StandardModules.SelectMany(x => x.Components).Select(c => c.Name);
        Assert.DoesNotContain(comps, n => n.Contains("Worker", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(comps, n => n.Contains("Master", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ImportedMatrix_100Users_ProducesReasonableTotals()
    {
        var m = Import();
        var engine = new SizingEngine(m);
        engine.SetProductType(ProductType.Standard);
        var req = engine.Calculate(new ProjectConfig
        {
            UserCount = 100, DeploymentType = DeploymentType.Kubernetes,
            ProductType = ProductType.Standard, LoadProfile = LoadProfile.Basic, DatabaseType = DatabaseType.MsSql
        });
        // 100 users on a default 8 vCPU / 32 GB worker should need only a handful of workers.
        Assert.True(req.TotalCpu < 60, $"TotalCpu too high: {req.TotalCpu}");
        Assert.True(req.WorkerNodeCount <= 6, $"Worker nodes too high: {req.WorkerNodeCount}");
    }

    [Fact]
    public void Import_LmsModule_KeepsCoreLmsComponent()
    {
        var m = Import();
        var lms = m.StandardModules.FirstOrDefault(x => x.Name == "LMS");
        Assert.NotNull(lms);
        // The core "LMS" pod must not be swallowed by the section header of the same name.
        Assert.Contains(lms!.Components, c => c.Name == "LMS");
    }

    [Fact]
    public void Windows_100Users_IsVmOnly_NoModuleDoubleCount()
    {
        var m = Import();
        var engine = new SizingEngine(m);
        engine.SetProductType(ProductType.Standard);
        var req = engine.Calculate(new ProjectConfig
        {
            UserCount = 100, DeploymentType = DeploymentType.Windows,
            ProductType = ProductType.Standard, LoadProfile = LoadProfile.Basic, DatabaseType = DatabaseType.MsSql
        });
        // VM-only: SQL(8) + App 4x2 + Web 4x1 = 20 vCPU. If pod modules were added it would be ~44.
        Assert.True(req.TotalCpu <= 24, $"Windows TotalCpu should be VM-only (~20), got {req.TotalCpu}");
    }

    [Fact]
    public void Import_SqlNode_HasMultiDiskLayout()
    {
        var m = Import();
        var sql = m.DefaultK8sSql;
        Assert.NotNull(sql);
        // The SQL row defines OS / Logs+TempDB / MainData / Content disks (sizes annotated "150*").
        // The asterisk-tolerant parser must still read them (regression: used to import as 0).
        Assert.True(sql!.StorageGb > 0, "OS disk");
        Assert.True(sql.StorageGb2 > 0, "Logs/TempDB disk");
        Assert.True(sql.StorageGb3 > 0, "MainData disk");
        Assert.True(sql.StorageGb4 > 0, "Content disk");
    }

    [Fact]
    public void ImportedMatrix_CalculatesK8s_ForSmallUserCount()
    {
        var m = Import();
        var engine = new SizingEngine(m);
        engine.SetProductType(ProductType.Standard);
        var req = engine.Calculate(new ProjectConfig
        {
            UserCount = 5,                       // uses the 1-10 DB range that used to be missing
            DeploymentType = DeploymentType.Kubernetes,
            ProductType = ProductType.Standard,
            LoadProfile = LoadProfile.Basic,
            DatabaseType = DatabaseType.MsSql
        });
        Assert.True(req.TotalCpu > 0);
        Assert.True(req.WorkerNodeCount >= 1);
        var sql = req.Infrastructure.FirstOrDefault(n => n.Name.Contains("SQL"));
        Assert.NotNull(sql);
        Assert.Equal(2, sql!.Cpu);               // 1-10 DB range → 2 vCPU (Excel VLOOKUP result)
    }
}

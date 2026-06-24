using AIResourceCalculator.Models;
using AIResourceCalculator.Services;

namespace AIResourceCalculator.Tests;

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
            LoadProfile = LoadProfile.Basic,
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
            LoadProfile = LoadProfile.Basic
        };
    }

    [Fact]
    public void ExportXml_ContainsProjectAndTotals()
    {
        var result = _svc.ExportXml(_req, _config);
        Assert.Contains("TestProject", result);
        Assert.Contains("<ResourceReport", result);
        Assert.Contains("<Totals", result);
        Assert.Contains("iopsDb=\"5000\"", result);
    }

    [Fact]
    public void ExportXml_IsCloudAgnostic()
    {
        var result = _svc.ExportXml(_req, _config);
        Assert.DoesNotContain("Azure", result);
        Assert.DoesNotContain("Standard_", result);
    }

    [Fact]
    public void ExportXml_EscapesMaliciousNodeName()
    {
        var req = new ResourceRequirement { DeploymentType = DeploymentType.Kubernetes };
        req.Infrastructure.Add(new InfrastructureNode { Name = "<script>", Cpu = 4, RamGb = 16, NodeCount = 1, StorageGb = 100 });
        var result = _svc.ExportXml(req, _config);
        Assert.DoesNotContain("<script>", result);
        Assert.Contains("&lt;script&gt;", result);
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
    public void ExportHtml_ContainsHtmlStructure()
    {
        var result = _svc.ExportHtml(_req, _config);
        Assert.Contains("<html", result);
        Assert.Contains("</html>", result);
        Assert.Contains("CPU", result);
        // Регресія: термін vCPU замінено на CPU.
        Assert.DoesNotContain("vCPU", result);
    }

    [Fact]
    public void ExportHtml_ShowsPerNodeDiskTotal()
    {
        // SQL-вузол: 300 + 150 = 450 GB на вузол.
        var result = _svc.ExportHtml(_req, _config);
        Assert.Contains("450", result);
    }

    [Fact]
    public void ExportHtml_IsCloudAgnostic()
    {
        var result = _svc.ExportHtml(_req, _config);
        Assert.DoesNotContain("Azure", result);
        Assert.DoesNotContain("Standard_", result);
    }

    // Регресія: екранування XSS у HTML-звіті (назви з імпортованого Excel)
    [Fact]
    public void ExportHtml_EscapesMaliciousNodeName()
    {
        var req = new ResourceRequirement { DeploymentType = DeploymentType.Kubernetes };
        req.Infrastructure.Add(new InfrastructureNode { Name = "<script>alert(1)</script>", Cpu = 4, RamGb = 16, NodeCount = 1, StorageGb = 100 });
        var result = _svc.ExportHtml(req, _config);
        Assert.DoesNotContain("<script>alert(1)</script>", result);
        Assert.Contains("&lt;script&gt;", result);
    }

    [Fact]
    public void SanitizeHtml_EscapesAngleBracketsAndQuotes()
    {
        Assert.Equal("&lt;b&gt;", ConfigExportService.SanitizeHtml("<b>"));
        Assert.Equal("&quot;x&quot;", ConfigExportService.SanitizeHtml("\"x\""));
        Assert.Equal("", ConfigExportService.SanitizeHtml(""));
    }
}

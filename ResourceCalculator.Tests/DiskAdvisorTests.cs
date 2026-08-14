using ResourceCalculator.Interfaces;
using ResourceCalculator.Localization;
using ResourceCalculator.Models;
using ResourceCalculator.Services;

namespace ResourceCalculator.Tests;

public class DiskAdvisorTests
{
    private static readonly ILocalizationService Loc = LocalizationService.Instance;

    private static ResourceRequirement WithSql(DeploymentType deploy, Action<InfrastructureNode>? cfg = null)
    {
        var req = new ResourceRequirement { DeploymentType = deploy };
        var sql = new InfrastructureNode { Name = "SQL Server", Cpu = 8, RamGb = 48, NodeCount = 1, StorageGb = 300, StorageType = "SSD" };
        cfg?.Invoke(sql);
        req.Infrastructure.Add(sql);
        return req;
    }

    [Fact]
    public void Sql_WithoutExplicitSplit_ComputesRecommendedLayout()
    {
        var req = WithSql(DeploymentType.Kubernetes);
        var text = DiskAdvisor.Build(req, new ProjectConfig { DeploymentType = DeploymentType.Kubernetes }, Loc);

        Assert.False(string.IsNullOrWhiteSpace(text));      // panel must never be empty for a SQL node
        Assert.Contains("SQL Server", text);
        Assert.Contains(Loc["disk.os"], text);
        Assert.Contains(Loc["disk.data"], text);
        Assert.Contains(Loc["disk.logs"], text);
    }

    [Fact]
    public void Sql_OnWindows_HasNoPageFile()
    {
        var req = WithSql(DeploymentType.Windows, n => { n.PageFileGb = 0; });
        var text = DiskAdvisor.Build(req, new ProjectConfig { DeploymentType = DeploymentType.Windows }, Loc);
        Assert.DoesNotContain(Loc["disk.pagefileLine"].Split(':')[0], text); // no "Файл підкачки"/"Page file" for SQL
    }

    [Fact]
    public void Node_WithIops_ShowsIopsAndLatency()
    {
        var req = WithSql(DeploymentType.Windows, n => { n.Iops = 800; n.IopsProfile = "50r/50w"; n.Latency = 3; });
        var text = DiskAdvisor.Build(req, new ProjectConfig { DeploymentType = DeploymentType.Windows }, Loc);
        Assert.Contains("800", text);
        Assert.Contains("50r/50w", text);
    }

    [Fact]
    public void AppServer_WithPageFile_ShowsPageFile()
    {
        var req = new ResourceRequirement { DeploymentType = DeploymentType.Windows };
        req.Infrastructure.Add(new InfrastructureNode { Name = "App Server", Cpu = 4, RamGb = 24, NodeCount = 3, StorageGb = 150, StorageType = "SSD", PageFileGb = 96, PageFileType = "SSD" });
        var text = DiskAdvisor.Build(req, new ProjectConfig { DeploymentType = DeploymentType.Windows }, Loc);
        Assert.Contains("96", text);   // page file allowed on non-SQL nodes
    }

    [Fact]
    public void Sql_WithMatrixSplit_HonorsImportedSizes()
    {
        var req = WithSql(DeploymentType.Kubernetes, n =>
        {
            n.StorageGb = 150; n.StorageType = "SSD";
            n.StorageGb2 = 150; n.StorageType2 = "SSD";
            n.StorageGb3 = 300; n.StorageType3 = "SSD";
            n.StorageGb4 = 200; n.StorageType4 = "SATA";
        });
        var text = DiskAdvisor.Build(req, new ProjectConfig { DeploymentType = DeploymentType.Kubernetes }, Loc);
        Assert.Contains("300", text);   // MainData from matrix
        Assert.Contains("200", text);   // Content from matrix
    }

    [Fact]
    public void NonDatabaseNode_WithoutSplit_StillShowsOsDisk()
    {
        // Kubernetes-вузли (Master/Worker) без IOPS/pagefile теж мають показувати свій OS-диск —
        // так само, як SQL, а не пропускатися мовчки.
        var req = new ResourceRequirement { DeploymentType = DeploymentType.Kubernetes };
        req.Infrastructure.Add(new InfrastructureNode { Name = "Worker Node", Cpu = 8, RamGb = 32, NodeCount = 3, StorageGb = 200 });
        var text = DiskAdvisor.Build(req, new ProjectConfig { DeploymentType = DeploymentType.Kubernetes }, Loc);
        Assert.False(string.IsNullOrWhiteSpace(text));
        Assert.Contains("Worker Node", text);
        Assert.Contains(Loc["disk.os"], text);
    }
}

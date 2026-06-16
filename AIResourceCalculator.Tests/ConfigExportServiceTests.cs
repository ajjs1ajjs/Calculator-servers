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
        _req.Infrastructure.Add(new InfrastructureNode { Name = "SQL Server", Cpu = 4, RamGb = 16, NodeCount = 1, StorageGb = 300 });
        _req.Infrastructure.Add(new InfrastructureNode { Name = "Master Node", Cpu = 4, RamGb = 8, NodeCount = 1, StorageGb = 100 });
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
    public void ExportTxt_ContainsAzureProvider()
    {
        var result = _svc.ExportTxt(_req, _config);
        Assert.Contains("Azure", result);
    }

    [Fact]
    public void ExportHtml_ContainsAzureProvider()
    {
        var result = _svc.ExportHtml(_req, _config);
        Assert.Contains("Azure", result);
    }

    [Fact]
    public void ExportTerraform_ContainsAzurermProvider()
    {
        var result = _svc.ExportTerraform(_req, _config);
        Assert.Contains("azurerm", result);
        Assert.Contains("resource_group", result);
    }

    [Fact]
    public void ExportTerraform_ContainsAksCluster()
    {
        var result = _svc.ExportTerraform(_req, _config);
        Assert.Contains("kubernetes_cluster", result);
    }

    [Fact]
    public void ExportArmTemplate_ContainsAzureJson()
    {
        var result = _svc.ExportArmTemplate(_req, _config);
        Assert.Contains("Microsoft.Compute/virtualMachines", result);
        Assert.Contains("deploymentTemplate", result);
    }

    [Fact]
    public void ExportBicep_ContainsAzureResources()
    {
        var result = _svc.ExportBicep(_req, _config);
        Assert.Contains("Microsoft.Compute/virtualMachines", result);
        Assert.Contains("param projectName", result);
    }

    [Fact]
    public void ExportPulumi_ContainsAzureNamespaces()
    {
        var result = _svc.ExportPulumi(_req, _config);
        Assert.Contains("AzureNative", result);
        Assert.Contains("ResourceGroup", result);
    }

    [Fact]
    public void ExportMermaid_ContainsAzureLabels()
    {
        var result = _svc.ExportMermaid(_req, _config);
        Assert.Contains("Load Balancer", result);
    }

    [Fact]
    public void ExportSvg_ContainsAzureTitle()
    {
        var result = _svc.ExportSvg(_req, _config);
        Assert.Contains("Azure", result);
    }

    [Fact]
    public void ExportHld_ContainsAzureProvider()
    {
        var result = _svc.ExportHld(_req, _config);
        Assert.Contains("Azure", result);
    }

    [Fact]
    public void ExportAnsible_ContainsAzure()
    {
        var result = _svc.ExportAnsible(_req, _config);
        Assert.Contains("Azure", result);
    }

    [Fact]
    public void GetAzureVmSize_CorrectForSmall()
    {
        var result = _svc.ExportTxt(_req, _config);
        Assert.Contains("Standard", result);
    }

    [Fact]
    public void ExportBicep_WindowsDeployment_NoAks()
    {
        var winReq = new ResourceRequirement { DeploymentType = DeploymentType.Windows, TotalCpu = 16, TotalRamGb = 64 };
        winReq.Infrastructure.Add(new InfrastructureNode { Name = "SQL Server", Cpu = 4, RamGb = 16, NodeCount = 1 });
        var winConfig = new ProjectConfig { ProjectName = "WinTest", UserCount = 50, DeploymentType = DeploymentType.Windows };
        var result = _svc.ExportBicep(winReq, winConfig);
        Assert.DoesNotContain("ContainerService", result);
    }

    [Fact]
    public void ExportArmTemplate_Hybrid_ContainsVmResources()
    {
        var hReq = new ResourceRequirement { DeploymentType = DeploymentType.Hybrid, TotalCpu = 40, TotalRamGb = 160 };
        hReq.Infrastructure.Add(new InfrastructureNode { Name = "Worker Node", Cpu = 8, RamGb = 32, NodeCount = 3 });
        var hConfig = new ProjectConfig { ProjectName = "Hybrid", UserCount = 200, DeploymentType = DeploymentType.Hybrid };
        var result = _svc.ExportArmTemplate(hReq, hConfig);
        Assert.Contains("Microsoft.Compute", result);
    }
}

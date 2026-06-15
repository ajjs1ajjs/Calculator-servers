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
    public void ExportTxt_ContainsAllSections()
    {
        var result = _svc.ExportTxt(_req, _config);

        Assert.Contains("Resource Report", result);
        Assert.Contains("vCPU", result);
        Assert.Contains("RAM", result);
        Assert.Contains("Storage", result);
        Assert.Contains("Infrastructure", result);
        Assert.Contains("Components", result);
        Assert.Contains("TestProject", result);
    }

    [Fact]
    public void ExportPdf_ContainsHtmlStructure()
    {
        var result = _svc.ExportPdf(_req, _config);

        Assert.StartsWith("<!DOCTYPE html>", result);
        Assert.Contains("<h1>", result);
        Assert.Contains("</body></html>", result);
        Assert.Contains("Kubernetes", result);
    }

    [Fact]
    public void ExportTerraform_ContainsResourceBlocks()
    {
        var result = _svc.ExportTerraform(_req, _config);

        Assert.Contains("aws_instance", result);
        Assert.Contains("aws_eks_cluster", result);
        Assert.Contains("TestProject", result);
        Assert.Contains("instance_type", result);
    }

    [Fact]
    public void ExportAnsible_ContainsTasks()
    {
        var result = _svc.ExportAnsible(_req, _config);

        Assert.Contains("hosts: all", result);
        Assert.Contains("tasks:", result);
        Assert.Contains("include_role", result);
    }

    [Fact]
    public void ExportHld_ContainsSections()
    {
        var result = _svc.ExportHld(_req, _config);

        Assert.Contains("High-Level Design", result);
        Assert.Contains("Project Overview", result);
        Assert.Contains("Resource Requirements", result);
        Assert.Contains("Infrastructure", result);
    }

    [Fact]
    public void ExportMermaid_ContainsGraphDefinition()
    {
        var result = _svc.ExportMermaid(_req, _config);

        Assert.Contains("mermaid", result);
        Assert.Contains("graph TD", result);
        Assert.Contains("Load Balancer", result);
    }

    [Fact]
    public void ExportSvg_ContainsSvgMarkup()
    {
        var result = _svc.ExportSvg(_req, _config);

        Assert.StartsWith("<svg", result);
        Assert.Contains("</svg>", result);
        Assert.Contains("TestProject", result);
        Assert.Contains("rect", result);
    }

    [Fact]
    public void ExportPulumi_ContainsPulumiCode()
    {
        var result = _svc.ExportPulumi(_req, _config);

        Assert.Contains("using Pulumi", result);
        Assert.Contains("new Instance", result);
        Assert.Contains("new Cluster", result);
        Assert.Contains("InstanceArgs", result);
    }

    [Fact]
    public void ExportCloudFormation_ContainsCfnStructure()
    {
        var result = _svc.ExportCloudFormation(_req, _config);

        Assert.Contains("AWSTemplateFormatVersion", result);
        Assert.Contains("Resources:", result);
        Assert.Contains("AWS::EC2::LaunchTemplate", result);
        Assert.Contains("AWS::AutoScaling::AutoScalingGroup", result);
        Assert.Contains("AWS::EKS::Cluster", result);
    }

    [Fact]
    public void ExportTxt_ShowsCorrectUserCount()
    {
        var result = _svc.ExportTxt(_req, _config);

        Assert.Contains("100", result);
    }

    [Fact]
    public void ExportPulumi_WindowsDeployment_DoesNotIncludeEks()
    {
        var winReq = new ResourceRequirement
        {
            DeploymentType = DeploymentType.Windows,
            TotalCpu = 16, TotalRamGb = 64
        };
        winReq.Infrastructure.Add(new InfrastructureNode { Name = "SQL Server", Cpu = 4, RamGb = 16, NodeCount = 1 });
        winReq.Infrastructure.Add(new InfrastructureNode { Name = "App Server", Cpu = 4, RamGb = 16, NodeCount = 1 });
        winReq.Infrastructure.Add(new InfrastructureNode { Name = "Web Server (IIS)", Cpu = 4, RamGb = 8, NodeCount = 1 });
        var winConfig = new ProjectConfig { ProjectName = "WinTest", UserCount = 50, DeploymentType = DeploymentType.Windows };

        var result = _svc.ExportPulumi(winReq, winConfig);

        Assert.DoesNotContain("EKS", result);
        Assert.DoesNotContain("Cluster", result);
    }

    [Fact]
    public void ExportCloudFormation_HybridDeployment_ContainsBothResources()
    {
        var hybridReq = new ResourceRequirement
        {
            DeploymentType = DeploymentType.Hybrid,
            TotalCpu = 40, TotalRamGb = 160
        };
        hybridReq.Infrastructure.Add(new InfrastructureNode { Name = "SQL Server", Cpu = 4, RamGb = 16, NodeCount = 1 });
        hybridReq.Infrastructure.Add(new InfrastructureNode { Name = "Worker Node", Cpu = 8, RamGb = 32, NodeCount = 3 });
        var hybridConfig = new ProjectConfig { ProjectName = "HybridTest", UserCount = 200, DeploymentType = DeploymentType.Hybrid };

        var result = _svc.ExportCloudFormation(hybridReq, hybridConfig);

        Assert.Contains("AWSTemplateFormatVersion", result);
        Assert.Contains("AutoScalingGroup", result);
    }
}

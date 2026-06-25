using AIResourceCalculator.Models;
using AIResourceCalculator.Services;

namespace AIResourceCalculator.Tests;

public class ResultsPresenterTests
{
    private readonly ResultsPresenter _presenter = new(new ConfigExportService(), new ValidationEngine());
    private readonly ResourceRequirement _req;
    private readonly ResourceRequirement _perfReq;
    private readonly ProjectConfig _config;

    public ResultsPresenterTests()
    {
        _req = new ResourceRequirement
        {
            UserCount = 100, DeploymentType = DeploymentType.Kubernetes,
            LoadProfile = LoadProfile.Basic, TotalCpu = 24, TotalRamGb = 96,
            TotalStorageGb = 1500, TotalIops = 5000, WorkerNodeCount = 3, MasterNodeCount = 1
        };
        _req.Components.Add(new ServiceComponent { Name = "AS", Cpu = 10, RamGb = 80, Replicas = 4 });
        _req.Infrastructure.Add(new InfrastructureNode { Name = "SQL Server", Cpu = 4, RamGb = 16, NodeCount = 1 });
        _req.Infrastructure.Add(new InfrastructureNode { Name = "Worker Node", Cpu = 8, RamGb = 32, NodeCount = 3 });

        _perfReq = new ResourceRequirement
        {
            UserCount = 100, DeploymentType = DeploymentType.Kubernetes,
            LoadProfile = LoadProfile.Performance, TotalCpu = 32, TotalRamGb = 128,
            TotalStorageGb = 2000, TotalIops = 8000, WorkerNodeCount = 4, MasterNodeCount = 1
        };

        _config = new ProjectConfig
        {
            ProjectName = "Test", UserCount = 100,
            DeploymentType = DeploymentType.Kubernetes, LoadProfile = LoadProfile.Basic
        };
    }

    [Fact]
    public void CompareProfiles_ReturnsValidationResults()
    {
        var results = _presenter.CompareProfiles(_req, _perfReq);
        Assert.NotEmpty(results);
        Assert.Contains(results, r => r.ResourceName == "CPU");
        Assert.Contains(results, r => r.ResourceName == "RAM");
        Assert.Contains(results, r => r.ResourceName is "Storage" or "Сховище");
        Assert.Contains(results, r => r.ResourceName == "IOPS");
    }

    [Fact]
    public void CompareProfiles_BasicVsPerf_DetectsDifferences()
    {
        var results = _presenter.CompareProfiles(_req, _perfReq);
        var cpuResult = results.First(r => r.ResourceName == "CPU");
        Assert.NotEqual(0, cpuResult.Delta);
        Assert.NotNull(cpuResult.Severity);
    }

    [Fact]
    public void Validate_EqualRequirements_ReturnsOk()
    {
        var results = _presenter.Validate(_req, _req);
        Assert.All(results, r => Assert.Equal("OK", r.Severity));
    }

    [Fact]
    public void Validate_UnderAllocated_ReturnsCritical()
    {
        var low = new ResourceRequirement { TotalCpu = 4 };
        var high = new ResourceRequirement { TotalCpu = 16 };
        var results = _presenter.Validate(high, low);
        var cpuResult = results.First(r => r.ResourceName == "CPU");
        Assert.Equal("CRITICAL", cpuResult.Severity);
    }

    [Fact]
    public void ExportExcel_ProducesWorkbookBytes()
    {
        var bytes = _presenter.ExportExcel(_req, _config);
        Assert.True(bytes.Length > 0);
        Assert.Equal((byte)'P', bytes[0]);
    }

    [Fact]
    public void ExportPdf_ProducesPdfBytes()
    {
        var bytes = _presenter.ExportPdf(_req, _config);
        Assert.True(bytes.Length > 0);
        Assert.Equal((byte)'%', bytes[0]);
        Assert.Equal((byte)'P', bytes[1]);
        Assert.Equal((byte)'D', bytes[2]);
        Assert.Equal((byte)'F', bytes[3]);
    }

    [Fact]
    public void ValidateProject_MissingInfrastructure_ReturnsCritical()
    {
        var calculated = new ResourceRequirement();
        calculated.Infrastructure.Add(new InfrastructureNode { Name = "SQL Server", Cpu = 4, RamGb = 16, NodeCount = 1 });
        var actual = new List<InfrastructureNode>();

        var results = _presenter.ValidateProject(_config, calculated, actual);
        Assert.Contains(results, r => r.Severity == "CRITICAL" && r.ResourceName.Contains("exists"));
    }

    [Fact]
    public void ValidateProject_AllPresent_ReturnsNodeValidations()
    {
        var calculated = new ResourceRequirement();
        calculated.Infrastructure.Add(new InfrastructureNode { Name = "SQL Server", Cpu = 4, RamGb = 16, NodeCount = 1 });
        var actual = new List<InfrastructureNode>
        {
            new() { Name = "SQL Server", Cpu = 4, RamGb = 16, NodeCount = 1 }
        };

        var results = _presenter.ValidateProject(_config, calculated, actual);
        Assert.Contains(results, r => r.ResourceName.Contains("CPU"));
        Assert.Contains(results, r => r.ResourceName.Contains("RAM"));
    }
}
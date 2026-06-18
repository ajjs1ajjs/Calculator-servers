using AIResourceCalculator.Models;
using AIResourceCalculator.Services;

namespace AIResourceCalculator.Tests;

public class ValidationEngineTests
{
    private readonly ValidationEngine _validator;

    public ValidationEngineTests()
    {
        _validator = new ValidationEngine();
    }

    [Fact]
    public void Validate_AllResourcesMatch_ReturnsOk()
    {
        var required = new ResourceRequirement
        {
            TotalCpu = 16, TotalRamGb = 64, TotalStorageGb = 500,
            TotalIops = 1000, WorkerNodeCount = 3, MasterNodeCount = 1
        };
        var allocated = new ResourceRequirement
        {
            TotalCpu = 16, TotalRamGb = 64, TotalStorageGb = 500,
            TotalIops = 1000, WorkerNodeCount = 3, MasterNodeCount = 1
        };

        var results = _validator.Validate(required, allocated);

        Assert.All(results, r => Assert.Equal("OK", r.Severity));
        Assert.All(results, r => Assert.True(r.IsCompliant));
    }

    [Fact]
    public void Validate_InsufficientCpu_ReturnsCritical()
    {
        var required = new ResourceRequirement { TotalCpu = 16 };
        var allocated = new ResourceRequirement { TotalCpu = 8 };

        var results = _validator.Validate(required, allocated);
        var cpuResult = results.First(r => r.ResourceName == "vCPU");

        Assert.Equal("CRITICAL", cpuResult.Severity);
        Assert.False(cpuResult.IsCompliant);
        Assert.Equal(-8, cpuResult.Delta);
        Assert.Equal(-50, cpuResult.DeltaPercent);
    }

    [Fact]
    public void Validate_SlightlyUnderAllocated_ReturnsWarning()
    {
        var required = new ResourceRequirement { TotalRamGb = 64 };
        var allocated = new ResourceRequirement { TotalRamGb = 55 };

        var results = _validator.Validate(required, allocated);
        var ramResult = results.First(r => r.ResourceName == "RAM");

        Assert.Equal("WARNING", ramResult.Severity);
        Assert.False(ramResult.IsCompliant);
    }

    [Fact]
    public void Validate_Overprovisioned_ReturnsOverprovisioned()
    {
        var required = new ResourceRequirement { TotalStorageGb = 200 };
        var allocated = new ResourceRequirement { TotalStorageGb = 500 };

        var results = _validator.Validate(required, allocated);
        var storageResult = results.First(r => r.ResourceName is "Storage" or "Сховище");

        Assert.Equal("OVERPROVISIONED", storageResult.Severity);
        Assert.True(storageResult.IsCompliant);
    }

    [Fact]
    public void ValidateProject_MissingInfrastructure_ReturnsCritical()
    {
        var calculated = new ResourceRequirement();
        calculated.Infrastructure.Add(new InfrastructureNode
        {
            Name = "SQL Server", Cpu = 4, RamGb = 16, NodeCount = 1
        });

        var actualResources = new List<InfrastructureNode>();

        var results = _validator.ValidateProject(new ProjectConfig(), calculated, actualResources);
        var missingResult = results.FirstOrDefault(r => r.ResourceName.Contains("exists"));

        Assert.NotNull(missingResult);
        Assert.Equal("CRITICAL", missingResult!.Severity);
        Assert.False(missingResult.IsCompliant);
    }

    [Fact]
    public void ValidateProject_MatchingInfrastructure_ReturnsValidResults()
    {
        var calculated = new ResourceRequirement();
        calculated.Infrastructure.Add(new InfrastructureNode
        {
            Name = "SQL Server", Cpu = 4, RamGb = 16, NodeCount = 1
        });

        var actualResources = new List<InfrastructureNode>
        {
            new() { Name = "SQL Server", Cpu = 4, RamGb = 16, NodeCount = 1 }
        };

        var results = _validator.ValidateProject(new ProjectConfig(), calculated, actualResources);

        Assert.Contains(results, r => r.ResourceName.Contains("vCPU"));
        Assert.Contains(results, r => r.ResourceName.Contains("RAM"));
        Assert.Contains(results, r => r.ResourceName.Contains("count"));
    }

    [Fact]
    public void Validate_ZeroRequired_HandlesGracefully()
    {
        var required = new ResourceRequirement();
        var allocated = new ResourceRequirement();

        var results = _validator.Validate(required, allocated);

        Assert.All(results, r => Assert.True(r.IsCompliant));
    }

    [Fact]
    public void GetSeverity_EdgeCases_ReturnsCorrectValues()
    {
        var required = new ResourceRequirement { TotalCpu = 10, TotalRamGb = 100 };
        var allocated = new ResourceRequirement { TotalCpu = 7, TotalRamGb = 151 };

        var results = _validator.Validate(required, allocated);

        Assert.Equal("CRITICAL", results[0].Severity);
        Assert.Equal("OVERPROVISIONED", results[1].Severity);
    }
}

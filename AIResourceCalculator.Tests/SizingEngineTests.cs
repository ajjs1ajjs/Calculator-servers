using AIResourceCalculator.Data;
using AIResourceCalculator.Models;
using AIResourceCalculator.Services;

namespace AIResourceCalculator.Tests;

public class SizingEngineTests
{
    private readonly SizingMatrix _matrix;
    private readonly SizingEngine _engine;

    public SizingEngineTests()
    {
        _matrix = new SizingMatrix();
        _engine = new SizingEngine(_matrix);
    }

    [Fact]
    public void Calculate_K8sBasic_100Users_ReturnsPositiveResources()
    {
        var config = new ProjectConfig
        {
            ProjectName = "Test",
            UserCount = 100,
            DeploymentType = DeploymentType.Kubernetes,
            LoadProfile = LoadProfile.Basic
        };

        var result = _engine.Calculate(config);

        Assert.True(result.TotalCpu > 0);
        Assert.True(result.TotalRamGb > 0);
        Assert.True(result.TotalStorageGb > 0);
        Assert.True(result.TotalIops > 0);
        Assert.True(result.WorkerNodeCount >= 1);
        Assert.True(result.MasterNodeCount >= 1);
        Assert.NotEmpty(result.Components);
        Assert.NotEmpty(result.Infrastructure);
    }

    [Fact]
    public void Calculate_K8sPerformance_100Users_ReturnsHigherThanBasic()
    {
        var basicConfig = new ProjectConfig
        {
            UserCount = 100,
            DeploymentType = DeploymentType.Kubernetes,
            LoadProfile = LoadProfile.Basic
        };
        var perfConfig = new ProjectConfig
        {
            UserCount = 100,
            DeploymentType = DeploymentType.Kubernetes,
            LoadProfile = LoadProfile.Performance
        };

        var basic = _engine.Calculate(basicConfig);
        var perf = _engine.Calculate(perfConfig);

        Assert.True(perf.TotalCpu >= basic.TotalCpu);
        Assert.True(perf.TotalRamGb >= basic.TotalRamGb);
    }

    [Fact]
    public void Calculate_Windows_50Users_ReturnsWindowsInfrastructure()
    {
        var config = new ProjectConfig
        {
            UserCount = 50,
            DeploymentType = DeploymentType.Windows,
            LoadProfile = LoadProfile.Basic
        };

        var result = _engine.Calculate(config);

        Assert.Contains(result.Infrastructure, n => n.Name.Contains("App") || n.Name.Contains("додатків"));
        Assert.Contains(result.Infrastructure, n => n.Name.Contains("Web") || n.Name.Contains("Веб"));
        Assert.Contains(result.Infrastructure, n => n.Name.Contains("SQL"));
        Assert.True(result.TotalCpu > 0);
    }

    [Fact]
    public void Calculate_Hybrid_200Users_ReturnsCombinedResources()
    {
        var config = new ProjectConfig
        {
            UserCount = 200,
            DeploymentType = DeploymentType.Hybrid,
            LoadProfile = LoadProfile.Basic
        };

        _engine.SetModules(_engine.Modules.Where(m => m.Name != "Windows Infrastructure").ToList());
        var result = _engine.Calculate(config);

        Assert.True(result.TotalCpu > 0);
        Assert.True(result.TotalRamGb > 0);
        Assert.True(result.WorkerNodeCount >= 2);
        Assert.NotEmpty(result.Infrastructure);
    }

    [Fact]
    public void FindMsSqlRange_25Users_ReturnsCorrectRange()
    {
        var range = _matrix.MsSqlRanges.FirstOrDefault(r => 25 >= r.MinUsers && 25 <= r.MaxUsers);
        Assert.NotNull(range);
        Assert.Equal(11, range!.MinUsers);
        Assert.Equal(25, range.MaxUsers);
        Assert.Equal(4, range.Cpu);
    }

    [Fact]
    public void Calculate_0Users_HandlesGracefully()
    {
        var config = new ProjectConfig
        {
            UserCount = 0,
            DeploymentType = DeploymentType.Kubernetes,
            LoadProfile = LoadProfile.Basic
        };

        var result = _engine.Calculate(config);
        Assert.True(result.TotalCpu >= 0);
        Assert.True(result.WorkerNodeCount >= 1);
    }

    [Fact]
    public void Calculate_500Users_ScalesProperly()
    {
        var config = new ProjectConfig
        {
            UserCount = 500,
            DeploymentType = DeploymentType.Kubernetes,
            LoadProfile = LoadProfile.Basic
        };

        var result = _engine.Calculate(config);

        Assert.True(result.TotalCpu > 50);
        Assert.True(result.WorkerNodeCount > 3);
        Assert.True(result.TotalRamGb > 100);
    }

    [Fact]
    public void Calculate_ModulesDisabled_ReturnsLessResources()
    {
        var allEnabled = new ProjectConfig
        {
            UserCount = 100,
            DeploymentType = DeploymentType.Kubernetes,
            LoadProfile = LoadProfile.Basic
        };

        var fullResult = _engine.Calculate(allEnabled);

        foreach (var m in _engine.Modules)
            m.IsEnabled = false;

        var emptyResult = _engine.Calculate(allEnabled);

        Assert.True(fullResult.TotalCpu > emptyResult.TotalCpu);
        Assert.True(fullResult.TotalRamGb > emptyResult.TotalRamGb);
    }

    [Fact]
    public void Calculate_Windows_ExcludesK8sSpecificModules()
    {
        var config = new ProjectConfig
        {
            UserCount = 100,
            DeploymentType = DeploymentType.Windows,
            LoadProfile = LoadProfile.Basic
        };

        var result = _engine.Calculate(config);

        Assert.DoesNotContain(result.Infrastructure, n => n.Name.Contains("Worker"));
        Assert.DoesNotContain(result.Infrastructure, n => n.Name.Contains("Master"));
    }
}

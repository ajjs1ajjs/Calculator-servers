using AIResourceCalculator.Models;
using AIResourceCalculator.Services;

namespace AIResourceCalculator.Tests;

public class CostEstimatorTests
{
    private readonly CostEstimator _estimator = new();
    private readonly ResourceRequirement _req;
    private readonly ProjectConfig _config;

    public CostEstimatorTests()
    {
        _req = new ResourceRequirement
        {
            TotalCpu = 24, TotalRamGb = 96, TotalStorageGb = 1500,
            WorkerNodeCount = 3, MasterNodeCount = 1
        };
        _req.Infrastructure.Add(new InfrastructureNode { Name = "SQL Server", Cpu = 4, RamGb = 16, NodeCount = 1, StorageGb = 300 });
        _req.Infrastructure.Add(new InfrastructureNode { Name = "Worker Node", Cpu = 8, RamGb = 32, NodeCount = 3, StorageGb = 200 });

        _config = new ProjectConfig { ProjectName = "Test", UserCount = 100, DeploymentType = DeploymentType.Kubernetes };
    }

    [Fact]
    public void EstimateAzure_ReturnsPositiveCosts()
    {
        var cost = _estimator.EstimateAzure(_req, _config);
        Assert.True(cost.MonthlyCompute > 0);
        Assert.True(cost.MonthlyStorage > 0);
        Assert.Equal("Azure", cost.Provider);
    }

    [Fact]
    public void EstimateAws_ReturnsPositiveCosts()
    {
        var cost = _estimator.EstimateAws(_req, _config);
        Assert.True(cost.MonthlyCompute > 0);
        Assert.True(cost.MonthlyTotal > cost.MonthlyStorage);
    }

    [Fact]
    public void EstimateGcp_ReturnsPositiveCosts()
    {
        var cost = _estimator.EstimateGcp(_req, _config);
        Assert.True(cost.MonthlyCompute > 0);
        Assert.True(cost.YearlyTotal > cost.MonthlyTotal);
    }

    [Fact]
    public void EstimateAzure_HasCorrectNodeCount()
    {
        var cost = _estimator.EstimateAzure(_req, _config);
        Assert.Equal(4, cost.NodeCount);
    }

    [Fact]
    public void Estimates_DifferByProvider()
    {
        var azure = _estimator.EstimateAzure(_req, _config);
        var aws = _estimator.EstimateAws(_req, _config);
        Assert.NotEqual(azure.MonthlyCompute, aws.MonthlyCompute);
    }
}

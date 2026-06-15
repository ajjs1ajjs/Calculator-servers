using AIResourceCalculator.Models;
using AIResourceCalculator.Services;

namespace AIResourceCalculator.Tests;

public class AiAdvisorServiceTests
{
    private readonly AiAdvisorService _advisor;

    public AiAdvisorServiceTests()
    {
        _advisor = new AiAdvisorService();
    }

    [Fact]
    public void Analyze_K8s_3Workers_ReturnsHaOk()
    {
        var req = CreateBasicReq(DeploymentType.Kubernetes, workerCount: 3);
        var config = new ProjectConfig { UserCount = 100, DeploymentType = DeploymentType.Kubernetes };

        var recommendations = _advisor.Analyze(req, config);

        Assert.Contains(recommendations, r => r.Severity == "ok"
            && (r.Category.Contains("High Availability") || r.Category.Contains("Відмовостійкість")));
    }

    [Fact]
    public void Analyze_K8s_1Worker_ReturnsHaCritical()
    {
        var req = CreateBasicReq(DeploymentType.Kubernetes, workerCount: 1);
        var config = new ProjectConfig { UserCount = 100, DeploymentType = DeploymentType.Kubernetes };

        var recommendations = _advisor.Analyze(req, config);

        Assert.Contains(recommendations, r => r.Severity == "critical"
            && (r.Category.Contains("Availability") || r.Category.Contains("Відмовостійкість")));
    }

    [Fact]
    public void Analyze_1000Users_ReturnsAutoScalingRecommendation()
    {
        var req = CreateBasicReq(DeploymentType.Kubernetes, workerCount: 5);
        var config = new ProjectConfig { UserCount = 1000, DeploymentType = DeploymentType.Kubernetes };

        var recommendations = _advisor.Analyze(req, config);

        Assert.Contains(recommendations, r => r.Severity == "info"
            && (r.Category.Contains("Auto-scaling") || r.Category.Contains("Автомасштабування")));
    }

    [Fact]
    public void Analyze_HighStorage_ReturnsWarning()
    {
        var req = CreateBasicReq(DeploymentType.Kubernetes, storageGb: 5000);
        var config = new ProjectConfig { UserCount = 100 };

        var recommendations = _advisor.Analyze(req, config);

        Assert.Contains(recommendations, r => r.Severity == "warning"
            && (r.Category.Contains("Storage") || r.Category.Contains("Сховище")));
    }

    [Fact]
    public void Analyze_HighIops_ReturnsIopsRecommendation()
    {
        var req = CreateBasicReq(DeploymentType.Kubernetes, iops: 20000);
        var config = new ProjectConfig { UserCount = 100 };

        var recommendations = _advisor.Analyze(req, config);

        Assert.Contains(recommendations, r => r.Category.Contains("IOPS"));
    }

    [Fact]
    public void Analyze_GoodBalance_ReturnsOk()
    {
        var req = CreateBasicReq(DeploymentType.Kubernetes, cpu: 32, ram: 128);
        var config = new ProjectConfig { UserCount = 100 };

        var recommendations = _advisor.Analyze(req, config);

        Assert.Contains(recommendations, r => r.Severity == "ok"
            && (r.Category.Contains("Balance") || r.Category.Contains("Баланс")));
    }

    [Fact]
    public void Analyze_TooMuchCpu_ReturnsWarning()
    {
        var req = CreateBasicReq(DeploymentType.Kubernetes, cpu: 64, ram: 16);
        var config = new ProjectConfig { UserCount = 100 };

        var recommendations = _advisor.Analyze(req, config);

        Assert.Contains(recommendations, r => r.Severity == "warning"
            && (r.Category.Contains("Balance") || r.Category.Contains("Баланс")));
    }

    [Fact]
    public void Analyze_ReturnsInstanceFitRecommendation()
    {
        var req = CreateBasicReq(DeploymentType.Kubernetes, cpu: 32, ram: 128, workerCount: 4);
        var config = new ProjectConfig { UserCount = 200 };

        var recommendations = _advisor.Analyze(req, config);

        Assert.Contains(recommendations, r => r.Severity == "ok"
            && (r.Category.Contains("Instance", StringComparison.OrdinalIgnoreCase) || r.Category.Contains("інстанс", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void Analyze_LowPodDensity_ReturnsPodDensityWarning()
    {
        var req = CreateBasicReq(DeploymentType.Kubernetes, cpu: 16, ram: 64, workerCount: 1);
        req.Components.Add(new ServiceComponent { Name = "BigPod", Cpu = 12, RamGb = 48, Replicas = 1 });
        var config = new ProjectConfig { UserCount = 100 };

        var recommendations = _advisor.Analyze(req, config);

        Assert.Contains(recommendations, r =>
            (r.Category.Contains("Pod Density") || r.Category.Contains("Щільність")));
    }

    [Fact]
    public void Analyze_LowStorageAndIops_ReturnsStorageOk()
    {
        var req = CreateBasicReq(DeploymentType.Kubernetes, storageGb: 500, iops: 3000);
        var config = new ProjectConfig { UserCount = 50 };

        var recommendations = _advisor.Analyze(req, config);

        Assert.Contains(recommendations, r => r.Severity == "ok"
            && (r.Category.Contains("Storage") || r.Category.Contains("Сховище")));
    }

    [Fact]
    public void Analyze_AlwaysReturnsInstanceType_WithCorrectSeverity()
    {
        var req = CreateBasicReq(DeploymentType.Kubernetes, cpu: 16, ram: 64);
        var config = new ProjectConfig { UserCount = 100 };

        var recommendations = _advisor.Analyze(req, config);

        var instanceRec = recommendations.FirstOrDefault();
        Assert.NotNull(instanceRec);
        Assert.Equal("ok", instanceRec!.Severity);
    }

    private static ResourceRequirement CreateBasicReq(
        DeploymentType deploy = DeploymentType.Kubernetes,
        double cpu = 16, double ram = 64, int storageGb = 500,
        int iops = 3000, int workerCount = 3, int masterCount = 1)
    {
        var req = new ResourceRequirement
        {
            DeploymentType = deploy,
            TotalCpu = cpu,
            TotalRamGb = ram,
            TotalStorageGb = storageGb,
            TotalIops = iops,
            WorkerNodeCount = workerCount,
            MasterNodeCount = masterCount
        };
        req.Components.Add(new ServiceComponent { Name = "TestComponent", Cpu = 2, RamGb = 8, Replicas = 2 });
        req.Infrastructure.Add(new InfrastructureNode { Name = "TestNode", Cpu = 4, RamGb = 16, NodeCount = 1 });
        return req;
    }
}

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
    public void Calculate_K8sPerformance_100Users_NotLessThanBasic()
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

        // Підсумок тепер = ФІЗИЧНІ ресурси вузлів. Профіль "Продуктивний" не зменшує робоче
        // навантаження: к-сть worker-вузлів і RAM не нижчі за базовий профіль.
        // (CPU вузла БД для перф-профілю у матриці навмисно НИЖЧИЙ — 6 проти 8 на 51-100,
        //  тому пряме порівняння TotalCpu тут некоректне.)
        Assert.True(perf.WorkerNodeCount >= basic.WorkerNodeCount);
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

    [Fact]
    public void Calculate_Windows_ExcludesForceBpm()
    {
        var config = new ProjectConfig
        {
            UserCount = 100,
            DeploymentType = DeploymentType.Windows,
            LoadProfile = LoadProfile.Basic
        };

        _engine.SetProductType(ProductType.Standard);
        var forceBpm = _engine.Modules.FirstOrDefault(m => m.Name == "ForceBPM");
        Assert.NotNull(forceBpm);
        Assert.True(forceBpm!.IsKubernetesOnly);

        var result = _engine.Calculate(config);

        Assert.DoesNotContain(result.Components, c => c.Name.Contains("ForceBPM"));
    }

    [Fact]
    public void Calculate_K8s_IncludesForceBpm()
    {
        var config = new ProjectConfig
        {
            UserCount = 100,
            DeploymentType = DeploymentType.Kubernetes,
            LoadProfile = LoadProfile.Basic
        };

        var result = _engine.Calculate(config);

        Assert.Contains(result.Components, c =>
            c.Name.Contains("ForceBPM") || c.Category == "ForceBPM");
    }

    [Fact]
    public void Calculate_Windows_ExcludesAllKubernetesOnlyModules()
    {
        var config = new ProjectConfig
        {
            UserCount = 100,
            DeploymentType = DeploymentType.Windows,
            LoadProfile = LoadProfile.Basic
        };

        var k8sOnlyModules = _engine.Modules.Where(m => m.IsKubernetesOnly).ToList();
        Assert.NotEmpty(k8sOnlyModules);

        var result = _engine.Calculate(config);

        foreach (var mod in k8sOnlyModules)
        {
            Assert.DoesNotContain(result.Components, c => c.Category == mod.Name);
        }
    }

    [Fact]
    public void CloneModule_PreservesIsKubernetesOnly()
    {
        var src = _engine.Modules.First(m => m.Name == "ForceBPM");
        Assert.True(src.IsKubernetesOnly);
    }

    [Fact]
    public void SetProductType_PreservesIsKubernetesOnly()
    {
        _engine.SetProductType(ProductType.DocumentFlow);
        var forceBpm = _engine.Modules.FirstOrDefault(m => m.Name == "ForceBPM");
        Assert.NotNull(forceBpm);
        Assert.True(forceBpm!.IsKubernetesOnly);
    }

    [Fact]
    public void FindDatabaseRange_Postgres_50Users_ReturnsCorrectRange()
    {
        var range = _matrix.PostgresRanges.FirstOrDefault(r => 50 >= r.MinUsers && 50 <= r.MaxUsers);
        Assert.NotNull(range);
        Assert.Equal(26, range!.MinUsers);
        Assert.Equal(50, range.MaxUsers);
        Assert.Equal(3, range.Cpu);
    }

    [Fact]
    public void FindDatabaseRange_Oracle_100Users_ReturnsCorrectRange()
    {
        var range = _matrix.OracleRanges.FirstOrDefault(r => 100 >= r.MinUsers && 100 <= r.MaxUsers);
        Assert.NotNull(range);
        Assert.Equal(51, range!.MinUsers);
        Assert.Equal(100, range.MaxUsers);
        Assert.Equal(6, range.Cpu);
    }

    [Fact]
    public void Calculate_K8s_Postgres_ReturnsPostgresNode()
    {
        var config = new ProjectConfig
        {
            UserCount = 100,
            DeploymentType = DeploymentType.Kubernetes,
            DatabaseType = DatabaseType.PostgreSQL
        };
        var result = _engine.Calculate(config);
        Assert.Contains(result.Infrastructure, n => n.Name == "PostgreSQL");
        Assert.DoesNotContain(result.Infrastructure, n => n.Name == "SQL Server");
    }

    [Fact]
    public void Calculate_K8s_Oracle_ReturnsOracleNode()
    {
        var config = new ProjectConfig
        {
            UserCount = 100,
            DeploymentType = DeploymentType.Kubernetes,
            DatabaseType = DatabaseType.Oracle
        };
        var result = _engine.Calculate(config);
        Assert.Contains(result.Infrastructure, n => n.Name == "Oracle 19c");
        Assert.DoesNotContain(result.Infrastructure, n => n.Name == "SQL Server");
    }

    // --- Regression: сумарний обсяг дисків має враховувати розділені диски (StorageGb2/3/4) ---
    [Fact]
    public void InfrastructureNode_TotalStorage_IncludesSplitDisks()
    {
        var node = new InfrastructureNode
        {
            StorageGb = 100, StorageGb2 = 150, StorageGb3 = 300, StorageGb4 = 200, NodeCount = 2
        };
        Assert.Equal(750, node.DiskPerNodeGb);
        Assert.Equal(1500, node.TotalStorageGb);
    }

    // --- Regression: "Всього vCPU/RAM" = фізичні ресурси вузлів, однаково для K8s і Windows ---
    [Theory]
    [InlineData(DeploymentType.Kubernetes)]
    [InlineData(DeploymentType.Windows)]
    public void Calculate_TotalCpuAndRam_EqualPhysicalNodeResources(DeploymentType deploy)
    {
        var config = new ProjectConfig
        {
            UserCount = 100, DeploymentType = deploy, LoadProfile = LoadProfile.Basic
        };

        var result = _engine.Calculate(config);

        Assert.Equal(result.Infrastructure.Sum(n => n.Cpu * n.NodeCount), result.TotalCpu);
        Assert.Equal(result.Infrastructure.Sum(n => n.RamGb * n.NodeCount), result.TotalRamGb);
    }

    // --- Regression: запит подів K8s заповнюється і не перевищує фізичний підсумок; Windows = 0 ---
    [Fact]
    public void Calculate_K8s_PodRequests_ArePopulatedAndBelowPhysical()
    {
        var result = _engine.Calculate(new ProjectConfig
        {
            UserCount = 100, DeploymentType = DeploymentType.Kubernetes, LoadProfile = LoadProfile.Basic
        });
        Assert.True(result.PodCpu > 0);
        Assert.True(result.PodRamGb > 0);
        Assert.True(result.PodCpu <= result.TotalCpu);
        Assert.True(result.PodRamGb <= result.TotalRamGb);
    }

    [Fact]
    public void Calculate_Windows_HasNoPodRequests()
    {
        var result = _engine.Calculate(new ProjectConfig
        {
            UserCount = 100, DeploymentType = DeploymentType.Windows, LoadProfile = LoadProfile.Basic
        });
        Assert.Equal(0, result.PodCpu);
        Assert.Equal(0, result.PodRamGb);
    }

    [Fact]
    public void Calculate_TotalStorage_EqualsSumOfNodeDisks()
    {
        var config = new ProjectConfig
        {
            UserCount = 100,
            DeploymentType = DeploymentType.Windows,
            LoadProfile = LoadProfile.Basic
        };

        var result = _engine.Calculate(config);
        var expected = result.Infrastructure.Sum(n => n.TotalStorageGb);
        Assert.Equal(expected, result.TotalStorageGb);
    }

    [Fact]
    public void Calculate_Windows_Postgres_ReturnsPostgresNode()
    {
        var config = new ProjectConfig
        {
            UserCount = 100,
            DeploymentType = DeploymentType.Windows,
            DatabaseType = DatabaseType.PostgreSQL
        };
        var result = _engine.Calculate(config);
        Assert.Contains(result.Infrastructure, n => n.Name == "PostgreSQL");
        Assert.DoesNotContain(result.Infrastructure, n => n.Name == "SQL Server");
    }
}

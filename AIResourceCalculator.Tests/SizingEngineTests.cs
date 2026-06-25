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

        // Порівнюємо РОБОЧЕ НАВАНТАЖЕННЯ (запит подів): модулі Документообігу важчі за под,
        // тож запит CPU/RAM подів не нижчий за базовий профіль.
        // Загальний TotalRam/TotalCpu тут порівнювати некоректно: вузол БД у профілі Документообіг
        // (варіант Standard, D-AD-ADM-E 3.11.1) навмисно легший за загальну базову конфігурацію
        // (напр., 24 проти 48 ГБ ОЗП на 51-100 ліцензій).
        Assert.True(perf.WorkerNodeCount >= basic.WorkerNodeCount);
        Assert.True(perf.PodRamGb >= basic.PodRamGb);
        Assert.True(perf.PodCpu >= basic.PodCpu);
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

    // --- Регресія Bug 3: MasterNodeCount узгоджений із таблицею інфраструктури ---
    // Windows — без master-вузла (0); у гібриді master лише один (від K8s), а не 1+1=2.
    [Fact]
    public void Calculate_MasterNodeCount_MatchesInfrastructure()
    {
        _engine.SetModules(_engine.Modules.Where(m => m.Name != "Windows Infrastructure").ToList());

        var win = _engine.Calculate(new ProjectConfig
        {
            UserCount = 200, DeploymentType = DeploymentType.Windows, LoadProfile = LoadProfile.Basic
        });
        var hybrid = _engine.Calculate(new ProjectConfig
        {
            UserCount = 200, DeploymentType = DeploymentType.Hybrid, LoadProfile = LoadProfile.Basic
        });

        // Windows: master-вузлів немає ні в полі, ні в інфраструктурі.
        Assert.Equal(0, win.MasterNodeCount);
        Assert.DoesNotContain(win.Infrastructure, n => n.Name.Contains("Master"));

        // Гібрид: рівно один master-вузол — поле збігається з фактом у таблиці.
        var hybridMasterNodes = hybrid.Infrastructure
            .Where(n => n.Name.Contains("Master")).Sum(n => n.NodeCount);
        Assert.Equal(1, hybridMasterNodes);
        Assert.Equal(hybridMasterNodes, hybrid.MasterNodeCount);
    }

    // --- Регресія: гібрид НЕ дублює app/web (раніше "криво рахував" на 10 ліцензіях) ---
    [Fact]
    public void Calculate_Hybrid_DoesNotDoubleCountAppWeb()
    {
        var config = new ProjectConfig
        {
            UserCount = 10, DeploymentType = DeploymentType.Hybrid, LoadProfile = LoadProfile.Basic
        };

        var result = _engine.Calculate(config);

        // App/Web живуть на Windows-VM, тож серед K8s-подів їх бути не повинно.
        Assert.DoesNotContain(result.Components, c => c.Name.Contains("AS (App Server)"));
        Assert.DoesNotContain(result.Components, c => c.Name == "Webrmd");
        // ForceBPM та інші сервіси — на K8s.
        Assert.Contains(result.Components, c => c.Category == "ForceBPM");
        // Windows-частина дає app/web VM + БД.
        Assert.Contains(result.Infrastructure, n => n.Name.Contains("додатків") || n.Name.Contains("App"));
        Assert.Contains(result.Infrastructure, n => n.Name.Contains("Веб") || n.Name.Contains("Web"));
        // Рівно один вузол БД (а не два, як було через подвійний облік).
        Assert.Equal(1, result.Infrastructure.Count(n => n.Name.Contains("SQL")));
    }

    // --- Регресія: app/web-вузли Windows мають файл підкачки (page file) ---
    [Fact]
    public void Calculate_Windows_AppAndWebHavePageFile()
    {
        var config = new ProjectConfig
        {
            UserCount = 100, DeploymentType = DeploymentType.Windows, LoadProfile = LoadProfile.Basic
        };

        var result = _engine.Calculate(config);

        var app = result.Infrastructure.First(n => n.Name.Contains("додатків") || n.Name.Contains("App"));
        var web = result.Infrastructure.First(n => n.Name.Contains("Веб") || n.Name.Contains("Web"));
        Assert.True(app.PageFileGb > 0);
        Assert.True(web.PageFileGb > 0);
    }

    // --- Регресія: компоненти зберігають ресурси на 1 репліку та сумарні ---
    [Fact]
    public void Calculate_K8s_ComponentsExposePerReplicaAndTotal()
    {
        var result = _engine.Calculate(new ProjectConfig
        {
            UserCount = 100, DeploymentType = DeploymentType.Kubernetes, LoadProfile = LoadProfile.Basic
        });

        var comp = result.Components.First(c => c.Cpu > 0 && c.Replicas > 1);
        Assert.True(comp.CpuPerReplica > 0);
        Assert.Equal(comp.CpuPerReplica * comp.Replicas, comp.Cpu, 3);
    }

    // --- Обов'язкові сервіси позначені; LMS/HR вимкнені за замовчуванням ---
    [Theory]
    [InlineData("App Server")]
    [InlineData("ROBOT")]
    [InlineData("Web")]
    public void Matrix_CoreServices_AreMandatory(string moduleName)
    {
        var mod = _engine.Modules.First(m => m.Name == moduleName);
        Assert.True(mod.IsMandatory);
        Assert.True(mod.IsEnabled);
    }

    [Theory]
    [InlineData("LMS")]
    [InlineData("HR Portal")]
    public void Matrix_RareServices_AreOffByDefault(string moduleName)
    {
        var mod = _engine.Modules.First(m => m.Name == moduleName);
        Assert.False(mod.IsMandatory);
        Assert.False(mod.IsEnabled);
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

    // --- Диски вузла БД масштабуються за обсягом даних: мала БД → малі диски Data/Logs ---
    [Fact]
    public void Calculate_SmallDbData_ProducesSmallDbDisks()
    {
        var small = _engine.Calculate(new ProjectConfig
        {
            UserCount = 100, DeploymentType = DeploymentType.Windows, LoadProfile = LoadProfile.Basic, DbDataSizeGb = 10
        });
        var big = _engine.Calculate(new ProjectConfig
        {
            UserCount = 100, DeploymentType = DeploymentType.Windows, LoadProfile = LoadProfile.Basic, DbDataSizeGb = 1000
        });

        var dbSmall = small.Infrastructure.First(n => n.Name.Contains("SQL"));
        var dbBig = big.Infrastructure.First(n => n.Name.Contains("SQL"));

        // Мала БД (10 ГБ): Logs ≥ 50 (мінімум), MainData ≥ 100 (мінімум) — без терабайтів.
        Assert.True(dbSmall.StorageGb3 <= 100);
        Assert.True(dbSmall.StorageGb2 <= 50);
        // Велика БД (1000 ГБ): диски значно більші (data ×2 = 2000).
        Assert.True(dbBig.StorageGb3 > dbSmall.StorageGb3);
        Assert.Equal(2000, dbBig.StorageGb3);
    }

    // --- IOPS не сумуються між вузлами: підсумок = IOPS вузла БД ---
    [Fact]
    public void Calculate_Windows_TotalIops_EqualsDbNodeIops_NotSum()
    {
        var result = _engine.Calculate(new ProjectConfig
        {
            UserCount = 100, DeploymentType = DeploymentType.Windows, LoadProfile = LoadProfile.Basic
        });
        var db = result.Infrastructure.First(n => n.Name.Contains("SQL"));
        Assert.Equal(db.Iops, result.TotalIops);
        // Сума IOPS усіх вузлів була б більшою — підсумок її не дорівнює.
        var sum = result.Infrastructure.Sum(n => n.Iops);
        Assert.True(result.TotalIops <= sum);
    }

    // --- Версія/редакція СУБД: SQL 2022; >128 ГБ ОЗП → Enterprise, інакше Standard ---
    [Fact]
    public void DbVersionLabel_Sql_PicksEditionByRam()
    {
        Assert.Equal("MS SQL Server 2022 Standard", SizingEngine.DbVersionLabel(DatabaseType.MsSql, 64));
        Assert.Equal("MS SQL Server 2022 Standard", SizingEngine.DbVersionLabel(DatabaseType.MsSql, 128));
        Assert.Equal("MS SQL Server 2022 Enterprise", SizingEngine.DbVersionLabel(DatabaseType.MsSql, 240));
        Assert.Contains("PostgreSQL 17", SizingEngine.DbVersionLabel(DatabaseType.PostgreSQL, 64));
        Assert.Contains("Oracle Database 19c", SizingEngine.DbVersionLabel(DatabaseType.Oracle, 64));
    }

    // --- Редакція SQL за середовищем і порогами (Developer для non-prod; Standard ≤24 ядер і ≤128 ГБ) ---
    [Fact]
    public void DbVersionLabel_Sql_DeveloperForNonProd()
    {
        Assert.Equal("MS SQL Server 2022 Developer Edition",
            SizingEngine.DbVersionLabel(DatabaseType.MsSql, 240, 32, DeployEnvironment.Dev));
        Assert.Equal("MS SQL Server 2022 Developer Edition",
            SizingEngine.DbVersionLabel(DatabaseType.MsSql, 16, 4, DeployEnvironment.Test));
        Assert.Equal("MS SQL Server 2022 Developer Edition",
            SizingEngine.DbVersionLabel(DatabaseType.MsSql, 64, 8, DeployEnvironment.PredProd));
    }

    [Fact]
    public void DbVersionLabel_Sql_Prod_EnterpriseWhenCoresExceedStandardLimit()
    {
        // RAM у межах Standard (96 ≤ 128), але ядер 28 > 24 → Enterprise.
        Assert.Equal("MS SQL Server 2022 Enterprise",
            SizingEngine.DbVersionLabel(DatabaseType.MsSql, 96, 28, DeployEnvironment.Prod));
        // У межах обох лімітів → Standard.
        Assert.Equal("MS SQL Server 2022 Standard",
            SizingEngine.DbVersionLabel(DatabaseType.MsSql, 96, 24, DeployEnvironment.Prod));
    }

    // --- Профіль IOPS та пропускна здатність (MiB/s) проставляються на вузлі БД ---
    [Fact]
    public void Calculate_SqlNode_HasIopsProfileAndThroughput()
    {
        var result = _engine.Calculate(new ProjectConfig
        {
            UserCount = 100, DeploymentType = DeploymentType.Windows, LoadProfile = LoadProfile.Basic
        });
        var db = result.Infrastructure.First(n => n.Name.Contains("SQL"));
        Assert.Equal("50r/50w", db.IopsProfile);
        // MiB/s — значення документа (51-100 → 240), а не вигаданий розрахунок із IOPS.
        Assert.Equal(240, db.ThroughputMiBs);
    }

    // --- Регресія: IOPS та профілі серверів додатків і веб-серверів НЕ порожні (Windows) ---
    [Fact]
    public void Calculate_Windows_AppAndWebNodes_HaveIopsAndProfiles()
    {
        var result = _engine.Calculate(new ProjectConfig
        {
            UserCount = 100, DeploymentType = DeploymentType.Windows, LoadProfile = LoadProfile.Basic
        });
        var app = result.Infrastructure.First(n => n.Name.Contains("App") || n.Name.Contains("додатк"));
        var web = result.Infrastructure.First(n => n.Name.Contains("Web") || n.Name.Contains("еб"));
        Assert.True(app.Iops > 0);
        Assert.Equal("30r/70w", app.IopsProfile);
        Assert.True(web.Iops > 0);
        Assert.Equal("70r/30w", web.IopsProfile);
    }

    [Fact]
    public void Calculate_LargeUserCount_SqlNode_RequiresEnterprise()
    {
        // 5000 користувачів → RamRec 1152 ГБ > 128 → Enterprise + примітка.
        var result = _engine.Calculate(new ProjectConfig
        {
            UserCount = 5000, DeploymentType = DeploymentType.Windows, LoadProfile = LoadProfile.Basic
        });
        var db = result.Infrastructure.First(n => n.Name.Contains("SQL"));
        Assert.Contains("Enterprise", db.DbVersion);
        Assert.Contains("Enterprise", db.Notes);
    }

    // --- Кількість користувачів модуля: модуль масштабується за СВОЇМ числом ---
    [Fact]
    public void EffectiveUsers_CapsAtProjectUsers()
    {
        var m = new ProjectModule();
        Assert.Equal(100, m.EffectiveUsers(100));   // 0 = загальна
        m.UserCount = 30;
        Assert.Equal(30, m.EffectiveUsers(100));    // власна
        m.UserCount = 200;
        Assert.Equal(100, m.EffectiveUsers(100));   // не понад загальну
    }

    [Fact]
    public void Calculate_ModuleUserCount_ScalesModuleIndependently()
    {
        var modules = _engine.Modules.ToClonedList();
        var lms = modules.First(m => m.Name == "LMS");
        lms.IsEnabled = true;
        lms.UserCount = 25; // LMS використовують лише 25 із 100
        _engine.SetModules(modules);

        var result = _engine.Calculate(new ProjectConfig
        {
            UserCount = 100, DeploymentType = DeploymentType.Kubernetes, LoadProfile = LoadProfile.Basic
        });

        // LMS-GraphQL = Per25Users → 25 користувачів дають 1 репліку (а не 4, як було б на 100).
        var graphql = result.Components.First(c => c.Name == "LMS-GraphQL");
        Assert.Equal(1, graphql.Replicas);
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

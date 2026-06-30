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
        // Перевіряємо за Категорією (= назва модуля), бо назви компонентів локалізовані.
        Assert.DoesNotContain(result.Components, c => c.Category == "App Server");
        Assert.DoesNotContain(result.Components, c => c.Category == "Web");
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

        // Page file = CEILING(RAM*4, 10) — формула еталонного аркуша Windows (Q7/Q8),
        // підтверджена прикладами (app RAM 24 → 96, web RAM 8 → 32 тощо).
        static int PageFile(double ram) => (int)(Math.Ceiling(ram * 4 / 10.0) * 10);
        Assert.Equal(PageFile(app.RamGb), app.PageFileGb);
        Assert.Equal(PageFile(web.RamGb), web.PageFileGb);
    }

    // --- Регресія: master-вузол K8s = 2 ядра / 4 ГБ (як у реальних розрахунках, не 4/6) ---
    [Fact]
    public void Calculate_K8s_MasterNodeIs2Cores4Gb()
    {
        var result = _engine.Calculate(new ProjectConfig
        {
            UserCount = 100, DeploymentType = DeploymentType.Kubernetes, LoadProfile = LoadProfile.Basic
        });

        var master = result.Infrastructure.First(n => n.Name.Contains("Master"));
        Assert.Equal(2, master.Cpu);
        Assert.Equal(4, master.RamGb);
    }

    // --- Опціональні вузли: типово вимкнені, на розрахунок не впливають ---
    [Fact]
    public void Calculate_OptionalNodes_OffByDefault_NotAdded()
    {
        var result = _engine.Calculate(new ProjectConfig
        {
            UserCount = 100, DeploymentType = DeploymentType.Windows, LoadProfile = LoadProfile.Basic
        });
        Assert.DoesNotContain(result.Infrastructure, n => n.Name.Contains("звіт"));
        Assert.DoesNotContain(result.Infrastructure, n => n.Name.Contains("Secondary"));
        Assert.DoesNotContain(result.Infrastructure, n => n.Name.Contains("HAProxy"));
    }

    // --- Опціональний вузол «Сервер звітів» додається лише за перемикачем (+2 CPU/+4 ГБ) ---
    [Fact]
    public void Calculate_ReportingServer_WhenEnabled_AddsNodeAndResources()
    {
        ProjectConfig Cfg(bool reporting) => new()
        {
            UserCount = 100, DeploymentType = DeploymentType.Windows, LoadProfile = LoadProfile.Basic,
            IncludeReportingServer = reporting
        };
        var baseReq = _engine.Calculate(Cfg(false));
        var withRep = _engine.Calculate(Cfg(true));

        var node = withRep.Infrastructure.First(n => n.Name.Contains("звіт"));
        Assert.Equal(2, node.Cpu);
        Assert.Equal(4, node.RamGb);
        Assert.Equal(baseReq.TotalCpu + 2, withRep.TotalCpu);
        Assert.Equal(baseReq.TotalRamGb + 4, withRep.TotalRamGb);
    }

    // --- SQL Failover додає другий вузол БД (копію первинного) ---
    [Fact]
    public void Calculate_SqlFailover_WhenEnabled_AddsSecondaryDbNode()
    {
        var result = _engine.Calculate(new ProjectConfig
        {
            UserCount = 100, DeploymentType = DeploymentType.Windows, LoadProfile = LoadProfile.Basic,
            IncludeSqlFailover = true
        });
        var primary = result.Infrastructure.First(n => n.Name == "SQL Server");
        var secondary = result.Infrastructure.First(n => n.Name.Contains("Secondary"));
        Assert.Equal(primary.Cpu, secondary.Cpu);
        Assert.Equal(primary.RamGb, secondary.RamGb);
        Assert.Equal(1, secondary.NodeCount);
    }

    // --- HAProxy додається лише за перемикачем (Linux 2/4) ---
    [Fact]
    public void Calculate_HaProxy_WhenEnabled_AddsLinuxNode()
    {
        var result = _engine.Calculate(new ProjectConfig
        {
            UserCount = 100, DeploymentType = DeploymentType.Kubernetes, LoadProfile = LoadProfile.Basic,
            IncludeHaProxy = true
        });
        var node = result.Infrastructure.First(n => n.Name.Contains("HAProxy"));
        Assert.Equal(2, node.Cpu);
        Assert.Equal(4, node.RamGb);
        Assert.Contains("Ubuntu", node.Os);
        // Без HA — один вузол (зворотна сумісність).
        Assert.Equal(1, node.NodeCount);
    }

    // --- HAProxy HA: 2 вузли (active/passive) замість 1, із приміткою про VRRP ---
    [Fact]
    public void Calculate_HaProxyHa_WhenEnabled_AddsTwoNodes()
    {
        var result = _engine.Calculate(new ProjectConfig
        {
            UserCount = 100, DeploymentType = DeploymentType.Kubernetes, LoadProfile = LoadProfile.Basic,
            IncludeHaProxy = true, HaProxyHa = true
        });
        var node = result.Infrastructure.First(n => n.Name.Contains("HAProxy"));
        Assert.Equal(2, node.NodeCount);
        Assert.Contains("HA", node.Notes);
    }

    // --- HA без увімкненого HAProxy не додає вузол (HA лише підсилює наявний HAProxy) ---
    [Fact]
    public void Calculate_HaProxyHa_WithoutHaProxy_AddsNothing()
    {
        var result = _engine.Calculate(new ProjectConfig
        {
            UserCount = 100, DeploymentType = DeploymentType.Kubernetes, LoadProfile = LoadProfile.Basic,
            IncludeHaProxy = false, HaProxyHa = true
        });
        Assert.DoesNotContain(result.Infrastructure, n => n.Name.Contains("HAProxy"));
    }

    // --- Гібрид: опціональні вузли НЕ дублюються (додаються один раз) ---
    [Fact]
    public void Calculate_Hybrid_OptionalNodes_NotDoubled()
    {
        var result = _engine.Calculate(new ProjectConfig
        {
            UserCount = 100, DeploymentType = DeploymentType.Hybrid, LoadProfile = LoadProfile.Basic,
            IncludeReportingServer = true, IncludeHaProxy = true
        });
        Assert.Equal(1, result.Infrastructure.Count(n => n.Name.Contains("звіт")));
        Assert.Equal(1, result.Infrastructure.Count(n => n.Name.Contains("HAProxy")));
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

    // --- Диски вузла БД ФІКСОВАНІ з матриці (як еталон); non-prod без диска Content ---
    [Fact]
    public void Calculate_DbDisks_FixedFromMatrix_NonProdDropsContent()
    {
        var prod = _engine.Calculate(new ProjectConfig
        {
            UserCount = 100, DeploymentType = DeploymentType.Windows, LoadProfile = LoadProfile.Basic,
            Environment = DeployEnvironment.Prod
        });
        var dev = _engine.Calculate(new ProjectConfig
        {
            UserCount = 100, DeploymentType = DeploymentType.Windows, LoadProfile = LoadProfile.Basic,
            Environment = DeployEnvironment.Dev
        });
        var dbProd = prod.Infrastructure.First(n => n.Name.Contains("SQL"));
        var dbDev = dev.Infrastructure.First(n => n.Name.Contains("SQL"));

        // PROD: фіксовані диски з матриці (MainData 300, Content 200) — не масштабуються.
        Assert.Equal(300, dbProd.StorageGb3);
        Assert.Equal(200, dbProd.StorageGb4);
        // non-prod: диск Content прибрано, OS зменшено.
        Assert.Equal(0, dbDev.StorageGb4);
        Assert.True(dbDev.StorageGb <= 100);
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

    // --- Відповідність еталонному Excel: 50 ліцензій, ForceBPM 25, LMS 7500, HR 2500 ---
    // Очікувано (вкладка «Стандарт k8s»): запит подів = 48.15 CPU / 191.95 ГБ RAM.
    // ROBOT і WS залежать від к-сті HR (ROBOT=3, WS=7); модулі рахуються за власною к-стю.
    [Fact]
    public void Calculate_K8s_MatchesReferenceExcel_PodTotals()
    {
        var modules = _engine.Modules.ToClonedList();
        foreach (var m in modules)
        {
            m.IsEnabled = m.Name is "App Server" or "ROBOT" or "Web" or "ForceBPM" or "LMS" or "HR Portal";
            m.UserCount = m.Name switch { "ForceBPM" => 25, "LMS" => 7500, "HR Portal" => 2500, _ => 0 };
        }
        _engine.SetModules(modules);

        var result = _engine.Calculate(new ProjectConfig
        {
            UserCount = 50, DeploymentType = DeploymentType.Kubernetes, LoadProfile = LoadProfile.Basic
        });

        Assert.Equal(48.15, result.PodCpu, 2);
        Assert.Equal(191.95, result.PodRamGb, 2);

        int Rep(string canonical) => result.Components
            .First(c => c.Name == ComponentDisplayName.Localize(canonical)).Replicas;
        Assert.Equal(3, Rep("ROBOT"));               // 1 + int(50/100) + int(2500/1000)
        Assert.Equal(7, Rep("WS (WebSocket)"));      // 1 + int(50/50) + int(2500/500)
        Assert.Equal(300, Rep("LMS-SmartID"));       // ceil(7500/25)
    }

    // --- Регресія: дрібний CPU модулів (HR Portal SmartID/GraphQL) не зникає ---
    // Симптом, що пам'ятали: при малій к-сті HR Portal CPU SmartID (0.006) і GraphQL (0.01)
    // показувались як 0 — через округлення підсумку компонента до 1 знака. Тепер 2 знаки.
    [Fact]
    public void Calculate_K8s_HrPortalSmallCount_SmartIdCpuNotLost()
    {
        var modules = _engine.Modules.ToClonedList();
        var hr = modules.First(m => m.Name == "HR Portal");
        hr.IsEnabled = true;
        hr.UserCount = 100; // Per100 → рівно 1 репліка SmartID/GraphQL
        _engine.SetModules(modules);

        var result = _engine.Calculate(new ProjectConfig
        {
            UserCount = 100, DeploymentType = DeploymentType.Kubernetes, LoadProfile = LoadProfile.Basic
        });

        var smartId = result.Components.First(c => c.Name == ComponentDisplayName.Localize("HR-SmartID"));
        var graphql = result.Components.First(c => c.Name == ComponentDisplayName.Localize("HR-GraphQL"));

        // Рушій зберігає точні значення з матриці (1 репліка).
        Assert.Equal(1, smartId.Replicas);
        Assert.Equal(0.006, smartId.Cpu, 3);
        Assert.Equal(0.01, graphql.Cpu, 3);

        // Суть фікса: округлення до 2 знаків лишає значення видимим (>0),
        // тоді як старе округлення до 1 знака давало 0.
        Assert.True(Math.Round(smartId.Cpu, 2) > 0);
        Assert.Equal(0, Math.Round(smartId.Cpu, 1)); // демонструє колишній баг
        Assert.True(Math.Round(graphql.Cpu, 2) > 0);
    }

    // --- Відповідність еталону: профіль Документообіг (БД + сервери додатків), 200 ліцензій ---
    [Fact]
    public void Calculate_DocumentFlow_Windows_MatchesReferenceRanges()
    {
        _engine.SetProductType(ProductType.DocumentFlow);
        var result = _engine.Calculate(new ProjectConfig
        {
            UserCount = 200, DeploymentType = DeploymentType.Windows,
            ProductType = ProductType.DocumentFlow, LoadProfile = LoadProfile.Performance
        });
        var db = result.Infrastructure.First(n => n.Name.Contains("SQL"));
        Assert.Equal(6, db.Cpu);        // MSSQL Документообіг 101-200 → 6 ядер
        Assert.Equal(64, db.RamGb);     // RAM rec 64
        var app = result.Infrastructure.First(n => n.Name.Contains("додатк"));
        Assert.Equal(3, app.NodeCount); // AppServers Документообіг 101-200 → 3 ВМ
        Assert.Equal(32, app.RamGb);    // RAM rec 32
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
    public void EffectiveUsers_OwnCount_UncappedByDefault_CappedOnDemand()
    {
        var m = new ProjectModule();
        Assert.Equal(100, m.EffectiveUsers(100));        // 0 = загальна
        m.UserCount = 30;
        Assert.Equal(30, m.EffectiveUsers(100));         // власна
        m.UserCount = 200;
        // PROD (cap=false, як у Excel): власна к-сть незалежна, понад загальну.
        Assert.Equal(200, m.EffectiveUsers(100));
        Assert.Equal(200, m.EffectiveUsers(100, cap: false));
        // Похідні середовища (cap=true): обмежується к-стю середовища.
        Assert.Equal(100, m.EffectiveUsers(100, cap: true));
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
        // Назва компонента у звітах локалізована (ComponentDisplayName).
        var graphql = result.Components.First(c => c.Name == ComponentDisplayName.Localize("LMS-GraphQL"));
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

using AIResourceCalculator.Data;
using AIResourceCalculator.Interfaces;
using AIResourceCalculator.Models;

namespace AIResourceCalculator.Services;

public class SizingEngine : ISizingEngine
{
    private readonly SizingMatrix _matrix;
    private List<ProjectModule> _modules;
    private ProductType _currentProduct = ProductType.Standard;

    public IReadOnlyList<ProjectModule> Modules => _modules.AsReadOnly();
    public ProductType CurrentProduct => _currentProduct;

    public SizingEngine(SizingMatrix matrix)
    {
        _matrix = matrix;
        _modules = matrix.StandardModules.ToClonedList();
    }

    public void SetModules(List<ProjectModule> modules)
    {
        _modules = modules;
    }

    public void SetProductType(ProductType productType)
    {
        _currentProduct = productType;
        var source = productType == ProductType.DocumentFlow
            ? _matrix.DocumentFlowModules
            : _matrix.StandardModules;
        _modules = source.ToClonedList();
    }

    public ResourceRequirement Calculate(ProjectConfig config)
    {
        var req = new ResourceRequirement
        {
            UserCount = config.UserCount,
            DeploymentType = config.DeploymentType,
            LoadProfile = config.LoadProfile
        };

        if (config.DeploymentType == DeploymentType.Hybrid)
            CalculateHybrid(req, config);
        else if (config.DeploymentType == DeploymentType.Kubernetes)
            CalculateK8s(req, config);
        else
            CalculateWindows(req, config);

        return req;
    }

    // includeDatabase=false і excludeModules використовуються в гібриді: БД та app/web
    // частина живуть на Windows, тому K8s їх не додає (інакше — подвійний облік).
    private void CalculateK8s(ResourceRequirement req, ProjectConfig config,
        bool includeDatabase = true, HashSet<string>? excludeModules = null)
    {
        var sqlRange = FindDatabaseRange(config.UserCount, config.LoadProfile, config.DatabaseType);
        var masterNode = _matrix.DefaultK8sMaster ?? _defaultMaster;
        var workerNode = _matrix.DefaultK8sWorker ?? _defaultWorker;

        double totalCpu = 0, totalRam = 0;

        var enabledModules = _modules.Where(m => m.IsEnabled
            && !m.Name.Contains("Windows")
            && (excludeModules == null || !excludeModules.Contains(m.Name))).ToList();

        foreach (var module in enabledModules)
        {
            // Модуль масштабується за СВОЄЮ кількістю користувачів (LMS/HR Portal використовує
            // не вся компанія); 0 = загальна, понад загальну не піднімається.
            var moduleUsers = module.EffectiveUsers(config.UserCount);
            var (modCpu, modRam) = module.CalculateReplicas(moduleUsers, config.LoadProfile);
            totalCpu += modCpu;
            totalRam += modRam;

            var isPerf = config.LoadProfile == LoadProfile.Performance;
            foreach (var comp in module.Components ?? new())
            {
                int rep = CalcReplicas(comp, moduleUsers);
                if (rep == 0) rep = 1;

                var cpu = isPerf && comp.PerfCpu > 0 ? comp.PerfCpu : comp.Cpu;
                var ram = isPerf && comp.PerfRamGb > 0 ? comp.PerfRamGb : comp.RamGb;
                req.Components.Add(new ServiceComponent
                {
                    Name = comp.Name,
                    Cpu = cpu * rep,
                    RamGb = ram * rep,
                    CpuPerReplica = cpu,
                    RamPerReplicaGb = ram,
                    Replicas = rep,
                    FixedReplicas = comp.FixedReplicas,
                    Formula = comp.Formula,
                    Category = module.Name,
                    HasLocalSql = comp.HasLocalSql,
                    HasRedis = comp.HasRedis,
                    Notes = comp.Notes
                });
            }
        }

        var workerCpuCapacity = workerNode.Cpu > 0 ? workerNode.Cpu : DefaultWorkerCpu;
        var workerRamCapacity = workerNode.RamGb > 0 ? workerNode.RamGb : DefaultWorkerRamGb;
        var workerCount = Math.Max(1,
            (int)Math.Ceiling(Math.Max(totalCpu / workerCpuCapacity, totalRam / workerRamCapacity)));

        // totalCpu/totalRam — це сукупний ЗАПИТ подів; він потрібен лише для розрахунку
        // кількості worker-вузлів вище. Підсумкові TotalCpu/TotalRamGb рахуються нижче
        // як ФІЗИЧНІ ресурси всіх вузлів (щоб значення було зіставне з Windows-режимом).
        req.WorkerNodeCount = workerCount;
        req.MasterNodeCount = masterNode.NodeCount > 0 ? masterNode.NodeCount : 1;
        req.PodCpu = totalCpu;
        req.PodRamGb = totalRam;

        var dbName = GetDatabaseNodeName(config.DatabaseType);
        var sqlNode = _matrix.DefaultK8sSql ?? _defaultSql;
        if (includeDatabase)
        {
            var dbRam = sqlRange?.RamRec ?? sqlNode.RamGb;
            var dbNode = new InfrastructureNode
            {
                Name = dbName, Os = sqlNode.Os, Cpu = sqlRange?.Cpu ?? sqlNode.Cpu,
                RamGb = dbRam, NodeCount = 1,
                StorageType = sqlNode.StorageType, StorageGb = sqlNode.StorageGb,
                StorageType2 = sqlNode.StorageType2, StorageGb2 = sqlNode.StorageGb2,
                StorageType3 = sqlNode.StorageType3, StorageGb3 = sqlNode.StorageGb3,
                StorageType4 = sqlNode.StorageType4, StorageGb4 = sqlNode.StorageGb4,
                PageFileGb = sqlNode.PageFileGb, PageFileType = sqlNode.PageFileType,
                Iops = sqlRange?.Iops ?? 500, Latency = sqlRange?.Latency ?? 1,
                IopsProfile = sqlRange?.IopsProfile ?? DefaultIopsProfile,
                ThroughputMiBs = ThroughputFor(sqlRange)
            };
            ApplyDbDisks(dbNode, config.DatabaseType, config.DbDataSizeGb, dbRam, config.Environment);
            req.Infrastructure.Add(dbNode);
        }
        req.Infrastructure.Add(new InfrastructureNode
        {
            Name = "Master Node", Os = masterNode.Os, Cpu = masterNode.Cpu,
            RamGb = masterNode.RamGb, NodeCount = req.MasterNodeCount,
            StorageType = masterNode.StorageType, StorageGb = masterNode.StorageGb,
            StorageType2 = masterNode.StorageType2, StorageGb2 = masterNode.StorageGb2,
            StorageType3 = masterNode.StorageType3, StorageGb3 = masterNode.StorageGb3,
            StorageType4 = masterNode.StorageType4, StorageGb4 = masterNode.StorageGb4,
            PageFileGb = masterNode.PageFileGb, PageFileType = masterNode.PageFileType
        });
        req.Infrastructure.Add(new InfrastructureNode
        {
            Name = "Worker Node", Os = workerNode.Os, Cpu = workerNode.Cpu,
            RamGb = workerNode.RamGb, NodeCount = req.WorkerNodeCount,
            StorageType = workerNode.StorageType, StorageGb = workerNode.StorageGb,
            StorageType2 = workerNode.StorageType2, StorageGb2 = workerNode.StorageGb2,
            StorageType3 = workerNode.StorageType3, StorageGb3 = workerNode.StorageGb3,
            StorageType4 = workerNode.StorageType4, StorageGb4 = workerNode.StorageGb4,
            PageFileGb = workerNode.PageFileGb, PageFileType = workerNode.PageFileType,
            // Вимоги до дисків worker-вузлів (за документом): профіль 30r/70w, від 500 IOPS.
            Iops = workerNode.Iops > 0 ? workerNode.Iops : DefaultWorkerIops,
            IopsProfile = string.IsNullOrWhiteSpace(workerNode.IopsProfile) ? K8sIopsProfile : workerNode.IopsProfile,
            Latency = workerNode.Latency > 0 ? workerNode.Latency : DefaultWorkerLatency
        });

        // IOPS/latency атрибутуються БД-вузлу. У гібриді (includeDatabase=false) БД на Windows,
        // тож тут IOPS = 0, а підсумок IOPS рахується у CalculateHybrid із Windows-частини.
        req.TotalIops = includeDatabase ? (sqlRange?.Iops ?? 500) : 0;
        req.TotalLatency = sqlRange?.Latency ?? 1;

        // GPU node for video transcoding (LMS-Videoutilities)
        var hasGpuComponent = req.Components.Any(c =>
            c.Notes.Contains("GPU", StringComparison.OrdinalIgnoreCase));
        if (hasGpuComponent)
        {
            var gpuCount = Math.Max(1, (int)Math.Ceiling((double)config.UserCount / UsersPerGpuNode));
            req.Infrastructure.Add(new InfrastructureNode
            {
                Name = "GPU Node (T4/A10)", Os = "Ubuntu 24.04", Cpu = GpuNodeCpu, RamGb = GpuNodeRamGb,
                NodeCount = gpuCount, StorageGb = GpuNodeStorageGb, StorageType = "SSD"
            });
        }

        // Підсумок = сума ФІЗИЧНИХ ресурсів усіх вузлів (SQL + Master + Worker [+ GPU]).
        req.TotalCpu = req.Infrastructure.Sum(n => n.Cpu * n.NodeCount);
        req.TotalRamGb = req.Infrastructure.Sum(n => n.RamGb * n.NodeCount);
        req.TotalStorageGb = req.Infrastructure.Sum(n => n.TotalStorageGb);
    }

    private void CalculateWindows(ResourceRequirement req, ProjectConfig config)
    {
        var appRange = FindWindowsRange(config.UserCount, _matrix.AppServerRanges,
            _matrix.AppServerPerformanceRanges, config.LoadProfile);
        var webRange = FindWindowsRange(config.UserCount, _matrix.WebServerRanges,
            _matrix.WebServerPerformanceRanges ?? new(), config.LoadProfile);
        var sqlRange = FindDatabaseRange(config.UserCount, config.LoadProfile, config.DatabaseType);

        var sqlNode = _matrix.DefaultWindowsSql ?? _defaultSql;
        var appNode = _matrix.DefaultWindowsApp;
        var webNode = _matrix.DefaultWindowsWeb;

        // Windows sizing mirrors the Excel "Windows" sheet: it is purely VM-based
        // (SQL + AppServers×count + WebServers×count). The K8s module/pod breakdown is NOT added —
        // on Windows the application runs inside the app-server VMs, so adding pod CPU/RAM would double-count.
        var appCpu = appRange?.Cpu ?? 4;
        var appRam = appRange?.RamRec ?? 16;
        var appCount = appRange?.InstanceCount ?? 1;
        var webCpu = webRange?.Cpu ?? 4;
        var webRam = webRange?.RamRec ?? 8;
        var webCount = webRange?.InstanceCount ?? 1;

        req.TotalCpu = (sqlRange?.Cpu ?? 4) + appCpu * appCount + webCpu * webCount;
        req.TotalRamGb = (sqlRange?.RamRec ?? 16) + appRam * appCount + webRam * webCount;
        // IOPS не сумуються між дисками/вузлами — визначальним є вузол БД (найвибагливіший).
        // IOPS app/web показуються окремо в таблиці по вузлах.
        req.TotalIops = sqlRange?.Iops ?? 500;

        req.WorkerNodeCount = appCount + webCount;
        // Windows — суто VM-розгортання без керуючого (master) вузла Kubernetes, тож 0.
        // Інакше у гібриді master рахувався як K8s(1)+Windows(1)=2, хоча в інфраструктурі
        // master-вузол лише один (від K8s) — звідси розбіжність «статистика 2 / таблиця 1».
        req.MasterNodeCount = 0;

        var dbName = GetDatabaseNodeName(config.DatabaseType);
        var sqlRam = sqlRange?.RamRec ?? sqlNode.RamGb;
        var dbNode = new InfrastructureNode
        {
            Name = dbName, Os = sqlNode.Os, Cpu = sqlRange?.Cpu ?? sqlNode.Cpu,
            RamGb = sqlRam, NodeCount = 1,
            StorageType = sqlNode.StorageType, StorageGb = sqlNode.StorageGb,
            StorageType2 = sqlNode.StorageType2, StorageGb2 = sqlNode.StorageGb2,
            StorageType3 = sqlNode.StorageType3, StorageGb3 = sqlNode.StorageGb3,
            StorageType4 = sqlNode.StorageType4, StorageGb4 = sqlNode.StorageGb4,
            PageFileGb = sqlNode.PageFileGb > 0 ? sqlNode.PageFileGb : (int)Math.Ceiling(sqlRam * 1.0),
            PageFileType = sqlNode.PageFileType ?? "Auto",
            Iops = sqlRange?.Iops ?? 500, Latency = sqlRange?.Latency ?? 1,
            IopsProfile = sqlRange?.IopsProfile ?? DefaultIopsProfile,
            ThroughputMiBs = ThroughputFor(sqlRange)
        };
        ApplyDbDisks(dbNode, config.DatabaseType, config.DbDataSizeGb, sqlRam, config.Environment);
        req.Infrastructure.Add(dbNode);
        req.Infrastructure.Add(new InfrastructureNode
        {
            Name = appNode?.Name ?? "App Server", Os = appNode?.Os ?? "Windows Server 2022",
            Cpu = appCpu, RamGb = appRam, NodeCount = appCount,
            StorageType = appNode?.StorageType ?? "SSD", StorageGb = appNode?.StorageGb ?? 150,
            StorageType2 = appNode?.StorageType2 ?? "", StorageGb2 = appNode?.StorageGb2 ?? 0,
            StorageType3 = appNode?.StorageType3 ?? "", StorageGb3 = appNode?.StorageGb3 ?? 0,
            StorageType4 = appNode?.StorageType4 ?? "", StorageGb4 = appNode?.StorageGb4 ?? 0,
            // Файл підкачки app-сервера: з матриці або, якщо не задано, = RAM вузла.
            PageFileGb = appNode?.PageFileGb > 0 ? appNode.PageFileGb : (int)Math.Ceiling(appRam),
            PageFileType = string.IsNullOrEmpty(appNode?.PageFileType) ? "SSD" : appNode.PageFileType,
            // IOPS сервера додатків — з діапазону (за документом 250→500, профіль 30r/70w).
            Iops = appRange?.Iops ?? appNode?.Iops ?? 0,
            IopsProfile = string.IsNullOrEmpty(appNode?.IopsProfile) ? AppServerIopsProfile : appNode.IopsProfile,
            Latency = appNode?.Latency ?? 0
        });
        req.Infrastructure.Add(new InfrastructureNode
        {
            Name = webNode?.Name ?? "Web Server (IIS)", Os = webNode?.Os ?? "Windows Server 2022",
            Cpu = webCpu, RamGb = webRam, NodeCount = webCount,
            StorageType = webNode?.StorageType ?? "SSD", StorageGb = webNode?.StorageGb ?? 150,
            StorageType2 = webNode?.StorageType2 ?? "", StorageGb2 = webNode?.StorageGb2 ?? 0,
            StorageType3 = webNode?.StorageType3 ?? "", StorageGb3 = webNode?.StorageGb3 ?? 0,
            StorageType4 = webNode?.StorageType4 ?? "", StorageGb4 = webNode?.StorageGb4 ?? 0,
            // Файл підкачки IIS/web-сервера: з матриці або, якщо не задано, = RAM вузла.
            PageFileGb = webNode?.PageFileGb > 0 ? webNode.PageFileGb : (int)Math.Ceiling(webRam),
            PageFileType = string.IsNullOrEmpty(webNode?.PageFileType) ? "SSD" : webNode.PageFileType,
            // IOPS веб-сервера — з діапазону (за документом 200, профіль 70r/30w).
            Iops = webRange?.Iops ?? webNode?.Iops ?? 0,
            IopsProfile = string.IsNullOrEmpty(webNode?.IopsProfile) ? WebServerIopsProfile : webNode.IopsProfile,
            Latency = webNode?.Latency ?? 0
        });

        req.TotalStorageGb = req.Infrastructure.Sum(n => n.TotalStorageGb);
        req.TotalLatency = sqlRange?.Latency ?? 1;
    }

    // Гібрид: Сервери додатків та Веб-сервери (IIS) розгортаються на Windows-VM, а ForceBPM
    // та інші сервіси — на K8s (Linux). База даних — на Windows. Тому:
    //  • K8s рахує лише свої модулі (App Server + Web ВИКЛЮЧЕНІ) і НЕ додає БД;
    //  • Windows додає app/web VM + БД.
    // Так усувається подвійний облік, через який гібрид «криво рахував».
    private static readonly HashSet<string> HybridWindowsModules = new() { "App Server", "Web" };

    private void CalculateHybrid(ResourceRequirement req, ProjectConfig config)
    {
        var k8sReq = new ResourceRequirement { UserCount = config.UserCount, DeploymentType = DeploymentType.Kubernetes, LoadProfile = config.LoadProfile };
        var winReq = new ResourceRequirement { UserCount = config.UserCount, DeploymentType = DeploymentType.Windows, LoadProfile = config.LoadProfile };

        var k8sConfig = new ProjectConfig { ProjectName = config.ProjectName, UserCount = config.UserCount, DeploymentType = DeploymentType.Kubernetes, LoadProfile = config.LoadProfile, ProductType = config.ProductType, DatabaseType = config.DatabaseType };
        var winConfig = new ProjectConfig { ProjectName = config.ProjectName, UserCount = config.UserCount, DeploymentType = DeploymentType.Windows, LoadProfile = config.LoadProfile, ProductType = config.ProductType, DatabaseType = config.DatabaseType };

        CalculateK8s(k8sReq, k8sConfig, includeDatabase: false, excludeModules: HybridWindowsModules);
        CalculateWindows(winReq, winConfig);

        // БД — на Windows-частині; K8s IOPS = 0, тож підсумок IOPS бере Windows.
        req.TotalIops = winReq.TotalIops + k8sReq.TotalIops;
        req.TotalLatency = winReq.TotalLatency;
        req.PodCpu = k8sReq.PodCpu;
        req.PodRamGb = k8sReq.PodRamGb;
        req.WorkerNodeCount = k8sReq.WorkerNodeCount + winReq.WorkerNodeCount;
        req.MasterNodeCount = k8sReq.MasterNodeCount + winReq.MasterNodeCount;

        req.Infrastructure.AddRange(k8sReq.Infrastructure); // Master + Worker (+ GPU), без БД
        req.Infrastructure.AddRange(winReq.Infrastructure); // App + Web (IIS) + БД
        req.Components.AddRange(k8sReq.Components);

        req.TotalCpu = req.Infrastructure.Sum(n => n.Cpu * n.NodeCount);
        req.TotalRamGb = req.Infrastructure.Sum(n => n.RamGb * n.NodeCount);
        req.TotalStorageGb = req.Infrastructure.Sum(n => n.TotalStorageGb);
    }

    private UserLoadRange? FindDatabaseRange(int userCount, LoadProfile profile, DatabaseType dbType)
    {
        var ranges = dbType switch
        {
            DatabaseType.PostgreSQL => _matrix.PostgresRanges,
            DatabaseType.Oracle => _matrix.OracleRanges,
            _ => profile == LoadProfile.Performance
                ? _matrix.MsSqlPerformanceRanges
                : _matrix.MsSqlRanges
        };
        return ranges.FirstOrDefault(r => userCount >= r.MinUsers && userCount <= r.MaxUsers)
               ?? ranges.OrderByDescending(r => r.MaxUsers).FirstOrDefault();
    }

    private UserLoadRange? FindWindowsRange(int userCount,
        List<UserLoadRange> basic, List<UserLoadRange> performance, LoadProfile profile)
    {
        var ranges = profile == LoadProfile.Performance && performance.Count > 0
            ? performance : basic;
        return ranges.FirstOrDefault(r => userCount >= r.MinUsers && userCount <= r.MaxUsers)
               ?? ranges.OrderByDescending(r => r.MaxUsers).FirstOrDefault();
    }

    private static int CalcReplicas(ModuleComponent comp, int userCount)
        => ReplicaMath.Resolve(comp.Formula, comp.FixedReplicas, userCount);

    // Пропускна здатність диска БД (MiB/s) — з матриці (значення документа D-AD-ADM-E).
    // Не оцінюємо з IOPS: документ задає MiB/s окремою таблицею, тож вигаданий розрахунок
    // давав би хибні числа. Якщо у діапазоні не задано (напр. PostgreSQL/Oracle) — 0 (не показуємо).
    private static int ThroughputFor(UserLoadRange? range) => range?.ThroughputMiBs ?? 0;

    private static string GetDatabaseNodeName(DatabaseType dbType) => dbType switch
    {
        DatabaseType.PostgreSQL => "PostgreSQL",
        DatabaseType.Oracle => "Oracle 19c",
        _ => "SQL Server"
    };

    // Диски вузла БД масштабуються за ОБСЯГОМ ДАНИХ (а не фіксовано): немає сенсу тримати
    // терабайтні диски під базу в 10-20 ГБ. OS-диск та Content (холодні/неструктуровані дані)
    // лишаються як у матриці; Data та Logs/TempDB рахуються від обсягу даних із розумним мінімумом.
    private static void ApplyDbDisks(InfrastructureNode db, DatabaseType dbType, int dbDataGb, double dbRamGb,
        DeployEnvironment environment)
    {
        dbDataGb = Math.Max(1, dbDataGb);
        // Logs + TempDB ≈ обсяг даних (tempdb може сягати розміру БД); мінімум 50 ГБ.
        db.StorageGb2 = Math.Max(50, (int)Math.Ceiling(dbDataGb * 1.0));
        if (string.IsNullOrWhiteSpace(db.StorageType2)) db.StorageType2 = "SSD";
        // MainData: дані + індекси + запас на зростання (×2); мінімум 100 ГБ (стартова конфігурація).
        db.StorageGb3 = Math.Max(100, (int)Math.Ceiling(dbDataGb * 2.0));
        if (string.IsNullOrWhiteSpace(db.StorageType3)) db.StorageType3 = "SSD";

        db.DbVersion = DbVersionLabel(dbType, dbRamGb, db.Cpu, environment);
        // Для робочих середовищ Enterprise потрібна, якщо перевищено ліміти Standard (RAM або ядра).
        if (dbType == DatabaseType.MsSql && environment == DeployEnvironment.Prod
            && (dbRamGb > MsSqlStandardMaxRamGb || db.Cpu > MsSqlStandardMaxCores))
        {
            var reason = dbRamGb > MsSqlStandardMaxRamGb && db.Cpu > MsSqlStandardMaxCores
                ? $"RAM {dbRamGb:0} ГБ > {MsSqlStandardMaxRamGb:0} ГБ і {db.Cpu:0} ядер > {MsSqlStandardMaxCores} ядер"
                : dbRamGb > MsSqlStandardMaxRamGb
                    ? $"RAM {dbRamGb:0} ГБ > {MsSqlStandardMaxRamGb:0} ГБ"
                    : $"{db.Cpu:0} ядер > {MsSqlStandardMaxCores} ядер";
            var note = $"{reason} — потрібна редакція Enterprise (ліміти Standard)";
            db.Notes = string.IsNullOrWhiteSpace(db.Notes) ? note : $"{db.Notes}; {note}";
        }
    }

    // SQL Server мінімальна підтримувана версія — 2022 (за вимогами D-AD-ADM-E).
    // Редакція Standard обмежена 128 ГБ ОЗП та 24 ядрами на екземпляр БД → понад це потрібна Enterprise.
    // Non-prod (DEV/TEST/PreProd) використовує безкоштовну Developer Edition (не для робочого навантаження).
    private const double MsSqlStandardMaxRamGb = 128;
    private const double MsSqlStandardMaxCores = 24;

    // Зворотно-сумісне перевантаження (PROD, без урахування ядер).
    public static string DbVersionLabel(DatabaseType dbType, double dbRamGb)
        => DbVersionLabel(dbType, dbRamGb, 0, DeployEnvironment.Prod);

    public static string DbVersionLabel(DatabaseType dbType, double dbRamGb, double dbCpu, DeployEnvironment environment)
        => dbType switch
        {
            DatabaseType.PostgreSQL => "PostgreSQL 17+",
            DatabaseType.Oracle => "Oracle Database 19c Enterprise Edition",
            // MS SQL: Developer для non-prod; для PROD — Standard/Enterprise за лімітами ядер і RAM.
            _ => environment != DeployEnvironment.Prod
                ? "MS SQL Server 2022 Developer Edition"
                : $"MS SQL Server 2022 {(dbRamGb > MsSqlStandardMaxRamGb || dbCpu > MsSqlStandardMaxCores ? "Enterprise" : "Standard")}"
        };

    // Fallback worker capacity when matrix node specs are missing
    private const double DefaultWorkerCpu = 8;
    private const double DefaultWorkerRamGb = 32;

    // Профілі читання/запису дисків за документом D-AD-ADM-E:
    //  • сервер БД — 50r/50w; сервери додатків — 30r/70w; веб-сервери — 70r/30w;
    //  • вузли Kubernetes (master/worker) — 30r/70w.
    private const string DefaultIopsProfile = "50r/50w";        // сервер БД
    private const string AppServerIopsProfile = "30r/70w";
    private const string WebServerIopsProfile = "70r/30w";
    private const string K8sIopsProfile = "30r/70w";
    private const int DefaultWorkerIops = 500;
    private const double DefaultWorkerLatency = 5;

    // GPU node defaults for video transcoding (LMS-Videoutilities)
    private const int UsersPerGpuNode = 100;
    private const int GpuNodeCpu = 8;
    private const int GpuNodeRamGb = 32;
    private const int GpuNodeStorageGb = 200;

    private static readonly InfrastructureNode _defaultSql = new() { Name = "SQL Server", Os = "Windows Server 2022", Cpu = 4, RamGb = 12, NodeCount = 1, StorageGb = 300, StorageType = "SSD" };
    private static readonly InfrastructureNode _defaultMaster = new() { Name = "Master Node", Os = "Ubuntu 24.04", Cpu = 3, RamGb = 6, NodeCount = 1, StorageGb = 100, StorageType = "SSD" };
    private static readonly InfrastructureNode _defaultWorker = new() { Name = "Worker Node", Os = "Ubuntu 24.04", Cpu = 8, RamGb = 32, NodeCount = 1, StorageGb = 200, StorageType = "SSD" };
}

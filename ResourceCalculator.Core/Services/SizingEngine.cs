using ResourceCalculator.Data;
using ResourceCalculator.Interfaces;
using ResourceCalculator.Models;

namespace ResourceCalculator.Services;

public class SizingEngine : ISizingEngine
{
    private readonly SizingMatrix _matrix;
    private List<ProjectModule> _modules;

    public IReadOnlyList<ProjectModule> Modules => _modules.AsReadOnly();

    public SizingEngine(SizingMatrix matrix)
    {
        _matrix = matrix;
        _modules = matrix.DocumentFlowModules.ToClonedList();
    }

    public void SetModules(List<ProjectModule> modules)
    {
        _modules = modules;
    }

    // Перечитує модулі з матриці (напр. після Reset/Import у редакторі матриці).
    public void ReloadModules()
    {
        _modules = _matrix.DocumentFlowModules.ToClonedList();
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

        AddOptionalNodes(req, config);

        return req;
    }

    // Опціональні вузли інфраструктури — додаються ЛИШЕ коли користувач їх увімкнув (типово
    // вимкнені, як модулі LMS/HR Portal). Додаються один раз тут (а не всередині
    // CalculateWindows/K8s), щоб у гібриді не було подвійного обліку. Підсумки CPU/RAM/диски
    // перераховуються від інфраструктури (для Windows/K8s/гібриду це тотожно базовому значенню,
    // коли нічого не додано, тож поведінка без перемикачів не змінюється).
    private void AddOptionalNodes(ResourceRequirement req, ProjectConfig config)
    {
        if (!config.IncludeSqlFailover && !config.IncludeReportingServer && !config.IncludeHaProxy)
            return;

        // SQL Failover: другий ідентичний вузол БД (failover-кластер) — клон первинного вузла БД.
        if (config.IncludeSqlFailover)
        {
            var primary = req.Infrastructure.FirstOrDefault(n => !string.IsNullOrEmpty(n.DbVersion))
                ?? req.Infrastructure.FirstOrDefault(n =>
                    n.Name.Contains("SQL") || n.Name.Contains("PostgreSQL") || n.Name.Contains("Oracle"));
            if (primary != null)
            {
                var secondary = primary.Clone();
                secondary.Name = $"{primary.Name} (Secondary)";
                secondary.NodeCount = 1;
                req.Infrastructure.Add(secondary);
            }
        }

        if (config.IncludeReportingServer)
            req.Infrastructure.Add((_matrix.DefaultReportingServer ?? _defaultReporting).Clone());

        if (config.IncludeHaProxy)
        {
            var ha = (_matrix.DefaultHaProxy ?? _defaultHaProxy).Clone();
            ha.NodeCount = 1;
            req.Infrastructure.Add(ha);
        }

        req.TotalCpu = req.Infrastructure.Sum(n => n.Cpu * n.NodeCount);
        req.TotalRamGb = req.Infrastructure.Sum(n => n.RamGb * n.NodeCount);
        req.TotalStorageGb = req.Infrastructure.Sum(n => n.TotalStorageGb);
    }

    // includeDatabase=false і excludeModules використовуються в гібриді: БД та app/web
    // частина живуть на Windows, тому K8s їх не додає (інакше — подвійний облік).
    private void CalculateK8s(ResourceRequirement req, ProjectConfig config,
        bool includeDatabase = true, HashSet<string>? excludeModules = null, bool includeSmartId = true)
    {
        var sqlRange = FindDatabaseRange(config.UserCount, config.DatabaseType);
        var masterNode = _matrix.DefaultK8sMaster ?? _defaultMaster;
        var workerNode = _matrix.DefaultK8sWorker ?? _defaultWorker;

        double totalCpu = 0, totalRam = 0;

        var enabledModules = _modules.Where(m => m.IsEnabled
            && !m.Name.Contains("Windows")
            && (excludeModules == null || !excludeModules.Contains(m.Name))).ToList();

        // Кожен модуль рахується за ВЛАСНОЮ (необмеженою) к-стю користувачів — як у Excel
        // (напр. LMS 7500 при 50 ліцензіях). Для похідних середовищ власні к-сті модулів
        // задаються окремо (MainViewModel клонує модулі з к-стями цього середовища).
        // За Excel ROBOT і WS масштабуються ще й від к-сті користувачів HR Portal (A40).
        int hrUsers = enabledModules.FirstOrDefault(m => m.Name == "HR Portal")
            ?.EffectiveUsers(config.UserCount) ?? 0;

        foreach (var module in enabledModules)
        {
            var moduleUsers = module.EffectiveUsers(config.UserCount);
            var (modCpu, modRam) = module.CalculateReplicas(moduleUsers, hrUsers);
            totalCpu += modCpu;
            totalRam += modRam;

            foreach (var comp in module.Components ?? new())
            {
                int rep = CalcReplicas(comp, moduleUsers, hrUsers);
                if (rep <= 0) rep = Math.Max(1, comp.FixedReplicas);

                var cpu = comp.PerfCpu > 0 ? comp.PerfCpu : comp.Cpu;
                var ram = comp.PerfRamGb > 0 ? comp.PerfRamGb : comp.RamGb;
                req.Components.Add(new ServiceComponent
                {
                    Name = ComponentDisplayName.Localize(comp.Name),
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

        // Центральний SmartID (SSO) — ОДИН на всю систему, масштабується від загальної к-сті
        // користувачів. У гібриді може йти на веб-сервери IIS (includeSmartId=false) — тоді окрема ВМ
        // не додається (IIS і є веб-сервер). У чистому Windows SmartID теж на IIS (поди не рахуються).
        if (includeSmartId)
        {
            var smartIdCpu = _matrix.Engine?.SmartIdCpuPerReplica > 0 ? _matrix.Engine.SmartIdCpuPerReplica : SmartIdCpuPerReplica;
            var smartIdRam = _matrix.Engine?.SmartIdRamPerReplicaGb > 0 ? _matrix.Engine.SmartIdRamPerReplicaGb : SmartIdRamPerReplicaGb;
            int rep = Math.Max(1, (int)Math.Ceiling(config.UserCount / 25.0));
            totalCpu += smartIdCpu * rep;
            totalRam += smartIdRam * rep;
            req.Components.Add(new ServiceComponent
            {
                Name = "SmartID", Cpu = smartIdCpu * rep, RamGb = smartIdRam * rep,
                CpuPerReplica = smartIdCpu, RamPerReplicaGb = smartIdRam,
                Replicas = rep, Formula = ReplicaFormula.Per25Users, Category = "SmartID"
            });
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
                Ghz = GhzFor(sqlRange, sqlNode),
                RamGb = dbRam, NodeCount = 1,
                StorageType = sqlNode.StorageType, StorageGb = sqlNode.StorageGb,
                StorageType2 = sqlNode.StorageType2, StorageGb2 = sqlNode.StorageGb2,
                StorageType3 = sqlNode.StorageType3, StorageGb3 = sqlNode.StorageGb3,
                StorageType4 = sqlNode.StorageType4, StorageGb4 = sqlNode.StorageGb4,
                PageFileGb = sqlNode.PageFileGb, PageFileType = sqlNode.PageFileType,
                Iops = sqlRange?.Iops ?? 500, Latency = sqlRange?.Latency ?? 1,
                IopsProfile = sqlRange?.IopsProfile ?? DbIopsProfile,
                ThroughputMiBs = ThroughputFor(sqlRange),
                PageFileNotApplicable = true
            };
            ApplyDbDisks(dbNode, config.DatabaseType, dbRam, config.Environment, config.DbSizeGb, config.ContentDbSizeGb);
            req.Infrastructure.Add(dbNode);
        }
        // Master node: etcd тут "зовнішній" відносно control-plane, але фізично на цьому ж
        // вузлі (не stacked, проте й не на окремих виділених серверах) — тож I/O etcd навантажує
        // саме цей диск, і IOPS/латентність варто показувати за офіційним etcd sizing guide,
        // а не лишати порожніми. Розподіл Logs/MainData/Content і файл підкачки — не застосовні
        // (control-plane не має БД-подібного розподілу; kubelet вимагає вимкнений swap).
        var masterIops = masterNode.Iops > 0 ? masterNode.Iops : EtcdIops2;
        req.Infrastructure.Add(new InfrastructureNode
        {
            Name = "Master node", Os = masterNode.Os, Cpu = masterNode.Cpu, Ghz = masterNode.Ghz,
            RamGb = masterNode.RamGb, NodeCount = req.MasterNodeCount,
            StorageType = masterNode.StorageType, StorageGb = masterNode.StorageGb,
            StorageType2 = masterNode.StorageType2, StorageGb2 = masterNode.StorageGb2,
            StorageType3 = masterNode.StorageType3, StorageGb3 = masterNode.StorageGb3,
            StorageType4 = masterNode.StorageType4, StorageGb4 = masterNode.StorageGb4,
            PageFileGb = masterNode.PageFileGb, PageFileType = masterNode.PageFileType,
            Iops = masterIops,
            IopsProfile = string.IsNullOrWhiteSpace(masterNode.IopsProfile) ? EtcdIopsProfile2 : masterNode.IopsProfile,
            ThroughputMiBs = masterNode.ThroughputMiBs > 0 ? masterNode.ThroughputMiBs : EtcdThroughputMiBs2,
            Latency = masterNode.Latency > 0 ? masterNode.Latency : EtcdLatency2,
            DiskSplitNotApplicable = true, PageFileNotApplicable = true
        });
        // Вимоги до дисків worker-вузлів (за документом): профіль 30r/70w, від 500 IOPS.
        var workerIops = workerNode.Iops > 0 ? workerNode.Iops : WorkerIops;
        req.Infrastructure.Add(new InfrastructureNode
        {
            Name = "Worker-node", Os = workerNode.Os, Cpu = workerNode.Cpu, Ghz = workerNode.Ghz,
            RamGb = workerNode.RamGb, NodeCount = req.WorkerNodeCount,
            StorageType = workerNode.StorageType, StorageGb = workerNode.StorageGb,
            StorageType2 = workerNode.StorageType2, StorageGb2 = workerNode.StorageGb2,
            StorageType3 = workerNode.StorageType3, StorageGb3 = workerNode.StorageGb3,
            StorageType4 = workerNode.StorageType4, StorageGb4 = workerNode.StorageGb4,
            PageFileGb = workerNode.PageFileGb, PageFileType = workerNode.PageFileType,
            Iops = workerIops,
            IopsProfile = string.IsNullOrWhiteSpace(workerNode.IopsProfile) ? K8sIopsProfile2 : workerNode.IopsProfile,
            // MiB/s не заданий у матриці окремо — похідне значення від наявних IOPS/латентності
            // (середній розмір блока контейнерного I/O ~16КБ), а не вигадане число.
            ThroughputMiBs = workerNode.ThroughputMiBs > 0 ? workerNode.ThroughputMiBs : WorkerThroughput(workerIops),
            Latency = workerNode.Latency > 0 ? workerNode.Latency : WorkerLatency,
            DiskSplitNotApplicable = true, PageFileNotApplicable = true
        });

        // IOPS/latency атрибутуються БД-вузлу. У гібриді (includeDatabase=false) БД на Windows,
        // тож тут IOPS = 0, а підсумок IOPS рахується у CalculateHybrid із Windows-частини.
        req.TotalIops = includeDatabase ? (sqlRange?.Iops ?? 500) : 0;
        req.TotalLatency = sqlRange?.Latency ?? 1;

        // Окремий GPU-вузол НЕ виділяється: вимог до GPU немає в документації, а LMS-Videoutilities
        // обслуговується звичайним worker-вузлом (його под уже враховано в запиті подів вище).

        // Підсумок = сума ФІЗИЧНИХ ресурсів усіх вузлів (SQL + Master + Worker).
        req.TotalCpu = req.Infrastructure.Sum(n => n.Cpu * n.NodeCount);
        req.TotalRamGb = req.Infrastructure.Sum(n => n.RamGb * n.NodeCount);
        req.TotalStorageGb = req.Infrastructure.Sum(n => n.TotalStorageGb);
    }

    private void CalculateWindows(ResourceRequirement req, ProjectConfig config)
    {
        var appRange = FindWindowsRange(config.UserCount, _matrix.AppServerRanges);
        var webRange = FindWindowsRange(config.UserCount, _matrix.WebServerRanges);
        var sqlRange = FindDatabaseRange(config.UserCount, config.DatabaseType);

        var sqlNode = _matrix.DefaultWindowsSql ?? _defaultSql;
        var appNode = _matrix.DefaultWindowsApp;
        var webNode = _matrix.DefaultWindowsWeb;

        // Windows sizing mirrors the Excel "Windows" sheet: it is purely VM-based
        // (SQL + AppServers×count + WebServers×count). The K8s module/pod breakdown is NOT added —
        // on Windows the application runs inside the app-server VMs, so adding pod CPU/RAM would double-count.
        var appCpu = appRange?.Cpu ?? 4;
        var appGhz = appRange?.Ghz ?? 2.4;
        var appRam = appRange?.RamRec ?? 16;
        var appCount = appRange?.InstanceCount ?? 1;
        var webCpu = webRange?.Cpu ?? 4;
        var webGhz = webRange?.Ghz ?? 2.4;
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
            Ghz = GhzFor(sqlRange, sqlNode),
            RamGb = sqlRam, NodeCount = 1,
            StorageType = sqlNode.StorageType, StorageGb = sqlNode.StorageGb,
            StorageType2 = sqlNode.StorageType2, StorageGb2 = sqlNode.StorageGb2,
            StorageType3 = sqlNode.StorageType3, StorageGb3 = sqlNode.StorageGb3,
            StorageType4 = sqlNode.StorageType4, StorageGb4 = sqlNode.StorageGb4,
            // Файл підкачки для вузла БД НЕ задаємо: документ D-AD-ADM-E / еталонний калькулятор
            // визначають pagefile лише для серверів додатків/веб, а виділений SQL Server не потребує
            // pagefile, масштабованого від RAM. Беремо лише явне значення з матриці (типово 0).
            PageFileGb = sqlNode.PageFileGb,
            PageFileType = sqlNode.PageFileType,
            Iops = sqlRange?.Iops ?? 500, Latency = sqlRange?.Latency ?? 1,
            IopsProfile = sqlRange?.IopsProfile ?? DbIopsProfile,
            ThroughputMiBs = ThroughputFor(sqlRange),
            PageFileNotApplicable = true
        };
        ApplyDbDisks(dbNode, config.DatabaseType, sqlRam, config.Environment, config.DbSizeGb, config.ContentDbSizeGb);
        req.Infrastructure.Add(dbNode);
        req.Infrastructure.Add(new InfrastructureNode
        {
            Name = appNode?.Name ?? "App Server", Os = appNode?.Os ?? "Windows Server 2022",
            Cpu = appCpu, Ghz = appGhz, RamGb = appRam, NodeCount = appCount,
            StorageType = appNode?.StorageType ?? "SSD", StorageGb = appNode?.StorageGb ?? 150,
            StorageType2 = appNode?.StorageType2 ?? "", StorageGb2 = appNode?.StorageGb2 ?? 0,
            StorageType3 = appNode?.StorageType3 ?? "", StorageGb3 = appNode?.StorageGb3 ?? 0,
            StorageType4 = appNode?.StorageType4 ?? "", StorageGb4 = appNode?.StorageGb4 ?? 0,
            // Файл підкачки app-сервера: з матриці або, якщо не задано, = CEILING(RAM×4, 10)
            // (формула еталонного аркуша Windows Q7=CEILING(E7*4,10); підтверджено прикладами).
            PageFileGb = appNode?.PageFileGb > 0 ? appNode.PageFileGb : PageFileFor(appRam),
            PageFileType = string.IsNullOrEmpty(appNode?.PageFileType) ? "SSD" : appNode.PageFileType,
            // IOPS сервера додатків — з діапазону (за документом 250→500, профіль 30r/70w).
            Iops = appRange?.Iops ?? appNode?.Iops ?? 0,
            IopsProfile = string.IsNullOrEmpty(appNode?.IopsProfile) ? AppServerIopsProfile2 : appNode.IopsProfile,
            // MiB/s і латентність поки немає в матриці — тимчасовий орієнтир з IIS/Windows
            // capacity planning (Microsoft Learn); підлягає уточненню за результатами LT-тесту.
            ThroughputMiBs = appNode?.ThroughputMiBs > 0 ? appNode.ThroughputMiBs : AppServerThroughputMiBs2,
            Latency = appNode?.Latency > 0 ? appNode.Latency : AppServerLatency2,
            DiskSplitNotApplicable = true
        });
        req.Infrastructure.Add(new InfrastructureNode
        {
            Name = webNode?.Name ?? "Web Server (IIS)", Os = webNode?.Os ?? "Windows Server 2022",
            Cpu = webCpu, Ghz = webGhz, RamGb = webRam, NodeCount = webCount,
            StorageType = webNode?.StorageType ?? "SSD", StorageGb = webNode?.StorageGb ?? 150,
            StorageType2 = webNode?.StorageType2 ?? "", StorageGb2 = webNode?.StorageGb2 ?? 0,
            StorageType3 = webNode?.StorageType3 ?? "", StorageGb3 = webNode?.StorageGb3 ?? 0,
            StorageType4 = webNode?.StorageType4 ?? "", StorageGb4 = webNode?.StorageGb4 ?? 0,
            // Файл підкачки IIS/web-сервера: з матриці або, якщо не задано, = CEILING(RAM×4, 10)
            // (формула еталонного аркуша Windows Q8=CEILING(E8*4,10); підтверджено прикладами).
            PageFileGb = webNode?.PageFileGb > 0 ? webNode.PageFileGb : PageFileFor(webRam),
            PageFileType = string.IsNullOrEmpty(webNode?.PageFileType) ? "SSD" : webNode.PageFileType,
            // IOPS веб-сервера — з діапазону (за документом 200, профіль 70r/30w).
            Iops = webRange?.Iops ?? webNode?.Iops ?? 0,
            IopsProfile = string.IsNullOrEmpty(webNode?.IopsProfile) ? WebServerIopsProfile2 : webNode.IopsProfile,
            // MiB/s і латентність поки немає в матриці — тимчасовий орієнтир з IIS capacity
            // planning; підлягає уточненню за результатами LT-тесту.
            ThroughputMiBs = webNode?.ThroughputMiBs > 0 ? webNode.ThroughputMiBs : WebServerThroughputMiBs2,
            Latency = webNode?.Latency > 0 ? webNode.Latency : WebServerLatency2,
            DiskSplitNotApplicable = true
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

        var k8sConfig = new ProjectConfig { ProjectName = config.ProjectName, UserCount = config.UserCount, DeploymentType = DeploymentType.Kubernetes, DatabaseType = config.DatabaseType, Environment = config.Environment, DbSizeGb = config.DbSizeGb, ContentDbSizeGb = config.ContentDbSizeGb };
        var winConfig = new ProjectConfig { ProjectName = config.ProjectName, UserCount = config.UserCount, DeploymentType = DeploymentType.Windows, DatabaseType = config.DatabaseType, Environment = config.Environment, DbSizeGb = config.DbSizeGb, ContentDbSizeGb = config.ContentDbSizeGb };

        // SmartID у гібриді: завжди под у Kubernetes (окрема ВМ на веб-серверах IIS не додається).
        CalculateK8s(k8sReq, k8sConfig, includeDatabase: false, excludeModules: HybridWindowsModules,
            includeSmartId: true);
        CalculateWindows(winReq, winConfig);

        // БД — на Windows-частині; K8s IOPS = 0, тож підсумок IOPS бере Windows.
        req.TotalIops = winReq.TotalIops + k8sReq.TotalIops;
        req.TotalLatency = winReq.TotalLatency;
        req.PodCpu = k8sReq.PodCpu;
        req.PodRamGb = k8sReq.PodRamGb;
        req.WorkerNodeCount = k8sReq.WorkerNodeCount + winReq.WorkerNodeCount;
        req.MasterNodeCount = k8sReq.MasterNodeCount + winReq.MasterNodeCount;

        req.Infrastructure.AddRange(k8sReq.Infrastructure); // Master + Worker, без БД
        req.Infrastructure.AddRange(winReq.Infrastructure); // App + Web (IIS) + БД
        req.Components.AddRange(k8sReq.Components);

        req.TotalCpu = req.Infrastructure.Sum(n => n.Cpu * n.NodeCount);
        req.TotalRamGb = req.Infrastructure.Sum(n => n.RamGb * n.NodeCount);
        req.TotalStorageGb = req.Infrastructure.Sum(n => n.TotalStorageGb);
    }

    private UserLoadRange? FindDatabaseRange(int userCount, DatabaseType dbType)
    {
        var ranges = dbType switch
        {
            DatabaseType.PostgreSQL => _matrix.PostgresRanges,
            DatabaseType.Oracle => _matrix.OracleRanges,
            _ => _matrix.MsSqlRanges
        };
        return ranges.FirstOrDefault(r => userCount >= r.MinUsers && userCount <= r.MaxUsers)
               ?? ranges.OrderByDescending(r => r.MaxUsers).FirstOrDefault();
    }

    private UserLoadRange? FindWindowsRange(int userCount, List<UserLoadRange> ranges)
    {
        return ranges.FirstOrDefault(r => userCount >= r.MinUsers && userCount <= r.MaxUsers)
               ?? ranges.OrderByDescending(r => r.MaxUsers).FirstOrDefault();
    }

    private static int CalcReplicas(ModuleComponent comp, int userCount, int auxUsers = -1)
        => ReplicaMath.Resolve(comp.Formula, comp.FixedReplicas, userCount, auxUsers);

    // Пропускна здатність диска БД (MiB/s) — з матриці (значення документа D-AD-ADM-E).
    // Не оцінюємо з IOPS: документ задає MiB/s окремою таблицею, тож вигаданий розрахунок
    // давав би хибні числа. Якщо у діапазоні не задано (напр. PostgreSQL/Oracle) — 0 (не показуємо).
    private static int ThroughputFor(UserLoadRange? range) => range?.ThroughputMiBs ?? 0;

    // Частота CPU (ГГц) — з діапазону користувачів, якщо задана (MsSql/Postgres/Oracle-діапазони
    // її не мають — там 0), інакше фіксоване значення вузла з матриці (типово 2.4 ГГц).
    private static double GhzFor(UserLoadRange? range, InfrastructureNode fallback)
        => range?.Ghz > 0 ? range.Ghz : fallback.Ghz;

    // --- Редаговані налаштування рушія (з матриці, fallback на вбудовані константи) ---
    private string DbIopsProfile => _matrix.Engine?.DbIopsProfile ?? DefaultIopsProfile;
    private string AppServerIopsProfile2 => _matrix.Engine?.AppServerIopsProfile ?? AppServerIopsProfile;
    private string WebServerIopsProfile2 => _matrix.Engine?.WebServerIopsProfile ?? WebServerIopsProfile;
    private string K8sIopsProfile2 => _matrix.Engine?.K8sIopsProfile ?? K8sIopsProfile;
    private int WorkerIops => _matrix.Engine?.DefaultWorkerIops > 0 ? _matrix.Engine.DefaultWorkerIops : DefaultWorkerIops;
    private double WorkerLatency => _matrix.Engine?.DefaultWorkerLatency > 0 ? _matrix.Engine.DefaultWorkerLatency : DefaultWorkerLatency;
    private int EtcdIops2 => _matrix.Engine?.EtcdIops > 0 ? _matrix.Engine.EtcdIops : EtcdIops;
    private string EtcdIopsProfile2 => _matrix.Engine?.EtcdIopsProfile ?? EtcdIopsProfile;
    private int EtcdThroughputMiBs2 => _matrix.Engine?.EtcdThroughputMiBs > 0 ? _matrix.Engine.EtcdThroughputMiBs : EtcdThroughputMiBs;
    private double EtcdLatency2 => _matrix.Engine?.EtcdLatency > 0 ? _matrix.Engine.EtcdLatency : EtcdLatency;
    private int AppServerThroughputMiBs2 => _matrix.Engine?.AppServerThroughputMiBs > 0 ? _matrix.Engine.AppServerThroughputMiBs : AppServerThroughputMiBs;
    private double AppServerLatency2 => _matrix.Engine?.AppServerLatency > 0 ? _matrix.Engine.AppServerLatency : AppServerLatency;
    private int WebServerThroughputMiBs2 => _matrix.Engine?.WebServerThroughputMiBs > 0 ? _matrix.Engine.WebServerThroughputMiBs : WebServerThroughputMiBs;
    private double WebServerLatency2 => _matrix.Engine?.WebServerLatency > 0 ? _matrix.Engine.WebServerLatency : WebServerLatency;
    private double MsSqlMaxRamGb => _matrix.Engine?.MsSqlStandardMaxRamGb > 0 ? _matrix.Engine.MsSqlStandardMaxRamGb : MsSqlStandardMaxRamGb;
    private double MsSqlMaxCores => _matrix.Engine?.MsSqlStandardMaxCores > 0 ? _matrix.Engine.MsSqlStandardMaxCores : MsSqlStandardMaxCores;

    private int PageFileFor(double ramGb)
    {
        var mult = _matrix.Engine?.PageFileMultiplier > 0 ? _matrix.Engine.PageFileMultiplier : 4;
        var round = _matrix.Engine?.PageFileRounding > 0 ? _matrix.Engine.PageFileRounding : 10;
        return (int)(Math.Ceiling(ramGb * mult / round) * round);
    }

    private static int ThroughputFromIops(int iops, int avgBlockSizeKb)
        => (int)Math.Round(iops * avgBlockSizeKb / 1024.0);

    // Оцінка MiB/s worker-вузла з IOPS (середній розмір блока з налаштувань рушія).
    private int WorkerThroughput(int iops)
    {
        var block = _matrix.Engine?.AvgBlockSizeKb > 0 ? _matrix.Engine.AvgBlockSizeKb : AvgBlockSizeKb;
        return ThroughputFromIops(iops, block);
    }

    private string GetDatabaseNodeName(DatabaseType dbType) => dbType switch
    {
        DatabaseType.PostgreSQL => "PostgreSQL",
        DatabaseType.Oracle => "Oracle 19c",
        _ => "SQL Server"
    };

    // Диски вузла БД масштабуються за ОБСЯГОМ ДАНИХ (а не фіксовано): немає сенсу тримати
    // терабайтні диски під базу в 10-20 ГБ. Data та Logs/TempDB рахуються від обсягу даних із
    // розумним мінімумом. У non-prod середовищах (DEV/TEST/PreProd) окреме сховище нереляційного
    // Content не виділяється (потрібне лише у PROD) і OS-диск зменшується.
    private const int NonProdOsDiskGb = 100;
    private void ApplyDbDisks(InfrastructureNode db, DatabaseType dbType, double dbRamGb,
        DeployEnvironment environment, int dbSizeGb = 0, int contentSizeGb = 0)
    {
        // Диски беремо ФІКСОВАНІ з матриці/еталона (OS / Logs+TempDB / MainData / Content) —
        // обсяг даних наперед невідомий, тож не масштабуємо вручну. Гарантуємо типи за замовчуванням.
        if (db.StorageGb2 > 0 && string.IsNullOrWhiteSpace(db.StorageType2)) db.StorageType2 = "SSD";
        if (db.StorageGb3 > 0 && string.IsNullOrWhiteSpace(db.StorageType3)) db.StorageType3 = "SSD";

        // Якщо користувач вручну задав обсяг даних БД — диск MainData встановлюємо РІВНО йому
        // (не лише як мінімум: фіксоване значення з матриці — це просто заглушка «поки невідомо»,
        // тож явно введене число має його замінювати, а не лише піднімати), а диск Logs+TempDB
        // масштабуємо пропорційно (25% від обсягу даних).
        if (dbSizeGb > 0)
        {
            db.StorageGb3 = dbSizeGb;
            db.StorageGb2 = (int)Math.Ceiling(dbSizeGb * 0.25);
        }

        // non-prod: типово прибираємо диск Content (холодні/бекап дані зазвичай не потрібні) і
        // зменшуємо OS-диск. Явно заданий обсяг Content (contentSizeGb, нижче) все одно може його
        // повернути — для будь-якого середовища, не лише PROD.
        if (environment != DeployEnvironment.Prod)
        {
            db.StorageGb4 = 0;
            db.StorageType4 = "";
            if (db.StorageGb > NonProdOsDiskGb) db.StorageGb = NonProdOsDiskGb;
        }

        if (contentSizeGb > 0)
        {
            // Явно заданий обсяг Content замінює фіксоване значення з матриці (або нуль non-prod
            // вище) — той самий підхід, що й для MainData вище.
            db.StorageGb4 = contentSizeGb;
            if (string.IsNullOrWhiteSpace(db.StorageType4)) db.StorageType4 = "SATA";
        }

        db.DbVersion = DbVersionLabel(dbType, dbRamGb, db.Cpu, environment,
            MsSqlMaxRamGb, MsSqlMaxCores);
        // Для робочих середовищ Enterprise потрібна, якщо перевищено ліміти Standard (RAM або ядра).
        if (dbType == DatabaseType.MsSql && environment == DeployEnvironment.Prod
            && (dbRamGb > MsSqlMaxRamGb || db.Cpu > MsSqlMaxCores))
        {
            var reason = dbRamGb > MsSqlMaxRamGb && db.Cpu > MsSqlMaxCores
                ? $"RAM {dbRamGb:0} ГБ > {MsSqlMaxRamGb:0} ГБ і {db.Cpu:0} ядер > {MsSqlMaxCores} ядер"
                : dbRamGb > MsSqlMaxRamGb
                    ? $"RAM {dbRamGb:0} ГБ > {MsSqlMaxRamGb:0} ГБ"
                    : $"{db.Cpu:0} ядер > {MsSqlMaxCores} ядер";
            var note = $"{reason} — потрібна редакція Enterprise (ліміти Standard)";
            db.Notes = string.IsNullOrWhiteSpace(db.Notes) ? note : $"{db.Notes}; {note}";
        }
    }

    // SQL Server: рекомендована версія — 2025, мінімально допустима — 2022 (за вимогами D-AD-ADM-E).
    // Редакція Standard обмежена 128 ГБ ОЗП та 24 ядрами на екземпляр БД → понад це потрібна Enterprise.
    // Non-prod (DEV/TEST/PreProd) використовує безкоштовну Developer Edition (не для робочого навантаження).
    private const double MsSqlStandardMaxRamGb = 128;
    private const double MsSqlStandardMaxCores = 24;
    // Базовий підпис версії MS SQL: рекомендовано 2025, допустимо від 2022.
    private const string MsSqlVersion = "MS SQL Server 2025 (мін. 2022)";

    // Зворотно-сумісне перевантаження (PROD, без урахування ядер, дефолтні ліміти).
    public static string DbVersionLabel(DatabaseType dbType, double dbRamGb)
        => DbVersionLabel(dbType, dbRamGb, 0, DeployEnvironment.Prod);

    public static string DbVersionLabel(DatabaseType dbType, double dbRamGb, double dbCpu, DeployEnvironment environment)
        => DbVersionLabel(dbType, dbRamGb, dbCpu, environment, MsSqlStandardMaxRamGb, MsSqlStandardMaxCores);

    public static string DbVersionLabel(DatabaseType dbType, double dbRamGb, double dbCpu, DeployEnvironment environment,
        double maxStandardRamGb, double maxStandardCores)
        => dbType switch
        {
            DatabaseType.PostgreSQL => "PostgreSQL 17+",
            DatabaseType.Oracle => "Oracle Database 19c Enterprise Edition",
            // MS SQL: Developer для non-prod; для PROD — Standard/Enterprise за лімітами ядер і RAM.
            _ => environment != DeployEnvironment.Prod
                ? $"{MsSqlVersion} Developer Edition"
                : $"{MsSqlVersion} {(dbRamGb > maxStandardRamGb || dbCpu > maxStandardCores ? "Enterprise" : "Standard")}"
        };

    // Fallback worker capacity when matrix node specs are missing
    private const double DefaultWorkerCpu = 8;
    private const double DefaultWorkerRamGb = 32;

    // Центральний SmartID (SSO) — ресурс на 1 репліку (на кожні 25 користувачів), один на систему.
    private const double SmartIdCpuPerReplica = 0.2;
    private const double SmartIdRamPerReplicaGb = 0.5;

    // Профілі читання/запису дисків за документом D-AD-ADM-E:
    //  • сервер БД — 50r/50w; сервери додатків — 30r/70w; веб-сервери — 70r/30w;
    //  • вузли Kubernetes (master/worker) — 30r/70w.
    private const string DefaultIopsProfile = "50r/50w";        // сервер БД
    private const string AppServerIopsProfile = "30r/70w";
    private const string WebServerIopsProfile = "70r/30w";
    private const string K8sIopsProfile = "30r/70w";
    private const int DefaultWorkerIops = 500;
    private const double DefaultWorkerLatency = 5;

    // Master node/etcd — за офіційним etcd sizing guide: SSD, fsync latency < 10мс,
    // переважно послідовний запис (WAL). Значення НЕ з документа D-AD-ADM-E (там master
    // не описаний детально) — окреме джерело, застосовне лише коли etcd на цьому вузлі.
    private const int EtcdIops = 1000;
    private const string EtcdIopsProfile = "10r/90w";
    private const int EtcdThroughputMiBs = 50;
    private const double EtcdLatency = 10;

    // Середній розмір I/O-блока контейнерного навантаження worker-вузла (оцінка, не вимірювання) —
    // використовується лише щоб отримати MiB/s з наявних IOPS, коли матриця його не задає.
    private const int AvgBlockSizeKb = 16;

    // App/Web servers: MiB/s і латентність поки не задані в матриці (D-AD-ADM-E їх не описує для
    // цих ролей) — тимчасовий орієнтир з Microsoft IIS/Windows capacity planning, до уточнення LT-тестом.
    private const int AppServerThroughputMiBs = 100;
    private const double AppServerLatency = 10;
    private const int WebServerThroughputMiBs = 80;
    private const double WebServerLatency = 10;

    private static readonly InfrastructureNode _defaultSql = new() { Name = "SQL Server", Os = "Windows Server 2022", Cpu = 4, Ghz = 2.4, RamGb = 12, NodeCount = 1, StorageGb = 300, StorageType = "SSD" };
    private static readonly InfrastructureNode _defaultMaster = new() { Name = "Master node", Os = "Ubuntu 24.04", Cpu = 2, Ghz = 2.4, RamGb = 4, NodeCount = 1, StorageGb = 100, StorageType = "SSD" };
    private static readonly InfrastructureNode _defaultWorker = new() { Name = "Worker-node", Os = "Ubuntu 24.04", Cpu = 8, Ghz = 2.4, RamGb = 32, NodeCount = 1, StorageGb = 200, StorageType = "SSD" };
    private static readonly InfrastructureNode _defaultReporting = new() { Name = "Сервер звітів", Os = "Windows Server 2022", Cpu = 2, Ghz = 2.4, RamGb = 4, NodeCount = 1, StorageGb = 150, StorageType = "SSD", Iops = 250, IopsProfile = "50r/50w", Latency = 10 };
    // HAProxy: L7-балансувальник, диск не в критичному шляху запиту (логи йдуть через
    // syslog асинхронно) — жоден вендорський стандарт не задає тут IOPS/latency/pagefile/розподіл дисків.
    private static readonly InfrastructureNode _defaultHaProxy = new()
    {
        Name = "HAProxy", Os = "Ubuntu 24.04", Cpu = 2, Ghz = 2.4, RamGb = 4, NodeCount = 1, StorageGb = 100, StorageType = "SSD",
        DiskSplitNotApplicable = true, PageFileNotApplicable = true, IopsNotApplicable = true
    };
}

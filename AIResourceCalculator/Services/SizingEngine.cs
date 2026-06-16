using AIResourceCalculator.Data;
using AIResourceCalculator.Models;

namespace AIResourceCalculator.Services;

public class SizingEngine
{
    private readonly SizingMatrix _matrix;
    private List<ProjectModule> _modules;
    private ProductType _currentProduct = ProductType.Standard;

    public IReadOnlyList<ProjectModule> Modules => _modules.AsReadOnly();
    public ProductType CurrentProduct => _currentProduct;

    public SizingEngine(SizingMatrix matrix)
    {
        _matrix = matrix;
        _modules = matrix.Modules.Count > 0
            ? matrix.Modules.Select(m => CloneModule(m)).ToList()
            : DefaultStandardModules();
    }

    public void SetModules(List<ProjectModule> modules)
    {
        _modules = modules;
    }

    public void SetProductType(ProductType productType)
    {
        _currentProduct = productType;
        var source = productType == ProductType.DocumentFlow
            ? (_matrix.DocumentFlowModules.Count > 0 ? _matrix.DocumentFlowModules : DefaultDocumentFlowModules())
            : (_matrix.StandardModules.Count > 0 ? _matrix.StandardModules : DefaultStandardModules());
        _modules = source.Select(m => CloneModule(m)).ToList();
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

    private void CalculateK8s(ResourceRequirement req, ProjectConfig config)
    {
        var sqlRange = FindMsSqlRange(config.UserCount, config.LoadProfile);
        var masterNode = _matrix.DefaultK8sMaster ?? _defaultMaster;
        var workerNode = _matrix.DefaultK8sWorker ?? _defaultWorker;

        double totalCpu = 0, totalRam = 0;

        var enabledModules = _modules.Where(m => m.IsEnabled && !m.Name.Contains("Windows")).ToList();

        foreach (var module in enabledModules)
        {
            var (modCpu, modRam) = module.CalculateReplicas(config.UserCount, config.LoadProfile);
            totalCpu += modCpu;
            totalRam += modRam;

            var isPerf = config.LoadProfile == LoadProfile.Performance;
            foreach (var comp in module.Components)
            {
                int rep = CalcReplicas(comp, config.UserCount);
                if (rep == 0) rep = 1;

                var cpu = isPerf && comp.PerfCpu > 0 ? comp.PerfCpu : comp.Cpu;
                var ram = isPerf && comp.PerfRamGb > 0 ? comp.PerfRamGb : comp.RamGb;
                req.Components.Add(new ServiceComponent
                {
                    Name = comp.Name,
                    Cpu = cpu * rep,
                    RamGb = ram * rep,
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

        var workerCpuCapacity = workerNode.Cpu > 0 ? workerNode.Cpu : 8;
        var workerRamCapacity = workerNode.RamGb > 0 ? workerNode.RamGb : 32;
        var workerCount = Math.Max(1,
            (int)Math.Ceiling(Math.Max(totalCpu / workerCpuCapacity, totalRam / workerRamCapacity)));

        req.TotalCpu = totalCpu;
        req.TotalRamGb = totalRam;
        req.WorkerNodeCount = workerCount;
        req.MasterNodeCount = masterNode.NodeCount > 0 ? masterNode.NodeCount : 1;

        var sqlNode = _matrix.DefaultK8sSql ?? _defaultSql;
        req.Infrastructure.Add(new InfrastructureNode
        {
            Name = "SQL Server", Os = sqlNode.Os, Cpu = sqlRange?.Cpu ?? sqlNode.Cpu,
            RamGb = sqlRange?.RamRec ?? sqlNode.RamGb, NodeCount = 1,
            StorageType = sqlNode.StorageType, StorageGb = sqlNode.StorageGb,
            StorageType2 = sqlNode.StorageType2, StorageGb2 = sqlNode.StorageGb2,
            StorageType3 = sqlNode.StorageType3, StorageGb3 = sqlNode.StorageGb3,
            StorageType4 = sqlNode.StorageType4, StorageGb4 = sqlNode.StorageGb4,
            PageFileGb = sqlNode.PageFileGb, PageFileType = sqlNode.PageFileType,
            Iops = sqlRange?.Iops ?? 500, Latency = sqlRange?.Latency ?? 1
        });
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
            PageFileGb = workerNode.PageFileGb, PageFileType = workerNode.PageFileType
        });

        req.TotalStorageGb = req.Infrastructure.Sum(n => n.StorageGb * n.NodeCount);
        req.TotalIops = sqlRange?.Iops ?? 500;
        req.TotalLatency = sqlRange?.Latency ?? 1;

        // GPU node for video transcoding (LMS-Videoutilities)
        var hasGpuComponent = req.Components.Any(c =>
            c.Notes.Contains("GPU", StringComparison.OrdinalIgnoreCase));
        if (hasGpuComponent)
        {
            var gpuCount = Math.Max(1, (int)Math.Ceiling(config.UserCount / 100.0));
            req.Infrastructure.Add(new InfrastructureNode
            {
                Name = "GPU Node (T4/A10)", Os = "Ubuntu 24.04", Cpu = 8, RamGb = 32,
                NodeCount = gpuCount, StorageGb = 200, StorageType = "SSD"
            });
            req.TotalCpu += 8 * gpuCount;
            req.TotalRamGb += 32 * gpuCount;
            req.TotalStorageGb += 200 * gpuCount;
        }
    }

    private void CalculateWindows(ResourceRequirement req, ProjectConfig config)
    {
        var appRange = FindWindowsRange(config.UserCount, _matrix.AppServerRanges,
            _matrix.AppServerPerformanceRanges, config.LoadProfile);
        var webRange = FindWindowsRange(config.UserCount, _matrix.WebServerRanges,
            _matrix.WebServerPerformanceRanges ?? new(), config.LoadProfile);
        var sqlRange = FindMsSqlRange(config.UserCount, config.LoadProfile);

        var sqlNode = _matrix.DefaultWindowsSql ?? _defaultSql;
        var appNode = _matrix.DefaultWindowsApp;
        var webNode = _matrix.DefaultWindowsWeb;

        var enabledModules = _modules.Where(m => m.IsEnabled).ToList();
        double totalCpu = 0, totalRam = 0;

        foreach (var module in enabledModules)
        {
            var (modCpu, modRam) = module.CalculateReplicas(config.UserCount, config.LoadProfile);
            totalCpu += modCpu;
            totalRam += modRam;
        }

        var appCpu = appRange?.Cpu ?? 4;
        var appRam = appRange?.RamRec ?? 16;
        var appCount = appRange?.InstanceCount ?? 1;
        var webCpu = webRange?.Cpu ?? 4;
        var webRam = webRange?.RamRec ?? 8;
        var webCount = webRange?.InstanceCount ?? 1;

        req.TotalCpu = totalCpu + (sqlRange?.Cpu ?? 4) + appCpu * appCount + webCpu * webCount;
        req.TotalRamGb = totalRam + (sqlRange?.RamRec ?? 16) + appRam * appCount + webRam * webCount;
        req.TotalIops = (appRange?.Iops ?? 200) + (webRange?.Iops ?? 200) + (sqlRange?.Iops ?? 500);

        req.WorkerNodeCount = appCount + webCount;
        req.MasterNodeCount = 1;

        req.Infrastructure.Add(new InfrastructureNode
        {
            Name = "SQL Server", Os = sqlNode.Os, Cpu = sqlRange?.Cpu ?? sqlNode.Cpu,
            RamGb = sqlRange?.RamRec ?? sqlNode.RamGb, NodeCount = 1,
            StorageType = sqlNode.StorageType, StorageGb = sqlNode.StorageGb,
            StorageType2 = sqlNode.StorageType2, StorageGb2 = sqlNode.StorageGb2,
            StorageType3 = sqlNode.StorageType3, StorageGb3 = sqlNode.StorageGb3,
            StorageType4 = sqlNode.StorageType4, StorageGb4 = sqlNode.StorageGb4,
            PageFileGb = sqlNode.PageFileGb > 0 ? sqlNode.PageFileGb : (int)Math.Ceiling((sqlRange?.RamRec ?? sqlNode.RamGb) * 1.0),
            PageFileType = sqlNode.PageFileType ?? "Auto",
            Iops = sqlRange?.Iops ?? 500, Latency = sqlRange?.Latency ?? 1
        });
        // Disk separation for SQL Server >64 GB RAM
        var sqlRam = sqlRange?.RamRec ?? sqlNode.RamGb;
        var sqlNodeRef = req.Infrastructure.Last(n => n.Name == "SQL Server");
        if (sqlRam > 64)
        {
            sqlNodeRef.StorageType2 = "Premium SSD";
            sqlNodeRef.StorageGb2 = Math.Max(100, (int)(sqlRam * 1.5));
            sqlNodeRef.StorageType3 = "Standard SSD";
            sqlNodeRef.StorageGb3 = Math.Max(100, (int)(sqlNodeRef.StorageGb * 0.15));
            sqlNodeRef.Notes = "Separate disks: Data, Logs, TempDB recommended";
        }
        req.Infrastructure.Add(new InfrastructureNode
        {
            Name = appNode?.Name ?? "App Server", Os = appNode?.Os ?? "Windows Server 2022",
            Cpu = appCpu, RamGb = appRam, NodeCount = appCount,
            StorageType = appNode?.StorageType ?? "SSD", StorageGb = appNode?.StorageGb ?? 150,
            StorageType2 = appNode?.StorageType2 ?? "", StorageGb2 = appNode?.StorageGb2 ?? 0,
            StorageType3 = appNode?.StorageType3 ?? "", StorageGb3 = appNode?.StorageGb3 ?? 0,
            StorageType4 = appNode?.StorageType4 ?? "", StorageGb4 = appNode?.StorageGb4 ?? 0,
            PageFileGb = appNode?.PageFileGb ?? 0, PageFileType = appNode?.PageFileType ?? "",
            Iops = appNode?.Iops ?? 0, IopsProfile = appNode?.IopsProfile ?? "",
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
            PageFileGb = webNode?.PageFileGb ?? 0, PageFileType = webNode?.PageFileType ?? "",
            Iops = webNode?.Iops ?? 0, IopsProfile = webNode?.IopsProfile ?? "",
            Latency = webNode?.Latency ?? 0
        });

        req.TotalStorageGb = req.Infrastructure.Sum(n => n.StorageGb * n.NodeCount);
        req.TotalLatency = sqlRange?.Latency ?? 1;
    }

    private void CalculateHybrid(ResourceRequirement req, ProjectConfig config)
    {
        var k8sReq = new ResourceRequirement { UserCount = config.UserCount, DeploymentType = DeploymentType.Kubernetes, LoadProfile = config.LoadProfile };
        var winReq = new ResourceRequirement { UserCount = config.UserCount, DeploymentType = DeploymentType.Windows, LoadProfile = config.LoadProfile };

        var k8sConfig = new ProjectConfig { ProjectName = config.ProjectName, UserCount = config.UserCount, DeploymentType = DeploymentType.Kubernetes, LoadProfile = config.LoadProfile, ProductType = config.ProductType };
        var winConfig = new ProjectConfig { ProjectName = config.ProjectName, UserCount = config.UserCount, DeploymentType = DeploymentType.Windows, LoadProfile = config.LoadProfile, ProductType = config.ProductType };

        CalculateK8s(k8sReq, k8sConfig);
        CalculateWindows(winReq, winConfig);

        req.TotalCpu = k8sReq.TotalCpu + winReq.TotalCpu;
        req.TotalRamGb = k8sReq.TotalRamGb + winReq.TotalRamGb;
        req.TotalStorageGb = k8sReq.TotalStorageGb + winReq.TotalStorageGb;
        req.TotalIops = k8sReq.TotalIops + winReq.TotalIops;
        req.TotalLatency = Math.Min(k8sReq.TotalLatency, winReq.TotalLatency);
        req.WorkerNodeCount = k8sReq.WorkerNodeCount + winReq.WorkerNodeCount;
        req.MasterNodeCount = k8sReq.MasterNodeCount + winReq.MasterNodeCount;

        req.Infrastructure.AddRange(k8sReq.Infrastructure);
        req.Infrastructure.AddRange(winReq.Infrastructure);
        req.Components.AddRange(k8sReq.Components);
        req.Components.AddRange(winReq.Components);

        // Deduplicate SQL Server (Hybrid runs both K8s + Windows, both add SQL)
        var sqlNodes = req.Infrastructure.Where(n => n.Name == "SQL Server").ToList();
        if (sqlNodes.Count > 1)
        {
            var best = sqlNodes.OrderByDescending(n => n.RamGb).First();
            foreach (var other in sqlNodes.Where(n => n != best))
            {
                best.StorageGb += other.StorageGb * other.NodeCount;
                best.StorageGb2 += other.StorageGb2 * other.NodeCount;
                best.StorageGb3 += other.StorageGb3 * other.NodeCount;
                best.StorageGb4 += other.StorageGb4 * other.NodeCount;
                best.Iops = Math.Max(best.Iops, other.Iops);
                best.Latency = Math.Min(best.Latency, other.Latency);
                best.PageFileGb = Math.Max(best.PageFileGb, other.PageFileGb);
                req.Infrastructure.Remove(other);
            }
        }
    }

    private UserLoadRange? FindMsSqlRange(int userCount, LoadProfile profile)
    {
        var ranges = profile == LoadProfile.Performance
            ? _matrix.MsSqlPerformanceRanges
            : _matrix.MsSqlRanges;
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
    {
        return comp.Formula switch
        {
            ReplicaFormula.Fixed => comp.FixedReplicas,
            ReplicaFormula.Per25Users => (int)Math.Ceiling(userCount / 25.0),
            ReplicaFormula.Per100Users => (int)Math.Ceiling(userCount / 100.0),
            ReplicaFormula.Per50Users => (int)Math.Ceiling(userCount / 50.0),
            ReplicaFormula.Per100Plus1000 => 1 + (int)(userCount / 100.0) + (int)(userCount / 1000.0),
            ReplicaFormula.Per50Plus500 => 1 + (int)(userCount / 50.0) + (int)(userCount / 500.0),
            ReplicaFormula.OnePlusPer100 => 1 + (int)(userCount / 100.0),
            _ => Math.Max(1, comp.FixedReplicas)
        };
    }

    private static ProjectModule CloneModule(ProjectModule src)
    {
        return new ProjectModule
        {
            Name = src.Name,
            Description = src.Description,
            IsEnabled = src.IsEnabled,
            Components = src.Components.Select(c => new ModuleComponent
            {
                Name = c.Name, Cpu = c.Cpu, RamGb = c.RamGb,
                PerfCpu = c.PerfCpu, PerfRamGb = c.PerfRamGb,
                Formula = c.Formula, FixedReplicas = c.FixedReplicas,
                HasLocalSql = c.HasLocalSql, HasRedis = c.HasRedis,
                Notes = c.Notes
            }).ToList()
        };
    }

    public static List<ProjectModule> DefaultStandardModules()
    {
        return new List<ProjectModule>
        {
            new()
            {
                Name = "App Server", Description = "Core application server with local SQL and Redis cache",
                IsEnabled = true,
                Components = new List<ModuleComponent>
                {
                    new() { Name = "AS (App Server)", Cpu = 1.0, RamGb = 8, PerfCpu = 1.3, PerfRamGb = 10, Formula = ReplicaFormula.Per25Users },
                    new() { Name = "AS-Local SQL", Cpu = 1.0, RamGb = 3, PerfCpu = 1.0, PerfRamGb = 5, Formula = ReplicaFormula.Fixed, FixedReplicas = 1, HasLocalSql = true },
                    new() { Name = "AS-Redis", Cpu = 0.1, RamGb = 0.1, PerfCpu = 0.2, PerfRamGb = 0.2, Formula = ReplicaFormula.Fixed, FixedReplicas = 1, HasRedis = true }
                }
            },
            new()
            {
                Name = "ROBOT", Description = "Robot process automation services",
                IsEnabled = true,
                Components = new List<ModuleComponent>
                {
                    new() { Name = "ROBOT", Cpu = 1.0, RamGb = 8, PerfCpu = 1.3, PerfRamGb = 10, Formula = ReplicaFormula.Per100Plus1000 },
                    new() { Name = "ROBOT-Local SQL", Cpu = 1.0, RamGb = 3, PerfCpu = 1.0, PerfRamGb = 5, Formula = ReplicaFormula.Fixed, FixedReplicas = 1, HasLocalSql = true },
                    new() { Name = "ROBOT-Redis", Cpu = 0.1, RamGb = 0.1, PerfCpu = 0.2, PerfRamGb = 0.2, Formula = ReplicaFormula.Fixed, FixedReplicas = 1, HasRedis = true }
                }
            },
            new()
            {
                Name = "Web", Description = "Web services including WebSocket and SmartID",
                IsEnabled = true,
                Components = new List<ModuleComponent>
                {
                    new() { Name = "Webrmd", Cpu = 0.2, RamGb = 1.5, Formula = ReplicaFormula.Per25Users },
                    new() { Name = "SmartID", Cpu = 0.2, RamGb = 0.5, Formula = ReplicaFormula.Per25Users },
                    new() { Name = "WS (WebSocket)", Cpu = 0.25, RamGb = 0.5, PerfCpu = 0.35, PerfRamGb = 0.6, Formula = ReplicaFormula.Per50Plus500 },
                    new() { Name = "WS-SignalR", Cpu = 0.25, RamGb = 0.5, Formula = ReplicaFormula.Per25Users }
                }
            },
            new()
            {
                Name = "ForceBPM", Description = "Business process management engine and tools",
                IsEnabled = true,
                Components = new List<ModuleComponent>
                {
                    new() { Name = "GraphQL", Cpu = 0.3, RamGb = 1, Formula = ReplicaFormula.Per25Users },
                    new() { Name = "ForceBPM Engine", Cpu = 1.0, RamGb = 4, Formula = ReplicaFormula.OnePlusPer100, HasLocalSql = true },
                    new() { Name = "ForceBPM Modeler", Cpu = 0.5, RamGb = 0.5, Formula = ReplicaFormula.Fixed, FixedReplicas = 1 },
                    new() { Name = "ForceBPM Processes", Cpu = 0.5, RamGb = 2, Formula = ReplicaFormula.Per25Users },
                    new() { Name = "ForceBPM Tasks", Cpu = 0.3, RamGb = 2, Formula = ReplicaFormula.Per25Users },
                    new() { Name = "ForceBPM Tasks-Graphql", Cpu = 0.3, RamGb = 1, Formula = ReplicaFormula.Per25Users }
                }
            },
            new()
            {
                Name = "LMS", Description = "Learning management system with video utilities",
                IsEnabled = false,
                Components = new List<ModuleComponent>
                {
                    new() { Name = "LMS-SmartID", Cpu = 0.006, RamGb = 0.05, Formula = ReplicaFormula.Per25Users },
                    new() { Name = "LMS", Cpu = 0.3, RamGb = 1, Formula = ReplicaFormula.Fixed, FixedReplicas = 1, HasLocalSql = true },
                    new() { Name = "LMS-GraphQL", Cpu = 0.09, RamGb = 0.3, Formula = ReplicaFormula.Per25Users },
                    new() { Name = "LMS-Videoutilities", Cpu = 4.0, RamGb = 6, Formula = ReplicaFormula.Fixed, FixedReplicas = 1, HasLocalSql = true, Notes = "Requires GPU for video transcoding" },
                    new() { Name = "LMS-Fileserver", Cpu = 0.5, RamGb = 8, Formula = ReplicaFormula.Fixed, FixedReplicas = 1 }
                }
            },
            new()
            {
                Name = "HR Portal", Description = "HR self-service portal with modeler and player",
                IsEnabled = false,
                Components = new List<ModuleComponent>
                {
                    new() { Name = "HR-SmartID", Cpu = 0.006, RamGb = 0.05, Formula = ReplicaFormula.Per100Users },
                    new() { Name = "HR-GraphQL", Cpu = 0.01, RamGb = 0.06, Formula = ReplicaFormula.Per100Users },
                    new() { Name = "WebAppModeler", Cpu = 0.5, RamGb = 2, Formula = ReplicaFormula.Fixed, FixedReplicas = 1, HasLocalSql = true },
                    new() { Name = "CommonAppPlayer", Cpu = 0.5, RamGb = 2, Formula = ReplicaFormula.Fixed, FixedReplicas = 1, HasLocalSql = true }
                }
            },
            new()
            {
                Name = "Windows Infrastructure", Description = "Windows App Servers and Web Servers",
                IsEnabled = true,
                Components = new List<ModuleComponent>
                {
                    new() { Name = "Windows App Server", Cpu = 4.0, RamGb = 16, Formula = ReplicaFormula.Fixed, FixedReplicas = 1, Notes = "Per Windows deployment VM" },
                    new() { Name = "Windows Web Server", Cpu = 4.0, RamGb = 8, Formula = ReplicaFormula.Fixed, FixedReplicas = 1, Notes = "Per Windows deployment VM" }
                }
            }
        };
    }

    public static List<ProjectModule> DefaultDocumentFlowModules()
    {
        return new List<ProjectModule>
        {
            new()
            {
                Name = "App Server", Description = "Core application server with local SQL and Redis cache (DocumentFlow)",
                IsEnabled = true,
                Components = new List<ModuleComponent>
                {
                    new() { Name = "AS (App Server)", Cpu = 1.3, RamGb = 10, PerfCpu = 1.3, PerfRamGb = 10, Formula = ReplicaFormula.Per25Users },
                    new() { Name = "AS-Local SQL", Cpu = 1.0, RamGb = 5, PerfCpu = 1.0, PerfRamGb = 5, Formula = ReplicaFormula.Fixed, FixedReplicas = 1, HasLocalSql = true },
                    new() { Name = "AS-Redis", Cpu = 0.2, RamGb = 0.2, PerfCpu = 0.2, PerfRamGb = 0.2, Formula = ReplicaFormula.Fixed, FixedReplicas = 1, HasRedis = true }
                }
            },
            new()
            {
                Name = "ROBOT", Description = "Robot process automation services (DocumentFlow)",
                IsEnabled = true,
                Components = new List<ModuleComponent>
                {
                    new() { Name = "ROBOT", Cpu = 1.3, RamGb = 10, PerfCpu = 1.3, PerfRamGb = 10, Formula = ReplicaFormula.Per100Plus1000 },
                    new() { Name = "ROBOT-Local SQL", Cpu = 1.0, RamGb = 5, PerfCpu = 1.0, PerfRamGb = 5, Formula = ReplicaFormula.Fixed, FixedReplicas = 1, HasLocalSql = true },
                    new() { Name = "ROBOT-Redis", Cpu = 0.2, RamGb = 0.2, PerfCpu = 0.2, PerfRamGb = 0.2, Formula = ReplicaFormula.Fixed, FixedReplicas = 1, HasRedis = true }
                }
            },
            new()
            {
                Name = "Web", Description = "Web services including WebSocket and SmartID (DocumentFlow)",
                IsEnabled = true,
                Components = new List<ModuleComponent>
                {
                    new() { Name = "Webrmd", Cpu = 0.2, RamGb = 1.5, Formula = ReplicaFormula.Per25Users },
                    new() { Name = "SmartID", Cpu = 0.2, RamGb = 0.5, Formula = ReplicaFormula.Per25Users },
                    new() { Name = "WS (WebSocket)", Cpu = 0.35, RamGb = 0.6, PerfCpu = 0.35, PerfRamGb = 0.6, Formula = ReplicaFormula.Per50Plus500 },
                    new() { Name = "WS-SignalR", Cpu = 0.25, RamGb = 0.5, Formula = ReplicaFormula.Per25Users }
                }
            },
            new()
            {
                Name = "ForceBPM", Description = "Business process management engine and tools (DocumentFlow)",
                IsEnabled = true,
                Components = new List<ModuleComponent>
                {
                    new() { Name = "GraphQL", Cpu = 0.3, RamGb = 1, Formula = ReplicaFormula.Per25Users },
                    new() { Name = "ForceBPM Engine", Cpu = 1.0, RamGb = 4, Formula = ReplicaFormula.OnePlusPer100, HasLocalSql = true },
                    new() { Name = "ForceBPM Modeler", Cpu = 0.5, RamGb = 0.5, Formula = ReplicaFormula.Fixed, FixedReplicas = 1 },
                    new() { Name = "ForceBPM Processes", Cpu = 0.5, RamGb = 2, Formula = ReplicaFormula.Per25Users },
                    new() { Name = "ForceBPM Tasks", Cpu = 0.3, RamGb = 2, Formula = ReplicaFormula.Per25Users },
                    new() { Name = "ForceBPM Tasks-Graphql", Cpu = 0.3, RamGb = 1, Formula = ReplicaFormula.Per25Users }
                }
            },
            new()
            {
                Name = "LMS", Description = "Learning management system with video utilities (DocumentFlow)",
                IsEnabled = false,
                Components = new List<ModuleComponent>
                {
                    new() { Name = "LMS-SmartID", Cpu = 0.006, RamGb = 0.05, Formula = ReplicaFormula.Per25Users },
                    new() { Name = "LMS", Cpu = 0.3, RamGb = 1, Formula = ReplicaFormula.Fixed, FixedReplicas = 1, HasLocalSql = true },
                    new() { Name = "LMS-GraphQL", Cpu = 0.09, RamGb = 0.3, Formula = ReplicaFormula.Per25Users },
                    new() { Name = "LMS-Videoutilities", Cpu = 4.0, RamGb = 6, Formula = ReplicaFormula.Fixed, FixedReplicas = 1, HasLocalSql = true, Notes = "Requires GPU for video transcoding" },
                    new() { Name = "LMS-Fileserver", Cpu = 0.5, RamGb = 8, Formula = ReplicaFormula.Fixed, FixedReplicas = 1 }
                }
            },
            new()
            {
                Name = "HR Portal", Description = "HR self-service portal with modeler and player (DocumentFlow)",
                IsEnabled = false,
                Components = new List<ModuleComponent>
                {
                    new() { Name = "HR-SmartID", Cpu = 0.006, RamGb = 0.05, Formula = ReplicaFormula.Per100Users },
                    new() { Name = "HR-GraphQL", Cpu = 0.01, RamGb = 0.06, Formula = ReplicaFormula.Per100Users },
                    new() { Name = "WebAppModeler", Cpu = 0.5, RamGb = 2, Formula = ReplicaFormula.Fixed, FixedReplicas = 1, HasLocalSql = true },
                    new() { Name = "CommonAppPlayer", Cpu = 0.5, RamGb = 2, Formula = ReplicaFormula.Fixed, FixedReplicas = 1, HasLocalSql = true }
                }
            },
            new()
            {
                Name = "Windows Infrastructure", Description = "Windows App Servers and Web Servers (DocumentFlow)",
                IsEnabled = true,
                Components = new List<ModuleComponent>
                {
                    new() { Name = "Windows App Server", Cpu = 4.0, RamGb = 24, Formula = ReplicaFormula.Fixed, FixedReplicas = 1, Notes = "Per Windows deployment VM (DocumentFlow)" },
                    new() { Name = "Windows Web Server", Cpu = 4.0, RamGb = 8, Formula = ReplicaFormula.Fixed, FixedReplicas = 1, Notes = "Per Windows deployment VM" }
                }
            }
        };
    }

    private static readonly InfrastructureNode _defaultSql = new() { Name = "SQL Server", Os = "Windows Server 2022", Cpu = 4, RamGb = 12, NodeCount = 1, StorageGb = 300, StorageType = "SSD" };
    private static readonly InfrastructureNode _defaultMaster = new() { Name = "Master Node", Os = "Ubuntu 24.04", Cpu = 4, RamGb = 6, NodeCount = 1, StorageGb = 100, StorageType = "SSD" };
    private static readonly InfrastructureNode _defaultWorker = new() { Name = "Worker Node", Os = "Ubuntu 24.04", Cpu = 8, RamGb = 32, NodeCount = 1, StorageGb = 200, StorageType = "SSD" };
}

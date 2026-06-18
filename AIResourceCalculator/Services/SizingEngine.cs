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

    private void CalculateK8s(ResourceRequirement req, ProjectConfig config)
    {
        var sqlRange = FindDatabaseRange(config.UserCount, config.LoadProfile, config.DatabaseType);
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
            foreach (var comp in module.Components ?? new())
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

        var workerCpuCapacity = workerNode.Cpu > 0 ? workerNode.Cpu : DefaultWorkerCpu;
        var workerRamCapacity = workerNode.RamGb > 0 ? workerNode.RamGb : DefaultWorkerRamGb;
        var workerCount = Math.Max(1,
            (int)Math.Ceiling(Math.Max(totalCpu / workerCpuCapacity, totalRam / workerRamCapacity)));

        req.TotalCpu = totalCpu;
        req.TotalRamGb = totalRam;
        req.WorkerNodeCount = workerCount;
        req.MasterNodeCount = masterNode.NodeCount > 0 ? masterNode.NodeCount : 1;

        var dbName = GetDatabaseNodeName(config.DatabaseType);
        var sqlNode = _matrix.DefaultK8sSql ?? _defaultSql;
        req.Infrastructure.Add(new InfrastructureNode
        {
            Name = dbName, Os = sqlNode.Os, Cpu = sqlRange?.Cpu ?? sqlNode.Cpu,
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
            var gpuCount = Math.Max(1, (int)Math.Ceiling((double)config.UserCount / UsersPerGpuNode));
            req.Infrastructure.Add(new InfrastructureNode
            {
                Name = "GPU Node (T4/A10)", Os = "Ubuntu 24.04", Cpu = GpuNodeCpu, RamGb = GpuNodeRamGb,
                NodeCount = gpuCount, StorageGb = GpuNodeStorageGb, StorageType = "SSD"
            });
            req.TotalCpu += GpuNodeCpu * gpuCount;
            req.TotalRamGb += GpuNodeRamGb * gpuCount;
            req.TotalStorageGb += GpuNodeStorageGb * gpuCount;
        }
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

        var enabledModules = _modules.Where(m => m.IsEnabled && !m.IsKubernetesOnly).ToList();
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

        var dbName = GetDatabaseNodeName(config.DatabaseType);
        req.Infrastructure.Add(new InfrastructureNode
        {
            Name = dbName, Os = sqlNode.Os, Cpu = sqlRange?.Cpu ?? sqlNode.Cpu,
            RamGb = sqlRange?.RamRec ?? sqlNode.RamGb, NodeCount = 1,
            StorageType = sqlNode.StorageType, StorageGb = sqlNode.StorageGb,
            StorageType2 = sqlNode.StorageType2, StorageGb2 = sqlNode.StorageGb2,
            StorageType3 = sqlNode.StorageType3, StorageGb3 = sqlNode.StorageGb3,
            StorageType4 = sqlNode.StorageType4, StorageGb4 = sqlNode.StorageGb4,
            PageFileGb = sqlNode.PageFileGb > 0 ? sqlNode.PageFileGb : (int)Math.Ceiling((sqlRange?.RamRec ?? sqlNode.RamGb) * 1.0),
            PageFileType = sqlNode.PageFileType ?? "Auto",
            Iops = sqlRange?.Iops ?? 500, Latency = sqlRange?.Latency ?? 1
        });
        // Disk separation for >64 GB RAM
        var sqlRam = sqlRange?.RamRec ?? sqlNode.RamGb;
        var sqlNodeRef = req.Infrastructure.Last(n => n.Name == dbName);
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

        // Deduplicate database nodes (Hybrid runs both K8s + Windows, both add DB)
        var dbNodeName = GetDatabaseNodeName(config.DatabaseType);
        var sqlNodes = req.Infrastructure.Where(n => n.Name == dbNodeName).ToList();
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

    private static string GetDatabaseNodeName(DatabaseType dbType) => dbType switch
    {
        DatabaseType.PostgreSQL => "PostgreSQL",
        DatabaseType.Oracle => "Oracle 19c",
        _ => "SQL Server"
    };

    // Fallback worker capacity when matrix node specs are missing
    private const double DefaultWorkerCpu = 8;
    private const double DefaultWorkerRamGb = 32;

    // GPU node defaults for video transcoding (LMS-Videoutilities)
    private const int UsersPerGpuNode = 100;
    private const int GpuNodeCpu = 8;
    private const int GpuNodeRamGb = 32;
    private const int GpuNodeStorageGb = 200;

    private static readonly InfrastructureNode _defaultSql = new() { Name = "SQL Server", Os = "Windows Server 2022", Cpu = 4, RamGb = 12, NodeCount = 1, StorageGb = 300, StorageType = "SSD" };
    private static readonly InfrastructureNode _defaultMaster = new() { Name = "Master Node", Os = "Ubuntu 24.04", Cpu = 4, RamGb = 6, NodeCount = 1, StorageGb = 100, StorageType = "SSD" };
    private static readonly InfrastructureNode _defaultWorker = new() { Name = "Worker Node", Os = "Ubuntu 24.04", Cpu = 8, RamGb = 32, NodeCount = 1, StorageGb = 200, StorageType = "SSD" };
}

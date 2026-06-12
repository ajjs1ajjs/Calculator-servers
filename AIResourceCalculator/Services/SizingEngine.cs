using AIResourceCalculator.Data;
using AIResourceCalculator.Models;

namespace AIResourceCalculator.Services;

public class SizingEngine
{
    private readonly SizingMatrix _matrix;
    private List<ProjectModule> _modules;

    public IReadOnlyList<ProjectModule> Modules => _modules.AsReadOnly();

    public SizingEngine(SizingMatrix matrix)
    {
        _matrix = matrix;
        _modules = ModuleDefinitions.GetAllModules();
    }

    public void SetModules(List<ProjectModule> modules)
    {
        _modules = modules;
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
        {
            CalculateHybrid(req, config);
        }
        else if (config.DeploymentType == DeploymentType.Kubernetes)
            CalculateK8s(req, config);
        else
            CalculateWindows(req, config);

        return req;
    }

    private void CalculateK8s(ResourceRequirement req, ProjectConfig config)
    {
        var sqlRange = FindMsSqlRange(config.UserCount, config.LoadProfile);
        var masterNode = _matrix.DefaultK8sMaster;
        var workerNode = _matrix.DefaultK8sWorker;

        double totalCpu = 0, totalRam = 0;

        var enabledModules = _modules.Where(m => m.IsEnabled).ToList();

        foreach (var module in enabledModules)
        {
            var (modCpu, modRam) = module.CalculateReplicas(config.UserCount, config.LoadProfile);
            totalCpu += modCpu;
            totalRam += modRam;

            var isPerf = config.LoadProfile == LoadProfile.Performance;
            foreach (var comp in module.Components)
            {
                int rep = comp.Formula switch
                {
                    ReplicaFormula.Fixed => comp.FixedReplicas,
                    ReplicaFormula.Per25Users => (int)Math.Ceiling(config.UserCount / 25.0),
                    ReplicaFormula.Per100Users => (int)Math.Ceiling(config.UserCount / 100.0),
                    ReplicaFormula.Per50Users => (int)Math.Ceiling(config.UserCount / 50.0),
                    ReplicaFormula.Per100Plus1000 => 1 + (int)(config.UserCount / 100.0) + (int)(config.UserCount / 1000.0),
                    ReplicaFormula.Per50Plus500 => 1 + (int)(config.UserCount / 50.0) + (int)(config.UserCount / 500.0),
                    ReplicaFormula.OnePlusPer100 => 1 + (int)(config.UserCount / 100.0),
                    _ => Math.Max(1, comp.FixedReplicas)
                };
                if (rep == 0) rep = 1;

                var cpu = isPerf && comp.PerfCpu > 0 ? comp.PerfCpu : comp.Cpu;
                var ram = isPerf && comp.PerfRamGb > 0 ? comp.PerfRamGb : comp.RamGb;
                req.Components.Add(new ServiceComponent
                {
                    Name = comp.Name,
                    Cpu = cpu * rep,
                    RamGb = ram * rep,
                    Replicas = rep,
                    Category = module.Name
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

        var sqlNode = _matrix.DefaultK8sSql;
        req.Infrastructure.Add(new InfrastructureNode
        {
            Name = "SQL Server", Os = sqlNode.Os, Cpu = sqlRange?.Cpu ?? 4,
            RamGb = sqlRange?.RamRec ?? 16, NodeCount = 1,
            StorageGb = 200, StorageType = "SSD"
        });
        req.Infrastructure.Add(new InfrastructureNode
        {
            Name = "Master Node", Os = masterNode.Os, Cpu = masterNode.Cpu,
            RamGb = masterNode.RamGb, NodeCount = req.MasterNodeCount,
            StorageGb = 100, StorageType = "SSD"
        });
        req.Infrastructure.Add(new InfrastructureNode
        {
            Name = "Worker Node", Os = workerNode.Os, Cpu = workerNode.Cpu,
            RamGb = workerNode.RamGb, NodeCount = req.WorkerNodeCount,
            StorageGb = 200, StorageType = "SSD"
        });

        req.TotalStorageGb = 200 + 100 * req.MasterNodeCount + 200 * req.WorkerNodeCount;
        req.TotalIops = sqlRange?.Iops ?? 500;
        req.TotalLatency = sqlRange?.Latency ?? 1;
    }

    private void CalculateWindows(ResourceRequirement req, ProjectConfig config)
    {
        var appRange = FindWindowsRange(config.UserCount, _matrix.AppServerRanges,
            _matrix.AppServerPerformanceRanges, config.LoadProfile);
        var webRange = FindWindowsRange(config.UserCount, _matrix.WebServerRanges,
            _matrix.WebServerPerformanceRanges ?? new(), config.LoadProfile);

        var sqlRange = FindMsSqlRange(config.UserCount, config.LoadProfile);

        var enabledModules = _modules.Where(m => m.IsEnabled).ToList();
        double totalCpu = 0, totalRam = 0;

        foreach (var module in enabledModules)
        {
            var (modCpu, modRam) = module.CalculateReplicas(config.UserCount);
            totalCpu += modCpu;
            totalRam += modRam;
        }

        var appCpu = appRange?.Cpu ?? 4;
        var appRam = appRange?.RamRec ?? 16;
        var appCount = appRange?.InstanceCount ?? 1;
        var webCpu = webRange?.Cpu ?? 4;
        var webRam = webRange?.RamRec ?? 8;
        var webCount = webRange?.InstanceCount ?? 1;

        req.TotalCpu = totalCpu + (sqlRange?.Cpu ?? 4);
        req.TotalRamGb = totalRam + (sqlRange?.RamRec ?? 16);
        req.TotalIops = (appRange?.Iops ?? 200) + (webRange?.Iops ?? 200) + (sqlRange?.Iops ?? 500);

        req.WorkerNodeCount = appCount + webCount;
        req.MasterNodeCount = 1;

        req.Infrastructure.Add(new InfrastructureNode
        {
            Name = "SQL Server", Os = "Windows Server 2022", Cpu = sqlRange?.Cpu ?? 4,
            RamGb = sqlRange?.RamRec ?? 16, NodeCount = 1,
            StorageGb = 300, StorageType = "SSD"
        });
        req.Infrastructure.Add(new InfrastructureNode
        {
            Name = "App Server", Os = "Windows Server 2022", Cpu = appCpu,
            RamGb = appRam, NodeCount = appCount,
            StorageGb = 150, StorageType = "SSD"
        });
        req.Infrastructure.Add(new InfrastructureNode
        {
            Name = "Web Server (IIS)", Os = "Windows Server 2022", Cpu = webCpu,
            RamGb = webRam, NodeCount = webCount,
            StorageGb = 150, StorageType = "SSD"
        });

        req.TotalStorageGb = 300 + 150 * appCount + 150 * webCount;
        req.TotalLatency = sqlRange?.Latency ?? 1;
    }

    private UserLoadRange? FindMsSqlRange(int userCount, LoadProfile profile)
    {
        var ranges = profile == LoadProfile.Performance
            ? _matrix.MsSqlPerformanceRanges
            : _matrix.MsSqlRanges;
        return ranges.FirstOrDefault(r => userCount >= r.MinUsers && userCount <= r.MaxUsers)
               ?? ranges.LastOrDefault();
    }

    private void CalculateHybrid(ResourceRequirement req, ProjectConfig config)
    {
        var k8sReq = new ResourceRequirement { UserCount = config.UserCount, DeploymentType = DeploymentType.Kubernetes, LoadProfile = config.LoadProfile };
        var winReq = new ResourceRequirement { UserCount = config.UserCount, DeploymentType = DeploymentType.Windows, LoadProfile = config.LoadProfile };

        CalculateK8s(k8sReq, config);
        CalculateWindows(winReq, config);

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
    }

    private UserLoadRange? FindWindowsRange(int userCount,
        List<UserLoadRange> basic, List<UserLoadRange> performance, LoadProfile profile)
    {
        var ranges = profile == LoadProfile.Performance && performance.Count > 0
            ? performance : basic;
        return ranges.FirstOrDefault(r => userCount >= r.MinUsers && userCount <= r.MaxUsers)
               ?? ranges.LastOrDefault();
    }
}

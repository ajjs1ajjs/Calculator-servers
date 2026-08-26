using ResourceCalculator.Data;
using ResourceCalculator.Models;
using ResourceCalculator.Services;

var matrix = new SizingMatrix();
var engine = new SizingEngine(matrix);
var modules = engine.Modules.Select(m => m.Clone()).ToList();
foreach (var m in modules)
{
    if (m.Name == "HR Portal") { m.IsEnabled = true; m.UserCount = 5000; }
    if (m.Name == "ForceBPM") { m.IsEnabled = true; }
}
engine.SetModules(modules);
var result = engine.Calculate(new ProjectConfig { UserCount = 500, DeploymentType = DeploymentType.Kubernetes, LoadProfile = LoadProfile.Performance });
Console.WriteLine($"SUMMARY CPU={result.TotalCpu} RAM={result.TotalRamGb} STORAGE={result.TotalStorageGb} IOPS={result.TotalIops} PODCPU={result.PodCpu} PODRAM={result.PodRamGb} WORKERS={result.WorkerNodeCount} MASTERS={result.MasterNodeCount}");
foreach (var c in result.Components) Console.WriteLine($"COMP|{c.Category}|{c.Name}|rep={c.Replicas}|cpu={c.Cpu}|ram={c.RamGb}");
foreach (var n in result.Infrastructure) Console.WriteLine($"NODE|{n.Name}|count={n.NodeCount}|cpu={n.Cpu}|ram={n.RamGb}|storage={n.TotalStorageGb}|iops={n.Iops}|lat={n.Latency}");

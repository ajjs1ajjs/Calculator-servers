using AIResourceCalculator.Data;
using AIResourceCalculator.Interfaces;
using AIResourceCalculator.Models;

namespace AIResourceCalculator.Services;

public class MatrixManager
{
    private SizingMatrix _matrix;
    private readonly IDataService _dataService;

    public SizingMatrix Matrix => _matrix;

    public MatrixManager(IDataService dataService)
    {
        _dataService = dataService;
        _matrix = dataService.LoadMatrix();
    }

    public void Import(string filePath)
    {
        var importer = new ExcelImporter();
        _matrix = importer.Import(filePath);
        _dataService.SaveMatrix(_matrix);
    }

    public void Save()
    {
        _dataService.SaveMatrix(_matrix);
    }

    public void Reset()
    {
        _dataService.ClearMatrix();
        _matrix = new SizingMatrix();
    }

    public void SyncGridsToMatrix(
        List<UserLoadRange> msSqlRanges,
        List<UserLoadRange> msSqlPerfRanges,
        List<ServiceComponent> k8sStandard,
        List<ServiceComponent> k8sDocFlow,
        List<ServiceComponent> k8sComponents,
        List<InfrastructureNode> infraNodes)
    {
        _matrix.MsSqlRanges = msSqlRanges;
        _matrix.MsSqlPerformanceRanges = msSqlPerfRanges;

        SyncComponentsToModules(k8sStandard, _matrix.StandardModules);
        SyncComponentsToModules(k8sDocFlow, _matrix.DocumentFlowModules);
        SyncComponentsToModules(k8sComponents, _matrix.Modules);

        _matrix.DefaultK8sSql = infraNodes.FirstOrDefault(n => n.Name.Contains("SQL"));
        _matrix.DefaultK8sMaster = infraNodes.FirstOrDefault(n => n.Name.Contains("Master"));
        _matrix.DefaultK8sWorker = infraNodes.FirstOrDefault(n => n.Name.Contains("Worker"));
    }

    private static void SyncComponentsToModules(List<ServiceComponent> components, List<ProjectModule> modules)
    {
        if (components.Count == 0) return;

        var grouped = components.GroupBy(c => c.Category);
        foreach (var group in grouped)
        {
            var module = modules.FirstOrDefault(m => m.Name == group.Key);
            if (module == null)
            {
                module = new ProjectModule { Name = group.Key, Description = group.Key, IsEnabled = true };
                modules.Add(module);
            }

            module.Components.Clear();
            foreach (var comp in group)
            {
                module.Components.Add(new ModuleComponent
                {
                    Name = comp.Name, Cpu = comp.Cpu, RamGb = comp.RamGb,
                    FixedReplicas = comp.FixedReplicas, Formula = comp.Formula
                });
            }
        }
    }
}

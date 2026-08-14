using ResourceCalculator.Data;
using ResourceCalculator.Interfaces;
using ResourceCalculator.Models;

namespace ResourceCalculator.Services;

public class MatrixManager
{
    private SizingMatrix _matrix;
    private readonly IDataService _dataService;

    public SizingMatrix Matrix => _matrix;

    public MatrixManager(IDataService dataService, SizingMatrix matrix)
    {
        _dataService = dataService;
        _matrix = matrix;
        var saved = dataService.LoadMatrix();
        CopyMatrix(saved, _matrix);
    }

    public void Save()
    {
        _dataService.SaveMatrix(_matrix);
    }

    public void Reset()
    {
        _dataService.ClearMatrix();
        CopyMatrix(new SizingMatrix(), _matrix);
    }

    // Глибоке копіювання: target отримує власні екземпляри списків і об'єктів,
    // щоб редагування грідів не мутувало спільний стан движка через аліасинг посилань.
    private static void CopyMatrix(SizingMatrix source, SizingMatrix target)
    {
        static List<UserLoadRange> CloneRanges(List<UserLoadRange> src)
            => src.Select(r => r.Clone()).ToList();

        target.MsSqlRanges = CloneRanges(source.MsSqlRanges);
        target.MsSqlPerformanceRanges = CloneRanges(source.MsSqlPerformanceRanges);
        target.AppServerRanges = CloneRanges(source.AppServerRanges);
        target.AppServerPerformanceRanges = CloneRanges(source.AppServerPerformanceRanges);
        target.WebServerRanges = CloneRanges(source.WebServerRanges);
        target.WebServerPerformanceRanges = CloneRanges(source.WebServerPerformanceRanges);
        target.PostgresRanges = CloneRanges(source.PostgresRanges);
        target.OracleRanges = CloneRanges(source.OracleRanges);
        target.DocumentFlowModules = source.DocumentFlowModules.ToClonedList();
        target.Modules = source.Modules.ToClonedList();
        target.DefaultK8sSql = source.DefaultK8sSql?.Clone();
        target.DefaultK8sMaster = source.DefaultK8sMaster?.Clone();
        target.DefaultK8sWorker = source.DefaultK8sWorker?.Clone();
        target.DefaultWindowsSql = source.DefaultWindowsSql?.Clone();
        target.DefaultWindowsApp = source.DefaultWindowsApp?.Clone();
        target.DefaultWindowsWeb = source.DefaultWindowsWeb?.Clone();
        target.DefaultReportingServer = source.DefaultReportingServer?.Clone();
        target.DefaultHaProxy = source.DefaultHaProxy?.Clone();
        target.Engine = source.Engine?.Clone() ?? new EngineSettings();

        NormalizeModulePolicy(target);
    }

    // Обов'язковість і дефолтний стан модулів — інваріант КОДУ, а не збережених/імпортованих
    // даних. Тому після будь-якого завантаження матриці примусово відновлюємо політику:
    //  • App Server / ROBOT / Web — обов'язкові, завжди ввімкнені;
    //  • LMS / HR Portal — вимкнені за замовчуванням (рідкі сервіси, вмикаються за потреби).
    private static readonly HashSet<string> MandatoryModules = new() { "App Server", "ROBOT", "Web" };
    private static readonly HashSet<string> OffByDefaultModules = new() { "LMS", "HR Portal" };

    private static void NormalizeModulePolicy(SizingMatrix m)
    {
        foreach (var mod in m.DocumentFlowModules)
        {
            mod.IsMandatory = MandatoryModules.Contains(mod.Name);
            if (mod.IsMandatory) mod.IsEnabled = true;
            else if (OffByDefaultModules.Contains(mod.Name)) mod.IsEnabled = false;
        }
    }

    public void SyncGridsToMatrix(
        List<UserLoadRange> msSqlRanges,
        List<UserLoadRange> msSqlPerfRanges,
        List<UserLoadRange> appServerRanges,
        List<UserLoadRange> appServerPerfRanges,
        List<UserLoadRange> webServerRanges,
        List<UserLoadRange> webServerPerfRanges,
        List<UserLoadRange> postgresRanges,
        List<UserLoadRange> oracleRanges,
        List<ServiceComponent> k8sDocFlow,
        List<InfrastructureNode> k8sNodes,
        List<InfrastructureNode> windowsNodes,
        List<InfrastructureNode> optionalNodes,
        EngineSettings engine)
    {
        _matrix.MsSqlRanges = msSqlRanges;
        _matrix.MsSqlPerformanceRanges = msSqlPerfRanges;
        _matrix.AppServerRanges = appServerRanges;
        _matrix.AppServerPerformanceRanges = appServerPerfRanges;
        _matrix.WebServerRanges = webServerRanges;
        _matrix.WebServerPerformanceRanges = webServerPerfRanges;
        _matrix.PostgresRanges = postgresRanges;
        _matrix.OracleRanges = oracleRanges;

        SyncComponentsToModules(k8sDocFlow, _matrix.DocumentFlowModules);

        var (k8sSql, k8sMaster, k8sWorker) = SyncNodes(k8sNodes);
        if (k8sSql != null) _matrix.DefaultK8sSql = k8sSql;
        if (k8sMaster != null) _matrix.DefaultK8sMaster = k8sMaster;
        if (k8sWorker != null) _matrix.DefaultK8sWorker = k8sWorker;

        SyncWindowsNodes(windowsNodes);
        SyncOptionalNodes(optionalNodes);

        _matrix.Engine = engine?.Clone() ?? new EngineSettings();
    }

    private static (InfrastructureNode? sql, InfrastructureNode? master, InfrastructureNode? worker)
        SyncNodes(List<InfrastructureNode> nodes)
    {
        InfrastructureNode? sql = null, master = null, worker = null;
        foreach (var n in nodes)
        {
            if (n.Name.Contains("SQL", StringComparison.OrdinalIgnoreCase)) sql = n;
            else if (n.Name.Contains("Master", StringComparison.OrdinalIgnoreCase)) master = n;
            else if (n.Name.Contains("Worker", StringComparison.OrdinalIgnoreCase)) worker = n;
        }
        return (sql, master, worker);
    }

    private void SyncWindowsNodes(List<InfrastructureNode> nodes)
    {
        if (nodes.Count == 0) return;
        foreach (var n in nodes)
        {
            if (n.Name.Contains("SQL", StringComparison.OrdinalIgnoreCase)) _matrix.DefaultWindowsSql = n;
            else if (n.Name.Contains("Сервер", StringComparison.OrdinalIgnoreCase)
                || n.Name.Contains("App", StringComparison.OrdinalIgnoreCase)) _matrix.DefaultWindowsApp = n;
            else if (n.Name.Contains("Веб", StringComparison.OrdinalIgnoreCase)
                || n.Name.Contains("IIS", StringComparison.OrdinalIgnoreCase)
                || n.Name.Contains("Web", StringComparison.OrdinalIgnoreCase)) _matrix.DefaultWindowsWeb = n;
        }
    }

    private void SyncOptionalNodes(List<InfrastructureNode> nodes)
    {
        if (nodes.Count == 0) return;
        foreach (var n in nodes)
        {
            if (n.Name.Contains("звіт", StringComparison.OrdinalIgnoreCase)
                || n.Name.Contains("report", StringComparison.OrdinalIgnoreCase)) _matrix.DefaultReportingServer = n;
            else if (n.Name.Contains("HAProxy", StringComparison.OrdinalIgnoreCase)
                || n.Name.Contains("haproxy", StringComparison.OrdinalIgnoreCase)) _matrix.DefaultHaProxy = n;
        }
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
                    PerfCpu = comp.PerfCpu, PerfRamGb = comp.PerfRamGb,
                    FixedReplicas = comp.FixedReplicas, Formula = comp.Formula,
                    HasLocalSql = comp.HasLocalSql, HasRedis = comp.HasRedis
                });
            }
        }
    }
}

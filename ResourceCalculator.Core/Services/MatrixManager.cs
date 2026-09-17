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
        target.AppServerRanges = CloneRanges(source.AppServerRanges);
        target.WebServerRanges = CloneRanges(source.WebServerRanges);
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

    // Повертає помилки валідації (порожньо = застосовано). Невалідні дані грідів
    // НЕ потрапляють у движок/експорт: викликач показує помилки і перериває Save/Recalculate.
    public List<string> SyncGridsToMatrix(
        List<UserLoadRange> msSqlRanges,
        List<UserLoadRange> appServerRanges,
        List<UserLoadRange> webServerRanges,
        List<UserLoadRange> postgresRanges,
        List<UserLoadRange> oracleRanges,
        List<ServiceComponent> k8sDocFlow,
        List<InfrastructureNode> k8sNodes,
        List<InfrastructureNode> windowsNodes,
        List<InfrastructureNode> optionalNodes,
        EngineSettings engine)
    {
        var candidate = CloneForValidation(msSqlRanges, appServerRanges, webServerRanges,
            postgresRanges, oracleRanges, k8sDocFlow, k8sNodes, windowsNodes, optionalNodes, engine);
        var errors = MatrixValidator.Validate(candidate);
        if (errors.Count > 0) return errors;

        _matrix.MsSqlRanges = msSqlRanges;
        _matrix.AppServerRanges = appServerRanges;
        _matrix.WebServerRanges = webServerRanges;
        _matrix.PostgresRanges = postgresRanges;
        _matrix.OracleRanges = oracleRanges;

        SyncComponentsToModules(k8sDocFlow, _matrix.DocumentFlowModules);

        ApplyNodes(_matrix, k8sNodes, windowsNodes, optionalNodes);

        _matrix.Engine = engine?.Clone() ?? new EngineSettings();
        return new List<string>();
    }

    // Будуємо кандидат для валідації без мутації живого стану: ті самі правила
    // злиття, що й бойовий шлях, але на клонах.
    private static SizingMatrix CloneForValidation(
        List<UserLoadRange> msSqlRanges,
        List<UserLoadRange> appServerRanges,
        List<UserLoadRange> webServerRanges,
        List<UserLoadRange> postgresRanges,
        List<UserLoadRange> oracleRanges,
        List<ServiceComponent> k8sDocFlow,
        List<InfrastructureNode> k8sNodes,
        List<InfrastructureNode> windowsNodes,
        List<InfrastructureNode> optionalNodes,
        EngineSettings engine)
    {
        var candidate = new SizingMatrix
        {
            MsSqlRanges = msSqlRanges.Select(r => r.Clone()).ToList(),
            AppServerRanges = appServerRanges.Select(r => r.Clone()).ToList(),
            WebServerRanges = webServerRanges.Select(r => r.Clone()).ToList(),
            PostgresRanges = postgresRanges.Select(r => r.Clone()).ToList(),
            OracleRanges = oracleRanges.Select(r => r.Clone()).ToList(),
            Engine = engine?.Clone() ?? new EngineSettings()
        };
        var modules = new List<ProjectModule>();
        SyncComponentsToModules(k8sDocFlow, modules);
        candidate.DocumentFlowModules = modules;
        ApplyNodes(candidate, k8sNodes, windowsNodes, optionalNodes);
        return candidate;
    }

    // Розкладає відредаговані гріди вузлів по слотах матриці за явним Slot.
    // Слот, якого немає у грідах, лишається з попереднім значенням; рядок без слота
    // (NodeSlot.None) ігнорується — модель матриці має рівно ці вісім вузлів.
    //
    // Раніше маппінг ішов через Name.Contains(...) і ламався на назвах, що
    // перетинаються: "Веб сервери (IIS)" містить "сервер" і потрапляв у слот
    // сервера додатків раніше, ніж перевірялася гілка Web — правки веб-вузла
    // губилися, а сервер додатків отримував чужі назву й диски.
    private static void ApplyNodes(SizingMatrix target,
        List<InfrastructureNode> k8s, List<InfrastructureNode> win, List<InfrastructureNode> opt)
    {
        foreach (var n in k8s.Concat(win).Concat(opt))
        {
            if (n is null) continue;
            switch (n.Slot)
            {
                case NodeSlot.K8sSql: target.DefaultK8sSql = n; break;
                case NodeSlot.K8sMaster: target.DefaultK8sMaster = n; break;
                case NodeSlot.K8sWorker: target.DefaultK8sWorker = n; break;
                case NodeSlot.WindowsSql: target.DefaultWindowsSql = n; break;
                case NodeSlot.WindowsApp: target.DefaultWindowsApp = n; break;
                case NodeSlot.WindowsWeb: target.DefaultWindowsWeb = n; break;
                case NodeSlot.ReportingServer: target.DefaultReportingServer = n; break;
                case NodeSlot.HaProxy: target.DefaultHaProxy = n; break;
            }
        }
    }

    private static void SyncComponentsToModules(List<ServiceComponent> components, List<ProjectModule> modules)
    {
        if (components.Count == 0) return;

        // Категорія — вільний текст з гріда: тримимо, відкидаємо порожні, дедуплимо
        // без урахування регістру, щоб не плодити сміттєві модулі у звітах.
        var grouped = components
            .Where(c => c is not null && !string.IsNullOrWhiteSpace(c.Category))
            .GroupBy(c => c.Category.Trim(), StringComparer.OrdinalIgnoreCase);
        foreach (var group in grouped)
        {
            var key = group.Key;
            var module = modules.FirstOrDefault(m => string.Equals(m.Name, key, StringComparison.OrdinalIgnoreCase));
            if (module == null)
            {
                if (modules.Count >= 500) continue;
                module = new ProjectModule { Name = key, Description = key, IsEnabled = true };
                modules.Add(module);
            }

            module.Components.Clear();
            foreach (var comp in group.Take(1000))
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

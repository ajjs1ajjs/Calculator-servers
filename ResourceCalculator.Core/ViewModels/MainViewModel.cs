using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ResourceCalculator.Data;
using ResourceCalculator.Interfaces;
using ResourceCalculator.Localization;
using ResourceCalculator.Models;
using ResourceCalculator.Services;

namespace ResourceCalculator.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly ISizingEngine _engine;
    private readonly ICalculationHistoryService _historyService;
    private readonly ILocalizationService _loc;
    private readonly ResultsPresenter _results;
    private readonly EnvironmentBuilder _envBuilder;
    private readonly IDialogService _dialogs;
    private readonly IFileSaveService _files;
    private readonly IThemeService? _theme;
    private ResourceRequirement? _lastResult;

    public MatrixViewModel MatrixVM { get; }

    public string AppVersion
    {
        get
        {
            // MainViewModel живе в Core, тому ExecutingAssembly() повертає Core (1.0.0).
            // Для UI потрібна версія entry executable, задана через AppVersion у props.
            var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
            var informational = assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (string.IsNullOrWhiteSpace(informational)) return "v?";
            var plus = informational.IndexOf('+');
            return "v" + (plus > 0 ? informational[..plus] : informational);
        }
    }

    private string _userCount = "100";
    private int _deploymentIndex;
    private int _databaseIndex;
    private string _statusText = "";
    private string _langFlag = "\U0001F1FA\U0001F1E6";
    private string _langName = "Українська";

    public MainViewModel(
        ILocalizationService localization,
        IDataService dataService,
        ICalculationHistoryService historyService,
        MatrixManager matrixManager,
        ResultsPresenter results,
        EnvironmentBuilder envBuilder,
        ISizingEngine engine,
        IDialogService? dialogs = null,
        IFileSaveService? files = null,
        IThemeService? theme = null)
    {
        _loc = localization;
        _historyService = historyService;
        _results = results;
        _envBuilder = envBuilder;
        _engine = engine;
        _dialogs = dialogs ?? new DefaultDialogService();
        _files = files ?? new DefaultFileSaveService();
        _theme = theme;

        MatrixVM = new MatrixViewModel(_loc, matrixManager, dialogs: dialogs);

        Modules = new ObservableCollection<ProjectModule>(_engine.Modules);
        _statusText = _loc["status.ready"];

        MatrixVM.LoadMatrixGrids();

        _loc.PropertyChanged += (_, _) => OnLanguageChanged();
        MatrixVM.MatrixChanged += OnMatrixChanged;

        InitializeCommands();

        OnDeploymentTypeChanged();
        RebuildEnvModuleCounts();
    }

    private void OnLanguageChanged()
    {
        var loc = _loc;
        LangFlag = loc.Flag;
        LangName = loc.LangName;
        StatusText = loc["status.ready"];
        OnPropertyChanged(nameof(TabMatrixHeader));
        OnPropertyChanged(nameof(TabSetupHeader));
        OnPropertyChanged(nameof(TabResultsHeader));
        OnPropertyChanged(nameof(ThemeName));
    }

    #region Properties

    public string UserCount
    {
        get => _userCount;
        set { _userCount = value; OnPropertyChanged(); }
    }

    public int DeploymentIndex
    {
        get => _deploymentIndex;
        set
        {
            _deploymentIndex = value;
            OnPropertyChanged();
            OnDeploymentTypeChanged();
        }
    }

    public int DatabaseIndex
    {
        get => _databaseIndex;
        set { _databaseIndex = value; OnPropertyChanged(); }
    }

    // --- Похідні середовища (PROD завжди; решта — за вибором) ---
    private bool _includeDev;
    private bool _includeTest;
    private bool _includePredProd;
    private string _devUserCount = "10";
    private string _testUserCount = "25";
    private string _predProdUserCount = "50";
    private string _prodDbSizeGb = "0";
    private string _prodContentDbSizeGb = "0";
    private string _devDbSizeGb = "0";
    private string _testDbSizeGb = "0";
    private string _predProdDbSizeGb = "0";
    private string _devContentDbSizeGb = "0";
    private string _testContentDbSizeGb = "0";
    private string _predProdContentDbSizeGb = "0";

    public bool IncludeDev { get => _includeDev; set { _includeDev = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasEnvironmentsSelected)); } }
    public bool IncludeTest { get => _includeTest; set { _includeTest = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasEnvironmentsSelected)); } }
    public bool IncludePredProd { get => _includePredProd; set { _includePredProd = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasEnvironmentsSelected)); } }

    // Чи ввімкнено хоч одне похідне середовище (для показу заголовка блоку карток середовищ).
    public bool HasEnvironmentsSelected => IncludeDev || IncludeTest || IncludePredProd;

    // Опціональні вузли інфраструктури (типово вимкнені, як модулі LMS/HR Portal).
    private bool _includeReportingServer;
    private bool _includeSqlFailover;
    private bool _includeHaProxy;
    private bool _canToggleHaProxy = true;
    public bool IncludeReportingServer { get => _includeReportingServer; set { _includeReportingServer = value; OnPropertyChanged(); } }
    public bool IncludeSqlFailover { get => _includeSqlFailover; set { _includeSqlFailover = value; OnPropertyChanged(); } }
    public bool IncludeHaProxy { get => _includeHaProxy; set { _includeHaProxy = value; OnPropertyChanged(); } }
    // У Гібриді HAProxy керується типом розгортання автоматично — чекбокс заблокований.
    public bool CanToggleHaProxy { get => _canToggleHaProxy; private set { _canToggleHaProxy = value; OnPropertyChanged(); } }
    public string DevUserCount { get => _devUserCount; set { _devUserCount = value; OnPropertyChanged(); } }
    public string TestUserCount { get => _testUserCount; set { _testUserCount = value; OnPropertyChanged(); } }
    public string PredProdUserCount { get => _predProdUserCount; set { _predProdUserCount = value; OnPropertyChanged(); } }
    // Обсяг даних БД (ГБ) — PROD задає вручну; Test/PreProd за замовчуванням = PROD, не менше PROD
    // (клампиться при розрахунку, а до того — видно попередження в реальному часі); Dev — незалежне
    // значення без нижньої межі.
    public string ProdDbSizeGb { get => _prodDbSizeGb; set { _prodDbSizeGb = value; OnPropertyChanged(); UpdateDbSizeWarnings(); } }
    public string DevDbSizeGb { get => _devDbSizeGb; set { _devDbSizeGb = value; OnPropertyChanged(); } }
    public string TestDbSizeGb { get => _testDbSizeGb; set { _testDbSizeGb = value; OnPropertyChanged(); UpdateDbSizeWarnings(); } }
    public string PredProdDbSizeGb { get => _predProdDbSizeGb; set { _predProdDbSizeGb = value; OnPropertyChanged(); UpdateDbSizeWarnings(); } }

    // Обсяг холодних/архівних даних Content (ГБ) — незалежне значення для кожного середовища
    // (0 = диск Content не виділяється; для non-prod це й типова поведінка без явного вводу).
    public string ProdContentDbSizeGb { get => _prodContentDbSizeGb; set { _prodContentDbSizeGb = value; OnPropertyChanged(); } }
    public string DevContentDbSizeGb { get => _devContentDbSizeGb; set { _devContentDbSizeGb = value; OnPropertyChanged(); } }
    public string TestContentDbSizeGb { get => _testContentDbSizeGb; set { _testContentDbSizeGb = value; OnPropertyChanged(); } }
    public string PredProdContentDbSizeGb { get => _predProdContentDbSizeGb; set { _predProdContentDbSizeGb = value; OnPropertyChanged(); } }

    // Попередження в реальному часі (поки друкує), якщо Test/PreProd менше PROD — значення все одно
    // буде піднято до PROD при розрахунку, але користувач бачить це одразу, а не лише постфактум.
    private string _testDbSizeWarning = "";
    public string TestDbSizeWarning { get => _testDbSizeWarning; private set { _testDbSizeWarning = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasTestDbSizeWarning)); } }
    public bool HasTestDbSizeWarning => !string.IsNullOrEmpty(TestDbSizeWarning);

    private string _predProdDbSizeWarning = "";
    public string PredProdDbSizeWarning { get => _predProdDbSizeWarning; private set { _predProdDbSizeWarning = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasPredProdDbSizeWarning)); } }
    public bool HasPredProdDbSizeWarning => !string.IsNullOrEmpty(PredProdDbSizeWarning);

    private void UpdateDbSizeWarnings()
    {
        int.TryParse(ProdDbSizeGb, out var prod);
        TestDbSizeWarning = int.TryParse(TestDbSizeGb, out var test) && test > 0 && test < prod
            ? $"Буде піднято до {prod} ГБ (не менше PROD)"
            : "";
        PredProdDbSizeWarning = int.TryParse(PredProdDbSizeGb, out var pp) && pp > 0 && pp < prod
            ? $"Буде піднято до {prod} ГБ (не менше PROD)"
            : "";
    }

    // Чи включати компоненти (поди) у сформований звіт (Excel/PDF). На розрахунок не впливає.
    private bool _includeComponentsInReport = true;
    public bool IncludeComponentsInReport { get => _includeComponentsInReport; set { _includeComponentsInReport = value; OnPropertyChanged(); } }

    public string StatusText
    {
        get => _statusText;
        set { _statusText = value; OnPropertyChanged(); }
    }

    public string LangFlag
    {
        get => _langFlag;
        set { _langFlag = value; OnPropertyChanged(); }
    }

    public string LangName
    {
        get => _langName;
        set { _langName = value; OnPropertyChanged(); }
    }

    // Перемикач світлої/темної теми — застосовується одразу через ThemeService (без перезапуску).
    private bool _isDarkTheme;
    public bool IsDarkTheme
    {
        get => _isDarkTheme;
        set
        {
            _isDarkTheme = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ThemeIcon));
            OnPropertyChanged(nameof(ThemeName));
        }
    }
    public string ThemeIcon => IsDarkTheme ? "\U0001F319" : "☀️";
    public string ThemeName => IsDarkTheme ? _loc["theme.dark"] : _loc["theme.light"];

    private int _selectedTabIndex;
    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set { _selectedTabIndex = value; OnPropertyChanged(); }
    }

    #endregion

    #region Matrix Properties (delegated to MatrixVM)

    public ObservableCollection<UserLoadRange> MsSqlRanges => MatrixVM.MsSqlRanges;
    public ObservableCollection<ServiceComponent> K8sDocumentFlowComponents => MatrixVM.K8sDocumentFlowComponents;
    public ObservableCollection<InfrastructureNode> InfraNodes => MatrixVM.InfraNodes;

    #endregion

    #region Result Properties

    private string _totalCpu = "0";
    private string _totalRam = "0 GB";
    private string _totalStorage = "0 GB";
    private string _totalIops = "0";
    private string _totalNodes = "0";

    private string _resultSummary = "";
    public string ResultSummary { get => _resultSummary; set { _resultSummary = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasResultSummary)); } }
    public bool HasResultSummary => !string.IsNullOrEmpty(_resultSummary);

    private string _diskRecommendations = "";
    public string DiskRecommendations { get => _diskRecommendations; set { _diskRecommendations = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasDiskRecommendations)); } }
    public bool HasDiskRecommendations => !string.IsNullOrEmpty(_diskRecommendations);

    private string _podRequests = "";
    public string PodRequests { get => _podRequests; set { _podRequests = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasPodRequests)); } }
    public bool HasPodRequests => !string.IsNullOrEmpty(_podRequests);

    public string TotalCpu { get => _totalCpu; set { _totalCpu = value; OnPropertyChanged(); } }
    public string TotalRam { get => _totalRam; set { _totalRam = value; OnPropertyChanged(); } }
    public string TotalStorage { get => _totalStorage; set { _totalStorage = value; OnPropertyChanged(); } }
    public string TotalIops { get => _totalIops; set { _totalIops = value; OnPropertyChanged(); } }
    public string TotalNodes { get => _totalNodes; set { _totalNodes = value; OnPropertyChanged(); } }

    public ObservableCollection<InfrastructureNode> ResultInfrastructure { get; private set; } = new();
    public ObservableCollection<ServiceComponent> ResultComponents { get; private set; } = new();

    private List<EnvironmentReport> _environments = new();
    public ObservableCollection<EnvironmentReport> Environments { get; private set; } = new();
    public bool HasEnvironments => Environments.Count > 1;
    // Окремий розділ «Інфраструктура (PROD)» показуємо ЛИШЕ коли немає розбивки по середовищах
    // (інакше PROD дублювався б: і у «ВМ по середовищах», і тут).
    public bool ShowProdInfraSection => !HasEnvironments;

    // Звірка розрахунку з вимогами документа D-AD-ADM-E (сервер БД, лише MS SQL).
    public ObservableCollection<DocComparisonItem> DocComparison { get; private set; } = new();
    public bool HasDocComparison => DocComparison.Count > 0;

    public ObservableCollection<CalculationHistoryItem> HistoryItems { get; private set; } = new();
    public bool HasHistory => HistoryItems.Count > 0;

    private int _selectedHistoryIndex = -1;
    public int SelectedHistoryIndex
    {
        get => _selectedHistoryIndex;
        set { _selectedHistoryIndex = value; OnPropertyChanged(); }
    }

    public ObservableCollection<ProjectModule> Modules { get; private set; }

    // Усі модулі (включно з App Server / ROBOT / Web) — кожен можна вмикати/вимикати окремо,
    // оскільки бувають різні конфігурації розгортання.
    public IEnumerable<ProjectModule> SelectableModules => Modules;

    // К-сть користувачів опціональних модулів (LMS/HR/ForceBPM) ОКРЕМО для DEV/TEST/PreProd.
    public ObservableCollection<EnvModuleCount> EnvModuleCounts { get; private set; } = new();
    public bool HasEnvModuleCounts => EnvModuleCounts.Count > 0;

    // Додаткові вузли (Сервер звітів / SQL Secondary / HAProxy) ОКРЕМО для DEV/TEST/PreProd.
    // PROD керується верхніми прапорцями IncludeReportingServer/IncludeSqlFailover/IncludeHaProxy.
    public ObservableCollection<EnvNodeToggle> EnvNodeToggles { get; private set; } = new()
    {
        new() { Key = "reporting",   NodeName = "Сервер звітів" },
        new() { Key = "failover",    NodeName = "SQL Secondary (Failover)" },
        new() { Key = "haproxy",     NodeName = "HAProxy" },
    };

    // Перебудова рядків к-сті модулів по середовищах зі збереженням раніше введених значень.
    private void RebuildEnvModuleCounts()
    {
        var existing = EnvModuleCounts.ToDictionary(r => r.ModuleName);
        var rows = Modules.Where(m => !m.IsMandatory).Select(m =>
            {
                var row = existing.TryGetValue(m.Name, out var old)
                    ? old
                    : new EnvModuleCount { ModuleName = m.Name, DevUsers = 10, TestUsers = 25, PredProdUsers = 50 };
                row.HasOwnUserCount = m.HasOwnUserCount; // ForceBPM — лише ✓, без к-сті
                return row;
            })
            .ToList();
        EnvModuleCounts = new ObservableCollection<EnvModuleCount>(rows);
        OnPropertyChanged(nameof(EnvModuleCounts));
        OnPropertyChanged(nameof(HasEnvModuleCounts));
    }

    #endregion

    #region Tab Headers

    public string TabMatrixHeader => _loc["tab.matrixTitle"];
    public string TabSetupHeader => _loc["tab.setupTitle"];
    public string TabResultsHeader => _loc["tab.resultsTitle"];

    #endregion

    #region Commands

    public ICommand CalculateCommand { get; private set; } = null!;
    public ICommand ExportExcelCommand { get; private set; } = null!;
    public ICommand ExportPdfCommand { get; private set; } = null!;
    public ICommand LangSwitchCommand { get; private set; } = null!;
    public ICommand ThemeSwitchCommand { get; private set; } = null!;
    public ICommand RecallHistoryCommand { get; private set; } = null!;

    private void InitializeCommands()
    {
        CalculateCommand = new AsyncRelayCommand(_ => CalculateAsync());
        ExportExcelCommand = new AsyncRelayCommand(_ => ExportExcelAsync());
        ExportPdfCommand = new AsyncRelayCommand(_ => ExportPdfAsync());
        LangSwitchCommand = new RelayCommand(_ => SwitchLanguage());
        ThemeSwitchCommand = new RelayCommand(_ => SwitchTheme());
        RecallHistoryCommand = new AsyncRelayCommand(_ => RecallHistoryAsync());

        LoadHistory();
    }

    #endregion

    #region Command Implementations

    private ProjectConfig GetConfig(int? userCountOverride = null)
    {
        if (!int.TryParse(UserCount, out var uc) || uc < 1) uc = 100;
        uc = Math.Clamp(uc, 1, 5000);
        if (!int.TryParse(ProdDbSizeGb, out var dbSize) || dbSize < 0) dbSize = 0;
        if (!int.TryParse(ProdContentDbSizeGb, out var contentSize) || contentSize < 0) contentSize = 0;
        return new ProjectConfig
        {
            ProjectName = "Project",
            UserCount = userCountOverride ?? uc,
            DeploymentType = DeploymentIndex switch
            {
                0 => DeploymentType.Kubernetes,
                1 => DeploymentType.Windows,
                _ => DeploymentType.Hybrid
            },
            LoadProfile = LoadProfile.Performance,
            DatabaseType = (DatabaseType)DatabaseIndex,
            IncludeReportingServer = IncludeReportingServer,
            IncludeSqlFailover = IncludeSqlFailover,
            IncludeHaProxy = IncludeHaProxy,
            DbSizeGb = dbSize,
            ContentDbSizeGb = contentSize,
            IncludeComponentsInReport = IncludeComponentsInReport
        };
    }

    private Task CalculateAsync()
    {
        try
        {
            var config = GetConfig();
            _engine.SetModules(Modules.ToList());
            var req = _engine.Calculate(config);
            _lastResult = req;
            ShowResults(req, config);
            _historyService.SaveToHistory(config, req);
            LoadHistory();
            SelectedTabIndex = 2;   // вкладка «Результати» (0=Матриця, 1=Параметри, 2=Результати)
            StatusText = string.Format(_loc["status.calculated"],
                config.UserCount, req.TotalCpu.ToString("F1"), req.TotalRamGb.ToString("F1"));
        }
        catch (Exception ex)
        {
            return ShowErrorAsync(ex, "error.calculation_failed");
        }
        return Task.CompletedTask;
    }

    private async Task ShowErrorAsync(Exception ex, string defaultKey)
    {
        var key = ex switch
        {
            FormatException or OverflowException or ArgumentException => "error.invalid_input",
            InvalidOperationException => defaultKey,
            _ => string.IsNullOrEmpty(defaultKey) ? "error.unknown" : defaultKey
        };
        var message = string.Format(_loc[key], ex.Message);
        await _dialogs.ErrorAsync(message, _loc["error.title"]);
    }

    private void ShowResults(ResourceRequirement req, ProjectConfig config)
    {
        // Спершу будуємо середовища — це додає бекап-резерв і до PROD (req),
        // тож KPI нижче вже відображають повний диск PROD з бекапом.
        var envSettings = _envBuilder.ParseSettings(
            DevUserCount, TestUserCount, PredProdUserCount,
            ProdDbSizeGb, DevDbSizeGb, TestDbSizeGb, PredProdDbSizeGb,
            DevContentDbSizeGb, TestContentDbSizeGb, PredProdContentDbSizeGb,
            IncludeDev, IncludeTest, IncludePredProd,
            out var resolvedTestDbSize, out var resolvedPredProdDbSize, out var resolvedDevDbSize);

        // Відображаємо застосовані значення (успадкування від PROD і підняття до мінімуму).
        DevDbSizeGb = resolvedDevDbSize.ToString();
        TestDbSizeGb = resolvedTestDbSize.ToString();
        PredProdDbSizeGb = resolvedPredProdDbSize.ToString();

        _environments = _envBuilder.Build(config, req, envSettings, Modules, EnvModuleCounts, EnvNodeToggles);
        Environments = new ObservableCollection<EnvironmentReport>(_environments);
        OnPropertyChanged(nameof(Environments));
        OnPropertyChanged(nameof(HasEnvironments));
        OnPropertyChanged(nameof(ShowProdInfraSection));

        TotalCpu = $"{req.TotalCpu:F1}";
        TotalRam = $"{req.TotalRamGb:F1} GB";
        TotalStorage = $"{req.TotalStorageGb} GB";
        // IOPS визначаються вузлом БД (не сумою) — показуємо саме його.
        TotalIops = $"{req.TotalIops}";
        TotalNodes = $"{req.Infrastructure.Sum(n => n.NodeCount)}";
        ResultSummary = BuildSummary(req, config);
        DiskRecommendations = BuildDiskRecommendations(req, config);
        PodRequests = req.PodCpu > 0
            ? string.Format(_loc["results.podRequests"], req.PodCpu.ToString("F1"), req.PodRamGb.ToString("F1"))
            : "";
        ResultInfrastructure = new ObservableCollection<InfrastructureNode>(req.Infrastructure);
        OnPropertyChanged(nameof(ResultInfrastructure));

        ResultComponents = new ObservableCollection<ServiceComponent>(req.Components);
        OnPropertyChanged(nameof(ResultComponents));

        DocComparison = new ObservableCollection<DocComparisonItem>(
            DocumentRequirements.Compare(req, config, MatrixRangesForProfile(config.LoadProfile)));
        OnPropertyChanged(nameof(DocComparison));
        OnPropertyChanged(nameof(HasDocComparison));
    }


    // Plain-language summary of what to provision, so the numbers read as understandable needs.
    private string BuildSummary(ResourceRequirement req, ProjectConfig config)
    {
        var deploy = config.DeploymentType switch
        {
            DeploymentType.Kubernetes => _loc["deploy.k8sName"],
            DeploymentType.Windows => _loc["deploy.windowsName"],
            _ => _loc["deploy.hybridName"]
        };
        var product = _loc["product.documentflow"];
        var db = config.DatabaseType switch
        {
            DatabaseType.PostgreSQL => "PostgreSQL",
            DatabaseType.Oracle => "Oracle 19c",
            _ => "MS SQL Server"
        };

        var sb = new System.Text.StringBuilder();
        sb.AppendLine(string.Format(_loc["results.summaryHeader"], config.UserCount, deploy, product, db));
        foreach (var n in req.Infrastructure.Where(n => n.NodeCount > 0))
        {
            var line = string.Format(_loc["results.summaryNode"],
                n.NodeCount, n.Name, n.Cpu, n.RamGb, n.TotalStorageGb, n.Os);
            // Версія/редакція СУБД для вузлів БД.
            if (!string.IsNullOrEmpty(n.DbVersion)) line += $" · {n.DbVersion}";
            sb.AppendLine(line);
        }
        var dbMib = req.Infrastructure
            .FirstOrDefault(n => n.Name.Contains("SQL", StringComparison.OrdinalIgnoreCase)
                || n.Name.Contains("PostgreSQL", StringComparison.OrdinalIgnoreCase)
                || n.Name.Contains("Oracle", StringComparison.OrdinalIgnoreCase))?.ThroughputMiBs ?? 0;
        sb.AppendLine(string.Format(_loc["results.summaryTotals"],
            req.TotalCpu.ToString("F1"), req.TotalRamGb.ToString("F1"), req.TotalStorageGb, req.TotalIops, dbMib));

        // Розподіл подів по worker-вузлах (а не лише перелік реплік).
        var pods = req.Components.Where(c => c.Cpu > 0).Sum(c => c.Replicas);
        if (pods > 0)
        {
            var workers = req.Infrastructure
                .Where(n => n.Name.Contains("Worker", StringComparison.OrdinalIgnoreCase))
                .Sum(n => n.NodeCount);
            var perNode = workers > 0 ? (int)Math.Ceiling((double)pods / workers) : pods;
            sb.Append(string.Format(_loc["results.summaryPods"], pods, workers, perNode));
        }
        return sb.ToString();
    }

    private string BuildDiskRecommendations(ResourceRequirement req, ProjectConfig config)
        => DiskAdvisor.Build(req, config, _loc);

    // Поточні (редаговані) діапазони MS SQL з матриці — єдиний профіль навантаження.
    private IEnumerable<UserLoadRange> MatrixRangesForProfile(LoadProfile profile)
        => MatrixVM.MsSqlRanges;

    private void LoadHistory()
    {
        HistoryItems = new ObservableCollection<CalculationHistoryItem>(_historyService.LoadHistory());
        OnPropertyChanged(nameof(HistoryItems));
        OnPropertyChanged(nameof(HasHistory));
    }

    private Task RecallHistoryAsync()
    {
        if (SelectedHistoryIndex < 0 || SelectedHistoryIndex >= HistoryItems.Count) return Task.CompletedTask;

        var item = HistoryItems[SelectedHistoryIndex];
        var config = item.Config;

        UserCount = config.UserCount.ToString();
        DeploymentIndex = config.DeploymentType switch
        {
            DeploymentType.Kubernetes => 0,
            DeploymentType.Windows => 1,
            _ => 2
        };

        if (config.SelectedModules.Count > 0)
        {
            foreach (var mod in Modules)
            {
                mod.IsEnabled = mod.IsMandatory || config.SelectedModules.Contains(mod.Name);
            }
            Modules = new ObservableCollection<ProjectModule>(Modules);
            OnPropertyChanged(nameof(Modules));
        }

        return CalculateAsync();
    }

    private void OnMatrixChanged()
    {
        _engine.ReloadModules();
        Modules = new ObservableCollection<ProjectModule>(_engine.Modules.ToClonedList());
        _engine.SetModules(Modules.ToList());
        OnPropertyChanged(nameof(Modules));
        OnDeploymentTypeChanged();
        RebuildEnvModuleCounts();
    }

    private void OnDeploymentTypeChanged()
    {
        var deploymentType = DeploymentIndex switch
        {
            0 => DeploymentType.Kubernetes,
            1 => DeploymentType.Windows,
            _ => DeploymentType.Hybrid
        };

        // Кожен модуль і кнопка керуються користувачем окремо — жодних примусових вмикань
        // чи блокувань залежно від типу розгортання. Користувач сам вирішує, що ввімкнути.

        // HAProxy — вільний перемикач у будь-якому типі розгортання.
        CanToggleHaProxy = true;

        var haproxy = EnvNodeToggles.FirstOrDefault(r => r.Key == "haproxy");
        if (haproxy != null) haproxy.IsEditable = true;

        Modules = new ObservableCollection<ProjectModule>(Modules);
        OnPropertyChanged(nameof(Modules));
        OnPropertyChanged(nameof(SelectableModules));

        var loc = _loc;
        var deployName = deploymentType switch
        {
            DeploymentType.Kubernetes => loc["deploy.k8sName"],
            DeploymentType.Windows => loc["deploy.windowsName"],
            _ => loc["deploy.hybridName"]
        };
        StatusText = string.Format(loc["status.deploymentChanged"], deployName);
    }

    private void ExportExcel()
    {
        _ = ExportExcelAsync();
    }

    private async Task ExportExcelAsync()
    {
        if (_lastResult == null) return;
        var path = await _files.PickSavePathAsync("resources.xlsx", "Excel files (*.xlsx)", ".xlsx");
        if (path is null) return;
        var cfg = GetConfig();
        var bytes = _results.ExportExcel(_lastResult, cfg, _environments, MatrixRangesForProfile(cfg.LoadProfile));
        System.IO.File.WriteAllBytes(path, bytes);
        StatusText = string.Format(_loc["status.saved"], path);
    }

    private void ExportPdf()
    {
        _ = ExportPdfAsync();
    }

    private async Task ExportPdfAsync()
    {
        if (_lastResult == null) return;
        var path = await _files.PickSavePathAsync("resources.pdf", "PDF files (*.pdf)", ".pdf");
        if (path is null) return;
        var cfg = GetConfig();
        var bytes = _results.ExportPdf(_lastResult, cfg, _environments, MatrixRangesForProfile(cfg.LoadProfile));
        System.IO.File.WriteAllBytes(path, bytes);
        StatusText = string.Format(_loc["status.saved"], path);
    }

    private void SwitchLanguage()
    {
        var loc = _loc;
        loc.LoadLanguage(loc.CurrentLang == "uk" ? "en" : "uk");
    }

    private void SwitchTheme()
    {
        IsDarkTheme = !IsDarkTheme;
        _theme?.SetDark(IsDarkTheme);
    }

    #endregion

    #region INotifyPropertyChanged

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    #endregion

    #region Helper

    public ISizingEngine Engine => _engine;
    public ResourceRequirement? LastResult => _lastResult;

    #endregion
}

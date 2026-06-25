using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using AIResourceCalculator.Data;
using AIResourceCalculator.Interfaces;
using AIResourceCalculator.Localization;
using AIResourceCalculator.Models;
using AIResourceCalculator.Services;

namespace AIResourceCalculator.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly ISizingEngine _engine;
    private readonly ICalculationHistoryService _historyService;
    private readonly ILocalizationService _loc;
    private readonly ResultsPresenter _results;
    private ResourceRequirement? _lastResult;
    private ResourceRequirement? _lastResultPerf;

    public MatrixViewModel MatrixVM { get; }

    private string _userCount = "100";
    private int _deploymentIndex;
    private int _productIndex;
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
        ISizingEngine engine)
    {
        _loc = localization;
        _historyService = historyService;
        _results = results;
        _engine = engine;

        MatrixVM = new MatrixViewModel(_loc, matrixManager);

        _engine.SetProductType(ProductType.Standard);

        Modules = new ObservableCollection<ProjectModule>(_engine.Modules);
        _statusText = _loc["status.ready"];

        MatrixVM.LoadMatrixGrids();

        _loc.PropertyChanged += (_, _) => OnLanguageChanged();
        MatrixVM.MatrixChanged += OnMatrixChanged;

        InitializeCommands();

        OnDeploymentTypeChanged();
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

    public int ProductIndex
    {
        get => _productIndex;
        set
        {
            _productIndex = value;
            OnPropertyChanged();
            OnProductTypeChanged();
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
    private string _backupDays = "7";
    private string _dbDataSizeGb = "20";

    // Припущення про стиснення бекапу. На практиці точний коефіцієнт наперед невідомий, тож його
    // не виносимо в UI — беремо типове для стисненого бекапу СУБД значення (50%).
    private const double DefaultBackupCompression = 0.5;

    public bool IncludeDev { get => _includeDev; set { _includeDev = value; OnPropertyChanged(); } }
    public bool IncludeTest { get => _includeTest; set { _includeTest = value; OnPropertyChanged(); } }
    public bool IncludePredProd { get => _includePredProd; set { _includePredProd = value; OnPropertyChanged(); } }
    public string DevUserCount { get => _devUserCount; set { _devUserCount = value; OnPropertyChanged(); } }
    public string TestUserCount { get => _testUserCount; set { _testUserCount = value; OnPropertyChanged(); } }
    public string PredProdUserCount { get => _predProdUserCount; set { _predProdUserCount = value; OnPropertyChanged(); } }
    public string BackupDays { get => _backupDays; set { _backupDays = value; OnPropertyChanged(); } }
    // Обсяг реляційних даних БД (ГБ) — визначає диски Data/Logs та резерв під бекап.
    public string DbDataSizeGb { get => _dbDataSizeGb; set { _dbDataSizeGb = value; OnPropertyChanged(); } }

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

    private int _selectedTabIndex;
    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set { _selectedTabIndex = value; OnPropertyChanged(); }
    }

    #endregion

    #region Matrix Properties (delegated to MatrixVM)

    public ObservableCollection<UserLoadRange> MsSqlRanges => MatrixVM.MsSqlRanges;
    public ObservableCollection<UserLoadRange> MsSqlPerformanceRanges => MatrixVM.MsSqlPerformanceRanges;
    public ObservableCollection<ServiceComponent> K8sStandardComponents => MatrixVM.K8sStandardComponents;
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

    // Лише опціональні модулі — обов'язкові (App Server / ROBOT / Web) у вибір не виносимо.
    public IEnumerable<ProjectModule> SelectableModules => Modules.Where(m => !m.IsMandatory);

    #endregion

    #region Tab Headers

    public string TabMatrixHeader => _loc["tab.matrixTitle"];
    public string TabSetupHeader => _loc["tab.setupTitle"];
    public string TabResultsHeader => _loc["tab.resultsTitle"];

    #endregion

    #region Commands

    public ICommand CalculateCommand { get; private set; } = null!;
    public ICommand ExportExcelCommand { get; private set; } = null!;
    public ICommand ExportXmlCommand { get; private set; } = null!;
    public ICommand ExportHtmlCommand { get; private set; } = null!;
    public ICommand LangSwitchCommand { get; private set; } = null!;
    public ICommand RecallHistoryCommand { get; private set; } = null!;

    private void InitializeCommands()
    {
        CalculateCommand = new RelayCommand(_ => Calculate());
        ExportExcelCommand = new RelayCommand(_ => ExportExcel());
        ExportXmlCommand = new RelayCommand(_ => ExportXml());
        ExportHtmlCommand = new RelayCommand(_ => ExportHtml());
        LangSwitchCommand = new RelayCommand(_ => SwitchLanguage());
        RecallHistoryCommand = new RelayCommand(_ => RecallHistory());

        LoadHistory();
    }

    #endregion

    #region Command Implementations

    private ProjectConfig GetConfig(int? userCountOverride = null)
    {
        if (!int.TryParse(UserCount, out var uc) || uc < 1) uc = 100;
        uc = Math.Clamp(uc, 1, 5000);
        var productType = ProductIndex == 0 ? ProductType.Standard : ProductType.DocumentFlow;
        var loadProfile = productType == ProductType.DocumentFlow ? LoadProfile.Performance : LoadProfile.Basic;
        if (!int.TryParse(DbDataSizeGb, out var dbData) || dbData < 0) dbData = 20;
        return new ProjectConfig
        {
            ProjectName = "Project",
            UserCount = userCountOverride ?? uc,
            DbDataSizeGb = Math.Clamp(dbData, 0, 1_000_000),
            DeploymentType = DeploymentIndex switch
            {
                0 => DeploymentType.Kubernetes,
                1 => DeploymentType.Windows,
                _ => DeploymentType.Hybrid
            },
            ProductType = productType,
            LoadProfile = loadProfile,
            DatabaseType = (DatabaseType)DatabaseIndex
        };
    }

    private void Calculate()
    {
        try
        {
            var config = GetConfig();
            var (req, perfReq) = CalculateInternal(config);
            _lastResult = req;
            _lastResultPerf = perfReq;
            ShowResults(req, perfReq, config);
            _historyService.SaveToHistory(config, req);
            LoadHistory();
            SelectedTabIndex = 2;
            StatusText = string.Format(_loc["status.calculated"],
                config.UserCount, req.TotalCpu.ToString("F1"), req.TotalRamGb.ToString("F1"));
        }
        catch (Exception ex)
        {
            ShowError(ex, "error.calculation_failed");
        }
    }

    private void ShowError(Exception ex, string defaultKey)
    {
        var key = ex switch
        {
            FormatException or OverflowException or ArgumentException => "error.invalid_input",
            InvalidOperationException => defaultKey,
            _ => string.IsNullOrEmpty(defaultKey) ? "error.unknown" : defaultKey
        };
        var message = string.Format(_loc[key], ex.Message);
        MessageBox.Show(message, _loc["error.title"], MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private (ResourceRequirement req, ResourceRequirement? perfReq) CalculateInternal(ProjectConfig config)
    {
        _engine.SetModules(Modules.ToList());
        var req = _engine.Calculate(config);
        ResourceRequirement? perfReq = null;
        var otherProduct = config.ProductType == ProductType.Standard ? ProductType.DocumentFlow : ProductType.Standard;
        var otherProfile = config.ProductType == ProductType.Standard ? LoadProfile.Performance : LoadProfile.Basic;
        var otherConfig = new ProjectConfig
        {
            ProjectName = config.ProjectName,
            UserCount = config.UserCount,
            DeploymentType = config.DeploymentType,
            ProductType = otherProduct,
            LoadProfile = otherProfile,
            DatabaseType = config.DatabaseType
        };
        _engine.SetProductType(otherProduct);
        _engine.SetModules(_engine.Modules.ToClonedList());
        perfReq = _engine.Calculate(otherConfig);
        _engine.SetProductType(config.ProductType);
        _engine.SetModules(Modules.ToList());
        return (req, perfReq);
    }

    private void ShowResults(ResourceRequirement req, ResourceRequirement? perfReq, ProjectConfig config)
    {
        // Спершу будуємо середовища — це додає бекап-резерв і до PROD (req),
        // тож KPI нижче вже відображають повний диск PROD з бекапом.
        BuildEnvironments(config, req);

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

    private EnvironmentSettings GetEnvSettings()
    {
        if (!int.TryParse(DevUserCount, out var dev) || dev < 1) dev = 10;
        if (!int.TryParse(TestUserCount, out var test) || test < 1) test = 25;
        if (!int.TryParse(PredProdUserCount, out var pp) || pp < 1) pp = 50;
        if (!int.TryParse(BackupDays, out var days) || days < 1) days = 7;
        return new EnvironmentSettings
        {
            IncludeDev = IncludeDev,
            IncludeTest = IncludeTest,
            IncludePredProd = IncludePredProd,
            DevUserCount = Math.Clamp(dev, 1, 5000),
            TestUserCount = Math.Clamp(test, 1, 5000),
            PredProdUserCount = Math.Clamp(pp, 1, 5000),
            BackupRetentionDays = Math.Clamp(days, 1, 365),
            BackupCompression = DefaultBackupCompression
        };
    }

    // PROD завжди; DEV/TEST/PreProd додаються за вибором. КОЖНЕ середовище рахується рушієм
    // ОКРЕМО за власною кількістю користувачів (з урахуванням к-сті користувачів по модулях) —
    // як у Excel-табличці, а не масштабуванням PROD. Бекап-резерв (від обсягу даних БД)
    // додається до кожного середовища, включно з PROD. Редакцію СУБД визначає Environment
    // (non-prod → Developer Edition).
    private void BuildEnvironments(ProjectConfig config, ResourceRequirement prodReq)
    {
        var s = GetEnvSettings();
        int reserve = EnvironmentScaler.BackupReserveGb(config.DbDataSizeGb, s);

        // PROD отримує бекап-резерв так само, як решта середовищ.
        EnvironmentScaler.AddBackupReserve(prodReq, reserve);

        EnvironmentReport BuildEnv(DeployEnvironment env, string name, int users)
        {
            var envConfig = new ProjectConfig
            {
                ProjectName = config.ProjectName, UserCount = users,
                DeploymentType = config.DeploymentType, ProductType = config.ProductType,
                LoadProfile = config.LoadProfile, DatabaseType = config.DatabaseType,
                DbDataSizeGb = config.DbDataSizeGb, Environment = env
            };
            _engine.SetModules(Modules.ToList());
            var req = _engine.Calculate(envConfig);
            EnvironmentScaler.AddBackupReserve(req, reserve);
            return new EnvironmentReport { Environment = env, Name = name, UserCount = users, Requirement = req };
        }

        var reports = new List<EnvironmentReport>
        {
            new() { Environment = DeployEnvironment.Prod, Name = "PROD", UserCount = config.UserCount, Requirement = prodReq }
        };

        if (s.IncludeDev) reports.Add(BuildEnv(DeployEnvironment.Dev, "DEV", s.DevUserCount));
        if (s.IncludeTest) reports.Add(BuildEnv(DeployEnvironment.Test, "TEST", s.TestUserCount));
        if (s.IncludePredProd) reports.Add(BuildEnv(DeployEnvironment.PredProd, "PreProd", s.PredProdUserCount));

        // Відновити стан рушія до PROD-конфігурації для подальших дій.
        _engine.SetModules(Modules.ToList());

        _environments = reports;
        Environments = new ObservableCollection<EnvironmentReport>(reports);
        OnPropertyChanged(nameof(Environments));
        OnPropertyChanged(nameof(HasEnvironments));
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
        var product = config.ProductType == ProductType.Standard ? _loc["product.standard"] : _loc["product.documentflow"];
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

    // Поточні (редаговані) діапазони MS SQL з матриці для активного профілю навантаження —
    // використовуються у звірці для стовпця «За матрицею».
    private IEnumerable<UserLoadRange> MatrixRangesForProfile(LoadProfile profile)
        => profile == LoadProfile.Performance ? MatrixVM.MsSqlPerformanceRanges : MatrixVM.MsSqlRanges;

    private void LoadHistory()
    {
        HistoryItems = new ObservableCollection<CalculationHistoryItem>(_historyService.LoadHistory());
        OnPropertyChanged(nameof(HistoryItems));
        OnPropertyChanged(nameof(HasHistory));
    }

    private void RecallHistory()
    {
        if (SelectedHistoryIndex < 0 || SelectedHistoryIndex >= HistoryItems.Count) return;

        var item = HistoryItems[SelectedHistoryIndex];
        var config = item.Config;

        UserCount = config.UserCount.ToString();
        DeploymentIndex = config.DeploymentType switch
        {
            DeploymentType.Kubernetes => 0,
            DeploymentType.Windows => 1,
            _ => 2
        };
        ProductIndex = config.ProductType == ProductType.DocumentFlow ? 1 : 0;

        if (config.SelectedModules.Count > 0)
        {
            foreach (var mod in Modules)
            {
                mod.IsEnabled = mod.IsMandatory || config.SelectedModules.Contains(mod.Name);
            }
            Modules = new ObservableCollection<ProjectModule>(Modules);
            OnPropertyChanged(nameof(Modules));
        }

        Calculate();
    }

    private void OnMatrixChanged()
    {
        var productType = ProductIndex == 0 ? ProductType.Standard : ProductType.DocumentFlow;
        _engine.SetProductType(productType);
        Modules = new ObservableCollection<ProjectModule>(_engine.Modules.ToClonedList());
        _engine.SetModules(Modules.ToList());
        OnPropertyChanged(nameof(Modules));
        OnDeploymentTypeChanged();
    }

    private void OnProductTypeChanged()
    {
        var productType = ProductIndex == 0 ? ProductType.Standard : ProductType.DocumentFlow;
        _engine.SetProductType(productType);
        Modules = new ObservableCollection<ProjectModule>(_engine.Modules.ToClonedList());
        _engine.SetModules(Modules.ToList());
        OnDeploymentTypeChanged();
        var loc = _loc;
        StatusText = string.Format(loc["status.productChanged"],
            productType == ProductType.Standard ? loc["product.standard"] : loc["product.documentflow"]);
    }

    private void OnDeploymentTypeChanged()
    {
        var deploymentType = DeploymentIndex switch
        {
            0 => DeploymentType.Kubernetes,
            1 => DeploymentType.Windows,
            _ => DeploymentType.Hybrid
        };

        foreach (var mod in Modules)
        {
            // Обов'язкові сервіси (App Server / ROBOT / Web) — завжди ввімкнені.
            if (mod.IsMandatory) { mod.IsEnabled = true; continue; }

            // Чи застосовний модуль до цього типу розгортання.
            bool applicable = deploymentType switch
            {
                DeploymentType.Kubernetes => !mod.Name.Contains("Windows"),
                DeploymentType.Windows => !mod.IsKubernetesOnly,
                _ => true
            };
            // Незастосовні вимикаємо; застосовні зберігають свій (типовий або обраний) стан —
            // зокрема LMS/HR лишаються вимкненими за замовчуванням.
            if (!applicable) mod.IsEnabled = false;
        }

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

    private void ExportXml()
    {
        if (_lastResult == null) return;
        var cfg = GetConfig();
        ExportConfig(_results.ExportXml(_lastResult, cfg, _environments, MatrixRangesForProfile(cfg.LoadProfile)), "xml");
    }

    private void ExportHtml()
    {
        if (_lastResult == null) return;
        var cfg = GetConfig();
        ExportConfig(_results.ExportHtml(_lastResult, cfg, _environments, MatrixRangesForProfile(cfg.LoadProfile)), "html");
    }

    private void ExportExcel()
    {
        if (_lastResult == null) return;
        var saveDialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Excel files (*.xlsx)|*.xlsx",
            FileName = "resources.xlsx"
        };
        if (saveDialog.ShowDialog() == true)
        {
            var cfg = GetConfig();
            var bytes = _results.ExportExcel(_lastResult, cfg, _environments, MatrixRangesForProfile(cfg.LoadProfile));
            System.IO.File.WriteAllBytes(saveDialog.FileName, bytes);
            StatusText = string.Format(_loc["status.saved"], saveDialog.FileName);
        }
    }

    private void ExportConfig(string content, string extension)
    {
        var saveDialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = extension switch
            {
                "xml" => "XML files (*.xml)|*.xml",
                "html" => "HTML files (*.html)|*.html",
                _ => $"*{extension}|*{extension}"
            },
            FileName = $"resources.{extension}"
        };
        if (saveDialog.ShowDialog() == true)
        {
            System.IO.File.WriteAllText(saveDialog.FileName, content);
            StatusText = string.Format(_loc["status.saved"], saveDialog.FileName);
        }
    }

    private void SwitchLanguage()
    {
        var loc = _loc;
        loc.LoadLanguage(loc.CurrentLang == "uk" ? "en" : "uk");
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
    public ResourceRequirement? LastResultPerf => _lastResultPerf;

    #endregion
}
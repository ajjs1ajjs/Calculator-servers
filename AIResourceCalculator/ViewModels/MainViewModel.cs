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
    private readonly IAiAdvisorService _advisor;
    private readonly IValidationEngine _validator;
    private readonly IDataService _dataService;
    private readonly ICalculationHistoryService _historyService;
    private readonly IThemeService _themeService;
    private readonly ILocalizationService _loc;
    private readonly MatrixManager _matrixManager;
    private readonly ResultsPresenter _results;
    private SizingMatrix _matrix;
    private AiSettings _aiSettings;
    private ResourceRequirement? _lastResult;
    private ResourceRequirement? _lastResultPerf;

    private string _userCount = "100";
    private int _deploymentIndex;
    private int _productIndex;
    private int _databaseIndex;
    private string _statusText = "";
    private string _aiBadgeText = "";
    private string _aiBadgeResultText = "";
    private string _aiRecCountText = "";
    private string _langFlag = "\U0001F1FA\U0001F1E6";
    private string _langName = "Українська";
    private string _aiNoDataText = "";
    private bool _isAiNoDataVisible = true;
    private bool _isAiRecListVisible;
    private bool _isDiagramVisible;
    private bool _isQuickRecVisible;
    private string _quickRecText = "";
    private bool _isDarkTheme;

    public MainViewModel(
        ISizingEngine? engine = null,
        IAiAdvisorService? advisor = null,
        IValidationEngine? validator = null,
        IDataService? dataService = null,
        ICalculationHistoryService? historyService = null,
        IThemeService? themeService = null,
        ILocalizationService? localization = null)
    {
        _loc = localization ?? LocalizationService.Instance;
        _dataService = dataService ?? new DataService();
        _historyService = historyService ?? new CalculationHistoryService();
        _themeService = themeService ?? new ThemeService();
        _advisor = advisor ?? new AiAdvisorService();
        _validator = validator ?? new ValidationEngine();
        _matrixManager = new MatrixManager(dataService ?? new DataService());
        _results = new ResultsPresenter();
        _matrix = _matrixManager.Matrix;
        _engine = engine ?? new SizingEngine(_matrix);
        _engine.SetProductType(ProductType.Standard);
        _aiSettings = AiSettings.Load();
        _advisor.UpdateSettings(_aiSettings);

        _isDarkTheme = _themeService.IsDark;

        Modules = new ObservableCollection<ProjectModule>(_engine.Modules);
        _statusText = _loc["status.ready"];
        _aiNoDataText = _loc["ai.noData"];

        LoadMatrixGrids();
        UpdateAiBadge();

        _loc.PropertyChanged += (_, _) => OnLanguageChanged();

        InitializeCommands();

        OnDeploymentTypeChanged();
    }

    private void OnLanguageChanged()
    {
        var loc = _loc;
        LangFlag = loc.Flag;
        LangName = loc.LangName;
        StatusText = loc["status.ready"];
        UpdateAiBadge();
        OnPropertyChanged(nameof(TabMatrixHeader));
        OnPropertyChanged(nameof(TabSetupHeader));
        OnPropertyChanged(nameof(TabResultsHeader));
        OnPropertyChanged(nameof(TabAssistantHeader));
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

    public string StatusText
    {
        get => _statusText;
        set { _statusText = value; OnPropertyChanged(); }
    }

    public string AiBadgeText
    {
        get => _aiBadgeText;
        set { _aiBadgeText = value; OnPropertyChanged(); }
    }

    public string AiBadgeResultText
    {
        get => _aiBadgeResultText;
        set { _aiBadgeResultText = value; OnPropertyChanged(); }
    }

    public string AiRecCountText
    {
        get => _aiRecCountText;
        set { _aiRecCountText = value; OnPropertyChanged(); }
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

    public string AiNoDataText
    {
        get => _aiNoDataText;
        set { _aiNoDataText = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsAiNoDataVisible)); }
    }

    public bool IsAiNoDataVisible
    {
        get => _isAiNoDataVisible;
        set { _isAiNoDataVisible = value; OnPropertyChanged(); }
    }

    public bool IsAiRecListVisible
    {
        get => _isAiRecListVisible;
        set { _isAiRecListVisible = value; OnPropertyChanged(); }
    }

    public bool IsDiagramVisible
    {
        get => _isDiagramVisible;
        set { _isDiagramVisible = value; OnPropertyChanged(); }
    }

    public bool IsDarkTheme
    {
        get => _isDarkTheme;
        set { _isDarkTheme = value; OnPropertyChanged(); OnPropertyChanged(nameof(ThemeIcon)); }
    }

    public string ThemeIcon => _isDarkTheme ? "\u2600" : "\uD83C\uDF19";

    private int _selectedTabIndex;
    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set { _selectedTabIndex = value; OnPropertyChanged(); }
    }

    public bool IsQuickRecVisible
    {
        get => _isQuickRecVisible;
        set { _isQuickRecVisible = value; OnPropertyChanged(); }
    }

    public string QuickRecText
    {
        get => _quickRecText;
        set { _quickRecText = value; OnPropertyChanged(); }
    }

    private string _assistantPrompt = "";
    public string AssistantPrompt
    {
        get => _assistantPrompt;
        set { _assistantPrompt = value; OnPropertyChanged(); }
    }

    private string _assistantResult = "";
    public string AssistantResult
    {
        get => _assistantResult;
        set { _assistantResult = value; OnPropertyChanged(); }
    }

    private bool _isAssistantResultVisible;
    public bool IsAssistantResultVisible
    {
        get => _isAssistantResultVisible;
        set { _isAssistantResultVisible = value; OnPropertyChanged(); }
    }

    private ProjectConfig? _parsedConfig;
    private List<string>? _parsedModules;

    #endregion

    #region Matrix Properties

    public ObservableCollection<UserLoadRange> MsSqlRanges { get; private set; } = new();
    public ObservableCollection<UserLoadRange> MsSqlPerformanceRanges { get; private set; } = new();
    public ObservableCollection<ServiceComponent> K8sStandardComponents { get; private set; } = new();
    public ObservableCollection<ServiceComponent> K8sDocumentFlowComponents { get; private set; } = new();
    public ObservableCollection<ServiceComponent> K8sComponents { get; private set; } = new();
    public ObservableCollection<InfrastructureNode> InfraNodes { get; private set; } = new();
    public ObservableCollection<ProjectModule> Modules { get; private set; }

    #endregion

    #region Result Properties

    private string _totalCpu = "0";
    private string _totalRam = "0 GB";
    private string _totalStorage = "0 GB";
    private string _totalIops = "0";
    private string _totalNodes = "0";

    public string TotalCpu { get => _totalCpu; set { _totalCpu = value; OnPropertyChanged(); } }
    public string TotalRam { get => _totalRam; set { _totalRam = value; OnPropertyChanged(); } }
    public string TotalStorage { get => _totalStorage; set { _totalStorage = value; OnPropertyChanged(); } }
    public string TotalIops { get => _totalIops; set { _totalIops = value; OnPropertyChanged(); } }
    public string TotalNodes { get => _totalNodes; set { _totalNodes = value; OnPropertyChanged(); } }

    public ObservableCollection<InfrastructureNode> ResultInfrastructure { get; private set; } = new();
    public ObservableCollection<InfrastructureNode> AiInfrastructure { get; private set; } = new();
    public ObservableCollection<AiRecommendation> AiRecommendations { get; private set; } = new();
    public ObservableCollection<ServiceComponent> ResultComponents { get; private set; } = new();
    public ObservableCollection<ValidationResult> ValidationResults { get; private set; } = new();

    public ObservableCollection<CalculationHistoryItem> HistoryItems { get; private set; } = new();
    public bool HasHistory => HistoryItems.Count > 0;

    public ObservableCollection<ServiceComponent> ScalingData { get; private set; } = new();
    private bool _isScalingVisible;
    public bool IsScalingVisible
    {
        get => _isScalingVisible;
        set { _isScalingVisible = value; OnPropertyChanged(); }
    }

    private int _selectedHistoryIndex = -1;
    public int SelectedHistoryIndex
    {
        get => _selectedHistoryIndex;
        set { _selectedHistoryIndex = value; OnPropertyChanged(); }
    }

    #endregion

    #region Tab Headers

    public string TabMatrixHeader => _loc["tab.matrixTitle"];
    public string TabSetupHeader => _loc["tab.setupTitle"];
    public string TabResultsHeader => _loc["tab.resultsTitle"];
    public string TabAssistantHeader => _loc["tab.assistantTitle"];

    #endregion

    #region Commands

    public ICommand CalculateCommand { get; private set; } = null!;
    public ICommand ImportMatrixCommand { get; private set; } = null!;
    public ICommand SaveMatrixCommand { get; private set; } = null!;
    public ICommand ResetMatrixCommand { get; private set; } = null!;
    public ICommand ExportTxtCommand { get; private set; } = null!;
    public ICommand ExportHtmlCommand { get; private set; } = null!;
    public ICommand ShowDiagramCommand { get; private set; } = null!;
    public ICommand ExportSvgCommand { get; private set; } = null!;
    public ICommand ExportMermaidCommand { get; private set; } = null!;
    public ICommand LangSwitchCommand { get; private set; } = null!;
    public ICommand ToggleThemeCommand { get; private set; } = null!;
    public ICommand AnalyzeAiCommand { get; private set; } = null!;
    public ICommand AiSettingsCommand { get; private set; } = null!;
    public ICommand AnalyzePromptCommand { get; private set; } = null!;
    public ICommand ApplyParsedConfigCommand { get; private set; } = null!;
    public ICommand UseTemplateCommand { get; private set; } = null!;
    public ICommand RecallHistoryCommand { get; private set; } = null!;
    public ICommand ShowScalingCommand { get; private set; } = null!;

    private void InitializeCommands()
    {
        CalculateCommand = new RelayCommand(_ => Calculate());
        ImportMatrixCommand = new RelayCommand(_ => ImportMatrix());
        SaveMatrixCommand = new RelayCommand(_ => SaveMatrix());
        ResetMatrixCommand = new RelayCommand(_ => ResetMatrix());
        ExportTxtCommand = new RelayCommand(_ => ExportTxt());
        ExportHtmlCommand = new RelayCommand(_ => ExportHtml());
        ShowDiagramCommand = new RelayCommand(_ => ShowDiagram());
        ExportSvgCommand = new RelayCommand(_ => ExportSvg());
        ExportMermaidCommand = new RelayCommand(_ => ExportMermaid());
        AiSettingsCommand = new RelayCommand(_ => OpenAiSettings());
        LangSwitchCommand = new RelayCommand(_ => SwitchLanguage());
        ToggleThemeCommand = new RelayCommand(_ => ToggleTheme());
        AnalyzeAiCommand = new RelayCommand(async _ => await AnalyzeWithAiAsync());
        AnalyzePromptCommand = new RelayCommand(_ => AnalyzePrompt());
        ApplyParsedConfigCommand = new RelayCommand(_ => ApplyParsedConfig());
        UseTemplateCommand = new RelayCommand(p => UseTemplate(p?.ToString() ?? ""));
        RecallHistoryCommand = new RelayCommand(_ => RecallHistory());
        ShowScalingCommand = new RelayCommand(_ => ShowScaling());

        LoadHistory();
    }

    #endregion

    #region Command Implementations

    private ProjectConfig GetConfig(int? userCountOverride = null)
    {
        if (!int.TryParse(UserCount, out var uc) || uc < 1) uc = 100;
        var productType = ProductIndex == 0 ? ProductType.Standard : ProductType.DocumentFlow;
        var loadProfile = productType == ProductType.DocumentFlow ? LoadProfile.Performance : LoadProfile.Basic;
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
            ShowResults(req, perfReq);
            _historyService.SaveToHistory(config, req);
            LoadHistory();
            SelectedTabIndex = 2;
            StatusText = string.Format(_loc["status.calculated"],
                config.UserCount, req.TotalCpu.ToString("F1"), req.TotalRamGb.ToString("F1"));
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
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
            LoadProfile = otherProfile
        };
        _engine.SetProductType(otherProduct);
        var otherModules = _engine.Modules.Select(m => m.Clone()).ToList();
        _engine.SetModules(otherModules);
        perfReq = _engine.Calculate(otherConfig);
        _engine.SetProductType(config.ProductType);
        _engine.SetModules(Modules.ToList());
        return (req, perfReq);
    }

    private void ShowResults(ResourceRequirement req, ResourceRequirement? perfReq)
    {
        TotalCpu = $"{req.TotalCpu:F1}";
        TotalRam = $"{req.TotalRamGb:F1} GB";
        TotalStorage = $"{req.TotalStorageGb} GB";
        TotalIops = $"{req.TotalIops}";
        TotalNodes = $"{req.Infrastructure.Sum(n => n.NodeCount)}";
        ResultInfrastructure = new ObservableCollection<InfrastructureNode>(req.Infrastructure);
        OnPropertyChanged(nameof(ResultInfrastructure));

        ResultComponents = new ObservableCollection<ServiceComponent>(req.Components);
        OnPropertyChanged(nameof(ResultComponents));

        if (_lastResult != null && _lastResultPerf != null)
        {
            ValidationResults = new ObservableCollection<ValidationResult>(_results.CompareProfiles(_lastResult, _lastResultPerf));
            OnPropertyChanged(nameof(ValidationResults));
        }

        AiNoDataText = _loc["ai.noData"];
        IsAiNoDataVisible = true;
        IsAiRecListVisible = false;
        IsQuickRecVisible = false;
        AiRecommendations = new ObservableCollection<AiRecommendation>();
        OnPropertyChanged(nameof(AiRecommendations));
        AiBadgeResultText = "";
        AiRecCountText = "";
    }

    public async Task AnalyzeWithAiAsync()
    {
        if (_lastResult == null) return;

        AiNoDataText = _loc["results.aiAnalyzing"];
        IsAiNoDataVisible = true;
        IsAiRecListVisible = false;
        IsQuickRecVisible = false;

        var dual = await _advisor.AnalyzeAsync(_lastResult, GetConfig(), _lastResultPerf);
        var balance = dual.Balance;

        if (balance.Recommendations.Count > 0)
        {
            IsAiNoDataVisible = false;
            IsAiRecListVisible = true;
            IsQuickRecVisible = true;

            AiRecommendations = new ObservableCollection<AiRecommendation>(balance.Recommendations);
            AiInfrastructure = new ObservableCollection<InfrastructureNode>(balance.Infrastructure);
            OnPropertyChanged(nameof(AiRecommendations));
            OnPropertyChanged(nameof(AiInfrastructure));

            var totalSavings = balance.Recommendations.Sum(r => r.PotentialSavings);
            var savingsText = totalSavings > 0
                ? " | " + string.Format(_loc["results.savings"], (int)totalSavings)
                : "";
            AiBadgeResultText = $"{balance.Recommendations.Count} rec{savingsText}";
            AiRecCountText = _loc.CurrentLang == "uk"
                ? $"📋 {balance.Recommendations.Count} рекомендацій"
                : $"📋 {balance.Recommendations.Count} recommendations";
        }
        else
        {
            AiNoDataText = _loc["ai.noData"];
            IsAiNoDataVisible = true;
        }
    }

    private void AnalyzePrompt()
    {
        if (string.IsNullOrWhiteSpace(AssistantPrompt)) return;

        try
        {
            var parser = new PromptParserService();
            var (config, modules) = parser.Parse(AssistantPrompt);
            _parsedConfig = config;
            _parsedModules = modules;

            var loc = _loc;
            var deployName = config.DeploymentType switch
            {
                DeploymentType.Kubernetes => loc["deploy.k8sName"],
                DeploymentType.Windows => loc["deploy.windowsName"],
                _ => loc["deploy.hybridName"]
            };
            var productName = config.ProductType == ProductType.DocumentFlow
                ? loc["product.documentflow"] : loc["product.standard"];

            var result = $"Users: {config.UserCount}\n" +
                         $"Deployment: {deployName}\n" +
                         $"Product: {productName}\n" +
                         $"Modules: {string.Join(", ", modules)}";

            if (loc.CurrentLang == "uk")
                result = $"Користувачів: {config.UserCount}\n" +
                         $"Розгортання: {deployName}\n" +
                         $"Продукт: {productName}\n" +
                         $"Модулі: {string.Join(", ", modules)}";

            AssistantResult = result;
            IsAssistantResultVisible = true;
            StatusText = loc["assistant.analyze"];
        }
        catch (Exception ex)
        {
            AssistantResult = $"Error: {ex.Message}";
            IsAssistantResultVisible = true;
        }
    }

    private void ApplyParsedConfig()
    {
        if (_parsedConfig == null) return;

        UserCount = _parsedConfig.UserCount.ToString();
        DeploymentIndex = _parsedConfig.DeploymentType switch
        {
            DeploymentType.Kubernetes => 0,
            DeploymentType.Windows => 1,
            _ => 2
        };
        ProductIndex = _parsedConfig.ProductType == ProductType.DocumentFlow ? 1 : 0;

        if (_parsedModules != null)
        {
            foreach (var mod in Modules)
            {
                mod.IsEnabled = _parsedModules.Contains(mod.Name);
            }
            Modules = new ObservableCollection<ProjectModule>(Modules);
            OnPropertyChanged(nameof(Modules));
        }

        SelectedTabIndex = 1;
        Calculate();

        StatusText = string.Format(_loc["status.applied"],
            _parsedConfig.UserCount, _parsedModules?.Count ?? 0);
    }

    private void UseTemplate(string template)
    {
        var loc = _loc;
        var templates = new Dictionary<string, string>
        {
            ["tpl1"] = loc.CurrentLang == "uk"
                ? "система на 200 користувачів з LMS та HR Portal"
                : "system for 200 users with LMS and HR Portal",
            ["tpl2"] = loc.CurrentLang == "uk"
                ? "високонавантажена система на 1000 користувачів"
                : "high-load system for 1000 users",
            ["tpl3"] = loc.CurrentLang == "uk"
                ? "мінімальна система на 25 користувачів"
                : "minimal system for 25 users"
        };

        if (templates.TryGetValue(template, out var text))
        {
            AssistantPrompt = text;
            AnalyzePrompt();
        }
    }

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
                mod.IsEnabled = config.SelectedModules.Contains(mod.Name);
            }
            Modules = new ObservableCollection<ProjectModule>(Modules);
            OnPropertyChanged(nameof(Modules));
        }

        Calculate();
    }

    private void ShowScaling()
    {
        var config = GetConfig();
        var points = ResultsPresenter.ComputeScaling(config, new List<ServiceComponent>(), _engine, Modules.ToList());
        var step = config.UserCount <= 100 ? 25 : config.UserCount <= 500 ? 50 : 100;
        var steps = Enumerable.Range(1, 30).Select(i => i * step).TakeWhile(s => s <= config.UserCount * 2).ToList();

        ScalingData = new ObservableCollection<ServiceComponent>(points);
        OnPropertyChanged(nameof(ScalingData));
        IsScalingVisible = true;
        SelectedTabIndex = 2;
        StatusText = $"Scaling: {steps.First()}–{steps.Last()} users ({steps.Count} points)";
    }

    private void ImportMatrix()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Excel files (*.xlsx)|*.xlsx",
            Title = "Import Excel Calculator"
        };
        if (dialog.ShowDialog() == true)
        {
            try
            {
                _matrixManager.Import(dialog.FileName);
                _matrix = _matrixManager.Matrix;
                ReloadMatrix();
                StatusText = _loc["status.imported"];
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Import error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void SaveMatrix()
    {
        SyncGridsToMatrix();
        _matrixManager.Save();
        MessageBox.Show(_loc["dialog.matrixSaved"], "Info", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void SyncGridsToMatrix()
    {
        _matrixManager.SyncGridsToMatrix(
            MsSqlRanges.ToList(), MsSqlPerformanceRanges.ToList(),
            K8sStandardComponents.ToList(), K8sDocumentFlowComponents.ToList(),
            K8sComponents.ToList(), InfraNodes.ToList());
        _matrix = _matrixManager.Matrix;
    }

    private void ResetMatrix()
    {
        _matrixManager.Reset();
        _matrix = _matrixManager.Matrix;
        _engine.SetProductType(ProductType.Standard);
        ReloadMatrix();
    }

    private void ReloadMatrix()
    {
        var productType = ProductIndex == 0 ? ProductType.Standard : ProductType.DocumentFlow;
        _engine.SetProductType(productType);
        var freshModules = _engine.Modules.Select(m => m.Clone()).ToList();
        _engine.SetModules(freshModules);
        LoadMatrixGrids();
        Modules = new ObservableCollection<ProjectModule>(_engine.Modules);
        OnPropertyChanged(nameof(Modules));
    }

    private void OnProductTypeChanged()
    {
        var productType = ProductIndex == 0 ? ProductType.Standard : ProductType.DocumentFlow;
        _engine.SetProductType(productType);
        var freshModules = _engine.Modules.Select(m => m.Clone()).ToList();
        _engine.SetModules(freshModules);
        Modules = new ObservableCollection<ProjectModule>(_engine.Modules);
        OnPropertyChanged(nameof(Modules));
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
            mod.IsEnabled = deploymentType switch
            {
                DeploymentType.Kubernetes => !mod.Name.Contains("Windows") && !mod.Name.Contains("Сервери додатків") && !mod.Name.Contains("Веб сервери"),
                DeploymentType.Windows => !mod.IsKubernetesOnly,
                DeploymentType.Hybrid => true,
                _ => mod.IsEnabled
            };
        }

        Modules = new ObservableCollection<ProjectModule>(Modules);
        OnPropertyChanged(nameof(Modules));

        var loc = _loc;
        var deployName = deploymentType switch
        {
            DeploymentType.Kubernetes => loc["deploy.k8sName"],
            DeploymentType.Windows => loc["deploy.windowsName"],
            _ => loc["deploy.hybridName"]
        };
        StatusText = string.Format(loc["status.deploymentChanged"], deployName);
    }

    private void LoadMatrixGrids()
    {
        MsSqlRanges = new ObservableCollection<UserLoadRange>(_matrix.MsSqlRanges);
        MsSqlPerformanceRanges = new ObservableCollection<UserLoadRange>(_matrix.MsSqlPerformanceRanges);

        K8sStandardComponents = new ObservableCollection<ServiceComponent>(
            _matrix.StandardModules.SelectMany(m => m.Components.Select(c => new ServiceComponent
            {
                Name = c.Name, Cpu = c.Cpu, RamGb = c.RamGb,
                Replicas = c.FixedReplicas, FixedReplicas = c.FixedReplicas,
                Formula = c.Formula, Category = m.Name
            }))
        );

        K8sDocumentFlowComponents = new ObservableCollection<ServiceComponent>(
            _matrix.DocumentFlowModules.SelectMany(m => m.Components.Select(c => new ServiceComponent
            {
                Name = c.Name, Cpu = c.Cpu, RamGb = c.RamGb,
                Replicas = c.FixedReplicas, FixedReplicas = c.FixedReplicas,
                Formula = c.Formula, Category = m.Name
            }))
        );

        K8sComponents = new ObservableCollection<ServiceComponent>(
            _matrix.Modules.SelectMany(m => m.Components.Select(c => new ServiceComponent
            {
                Name = c.Name, Cpu = c.Cpu, RamGb = c.RamGb,
                Replicas = c.FixedReplicas, FixedReplicas = c.FixedReplicas,
                Formula = c.Formula, Category = m.Name
            }))
        );

        InfraNodes = new ObservableCollection<InfrastructureNode>();
        if (_matrix.DefaultK8sSql != null) InfraNodes.Add(_matrix.DefaultK8sSql);
        if (_matrix.DefaultK8sMaster != null) InfraNodes.Add(_matrix.DefaultK8sMaster);
        if (_matrix.DefaultK8sWorker != null) InfraNodes.Add(_matrix.DefaultK8sWorker);

        OnPropertyChanged(nameof(MsSqlRanges));
        OnPropertyChanged(nameof(MsSqlPerformanceRanges));
        OnPropertyChanged(nameof(K8sStandardComponents));
        OnPropertyChanged(nameof(K8sDocumentFlowComponents));
        OnPropertyChanged(nameof(K8sComponents));
        OnPropertyChanged(nameof(InfraNodes));
    }

    private void ExportTxt()
    {
        if (_lastResult == null) return;
        ExportConfig(_results.ExportText(_lastResult, GetConfig()), "txt");
    }

    private void ExportHtml()
    {
        if (_lastResult == null) return;
        ExportConfig(_results.ExportHtml(_lastResult, GetConfig()), "html");
    }

    private void ShowDiagram()
    {
        if (_lastResult == null) return;
        IsDiagramVisible = true;
        StatusText = _loc["status.diagramBuilt"];
    }

    private void ExportSvg()
    {
        if (_lastResult == null) return;
        var svg = ResultsPresenter.BuildSvgDiagram(_lastResult, GetConfig());
        var saveDialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "SVG files (*.svg)|*.svg",
            FileName = "infrastructure.svg"
        };
        if (saveDialog.ShowDialog() == true)
        {
            System.IO.File.WriteAllText(saveDialog.FileName, svg);
            StatusText = string.Format(_loc["status.saved"], saveDialog.FileName);
        }
    }

    private void ExportMermaid()
    {
        if (_lastResult == null) return;
        var mermaid = _results.ExportMermaid(_lastResult, GetConfig());
        var saveDialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Mermaid files (*.mmd)|*.mmd|Text files (*.txt)|*.txt",
            FileName = "infrastructure.mmd"
        };
        if (saveDialog.ShowDialog() == true)
        {
            System.IO.File.WriteAllText(saveDialog.FileName, mermaid);
            StatusText = string.Format(_loc["status.saved"], saveDialog.FileName);
        }
    }

    private void ExportConfig(string content, string extension)
    {
        var saveDialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = extension switch
            {
                "txt" => "Text files (*.txt)|*.txt",
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

    private void OpenAiSettings()
    {
        var dialog = new AiSettingsDialog(_aiSettings);
        if (dialog.ShowDialog() == true)
        {
            _aiSettings = dialog.Settings;
            _aiSettings.Save();
            _advisor.UpdateSettings(_aiSettings);
            UpdateAiBadge();
        }
    }

    private void SwitchLanguage()
    {
        var loc = _loc;
        loc.LoadLanguage(loc.CurrentLang == "uk" ? "en" : "uk");
    }

    private void ToggleTheme()
    {
        _themeService.Toggle();
        IsDarkTheme = _themeService.IsDark;
    }

    private void UpdateAiBadge()
    {
        if (_aiSettings.EnableRealAi)
        {
            AiBadgeText = $"\u2705 {_aiSettings.ProviderDisplay()}";
        }
        else
        {
            AiBadgeText = _loc["ai.badgeDisabled"];
        }
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

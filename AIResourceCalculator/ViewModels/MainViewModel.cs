using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using AIResourceCalculator.Data;
using AIResourceCalculator.Localization;
using AIResourceCalculator.Models;
using AIResourceCalculator.Services;

namespace AIResourceCalculator.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private SizingEngine _engine;
    private readonly AiAdvisorService _advisor;
    private readonly ValidationEngine _validator;
    private SizingMatrix _matrix;
    private AiSettings _aiSettings;
    private ResourceRequirement? _lastResult;
    private ResourceRequirement? _lastResultPerf;

    private string _userCount = "100";
    private int _deploymentIndex;
    private int _productIndex;
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

    public MainViewModel()
    {
        _advisor = new AiAdvisorService();
        _validator = new ValidationEngine();
        _matrix = DataService.LoadMatrix();
        _engine = new SizingEngine(_matrix);
        _engine.SetProductType(ProductType.Standard);
        _aiSettings = AiSettings.Load();
        _advisor.UpdateSettings(_aiSettings);

        _isDarkTheme = ThemeService.IsDark;

        Modules = new ObservableCollection<ProjectModule>(_engine.Modules);
        _statusText = LocalizationService.Instance["status.ready"];
        _aiNoDataText = LocalizationService.Instance["ai.noData"];

        LoadMatrixGrids();
        UpdateAiBadge();

        LocalizationService.Instance.PropertyChanged += (_, _) => OnLanguageChanged();

        InitializeCommands();

        OnDeploymentTypeChanged();
    }

    private void OnLanguageChanged()
    {
        var loc = LocalizationService.Instance;
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

    private int _selectedHistoryIndex = -1;
    public int SelectedHistoryIndex
    {
        get => _selectedHistoryIndex;
        set { _selectedHistoryIndex = value; OnPropertyChanged(); }
    }

    #endregion

    #region Tab Headers

    public string TabMatrixHeader => LocalizationService.Instance["tab.matrixTitle"];
    public string TabSetupHeader => LocalizationService.Instance["tab.setupTitle"];
    public string TabResultsHeader => LocalizationService.Instance["tab.resultsTitle"];
    public string TabAssistantHeader => LocalizationService.Instance["tab.assistantTitle"];

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
            LoadProfile = loadProfile
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
            CalculationHistoryService.SaveToHistory(config, req);
            LoadHistory();
            SelectedTabIndex = 2;
            StatusText = string.Format(LocalizationService.Instance["status.calculated"],
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
        var otherModules = _engine.Modules.Select(m => CloneModule(m)).ToList();
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

        if (_lastResult != null)
        {
            ValidationResults = new ObservableCollection<ValidationResult>(_validator.Validate(req, req));
            OnPropertyChanged(nameof(ValidationResults));
        }

        AiNoDataText = LocalizationService.Instance["ai.noData"];
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

        AiNoDataText = LocalizationService.Instance["results.aiAnalyzing"];
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
            AiBadgeResultText = $"{balance.Recommendations.Count} rec" +
                (totalSavings > 0 ? $" | ~${totalSavings:F0}/mo economy" : "");
            AiRecCountText = LocalizationService.Instance.CurrentLang == "uk"
                ? $"📋 {balance.Recommendations.Count} рекомендацій"
                : $"📋 {balance.Recommendations.Count} recommendations";
        }
        else
        {
            AiNoDataText = LocalizationService.Instance["ai.noData"];
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

            var loc = LocalizationService.Instance;
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

        var loc = LocalizationService.Instance;
        StatusText = loc.CurrentLang == "uk"
            ? $"Застосовано: {_parsedConfig.UserCount} користувачів, {_parsedModules?.Count ?? 0} модулів"
            : $"Applied: {_parsedConfig.UserCount} users, {_parsedModules?.Count ?? 0} modules";
    }

    private void UseTemplate(string template)
    {
        var loc = LocalizationService.Instance;
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
        HistoryItems = new ObservableCollection<CalculationHistoryItem>(CalculationHistoryService.LoadHistory());
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
                var importer = new ExcelImporter();
                _matrix = importer.Import(dialog.FileName);
                DataService.SaveMatrix(_matrix);
                ReloadMatrix();
                StatusText = LocalizationService.Instance["status.imported"];
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
        DataService.SaveMatrix(_matrix);
        var lang = LocalizationService.Instance;
        MessageBox.Show(lang["dialog.matrixSaved"], "Info", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void SyncGridsToMatrix()
    {
        _matrix.MsSqlRanges = MsSqlRanges.ToList();
        _matrix.MsSqlPerformanceRanges = MsSqlPerformanceRanges.ToList();

        SyncComponentsToModules(K8sStandardComponents, _matrix.StandardModules);
        SyncComponentsToModules(K8sDocumentFlowComponents, _matrix.DocumentFlowModules);
        SyncComponentsToModules(K8sComponents, _matrix.Modules);

        _matrix.DefaultK8sSql = InfraNodes.FirstOrDefault(n => n.Name.Contains("SQL"));
        _matrix.DefaultK8sMaster = InfraNodes.FirstOrDefault(n => n.Name.Contains("Master"));
        _matrix.DefaultK8sWorker = InfraNodes.FirstOrDefault(n => n.Name.Contains("Worker"));
    }

    private void SyncComponentsToModules(ObservableCollection<ServiceComponent> components, List<ProjectModule> modules)
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
                    Name = comp.Name,
                    Cpu = comp.Cpu,
                    RamGb = comp.RamGb,
                    FixedReplicas = comp.FixedReplicas,
                    Formula = comp.Formula
                });
            }
        }
    }

    private void ResetMatrix()
    {
        DataService.ClearMatrix();
        _matrix = new SizingMatrix();
        _engine = new SizingEngine(_matrix);
        ReloadMatrix();
    }

    private void ReloadMatrix()
    {
        var productType = ProductIndex == 0 ? ProductType.Standard : ProductType.DocumentFlow;
        _engine.SetProductType(productType);
        var freshModules = _engine.Modules.Select(m => CloneModule(m)).ToList();
        _engine.SetModules(freshModules);
        LoadMatrixGrids();
        Modules = new ObservableCollection<ProjectModule>(_engine.Modules);
        OnPropertyChanged(nameof(Modules));
    }

    private void OnProductTypeChanged()
    {
        var productType = ProductIndex == 0 ? ProductType.Standard : ProductType.DocumentFlow;
        _engine.SetProductType(productType);
        var freshModules = _engine.Modules.Select(m => CloneModule(m)).ToList();
        _engine.SetModules(freshModules);
        Modules = new ObservableCollection<ProjectModule>(_engine.Modules);
        OnPropertyChanged(nameof(Modules));
        OnDeploymentTypeChanged();
        StatusText = string.Format(LocalizationService.Instance["status.productChanged"],
            productType == ProductType.Standard ? "Стандарт" : "Документообіг");
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
                DeploymentType.Kubernetes => !mod.Name.Contains("Windows"),
                DeploymentType.Windows => true,
                DeploymentType.Hybrid => true,
                _ => mod.IsEnabled
            };
        }

        Modules = new ObservableCollection<ProjectModule>(Modules);
        OnPropertyChanged(nameof(Modules));

        var loc = LocalizationService.Instance;
        var deployName = deploymentType switch
        {
            DeploymentType.Kubernetes => loc["deploy.k8sName"],
            DeploymentType.Windows => loc["deploy.windowsName"],
            _ => loc["deploy.hybridName"]
        };
        StatusText = string.Format(loc["status.deploymentChanged"], deployName);
    }

    private static ProjectModule CloneModule(ProjectModule src)
    {
        return new ProjectModule
        {
            Name = src.Name, Description = src.Description, IsEnabled = src.IsEnabled,
            Components = src.Components.Select(c => new ModuleComponent
            {
                Name = c.Name, Cpu = c.Cpu, RamGb = c.RamGb,
                PerfCpu = c.PerfCpu, PerfRamGb = c.PerfRamGb,
                Formula = c.Formula, FixedReplicas = c.FixedReplicas,
                HasLocalSql = c.HasLocalSql, HasRedis = c.HasRedis, Notes = c.Notes
            }).ToList()
        };
    }

    private void LoadMatrixGrids()
    {
        MsSqlRanges = new ObservableCollection<UserLoadRange>(_matrix.MsSqlRanges);
        MsSqlPerformanceRanges = new ObservableCollection<UserLoadRange>(_matrix.MsSqlPerformanceRanges);

        var standardModules = _matrix.StandardModules.Count > 0
            ? _matrix.StandardModules
            : SizingEngine.DefaultStandardModules();
        K8sStandardComponents = new ObservableCollection<ServiceComponent>(
            standardModules.SelectMany(m => m.Components.Select(c => new ServiceComponent
            {
                Name = c.Name, Cpu = c.Cpu, RamGb = c.RamGb,
                Replicas = c.FixedReplicas, FixedReplicas = c.FixedReplicas,
                Formula = c.Formula, Category = m.Name
            }))
        );

        var docFlowModules = _matrix.DocumentFlowModules.Count > 0
            ? _matrix.DocumentFlowModules
            : SizingEngine.DefaultDocumentFlowModules();
        K8sDocumentFlowComponents = new ObservableCollection<ServiceComponent>(
            docFlowModules.SelectMany(m => m.Components.Select(c => new ServiceComponent
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
        var svc = new ConfigExportService();
        var text = svc.ExportTxt(_lastResult, GetConfig());
        ExportConfig(text, "txt");
    }

    private void ExportHtml()
    {
        if (_lastResult == null) return;
        var svc = new ConfigExportService();
        var html = svc.ExportHtml(_lastResult, GetConfig());
        ExportConfig(html, "html");
    }

    private void ShowDiagram()
    {
        if (_lastResult == null) return;
        IsDiagramVisible = true;
        StatusText = "Схему побудовано";
    }

    private void ExportSvg()
    {
        if (_lastResult == null) return;
        var svg = DiagramBuilder.BuildSvg(_lastResult);
        var saveDialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "SVG files (*.svg)|*.svg",
            FileName = "infrastructure.svg"
        };
        if (saveDialog.ShowDialog() == true)
        {
            System.IO.File.WriteAllText(saveDialog.FileName, svg);
            StatusText = string.Format(LocalizationService.Instance["status.saved"], saveDialog.FileName);
        }
    }

    private void ExportMermaid()
    {
        if (_lastResult == null) return;
        var svc = new ConfigExportService();
        var mermaid = svc.ExportMermaid(_lastResult, GetConfig());
        Clipboard.SetText(mermaid);
        StatusText = LocalizationService.Instance["status.copied"];
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
            StatusText = string.Format(LocalizationService.Instance["status.saved"], saveDialog.FileName);
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
        var loc = LocalizationService.Instance;
        loc.LoadLanguage(loc.CurrentLang == "uk" ? "en" : "uk");
    }

    private void ToggleTheme()
    {
        ThemeService.Toggle();
        IsDarkTheme = ThemeService.IsDark;
    }

    private void UpdateAiBadge()
    {
        if (_aiSettings.EnableRealAi)
        {
            AiBadgeText = $"\u2705 {_aiSettings.ProviderDisplay()}";
        }
        else
        {
            AiBadgeText = LocalizationService.Instance["ai.badgeDisabled"];
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

    public SizingEngine Engine => _engine;
    public ResourceRequirement? LastResult => _lastResult;
    public ResourceRequirement? LastResultPerf => _lastResultPerf;

    #endregion
}

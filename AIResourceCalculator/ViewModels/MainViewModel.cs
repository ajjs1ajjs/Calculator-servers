using System.Collections.ObjectModel;
using System.ComponentModel;
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
    private readonly PromptParserService _promptParser;
    private SizingMatrix _matrix;
    private AiSettings _aiSettings;
    private ResourceRequirement? _lastResult;
    private ResourceRequirement? _lastResultPerf;

    private string _userCount = "100";
    private int _deploymentIndex;
    private string _statusText = "";
    private string _aiBadgeText = "";
    private string _aiBadgeResultText = "";
    private string _langFlag = "\U0001F1FA\U0001F1E6";
    private string _langName = "Українська";
    private string _aiQueryPrompt = "";
    private string _aiQueryResult = "";
    private string _aiNoDataText = "";
    private bool _isAiNoDataVisible = true;
    private bool _isAiRecListVisible;
    private bool _isAiQueryResultVisible;
    private bool _isApplyAiQueryVisible;
    private bool _isDiagramVisible;
    private bool _isQuickRecVisible;
    private string _quickRecText = "";
    private bool _isDarkTheme;

    public MainViewModel()
    {
        _advisor = new AiAdvisorService();
        _validator = new ValidationEngine();
        _promptParser = new PromptParserService();
        _matrix = DataService.LoadMatrix();
        _engine = new SizingEngine(_matrix);
        _aiSettings = AiSettings.Load();
        _advisor.UpdateSettings(_aiSettings);

        Modules = new ObservableCollection<ProjectModule>(_engine.Modules);
        _statusText = LocalizationService.Instance["status.ready"];
        _aiNoDataText = LocalizationService.Instance["ai.noData"];

        LoadMatrixGrids();
        UpdateAiBadge();

        LocalizationService.Instance.PropertyChanged += (_, _) => OnLanguageChanged();

        InitializeCommands();
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
        set { _deploymentIndex = value; OnPropertyChanged(); }
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

    public string AiQueryPrompt
    {
        get => _aiQueryPrompt;
        set { _aiQueryPrompt = value; OnPropertyChanged(); }
    }

    public string AiQueryResult
    {
        get => _aiQueryResult;
        set { _aiQueryResult = value; OnPropertyChanged(); }
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

    public bool IsAiQueryResultVisible
    {
        get => _isAiQueryResultVisible;
        set { _isAiQueryResultVisible = value; OnPropertyChanged(); }
    }

    public bool IsApplyAiQueryVisible
    {
        get => _isApplyAiQueryVisible;
        set { _isApplyAiQueryVisible = value; OnPropertyChanged(); }
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

    #endregion

    #region Matrix Properties

    public ObservableCollection<UserLoadRange> MsSqlRanges { get; private set; } = new();
    public ObservableCollection<UserLoadRange> MsSqlPerformanceRanges { get; private set; } = new();
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
    public ObservableCollection<InfrastructureNode> AiInfrastructureBalance { get; private set; } = new();
    public ObservableCollection<InfrastructureNode> AiInfrastructurePerformance { get; private set; } = new();
    public ObservableCollection<AiRecommendation> AiRecommendations { get; private set; } = new();
    public ObservableCollection<ServiceComponent> ResultComponents { get; private set; } = new();
    public ObservableCollection<ValidationResult> ValidationResults { get; private set; } = new();

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
    public ICommand AssistantSendCommand { get; private set; } = null!;
    public ICommand ApplyAssistantCommand { get; private set; } = null!;
    public ICommand LangSwitchCommand { get; private set; } = null!;
    public ICommand Template1Command { get; private set; } = null!;
    public ICommand Template2Command { get; private set; } = null!;
    public ICommand Template3Command { get; private set; } = null!;
    public ICommand ToggleThemeCommand { get; private set; } = null!;
    public ICommand AnalyzeAiCommand { get; private set; } = null!;
    public ICommand AiSettingsCommand { get; private set; } = null!;

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
        AssistantSendCommand = new RelayCommand(_ => AssistantSend());
        ApplyAssistantCommand = new RelayCommand(_ => ApplyAssistant());
        AiSettingsCommand = new RelayCommand(_ => OpenAiSettings());
        LangSwitchCommand = new RelayCommand(_ => SwitchLanguage());
        ToggleThemeCommand = new RelayCommand(_ => ToggleTheme());
        AnalyzeAiCommand = new RelayCommand(async _ => await AnalyzeWithAiAsync());
        Template1Command = new RelayCommand(_ => ApplyTemplate(200, 0, new[] { "App Server", "ROBOT", "Web", "ForceBPM", "LMS", "HR Portal" }));
        Template2Command = new RelayCommand(_ => ApplyTemplate(1000, 0, _engine.Modules.Where(m => m.Name != "Windows Infrastructure").Select(m => m.Name).ToArray()));
        Template3Command = new RelayCommand(_ => ApplyTemplate(25, 0, new[] { "App Server", "Web", "ForceBPM" }));
    }

    #endregion

    #region Command Implementations

    private ProjectConfig GetConfig(int? userCountOverride = null)
    {
        if (!int.TryParse(UserCount, out var uc) || uc < 1) uc = 100;
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
            LoadProfile = LoadProfile.Basic
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
        if (config.LoadProfile == LoadProfile.Basic)
        {
            var perfConfig = new ProjectConfig
            {
                ProjectName = config.ProjectName,
                UserCount = config.UserCount,
                DeploymentType = config.DeploymentType,
                LoadProfile = LoadProfile.Performance
            };
            perfReq = _engine.Calculate(perfConfig);
        }
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
    }

    public async Task AnalyzeWithAiAsync()
    {
        if (_lastResult == null) return;

        AiNoDataText = LocalizationService.Instance["results.aiAnalyzing"];
        IsAiNoDataVisible = true;
        IsAiRecListVisible = false;
        IsQuickRecVisible = false;

        var dual = await _advisor.AnalyzeAsync(_lastResult, GetConfig(), _lastResultPerf);

        var allRecs = dual.Balance.Recommendations.Concat(dual.Performance.Recommendations).ToList();
        if (allRecs.Count > 0)
        {
            IsAiNoDataVisible = false;
            IsAiRecListVisible = true;
            IsQuickRecVisible = true;

            AiRecommendations = new ObservableCollection<AiRecommendation>(allRecs);
            AiInfrastructureBalance = new ObservableCollection<InfrastructureNode>(dual.Balance.Infrastructure);
            AiInfrastructurePerformance = new ObservableCollection<InfrastructureNode>(dual.Performance.Infrastructure);
            OnPropertyChanged(nameof(AiRecommendations));
            OnPropertyChanged(nameof(AiInfrastructureBalance));
            OnPropertyChanged(nameof(AiInfrastructurePerformance));

            var totalSavings = allRecs.Sum(r => r.PotentialSavings);
            AiBadgeResultText = $"{allRecs.Count} rec | {dual.Balance.Infrastructure.Count + dual.Performance.Infrastructure.Count} infra" +
                (totalSavings > 0 ? $" | ~${totalSavings:F0}/mo economy" : "");
        }
        else
        {
            AiNoDataText = LocalizationService.Instance["ai.noData"];
            IsAiNoDataVisible = true;
        }
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
        DataService.SaveMatrix(_matrix);
        var lang = LocalizationService.Instance;
        MessageBox.Show(lang["dialog.matrixSaved"], "Info", MessageBoxButton.OK, MessageBoxImage.Information);
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
        var freshModules = _matrix.Modules.Count > 0
            ? _matrix.Modules.Select(m => CloneModule(m)).ToList()
            : _engine.Modules.Select(m => CloneModule(m)).ToList();
        _engine.SetModules(freshModules);
        LoadMatrixGrids();
        Modules = new ObservableCollection<ProjectModule>(_engine.Modules);
        OnPropertyChanged(nameof(Modules));
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
        K8sComponents = new ObservableCollection<ServiceComponent>(
            _matrix.Modules.SelectMany(m => m.Components.Select(c => new ServiceComponent
            {
                Name = c.Name, Cpu = c.Cpu, RamGb = c.RamGb,
                Replicas = c.FixedReplicas, Category = m.Name
            }))
        );
        InfraNodes = new ObservableCollection<InfrastructureNode>();
        if (_matrix.DefaultK8sSql != null) InfraNodes.Add(_matrix.DefaultK8sSql);
        if (_matrix.DefaultK8sMaster != null) InfraNodes.Add(_matrix.DefaultK8sMaster);
        if (_matrix.DefaultK8sWorker != null) InfraNodes.Add(_matrix.DefaultK8sWorker);
        OnPropertyChanged(nameof(MsSqlRanges));
        OnPropertyChanged(nameof(MsSqlPerformanceRanges));
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

    private void AssistantSend()
    {
        var prompt = AiQueryPrompt.Trim();
        if (string.IsNullOrEmpty(prompt)) return;

        var (parsedConfig, modules) = _promptParser.Parse(prompt);
        ApplyParsedResult(parsedConfig, modules);
    }

    private void ApplyParsedResult(ProjectConfig config, List<string> moduleNames)
    {
        UserCount = config.UserCount.ToString();
        DeploymentIndex = config.DeploymentType switch
        {
            DeploymentType.Kubernetes => 0,
            DeploymentType.Windows => 1,
            _ => 2
        };

        if (moduleNames.Count > 0)
        {
            foreach (var mod in Modules)
                mod.IsEnabled = moduleNames.Contains(mod.Name, StringComparer.OrdinalIgnoreCase);
            Modules = new ObservableCollection<ProjectModule>(Modules);
            OnPropertyChanged(nameof(Modules));
        }

        AiQueryResult = $"Застосовано: {config.UserCount} користувачів, {config.DeploymentType}";
        IsAiQueryResultVisible = true;
        IsApplyAiQueryVisible = false;
    }

    private void ApplyAssistant()
    {
        var (parsedConfig, moduleNames) = _promptParser.Parse(AiQueryPrompt);
        ApplyParsedResult(parsedConfig, moduleNames);
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

    private void ApplyTemplate(int users, int deployment, string[] enabledModules)
    {
        UserCount = users.ToString();
        DeploymentIndex = deployment;
        foreach (var mod in Modules)
            mod.IsEnabled = enabledModules.Contains(mod.Name);
        Modules = new ObservableCollection<ProjectModule>(Modules);
        OnPropertyChanged(nameof(Modules));
        StatusText = $"Template: {users} users";
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
    public bool IsQuerySendEnabled => true;

    #endregion
}

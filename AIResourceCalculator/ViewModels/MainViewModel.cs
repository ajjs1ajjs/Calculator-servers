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
    private readonly SizingEngine _engine;
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
    private bool _isCompareVisible;
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
        OnPropertyChanged(nameof(TabAiQueryHeader));
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

    public bool IsCompareVisible
    {
        get => _isCompareVisible;
        set { _isCompareVisible = value; OnPropertyChanged(); }
    }

    public bool IsDarkTheme
    {
        get => _isDarkTheme;
        set { _isDarkTheme = value; OnPropertyChanged(); OnPropertyChanged(nameof(ThemeIcon)); }
    }

    public string ThemeIcon => _isDarkTheme ? "\u2600" : "\uD83C\uDF19";

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
    public ObservableCollection<CompareRow> CompareResults { get; private set; } = new();
    public ObservableCollection<CompareRow> CompareQuick { get; private set; } = new();
    public ObservableCollection<AiRecommendation> AiRecommendations { get; private set; } = new();

    #endregion

    #region Tab Headers

    public string TabMatrixHeader => LocalizationService.Instance["tab.matrixTitle"];
    public string TabSetupHeader => LocalizationService.Instance["tab.setupTitle"];
    public string TabResultsHeader => LocalizationService.Instance["tab.resultsTitle"];
    public string TabAiQueryHeader => LocalizationService.Instance["tab.aiQueryTitle"];

    #endregion

    #region Commands

    public ICommand CalculateCommand { get; private set; } = null!;
    public ICommand CompareCommand { get; private set; } = null!;
    public ICommand ImportMatrixCommand { get; private set; } = null!;
    public ICommand SaveMatrixCommand { get; private set; } = null!;
    public ICommand ResetMatrixCommand { get; private set; } = null!;
    public ICommand ExportTxtCommand { get; private set; } = null!;
    public ICommand ExportPdfCommand { get; private set; } = null!;
    public ICommand ShowDiagramCommand { get; private set; } = null!;
    public ICommand ExportSvgCommand { get; private set; } = null!;
    public ICommand ExportMermaidCommand { get; private set; } = null!;
    public ICommand AiQuerySendCommand { get; private set; } = null!;
    public ICommand ApplyAiQueryCommand { get; private set; } = null!;
    public ICommand AiSettingsCommand { get; private set; } = null!;
    public ICommand LangSwitchCommand { get; private set; } = null!;
    public ICommand Template1Command { get; private set; } = null!;
    public ICommand Template2Command { get; private set; } = null!;
    public ICommand Template3Command { get; private set; } = null!;
    public ICommand ToggleThemeCommand { get; private set; } = null!;

    private void InitializeCommands()
    {
        CalculateCommand = new RelayCommand(_ => Calculate());
        CompareCommand = new RelayCommand(_ => Compare());
        ImportMatrixCommand = new RelayCommand(_ => ImportMatrix());
        SaveMatrixCommand = new RelayCommand(_ => SaveMatrix());
        ResetMatrixCommand = new RelayCommand(_ => ResetMatrix());
        ExportTxtCommand = new RelayCommand(_ => ExportTxt());
        ExportPdfCommand = new RelayCommand(_ => ExportPdf());
        ShowDiagramCommand = new RelayCommand(_ => ShowDiagram());
        ExportSvgCommand = new RelayCommand(_ => ExportSvg());
        ExportMermaidCommand = new RelayCommand(_ => ExportMermaid());
        AiQuerySendCommand = new RelayCommand(async _ => await AiQuerySendAsync());
        ApplyAiQueryCommand = new RelayCommand(_ => ApplyAiQuery());
        AiSettingsCommand = new RelayCommand(_ => OpenAiSettings());
        LangSwitchCommand = new RelayCommand(_ => SwitchLanguage());
        ToggleThemeCommand = new RelayCommand(_ => ToggleTheme());
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

    private void Compare()
    {
        try
        {
            var basicConfig = GetConfig();
            basicConfig.LoadProfile = LoadProfile.Basic;
            var (basicReq, _) = CalculateInternal(basicConfig);

            var perfConfig = GetConfig();
            perfConfig.LoadProfile = LoadProfile.Performance;
            _engine.SetModules(Modules.ToList());
            var perfReq = _engine.Calculate(perfConfig);

            _lastResult = basicReq;
            _lastResultPerf = perfReq;

            var nodeLabel = basicReq.DeploymentType == DeploymentType.Windows ? "Сервери" : "Worker";
            var compareData = new List<CompareRow>
            {
                new() { Name = "vCPU", Basic = $"{basicReq.TotalCpu:F1}", Performance = $"{perfReq.TotalCpu:F1}",
                        Recommended = basicReq.TotalCpu <= perfReq.TotalCpu * 0.8 ? "Basic" : "Performance" },
                new() { Name = "RAM", Basic = $"{basicReq.TotalRamGb:F1} GB", Performance = $"{perfReq.TotalRamGb:F1} GB",
                        Recommended = basicReq.TotalRamGb <= perfReq.TotalRamGb * 0.8 ? "Basic" : "Performance" },
                new() { Name = nodeLabel, Basic = $"{basicReq.WorkerNodeCount}", Performance = $"{perfReq.WorkerNodeCount}",
                        Recommended = basicReq.WorkerNodeCount <= perfReq.WorkerNodeCount ? "Basic" : "Performance" },
                new() { Name = "Storage", Basic = $"{basicReq.TotalStorageGb} GB", Performance = $"{perfReq.TotalStorageGb} GB", Recommended = "" },
                new() { Name = "IOPS", Basic = $"{basicReq.TotalIops}", Performance = $"{perfReq.TotalIops}", Recommended = "" }
            };

            CompareQuick = new ObservableCollection<CompareRow>(compareData);
            IsCompareVisible = true;

            ShowResults(basicReq, perfReq);

            StatusText = string.Format(LocalizationService.Instance["status.calculated"],
                basicConfig.UserCount, basicReq.TotalCpu.ToString("F1"), basicReq.TotalRamGb.ToString("F1"));
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void ShowResults(ResourceRequirement req, ResourceRequirement? perfReq)
    {
        TotalCpu = $"{req.TotalCpu:F1}";
        TotalRam = $"{req.TotalRamGb:F1} GB";
        TotalStorage = $"{req.TotalStorageGb} GB";
        TotalIops = $"{req.TotalIops}";
        TotalNodes = $"{req.Infrastructure.Sum(n => n.NodeCount)}";
        ResultInfrastructure = new ObservableCollection<InfrastructureNode>(req.Infrastructure);
        OnPropertyChanged(nameof(ResultInfrastructure));

        if (perfReq != null)
        {
            var nodeLabel = req.DeploymentType == DeploymentType.Windows ? "Сервери" : "Worker";
            var rows = new List<CompareRow>
            {
                new() { Name = "vCPU", Basic = $"{req.TotalCpu:F1}", Performance = $"{perfReq.TotalCpu:F1}" },
                new() { Name = "RAM", Basic = $"{req.TotalRamGb:F1} GB", Performance = $"{perfReq.TotalRamGb:F1} GB" },
                new() { Name = nodeLabel, Basic = $"{req.WorkerNodeCount}", Performance = $"{perfReq.WorkerNodeCount}" },
                new() { Name = "Storage", Basic = $"{req.TotalStorageGb} GB", Performance = $"{perfReq.TotalStorageGb} GB" },
                new() { Name = "IOPS", Basic = $"{req.TotalIops}", Performance = $"{perfReq.TotalIops}" }
            };
            CompareResults = new ObservableCollection<CompareRow>(rows);
            OnPropertyChanged(nameof(CompareResults));
        }

        AiNoDataText = LocalizationService.Instance["results.aiAnalyzing"];
        IsAiNoDataVisible = true;
        IsAiRecListVisible = false;

        var recommendations = await _advisor.AnalyzeAsync(req, GetConfig());
        if (recommendations.Count > 0)
        {
            var sorted = recommendations.OrderByDescending(r => r.Severity == "critical")
                .ThenByDescending(r => r.Severity == "warning")
                .ThenByDescending(r => r.Severity == "info").ToList();

            IsAiNoDataVisible = false;
            IsAiRecListVisible = true;
            AiRecommendations = new ObservableCollection<AiRecommendation>(sorted);
            OnPropertyChanged(nameof(AiRecommendations));

            var totalSavings = sorted.Sum(r => r.PotentialSavings);
            AiBadgeResultText = $"{sorted.Count} rec" + (totalSavings > 0 ? $" | ${totalSavings:F0}/mo" : "");
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
        ReloadMatrix();
    }

    private void ReloadMatrix()
    {
        _engine.SetModules(Modules.ToList());
        LoadMatrixGrids();
        Modules = new ObservableCollection<ProjectModule>(_engine.Modules);
        OnPropertyChanged(nameof(Modules));
    }

    private void LoadMatrixGrids()
    {
        MsSqlRanges = new ObservableCollection<UserLoadRange>(_matrix.MsSqlRanges);
        MsSqlPerformanceRanges = new ObservableCollection<UserLoadRange>(_matrix.MsSqlPerformanceRanges);
        K8sComponents = new ObservableCollection<ServiceComponent>(_matrix.K8sBasicComponents);
        InfraNodes = new ObservableCollection<InfrastructureNode>
        {
            _matrix.DefaultK8sSql,
            _matrix.DefaultK8sMaster,
            _matrix.DefaultK8sWorker
        };
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

    private void ExportPdf()
    {
        if (_lastResult == null) return;
        var svc = new ConfigExportService();
        var html = svc.ExportPdf(_lastResult, GetConfig());
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

    private async Task AiQuerySendAsync()
    {
        var prompt = AiQueryPrompt.Trim();
        if (string.IsNullOrEmpty(prompt)) return;

        if (_aiSettings.EnableRealAi && _aiSettings.Provider != AiProvider.None && !string.IsNullOrEmpty(_aiSettings.ApiKey))
        {
            await AnalyzeWithRealAiAsync(prompt);
        }
        else if (_aiSettings.EnableRealAi && _aiSettings.Provider == AiProvider.LocalOllama)
        {
            await AnalyzeWithRealAiAsync(prompt);
        }
        else
        {
            if (_aiSettings.EnableRealAi)
            {
                AiQueryResult = "⚠️ Real AI увімкнено, але не налаштовано API ключ.\nНатисніть «AI Settings» вгорі, оберіть провайдера та вкажіть ключ.\n\nАбо використайте шаблони нижче.";
                IsAiQueryResultVisible = true;
                IsApplyAiQueryVisible = false;
                return;
            }
            var (parsedConfig, modules) = _promptParser.Parse(prompt);
            ApplyParsedResult(parsedConfig, modules);
        }
    }

    private async Task AnalyzeWithRealAiAsync(string prompt)
    {
        AiQueryResult = LocalizationService.Instance["results.aiAnalyzing"];
        IsAiQueryResultVisible = true;
        IsApplyAiQueryVisible = false;

        try
        {
            var aiService = new AiApiService(_aiSettings);
            var response = await aiService.GetRecommendation(prompt);
            AiQueryResult = response ?? "No response from AI.";
            IsApplyAiQueryVisible = true;
        }
        catch (Exception ex)
        {
            AiQueryResult = $"Error: {ex.Message}\n\nTry using the templates below instead.";
        }
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

    private void ApplyAiQuery()
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

public class CompareRow
{
    public string Name { get; set; } = "";
    public string Basic { get; set; } = "";
    public string Performance { get; set; } = "";
    public string Recommended { get; set; } = "";
}

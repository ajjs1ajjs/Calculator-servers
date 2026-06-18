using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
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

    public ObservableCollection<CalculationHistoryItem> HistoryItems { get; private set; } = new();
    public bool HasHistory => HistoryItems.Count > 0;

    private int _selectedHistoryIndex = -1;
    public int SelectedHistoryIndex
    {
        get => _selectedHistoryIndex;
        set { _selectedHistoryIndex = value; OnPropertyChanged(); }
    }

    public ObservableCollection<ProjectModule> Modules { get; private set; }

    #endregion

    #region Tab Headers

    public string TabMatrixHeader => _loc["tab.matrixTitle"];
    public string TabSetupHeader => _loc["tab.setupTitle"];
    public string TabResultsHeader => _loc["tab.resultsTitle"];

    #endregion

    #region Commands

    public ICommand CalculateCommand { get; private set; } = null!;
    public ICommand ExportTxtCommand { get; private set; } = null!;
    public ICommand ExportHtmlCommand { get; private set; } = null!;
    public ICommand LangSwitchCommand { get; private set; } = null!;
    public ICommand RecallHistoryCommand { get; private set; } = null!;

    private void InitializeCommands()
    {
        CalculateCommand = new RelayCommand(_ => Calculate());
        ExportTxtCommand = new RelayCommand(_ => ExportTxt());
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
        TotalCpu = $"{req.TotalCpu:F1}";
        TotalRam = $"{req.TotalRamGb:F1} GB";
        TotalStorage = $"{req.TotalStorageGb} GB";
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
            sb.AppendLine(string.Format(_loc["results.summaryNode"],
                n.NodeCount, n.Name, n.Cpu, n.RamGb, n.TotalStorageGb));
        }
        sb.Append(string.Format(_loc["results.summaryTotals"],
            req.TotalCpu.ToString("F1"), req.TotalRamGb.ToString("F1"), req.TotalStorageGb, req.TotalIops));
        return sb.ToString();
    }

    private string BuildDiskRecommendations(ResourceRequirement req, ProjectConfig config)
        => DiskAdvisor.Build(req, config, _loc);

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
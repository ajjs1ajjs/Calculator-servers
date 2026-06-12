using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AIResourceCalculator.Data;
using AIResourceCalculator.Localization;
using AIResourceCalculator.Models;
using AIResourceCalculator.Services;

namespace AIResourceCalculator;

public partial class MainWindow : Window
{
    private SizingEngine _engine;
    private readonly AiAdvisorService _advisor;
    private readonly ValidationEngine _validator;
    private readonly PromptParserService _promptParser;
    private SizingMatrix _matrix;
    private ResourceRequirement? _lastResult;
    private ResourceRequirement? _lastResultPerf;
    private AiSettings _aiSettings;

    public MainWindow()
    {
        InitializeComponent();

        _advisor = new AiAdvisorService();
        _validator = new ValidationEngine();
        _promptParser = new PromptParserService();

        _matrix = DataService.LoadMatrix();
        _engine = new SizingEngine(_matrix);

        _aiSettings = AiSettings.Load();
        _advisor.UpdateSettings(_aiSettings);

        ModulesPanel.ItemsSource = _engine.Modules;

        DataObject.AddPastingHandler(TxtUserCount, NumberPaste);

        SldOverprov.ValueChanged += (_, _) =>
            TxtOverprovVal.Text = $"{SldOverprov.Value:F1}x";

        var lang = LocalizationService.Instance;
        TxtLangFlag.Text = lang.Flag;
        TxtLangName.Text = lang.LangName;

        UpdateAiBadge();
        LoadMatrixGrids();
    }

    private void LoadMatrixGrids()
    {
        GridMatrixMsSql.ItemsSource = _matrix.MsSqlRanges;
        GridMatrixMsSqlPerf.ItemsSource = _matrix.MsSqlPerformanceRanges;
        GridMatrixComponents.ItemsSource = _matrix.K8sBasicComponents;
        GridMatrixInfra.ItemsSource = new List<InfrastructureNode>
        {
            _matrix.DefaultK8sSql,
            _matrix.DefaultK8sMaster,
            _matrix.DefaultK8sWorker
        };
    }

    private void BtnMatrixImport_Click(object sender, RoutedEventArgs e)
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
                _engine = new SizingEngine(_matrix);
                LoadMatrixGrids();
                ModulesPanel.ItemsSource = _engine.Modules;
                UpdateStatus(LocalizationService.Instance["status.imported"]);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Import error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void BtnMatrixSave_Click(object sender, RoutedEventArgs e)
    {
        DataService.SaveMatrix(_matrix);
        var lang = LocalizationService.Instance;
        MessageBox.Show(lang["dialog.matrixSaved"], "Info", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnMatrixReset_Click(object sender, RoutedEventArgs e)
    {
        DataService.ClearMatrix();
        _matrix = new SizingMatrix();
        _engine = new SizingEngine(_matrix);
        LoadMatrixGrids();
        ModulesPanel.ItemsSource = _engine.Modules;
    }

    private void UpdateAiBadge()
    {
        if (_aiSettings.EnableRealAi)
        {
            TxtAiBadge.Text = $"\u2705 {_aiSettings.ProviderDisplay()}";
            TxtAiBadge.Foreground = System.Windows.Media.Brushes.LimeGreen;
        }
        else
        {
            TxtAiBadge.Text = LocalizationService.Instance["ai.badgeDisabled"];
            TxtAiBadge.Foreground = System.Windows.Media.Brushes.Orange;
        }
    }

    private void NumberValidation(object sender, TextCompositionEventArgs e) =>
        e.Handled = !int.TryParse(e.Text, out _);

    private void NumberPaste(object sender, DataObjectPastingEventArgs e)
    {
        if (e.DataObject.GetDataPresent(typeof(string)))
            e.Handled = !int.TryParse((string)e.DataObject.GetData(typeof(string)), out _);
    }

    private void BtnAiSettings_Click(object sender, RoutedEventArgs e)
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

    private void BtnLangSwitch_Click(object sender, RoutedEventArgs e)
    {
        var loc = LocalizationService.Instance;
        loc.LoadLanguage(loc.CurrentLang == "uk" ? "en" : "uk");
        ReloadUi();
    }

    private void ReloadUi()
    {
        var loc = LocalizationService.Instance;
        TxtLangFlag.Text = loc.Flag;
        TxtLangName.Text = loc.LangName;

        for (int i = 0; i < MainTabControl.Items.Count; i++)
        {
            if (MainTabControl.Items[i] is TabItem tab)
            {
                var key = i switch
                {
                    0 => "tab.matrixTitle", 1 => "tab.setupTitle", 2 => "tab.resultsTitle", 3 => "tab.aiQueryTitle",
                    _ => ""
                };
                if (!string.IsNullOrEmpty(key)) tab.Header = loc[key];
            }
        }

        UpdateStatus(loc["status.ready"]);
    }

    private void UpdateStatus(string text) => TxtStatus.Text = text;

    private ProjectConfig GetConfig(int? userCount = null)
    {
        if (!int.TryParse(TxtUserCount.Text, out var uc) || uc < 1) uc = 100;
        return new ProjectConfig
        {
            ProjectName = "Project",
            UserCount = userCount ?? uc,
            DeploymentType = CmbDeployment.SelectedIndex switch
            {
                0 => DeploymentType.Kubernetes,
                1 => DeploymentType.Windows,
                _ => DeploymentType.Hybrid
            },
            LoadProfile = LoadProfile.Basic,
            HaEnabled = ChkHa.IsChecked ?? true,
            OverprovisioningFactor = SldOverprov.Value
        };
    }

    private (ResourceRequirement req, ResourceRequirement? perfReq) Calculate(ProjectConfig config)
    {
        _engine.SetModules(_engine.Modules.ToList());
        var req = _engine.Calculate(config);

        ResourceRequirement? perfReq = null;
        if (config.LoadProfile == LoadProfile.Basic)
        {
            var perfConfig = new ProjectConfig
            {
                ProjectName = config.ProjectName,
                UserCount = config.UserCount,
                DeploymentType = config.DeploymentType,
                LoadProfile = LoadProfile.Performance,
                HaEnabled = config.HaEnabled,
                OverprovisioningFactor = config.OverprovisioningFactor
            };
            perfReq = _engine.Calculate(perfConfig);
        }

        return (req, perfReq);
    }

    private void BtnCalculate_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var config = GetConfig();
            var (req, perfReq) = Calculate(config);
            _lastResult = req;
            _lastResultPerf = perfReq;

            ShowResults(req, perfReq);

            MainTabControl.SelectedIndex = 1;
            var lang = LocalizationService.Instance;
            UpdateStatus(string.Format(lang["status.calculated"], config.UserCount,
                req.TotalCpu.ToString("F1"), req.TotalRamGb.ToString("F1")));
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnCompare_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var basicConfig = GetConfig();
            basicConfig.LoadProfile = LoadProfile.Basic;
            var (basicReq, _) = Calculate(basicConfig);

            var perfConfig = GetConfig();
            perfConfig.LoadProfile = LoadProfile.Performance;
            _engine.SetModules(_engine.Modules.ToList());
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

            GridCompare.ItemsSource = null;
            GridCompare.ItemsSource = compareData;
            PanelCompareResult.Visibility = Visibility.Visible;

            ShowResults(basicReq, perfReq);
            MainTabControl.SelectedIndex = 1;

            var lang = LocalizationService.Instance;
            UpdateStatus(string.Format(lang["status.calculated"], basicConfig.UserCount,
                basicReq.TotalCpu.ToString("F1"), basicReq.TotalRamGb.ToString("F1")));
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void ShowResults(ResourceRequirement req, ResourceRequirement? perfReq)
    {
        TblTotalCpu.Text = $"{req.TotalCpu:F1}";
        TblTotalRam.Text = $"{req.TotalRamGb:F1} GB";
        TblTotalStorage.Text = $"{req.TotalStorageGb} GB";
        TblTotalIops.Text = $"{req.TotalIops}";
        TblTotalNodes.Text = $"{req.MasterNodeCount + req.WorkerNodeCount}";

        GridInfrastructure.ItemsSource = null;
        GridInfrastructure.ItemsSource = req.Infrastructure;

        if (perfReq != null)
        {
            var nodeLabel = req.DeploymentType == DeploymentType.Windows ? "Сервери" : "Worker";
            var compareRows = new List<CompareRow>
            {
                new() { Name = "vCPU", Basic = $"{req.TotalCpu:F1}", Performance = $"{perfReq.TotalCpu:F1}" },
                new() { Name = "RAM", Basic = $"{req.TotalRamGb:F1} GB", Performance = $"{perfReq.TotalRamGb:F1} GB" },
                new() { Name = nodeLabel, Basic = $"{req.WorkerNodeCount}", Performance = $"{perfReq.WorkerNodeCount}" },
                new() { Name = "Storage", Basic = $"{req.TotalStorageGb} GB", Performance = $"{perfReq.TotalStorageGb} GB" },
                new() { Name = "IOPS", Basic = $"{req.TotalIops}", Performance = $"{perfReq.TotalIops}" }
            };
            GridCompareResults.ItemsSource = null;
            GridCompareResults.ItemsSource = compareRows;
        }

        TxtAiNoData.Text = LocalizationService.Instance["results.aiAnalyzing"];
        TxtAiNoData.Visibility = Visibility.Visible;
        AiRecList.Visibility = Visibility.Collapsed;

        var recommendations = await _advisor.AnalyzeAsync(req, GetConfig());
        if (recommendations.Count > 0)
        {
            var sorted = recommendations.OrderByDescending(r => r.Severity == "critical")
                .ThenByDescending(r => r.Severity == "warning")
                .ThenByDescending(r => r.Severity == "info").ToList();

            TxtAiNoData.Visibility = Visibility.Collapsed;
            AiRecList.Visibility = Visibility.Visible;
            AiRecList.ItemsSource = null;
            AiRecList.ItemsSource = sorted;

            var totalSavings = sorted.Sum(r => r.PotentialSavings);
            TxtAiBadgeResult.Text = $"{sorted.Count} rec" + (totalSavings > 0 ? $" | ${totalSavings:F0}/mo" : "");
        }
        else
        {
            TxtAiNoData.Text = LocalizationService.Instance["ai.noData"];
            TxtAiNoData.Visibility = Visibility.Visible;
        }

    }

    private void BtnExportTxt_Click(object sender, RoutedEventArgs e)
    {
        if (_lastResult == null) return;
        var svc = new ConfigExportService();
        var text = svc.ExportTxt(_lastResult, GetConfig());
        ExportConfig(text, "txt");
    }

    private void BtnExportPdf_Click(object sender, RoutedEventArgs e)
    {
        if (_lastResult == null) return;
        var svc = new ConfigExportService();
        var html = svc.ExportPdf(_lastResult, GetConfig());
        ExportConfig(html, "html");
    }

    private void BtnShowDiagram_Click(object sender, RoutedEventArgs e)
    {
        if (_lastResult == null) return;
        var diagram = DiagramBuilder.BuildDiagram(_lastResult);
        DiagramContainer.Child = diagram;
        PanelDiagram.Visibility = Visibility.Visible;
        UpdateStatus("Схему побудовано");
    }

    private void BtnExportDiagramSvg_Click(object sender, RoutedEventArgs e)
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
            UpdateStatus(string.Format(LocalizationService.Instance["status.saved"], saveDialog.FileName));
        }
    }

    private void BtnExportMermaid_Click(object sender, RoutedEventArgs e)
    {
        if (_lastResult == null) return;
        var svc = new ConfigExportService();
        var mermaid = svc.ExportMermaid(_lastResult, GetConfig());
        Clipboard.SetText(mermaid);
        UpdateStatus(LocalizationService.Instance["status.copied"]);
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
            UpdateStatus(string.Format(LocalizationService.Instance["status.saved"], saveDialog.FileName));
        }
    }

    private void BtnAiQuerySend_Click(object sender, RoutedEventArgs e)
    {
        var prompt = TxtAiQueryPrompt.Text.Trim();
        if (string.IsNullOrEmpty(prompt)) return;

        if (_aiSettings.EnableRealAi && _aiSettings.Provider != AiProvider.None && !string.IsNullOrEmpty(_aiSettings.ApiKey))
        {
            var _ = AnalyzeWithRealAiAsync(prompt);
        }
        else if (_aiSettings.EnableRealAi && _aiSettings.Provider == AiProvider.LocalOllama)
        {
            var _ = AnalyzeWithRealAiAsync(prompt);
        }
        else
        {
            if (_aiSettings.EnableRealAi)
            {
                TxtAiQueryResult.Text = "⚠️ Real AI увімкнено, але не налаштовано API ключ.\nНатисніть «AI Settings» вгорі, оберіть провайдера та вкажіть ключ.\n\nАбо використайте шаблони нижче.";
                AiQueryResultPanel.Visibility = Visibility.Visible;
                BtnApplyAiQuery.Visibility = Visibility.Collapsed;
                return;
            }
            var (parsedConfig, modules) = _promptParser.Parse(prompt);
            ApplyParsedResult(parsedConfig, modules);
        }
    }

    private async Task AnalyzeWithRealAiAsync(string prompt)
    {
        BtnAiQuerySend.IsEnabled = false;
        TxtAiQueryResult.Text = LocalizationService.Instance["results.aiAnalyzing"];
        AiQueryResultPanel.Visibility = Visibility.Visible;
        BtnApplyAiQuery.Visibility = Visibility.Collapsed;

        try
        {
            var aiService = new AiApiService(_aiSettings);
            var response = await aiService.GetRecommendation(prompt);
            TxtAiQueryResult.Text = response ?? "No response from AI.";
            BtnApplyAiQuery.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            TxtAiQueryResult.Text = $"Error: {ex.Message}\n\nTry using the templates below instead.";
        }
        finally
        {
            BtnAiQuerySend.IsEnabled = true;
        }
    }

    private void ApplyParsedResult(ProjectConfig config, List<string> moduleNames)
    {
        TxtUserCount.Text = config.UserCount.ToString();
        CmbDeployment.SelectedIndex = config.DeploymentType switch
        {
            DeploymentType.Kubernetes => 0,
            DeploymentType.Windows => 1,
            _ => 2
        };

        if (moduleNames.Count > 0)
        {
            foreach (var mod in _engine.Modules)
            {
                mod.IsEnabled = moduleNames.Contains(mod.Name, StringComparer.OrdinalIgnoreCase);
            }
            ModulesPanel.ItemsSource = null;
            ModulesPanel.ItemsSource = _engine.Modules;
        }

        AiQueryResultPanel.Visibility = Visibility.Visible;
        TxtAiQueryResult.Text = $"Застосовано: {config.UserCount} користувачів, {config.DeploymentType}";
        BtnApplyAiQuery.Visibility = Visibility.Collapsed;
    }

    private void BtnApplyAiQuery_Click(object sender, RoutedEventArgs e)
    {
        var (parsedConfig, moduleNames) = _promptParser.Parse(TxtAiQueryPrompt.Text);
        ApplyParsedResult(parsedConfig, moduleNames);
    }

    private void BtnTemplate1_Click(object sender, RoutedEventArgs e)
    {
        TxtUserCount.Text = "200";
        foreach (var m in _engine.Modules)
        {
            m.IsEnabled = m.Name is "App Server" or "ROBOT" or "Web" or "ForceBPM" or "LMS" or "HR Portal";
        }
        ModulesPanel.ItemsSource = null;
        ModulesPanel.ItemsSource = _engine.Modules;
        UpdateStatus("Template: 200 users + LMS + HR");
    }

    private void BtnTemplate2_Click(object sender, RoutedEventArgs e)
    {
        TxtUserCount.Text = "1000";
        CmbDeployment.SelectedIndex = 0;
        foreach (var m in _engine.Modules)
        {
            m.IsEnabled = m.Name is not "Windows Infrastructure";
        }
        ChkHa.IsChecked = true;
        ModulesPanel.ItemsSource = null;
        ModulesPanel.ItemsSource = _engine.Modules;
        UpdateStatus("Template: 1000 users, HA, K8s");
    }

    private void BtnTemplate3_Click(object sender, RoutedEventArgs e)
    {
        TxtUserCount.Text = "25";
        CmbDeployment.SelectedIndex = 0;
        ChkHa.IsChecked = false;
        SldOverprov.Value = 1.0;
        foreach (var m in _engine.Modules)
        {
            m.IsEnabled = m.Name is "App Server" or "Web" or "ForceBPM";
        }
        ModulesPanel.ItemsSource = null;
        ModulesPanel.ItemsSource = _engine.Modules;
        UpdateStatus("Template: 25 users, minimal");
    }
}

public class CompareRow
{
    public string Name { get; set; } = "";
    public string Basic { get; set; } = "";
    public string Performance { get; set; } = "";
    public string Recommended { get; set; } = "";
}

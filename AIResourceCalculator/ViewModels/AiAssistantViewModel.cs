using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using AIResourceCalculator.Interfaces;
using AIResourceCalculator.Localization;
using AIResourceCalculator.Models;
using AIResourceCalculator.Services;

namespace AIResourceCalculator.ViewModels;

public class AiAssistantViewModel : INotifyPropertyChanged
{
    private readonly ILocalizationService _loc;
    private ProjectConfig? _parsedConfig;
    private List<string>? _parsedModules;

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

    public ICommand AnalyzePromptCommand { get; }
    public ICommand ApplyParsedConfigCommand { get; }
    public ICommand UseTemplateCommand { get; }

    public event System.Action<ProjectConfig, List<string>?>? ConfigParsed;

    public AiAssistantViewModel()
    {
        _loc = LocalizationService.Instance;

        AnalyzePromptCommand = new RelayCommand(_ => AnalyzePrompt());
        ApplyParsedConfigCommand = new RelayCommand(_ => ApplyParsedConfig());
        UseTemplateCommand = new RelayCommand(p => UseTemplate(p?.ToString() ?? ""));
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
        }
        catch (System.Exception ex)
        {
            AssistantResult = $"Error: {ex.Message}";
            IsAssistantResultVisible = true;
        }
    }

    private void ApplyParsedConfig()
    {
        if (_parsedConfig != null)
            ConfigParsed?.Invoke(_parsedConfig, _parsedModules);
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

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
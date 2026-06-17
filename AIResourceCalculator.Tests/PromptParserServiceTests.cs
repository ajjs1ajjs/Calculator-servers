using AIResourceCalculator.Models;
using AIResourceCalculator.Services;

namespace AIResourceCalculator.Tests;

public class PromptParserServiceTests
{
    private readonly PromptParserService _parser = new();

    [Fact]
    public void Parse_UkrainianUsers_ReturnsCorrectUserCount()
    {
        var (config, modules) = _parser.Parse("система на 200 користувачів з LMS та HR Portal");
        Assert.Equal(200, config.UserCount);
    }

    [Fact]
    public void Parse_EnglishUsers_ReturnsCorrectUserCount()
    {
        var (config, modules) = _parser.Parse("system for 500 users with App Server and Web");
        Assert.Equal(500, config.UserCount);
    }

    [Fact]
    public void Parse_DefaultUsers_WhenNoUserMatch()
    {
        var (config, modules) = _parser.Parse("k8s system with performance");
        Assert.Equal(100, config.UserCount);
    }

    [Fact]
    public void Parse_Kubernetes_ReturnsK8sDeployment()
    {
        var (config, modules) = _parser.Parse("k8s deployment for 300 users");
        Assert.Equal(DeploymentType.Kubernetes, config.DeploymentType);
    }

    [Fact]
    public void Parse_Windows_ReturnsWindowsDeployment()
    {
        var (config, modules) = _parser.Parse("windows deployment for 100 users");
        Assert.Equal(DeploymentType.Windows, config.DeploymentType);
    }

    [Fact]
    public void Parse_Hybrid_ReturnsHybridDeployment()
    {
        var (config, modules) = _parser.Parse("гібрид розгортання для 200 users");
        Assert.Equal(DeploymentType.Hybrid, config.DeploymentType);
    }

    [Fact]
    public void Parse_HybridUkrainian_FormMatches()
    {
        var (config, modules) = _parser.Parse("гібридне розгортання");
        Assert.Equal(DeploymentType.Hybrid, config.DeploymentType);
    }

    [Fact]
    public void Parse_K8sTakesPrecedenceOverWindows()
    {
        var (config, modules) = _parser.Parse("k8s windows system");
        Assert.Equal(DeploymentType.Kubernetes, config.DeploymentType);
    }

    [Fact]
    public void Parse_PerformanceProfile_ReturnsDocumentFlow()
    {
        var (config, modules) = _parser.Parse("document flow system for 150 users");
        Assert.Equal(LoadProfile.Performance, config.LoadProfile);
        Assert.Equal(ProductType.DocumentFlow, config.ProductType);
    }

    [Fact]
    public void Parse_StandardProfile_ByDefault()
    {
        var (config, modules) = _parser.Parse("k8s system for 100 users");
        Assert.Equal(LoadProfile.Basic, config.LoadProfile);
        Assert.Equal(ProductType.Standard, config.ProductType);
    }

    [Fact]
    public void Parse_DetectsModules()
    {
        var (config, modules) = _parser.Parse("system with LMS, HR Portal and ForceBPM");
        Assert.Contains("LMS", modules);
        Assert.Contains("HR Portal", modules);
        Assert.Contains("ForceBPM", modules);
    }

    [Fact]
    public void Parse_NoModulesSpecified_ReturnsDefaults()
    {
        var (config, modules) = _parser.Parse("system for 50 users");
        Assert.NotEmpty(modules);
        Assert.Contains("App Server", modules);
    }

    [Fact]
    public void Parse_SelectedModules_StoredInConfig()
    {
        var (config, modules) = _parser.Parse("system with ROBOT and Web");
        Assert.Equal(modules, config.SelectedModules);
    }

    [Fact]
    public void Parse_WindowsInfraModule_Detected()
    {
        var (config, modules) = _parser.Parse("windows infrastructure deployment");
        Assert.Contains("Windows Infrastructure", modules);
    }
}

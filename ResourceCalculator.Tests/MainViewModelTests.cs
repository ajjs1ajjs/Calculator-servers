using System.Linq;
using ResourceCalculator.Data;
using ResourceCalculator.Interfaces;
using ResourceCalculator.Localization;
using ResourceCalculator.Models;
using ResourceCalculator.Services;
using ResourceCalculator.ViewModels;

namespace ResourceCalculator.Tests;

// Тести логіки за кнопками/прив'язками (MVVM). Реальний WPF-UI не інстанціюється — перевіряємо
// команди та властивості ViewModel, на які зав'язані елементи інтерфейсу.
public class MainViewModelTests
{
    private sealed class FakeDataService : IDataService
    {
        public void SaveMatrix(SizingMatrix matrix) { }
        public SizingMatrix LoadMatrix() => new();
        public void ClearMatrix() { }
    }

    private sealed class FakeHistoryService : ICalculationHistoryService
    {
        public readonly List<CalculationHistoryItem> Items = new();
        public List<CalculationHistoryItem> LoadHistory() => new(Items);
        public void SaveToHistory(ProjectConfig config, ResourceRequirement req)
            => Items.Insert(0, new CalculationHistoryItem { Config = config, TotalCpu = req.TotalCpu });
    }

    private static MainViewModel BuildVm(out FakeHistoryService history)
    {
        var loc = LocalizationService.Instance;
        loc.LoadLanguage("uk"); // детермінований старт
        var data = new FakeDataService();
        var manager = new MatrixManager(data, new SizingMatrix());
        history = new FakeHistoryService();
        var presenter = new ResultsPresenter(new ConfigExportService(), new ValidationEngine(loc));
        var engine = new SizingEngine(manager.Matrix);
        var envBuilder = new EnvironmentBuilder(engine, loc);
        return new MainViewModel(loc, data, history, manager, presenter, envBuilder, engine);
    }

    [Fact]
    public void Constructor_PopulatesModulesAndStatus()
    {
        var vm = BuildVm(out _);
        Assert.NotEmpty(vm.Modules);
        Assert.False(string.IsNullOrEmpty(vm.StatusText));
    }

    [Fact]
    public void EnvModuleCounts_DevUsesOwnModuleCount()
    {
        var vm = BuildVm(out _);
        vm.UserCount = "100";
        // Увімкнути LMS і задати велику к-сть LMS у DEV.
        foreach (var m in vm.Modules) if (m.Name == "LMS") m.IsEnabled = true;
        vm.IncludeDev = true;
        var lmsRow = vm.EnvModuleCounts.First(r => r.ModuleName == "LMS");
        lmsRow.DevUsers = 500;     // DEV LMS = 500 → LMS-GraphQL (LmsGraphqlLoadTest): 7 (на 250) + 10 (екстраполяція) = 17

        vm.CalculateCommand.Execute(null);

        var dev = vm.Environments.First(e => e.Name == "DEV");
        var lmsComp = dev.Requirement.Components.First(c =>
            c.Category == "LMS" && c.Name == ComponentDisplayName.Localize("LMS-GraphQL"));
        Assert.Equal(17, lmsComp.Replicas);
    }

    // --- HR Portal і LMS увімкнені в PROD автоматично додаються в DEV/TEST/PreProd (за замовчуванням,
    // без ручного налаштування рядків EnvModuleCounts) ---
    [Fact]
    public void EnabledModulesInProd_AutomaticallyIncludedInAllDerivedEnvironments()
    {
        var vm = BuildVm(out _);
        vm.UserCount = "100";
        foreach (var m in vm.Modules) if (m.Name is "LMS" or "HR Portal") m.IsEnabled = true;
        vm.IncludeDev = true;
        vm.IncludeTest = true;
        vm.IncludePredProd = true;

        vm.CalculateCommand.Execute(null);

        foreach (var envName in new[] { "DEV", "TEST", "PreProd" })
        {
            var env = vm.Environments.First(e => e.Name == envName);
            Assert.Contains(env.Requirement.Components, c => c.Category == "LMS");
            Assert.Contains(env.Requirement.Components, c => c.Category == "HR Portal");
        }
    }

    [Fact]
    public void EnvModuleCounts_DisablingModuleExcludesItFromEnvironment()
    {
        var vm = BuildVm(out _);
        vm.UserCount = "100";
        foreach (var m in vm.Modules) if (m.Name == "LMS") m.IsEnabled = true; // LMS у PROD
        vm.IncludeDev = true;
        vm.EnvModuleCounts.First(r => r.ModuleName == "LMS").DevEnabled = false; // але не в DEV

        vm.CalculateCommand.Execute(null);

        var prod = vm.Environments.First(e => e.Name == "PROD");
        var dev = vm.Environments.First(e => e.Name == "DEV");
        Assert.Contains(prod.Requirement.Components, c => c.Category == "LMS");      // у PROD є
        Assert.DoesNotContain(dev.Requirement.Components, c => c.Category == "LMS"); // у DEV немає
    }

    [Fact]
    public void CalculateCommand_ProducesResultsAndSwitchesToResultsTab()
    {
        var vm = BuildVm(out _);
        vm.UserCount = "100";

        vm.CalculateCommand.Execute(null);

        Assert.NotEqual("0", vm.TotalCpu);
        Assert.Equal(2, vm.SelectedTabIndex);          // вкладка "Результати" (0=Матриця, 1=Параметри, 2=Результати)
        Assert.NotEmpty(vm.ResultInfrastructure);
    }

    [Fact]
    public void CalculateCommand_SavesToHistory()
    {
        var vm = BuildVm(out var history);
        vm.UserCount = "100";

        vm.CalculateCommand.Execute(null);

        Assert.NotEmpty(history.Items);
        Assert.True(vm.HasHistory);
    }

    [Fact]
    public void CalculateCommand_InvalidUserCount_FallsBackWithoutThrow()
    {
        var vm = BuildVm(out _);
        vm.UserCount = "не-число";

        var ex = Record.Exception(() => vm.CalculateCommand.Execute(null));

        Assert.Null(ex);
        Assert.NotEqual("0", vm.TotalCpu);             // відкат на 100 користувачів
    }

    [Fact]
    public void DeploymentIndex_Windows_CalculatesVmInfrastructure()
    {
        var vm = BuildVm(out _);
        vm.DeploymentIndex = 1;                         // Windows
        vm.UserCount = "100";

        vm.CalculateCommand.Execute(null);

        Assert.Contains(vm.ResultInfrastructure, n => n.Name.Contains("SQL"));
        Assert.False(vm.HasPodRequests);                // у Windows подів немає
    }

    [Fact]
    public void Calculate_K8s_ExposesPodRequests()
    {
        var vm = BuildVm(out _);
        vm.DeploymentIndex = 0;                         // Kubernetes
        vm.UserCount = "100";

        vm.CalculateCommand.Execute(null);

        Assert.True(vm.HasPodRequests);
    }

    // --- Гібрид: HAProxy та всі кнопки вільно керуються користувачем (без блокувань) ---
    [Fact]
    public void DeploymentIndex_Hybrid_AllTogglesEditable()
    {
        var vm = BuildVm(out _);
        vm.DeploymentIndex = 2;                         // Hybrid

        Assert.True(vm.CanToggleHaProxy);               // HAProxy вільно вмикається/вимикається
        var haproxy = vm.EnvNodeToggles.First(r => r.Key == "haproxy");
        Assert.True(haproxy.IsEditable);                // DEV/TEST/PreProd — теж клікабельні
    }

    [Fact]
    public void DeploymentIndex_SwitchDeployment_KeepsUserTogglesEditable()
    {
        var vm = BuildVm(out _);
        vm.DeploymentIndex = 2;                         // Hybrid
        vm.DeploymentIndex = 0;                         // Kubernetes

        Assert.True(vm.CanToggleHaProxy);
        var haproxy = vm.EnvNodeToggles.First(r => r.Key == "haproxy");
        Assert.True(haproxy.IsEditable);
    }

    // --- ForceBPM: клікабельний у будь-якому типі розгортання (кожен модуль керується окремо) ---
    [Fact]
    public void DeploymentIndex_Kubernetes_ForceBpmIsToggleable()
    {
        var vm = BuildVm(out _);
        vm.DeploymentIndex = 0;                         // Kubernetes

        var forceBpm = vm.Modules.First(m => m.Name == "ForceBPM");
        Assert.True(forceBpm.IsEnabled);
        Assert.True(forceBpm.IsUserToggleable);
    }

    [Fact]
    public void DeploymentIndex_Hybrid_ForceBpmIsToggleable()
    {
        var vm = BuildVm(out _);
        vm.DeploymentIndex = 2;                         // Hybrid

        var forceBpm = vm.Modules.First(m => m.Name == "ForceBPM");
        Assert.True(forceBpm.IsEnabled);
        Assert.True(forceBpm.IsUserToggleable);
    }

    [Fact]
    public void LangSwitchCommand_TogglesLanguage()
    {
        var vm = BuildVm(out _);
        var before = vm.LangName;

        vm.LangSwitchCommand.Execute(null);

        Assert.NotEqual(before, vm.LangName);
        LocalizationService.Instance.LoadLanguage("uk"); // відновити стан для інших тестів
    }
}

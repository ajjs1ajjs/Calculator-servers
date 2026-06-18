using AIResourceCalculator.Data;
using AIResourceCalculator.Interfaces;
using AIResourceCalculator.Localization;
using AIResourceCalculator.Models;
using AIResourceCalculator.Services;
using AIResourceCalculator.ViewModels;

namespace AIResourceCalculator.Tests;

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
        return new MainViewModel(loc, data, history, manager, presenter, engine);
    }

    [Fact]
    public void Constructor_PopulatesModulesAndStatus()
    {
        var vm = BuildVm(out _);
        Assert.NotEmpty(vm.Modules);
        Assert.False(string.IsNullOrEmpty(vm.StatusText));
    }

    [Fact]
    public void CalculateCommand_ProducesResultsAndSwitchesToResultsTab()
    {
        var vm = BuildVm(out _);
        vm.UserCount = "100";

        vm.CalculateCommand.Execute(null);

        Assert.NotEqual("0", vm.TotalCpu);
        Assert.Equal(2, vm.SelectedTabIndex);          // вкладка "Результати"
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

    [Fact]
    public void ProductIndex_DocumentFlow_KeepsModulesAndUpdatesStatus()
    {
        var vm = BuildVm(out _);
        vm.ProductIndex = 1;                            // Документообіг

        Assert.Equal(1, vm.ProductIndex);
        Assert.NotEmpty(vm.Modules);
        Assert.False(string.IsNullOrEmpty(vm.StatusText));
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

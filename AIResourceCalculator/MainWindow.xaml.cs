using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using AIResourceCalculator.Services;
using AIResourceCalculator.ViewModels;

namespace AIResourceCalculator;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;

    public MainWindow()
    {
        InitializeComponent();
        _vm = new MainViewModel();
        DataContext = _vm;

        ModulesPanel.ItemsSource = _vm.Modules;
        GridMatrixMsSql.ItemsSource = _vm.MsSqlRanges;
        GridMatrixMsSqlPerf.ItemsSource = _vm.MsSqlPerformanceRanges;
        GridMatrixK8sStandard.ItemsSource = _vm.K8sStandardComponents;
        GridMatrixK8sDocumentFlow.ItemsSource = _vm.K8sDocumentFlowComponents;
        GridMatrixInfra.ItemsSource = _vm.InfraNodes;
        GridInfrastructure.ItemsSource = _vm.ResultInfrastructure;
        AiTableResults.ItemsSource = _vm.AiInfrastructure;
        QuickRecList.ItemsSource = _vm.AiRecommendations;

        CommandManager.AddPreviewExecutedHandler(TxtUserCount, OnPaste);
        PreviewTextInput += (_, e) =>
        {
            if (e.Text != null && e.Text.Any(c => !char.IsDigit(c)))
                e.Handled = true;
        };

        _vm.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnPaste(object? sender, ExecutedRoutedEventArgs e)
    {
        if (e.Command == ApplicationCommands.Paste && Clipboard.ContainsText())
        {
            if (!int.TryParse(Clipboard.GetText().Trim(), out _))
                e.Handled = true;
        }
    }

    private void OnViewModelPropertyChanged(object? _, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.ResultInfrastructure))
            { GridInfrastructure.ItemsSource = null; GridInfrastructure.ItemsSource = _vm.ResultInfrastructure; }
        else if (e.PropertyName == nameof(MainViewModel.AiInfrastructure))
            { AiTableResults.ItemsSource = null; AiTableResults.ItemsSource = _vm.AiInfrastructure; }
        else if (e.PropertyName == nameof(MainViewModel.AiRecommendations))
            { QuickRecList.ItemsSource = null; QuickRecList.ItemsSource = _vm.AiRecommendations; AiRecResults.ItemsSource = null; AiRecResults.ItemsSource = _vm.AiRecommendations; }
        else if (e.PropertyName == nameof(MainViewModel.IsDiagramVisible))
        {
            PanelDiagram.Visibility = _vm.IsDiagramVisible ? Visibility.Visible : Visibility.Collapsed;
            if (_vm.IsDiagramVisible && _vm.LastResult != null)
                DiagramContainer.Child = DiagramBuilder.BuildDiagram(_vm.LastResult);
        }
        else if (e.PropertyName == nameof(MainViewModel.MsSqlRanges))
            { GridMatrixMsSql.ItemsSource = null; GridMatrixMsSql.ItemsSource = _vm.MsSqlRanges; }
        else if (e.PropertyName == nameof(MainViewModel.MsSqlPerformanceRanges))
            { GridMatrixMsSqlPerf.ItemsSource = null; GridMatrixMsSqlPerf.ItemsSource = _vm.MsSqlPerformanceRanges; }
        else if (e.PropertyName == nameof(MainViewModel.K8sStandardComponents))
            { GridMatrixK8sStandard.ItemsSource = null; GridMatrixK8sStandard.ItemsSource = _vm.K8sStandardComponents; }
        else if (e.PropertyName == nameof(MainViewModel.K8sDocumentFlowComponents))
            { GridMatrixK8sDocumentFlow.ItemsSource = null; GridMatrixK8sDocumentFlow.ItemsSource = _vm.K8sDocumentFlowComponents; }
        else if (e.PropertyName == nameof(MainViewModel.InfraNodes))
            { GridMatrixInfra.ItemsSource = null; GridMatrixInfra.ItemsSource = _vm.InfraNodes; }
        else if (e.PropertyName == nameof(MainViewModel.Modules))
            { ModulesPanel.ItemsSource = null; ModulesPanel.ItemsSource = _vm.Modules; }
    }

}

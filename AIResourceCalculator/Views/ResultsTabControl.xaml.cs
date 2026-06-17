using System.Windows;
using System.Windows.Controls;
using AIResourceCalculator.Services;
using AIResourceCalculator.ViewModels;

namespace AIResourceCalculator.Views;

public partial class ResultsTabControl : UserControl
{
    public ResultsTabControl()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (DataContext is not MainViewModel vm) return;
            GridInfrastructure.ItemsSource = vm.ResultInfrastructure;
            AiTableResults.ItemsSource = vm.AiInfrastructure;
            AiRecResults.ItemsSource = vm.AiRecommendations;

            vm.PropertyChanged += (_, e) =>
            {
                switch (e.PropertyName)
                {
                    case nameof(MainViewModel.ResultInfrastructure):
                        GridInfrastructure.ItemsSource = vm.ResultInfrastructure;
                        break;
                    case nameof(MainViewModel.AiInfrastructure):
                        AiTableResults.ItemsSource = vm.AiInfrastructure;
                        break;
                    case nameof(MainViewModel.AiRecommendations):
                        AiRecResults.ItemsSource = vm.AiRecommendations;
                        break;
                    case nameof(MainViewModel.IsDiagramVisible):
                        PanelDiagram.Visibility = vm.IsDiagramVisible ? Visibility.Visible : Visibility.Collapsed;
                        if (vm.IsDiagramVisible && vm.LastResult != null)
                            DiagramContainer.Child = DiagramBuilder.BuildDiagram(vm.LastResult);
                        break;
                }
            };
        };
    }
}

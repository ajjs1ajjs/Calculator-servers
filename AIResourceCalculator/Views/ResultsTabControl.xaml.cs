using System.Windows.Controls;
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

            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(MainViewModel.ResultInfrastructure))
                    GridInfrastructure.ItemsSource = vm.ResultInfrastructure;
            };
        };
    }
}

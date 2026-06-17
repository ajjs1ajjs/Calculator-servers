using System.Windows.Controls;
using AIResourceCalculator.ViewModels;

namespace AIResourceCalculator.Views;

public partial class MatrixTabControl : UserControl
{
    public MatrixTabControl()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (DataContext is not MainViewModel vm) return;
            GridMatrixMsSql.ItemsSource = vm.MsSqlRanges;
            GridMatrixMsSqlPerf.ItemsSource = vm.MsSqlPerformanceRanges;
            GridMatrixInfra.ItemsSource = vm.InfraNodes;
        };
    }
}

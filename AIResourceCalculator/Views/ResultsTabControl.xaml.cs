using System.Windows.Controls;
using System.Windows.Input;
using AIResourceCalculator.ViewModels;

namespace AIResourceCalculator.Views;

public partial class ResultsTabControl : UserControl
{
    // Колесо миші прокручує всю сторінку результатів, де б не був курсор — навіть над таблицями
    // (DataGrid за замовчуванням «з'їдає» прокрутку, тож перехоплюємо її на рівні ScrollViewer).
    private void RootScroll_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        RootScroll.ScrollToVerticalOffset(RootScroll.VerticalOffset - e.Delta);
        e.Handled = true;
    }

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

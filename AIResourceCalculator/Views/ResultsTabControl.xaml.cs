using System.Windows.Controls;
using System.Windows.Input;

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
        // GridInfrastructure.ItemsSource прив'язано в XAML (ResultInfrastructure). Раніше тут на
        // кожен Loaded реєструвався анонімний PropertyChanged без відписки — витік пам'яті, бо
        // обробники накопичувались при кожному показі вкладки.
    }
}

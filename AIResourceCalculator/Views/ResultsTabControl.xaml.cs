using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace AIResourceCalculator.Views;

public partial class ResultsTabControl : UserControl
{
    // Прокрутка колесом: спершу намагаємось прокрутити ВНУТРІШНІЙ ScrollViewer під курсором
    // (розбивка по середовищах, велика таблиця) — і лише коли він уперся в межу, прокручуємо всю
    // сторінку. Так працює і прокрутка в розділі, і плавна прокрутка сторінки, попри те, що
    // DataGrid за замовчуванням «з'їдає» подію.
    private void RootScroll_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var inner = FindScrollableAncestor(e.OriginalSource as DependencyObject, e.Delta);
        if (inner != null && inner != RootScroll)
            return; // дозволяємо внутрішньому ScrollViewer прокрутитись

        RootScroll.ScrollToVerticalOffset(RootScroll.VerticalOffset - e.Delta);
        e.Handled = true;
    }

    // Шукає найближчий ScrollViewer-предок, який ще МОЖЕ прокрутитись у напрямку колеса.
    private static ScrollViewer? FindScrollableAncestor(DependencyObject? from, int delta)
    {
        for (var node = from; node != null; node = VisualTreeHelper.GetParent(node))
        {
            if (node is ScrollViewer sv && sv.ScrollableHeight > 0)
            {
                bool canUp = delta > 0 && sv.VerticalOffset > 0;
                bool canDown = delta < 0 && sv.VerticalOffset < sv.ScrollableHeight;
                if (canUp || canDown) return sv;
            }
        }
        return null;
    }

    public ResultsTabControl()
    {
        InitializeComponent();
        // GridInfrastructure.ItemsSource прив'язано в XAML (ResultInfrastructure). Раніше тут на
        // кожен Loaded реєструвався анонімний PropertyChanged без відписки — витік пам'яті, бо
        // обробники накопичувались при кожному показі вкладки.
    }
}

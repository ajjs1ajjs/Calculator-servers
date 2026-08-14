using System.Windows.Controls;

namespace ResourceCalculator.Views;

public partial class MatrixTabControl : UserControl
{
    public MatrixTabControl()
    {
        InitializeComponent();
        // ItemsSource усіх таблиць прив'язано в XAML (MatrixVM.*) — вони самі оновлюються при
        // реімпорті/скиданні матриці, бо MatrixViewModel піднімає PropertyChanged для колекцій.
    }
}

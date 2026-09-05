using System.Windows.Controls;
using ResourceCalculator.ViewModels;

namespace ResourceCalculator.Views;

public partial class MatrixTabControl : UserControl
{
    public MatrixTabControl()
    {
        InitializeComponent();
        // ItemsSource усіх таблиць прив'язано в XAML (MatrixVM.*) — вони самі оновлюються при
        // реімпорті/скиданні матриці, бо MatrixViewModel піднімає PropertyChanged для колекцій.
    }

    // Захист: спроба змінити будь-яке значення в матриці потребує пароля (як і кнопки
    // «Зберегти/Перерахувати/Скинути»). Якщо пароль не підтверджено — редагування скасовується.
    private void Grid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        if (!vm.MatrixVM.EnsureUnlocked())
            e.Cancel = true;
    }
}

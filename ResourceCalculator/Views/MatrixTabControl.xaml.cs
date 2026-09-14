using Avalonia.Controls;
using Avalonia.Threading;
using ResourceCalculator.ViewModels;

namespace ResourceCalculator.Views;

public partial class MatrixTabControl : UserControl
{
    public MatrixTabControl()
    {
        InitializeComponent();
    }

    // Захист (як у WPF-версії): спроба змінити значення в матриці потребує пароля.
    // Якщо пароль не підтверджено — редагування скасовується.
    // Діалог показуємо через Dispatcher.Post: всередині BeginningEdit модальне
    // вікно зависає (DataGrid ще тримає захоплення миші), тому спочатку виходимо з події.
    private void Grid_BeginningEdit(object? sender, DataGridBeginningEditEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        if (vm.MatrixVM.IsUnlocked) return;
        e.Cancel = true;
        var dg = sender as DataGrid;
        Dispatcher.UIThread.Post(async () =>
        {
            try
            {
                var ok = await vm.MatrixVM.EnsureUnlockedAsync();
                if (ok && dg is not null)
                    dg.BeginEdit();
            }
            catch { }
        });
    }
}

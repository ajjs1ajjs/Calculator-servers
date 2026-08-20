using Avalonia.Controls;
using Avalonia.Interactivity;
using ResourceCalculator.ViewModels;

namespace ResourceCalculator.Avalonia.Views;

public partial class CalculatorTabControl : UserControl
{
    private bool _wired;

    public CalculatorTabControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        if (DataContext is not ViewModels.MainViewModel vm) return;
        ModulesPanel.ItemsSource = vm.SelectableModules;

        if (_wired) return;
        _wired = true;

        // Дозволяємо лише цифри саме у полі кількості користувачів.
        TxtUserCount.TextInput += (_, args) =>
        {
            if (args.Text != null && args.Text.Any(c => !char.IsDigit(c)))
                args.Handled = true;
        };

        vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(ViewModels.MainViewModel.Modules))
                ModulesPanel.ItemsSource = vm.SelectableModules;
        };
    }

    // Посилання «Детальніше у вкладці «Результати»» — просто перемикає вкладку.
    private void OpenResultsTab_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.MainViewModel vm) vm.SelectedTabIndex = 2; // 0=Матриця, 1=Параметри, 2=Результати
    }
}
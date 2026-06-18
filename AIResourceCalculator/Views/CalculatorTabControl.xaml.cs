using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using AIResourceCalculator.ViewModels;

namespace AIResourceCalculator.Views;

public partial class CalculatorTabControl : UserControl
{
    private bool _wired;

    public CalculatorTabControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        ModulesPanel.ItemsSource = vm.Modules;

        // Підписки чіпляємо лише один раз — Loaded може спрацьовувати багаторазово.
        if (_wired) return;
        _wired = true;

        CommandManager.AddPreviewExecutedHandler(TxtUserCount, OnPaste);
        // Дозволяємо лише цифри саме у полі кількості користувачів, а не в усьому контролі.
        TxtUserCount.PreviewTextInput += (_, args) =>
        {
            if (args.Text != null && args.Text.Any(c => !char.IsDigit(c)))
                args.Handled = true;
        };

        vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(MainViewModel.Modules))
                ModulesPanel.ItemsSource = vm.Modules;
        };
    }

    private void OnPaste(object? sender, ExecutedRoutedEventArgs e)
    {
        if (e.Command == ApplicationCommands.Paste && Clipboard.ContainsText())
        {
            if (!int.TryParse(Clipboard.GetText().Trim(), out _))
                e.Handled = true;
        }
    }
}

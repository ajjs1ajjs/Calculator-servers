using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using AIResourceCalculator.ViewModels;

namespace AIResourceCalculator.Views;

public partial class CalculatorTabControl : UserControl
{
    public CalculatorTabControl()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (DataContext is not MainViewModel vm) return;
            ModulesPanel.ItemsSource = vm.Modules;
            QuickRecList.ItemsSource = vm.AiRecommendations;

            CommandManager.AddPreviewExecutedHandler(TxtUserCount, OnPaste);
            PreviewTextInput += (_, e) =>
            {
                if (e.Text != null && e.Text.Any(c => !char.IsDigit(c)))
                    e.Handled = true;
            };

            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(MainViewModel.AiRecommendations))
                    QuickRecList.ItemsSource = vm.AiRecommendations;
                if (e.PropertyName == nameof(MainViewModel.Modules))
                    ModulesPanel.ItemsSource = vm.Modules;
            };
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

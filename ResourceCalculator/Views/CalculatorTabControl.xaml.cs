using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ResourceCalculator.ViewModels;

namespace ResourceCalculator.Views;

public partial class CalculatorTabControl : UserControl
{
    private bool _wired;

    public CalculatorTabControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        ModulesPanel.ItemsSource = vm.SelectableModules;

        if (_wired) return;
        _wired = true;

        TxtUserCount.AddHandler(TextInputEvent, OnTextInput, RoutingStrategies.Tunnel);
        
        vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(MainViewModel.Modules))
                ModulesPanel.ItemsSource = vm.SelectableModules;
        };
    }

    private void OnTextInput(object? sender, TextInputEventArgs e)
    {
        if (e.Text != null && e.Text.Any(c => !char.IsDigit(c)))
            e.Handled = true;
    }

    private void OpenResultsTab_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm) vm.SelectedTabIndex = 2;
    }
}

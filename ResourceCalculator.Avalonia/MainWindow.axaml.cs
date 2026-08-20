using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ResourceCalculator.Avalonia;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private async void CheckUpdates_Click(object? sender, RoutedEventArgs e)
    {
        await ((App)Application.Current).CheckForUpdatesAsync(silent: false);
    }
}
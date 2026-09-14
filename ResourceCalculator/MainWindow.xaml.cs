using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ResourceCalculator;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private async void CheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        if (Application.Current is App app)
            await app.CheckForUpdatesAsync(silent: false);
    }
}

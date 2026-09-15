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
        try
        {
            if (Application.Current is App app)
                await app.CheckForUpdatesAsync(silent: false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"CheckUpdates_Click crashed: {ex.Message}");
        }
    }
}

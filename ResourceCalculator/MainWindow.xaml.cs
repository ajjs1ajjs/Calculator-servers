using System.Threading.Tasks;
using System.Windows;

namespace ResourceCalculator;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private async void CheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        await ((App)Application.Current).CheckForUpdatesAsync(silent: false);
    }
}

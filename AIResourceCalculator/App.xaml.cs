using System.Windows;
using AIResourceCalculator.Services;

namespace AIResourceCalculator;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ThemeService.Initialize();
    }
}


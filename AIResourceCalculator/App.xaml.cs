using System.Windows;
using AIResourceCalculator.Interfaces;
using AIResourceCalculator.Services;

namespace AIResourceCalculator;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var themeService = new ThemeService();
        themeService.Initialize();
    }
}

using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using AIResourceCalculator.Interfaces;
using AIResourceCalculator.Localization;
using AIResourceCalculator.Services;
using AIResourceCalculator.ViewModels;

namespace AIResourceCalculator;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var sc = new ServiceCollection();

        sc.AddSingleton<ILocalizationService>(_ => LocalizationService.Instance);
        sc.AddSingleton<IThemeService, ThemeService>();
        sc.AddTransient<IDataService, DataService>();
        sc.AddTransient<ICalculationHistoryService, CalculationHistoryService>();
        sc.AddTransient<IValidationEngine, ValidationEngine>();
        sc.AddTransient<IAiAdvisorService, AiAdvisorService>();
        sc.AddTransient<MatrixManager>();
        sc.AddTransient<ResultsPresenter>();
        sc.AddSingleton<ISizingEngine>(sp =>
        {
            var mm = sp.GetRequiredService<MatrixManager>();
            return new SizingEngine(mm.Matrix);
        });
        sc.AddTransient<MainViewModel>();

        Services = sc.BuildServiceProvider();

        var themeService = Services.GetRequiredService<IThemeService>();
        themeService.Initialize();

        var mainWindow = new MainWindow();
        mainWindow.DataContext = Services.GetRequiredService<MainViewModel>();
        mainWindow.Show();
    }
}

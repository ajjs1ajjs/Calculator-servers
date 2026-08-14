using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using ResourceCalculator.Interfaces;
using ResourceCalculator.Localization;
using ResourceCalculator.Services;
using ResourceCalculator.ViewModels;

namespace ResourceCalculator;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Глобальний перехоплювач помилок: не даємо застосунку аварійно завершитися без повідомлення.
        DispatcherUnhandledException += OnUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Debug.WriteLine($"Domain unhandled exception: {(args.ExceptionObject as Exception)?.Message}");

        var sc = new ServiceCollection();

        sc.AddSingleton<ILocalizationService>(_ => LocalizationService.Instance);
        sc.AddTransient<IDataService, DataService>();
        sc.AddTransient<ICalculationHistoryService, CalculationHistoryService>();
        sc.AddTransient<IValidationEngine, ValidationEngine>();
        sc.AddSingleton<ResourceCalculator.Data.SizingMatrix>();
        sc.AddSingleton<MatrixManager>();
        sc.AddSingleton<AccessService>();
        sc.AddTransient<ConfigExportService>();
        sc.AddTransient<ResultsPresenter>();
        sc.AddTransient<EnvironmentBuilder>();
        sc.AddSingleton<ISizingEngine>(sp =>
        {
            var mm = sp.GetRequiredService<MatrixManager>();
            return new SizingEngine(mm.Matrix);
        });
        sc.AddTransient<MainViewModel>();
        sc.AddSingleton<IUpdateCheckService, UpdateCheckService>();

        Services = sc.BuildServiceProvider();

        var mainWindow = new MainWindow();
        mainWindow.DataContext = Services.GetRequiredService<MainViewModel>();
        mainWindow.Show();

        _ = CheckForUpdatesAsync().ContinueWith(t =>
        {
            if (t.IsFaulted)
                Debug.WriteLine($"Update check crashed: {t.Exception?.InnerException?.Message}");
        }, TaskContinuationOptions.OnlyOnFaulted);
    }

    private async Task CheckForUpdatesAsync()
    {
        var update = await Services.GetRequiredService<IUpdateCheckService>().CheckForUpdateAsync().ConfigureAwait(false);
        if (update is null) return;

        var loc = LocalizationService.Instance;
        var currentVersion = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "?";
        var result = MessageBox.Show(
            string.Format(loc["update.message"], update.Version, currentVersion),
            loc["update.title"], MessageBoxButton.YesNo, MessageBoxImage.Information);

        if (result == MessageBoxResult.Yes)
        {
            Process.Start(new ProcessStartInfo(update.DownloadUrl) { UseShellExecute = true });
        }
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Debug.WriteLine($"Unhandled exception: {e.Exception}");
        try
        {
            var logPath = Path.Combine(AppContext.BaseDirectory, "error.log");
            File.AppendAllText(logPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {e.Exception}\n\n");
        }
        catch { /* logging must never crash the handler itself */ }
        var loc = LocalizationService.Instance;
        MessageBox.Show(
            string.Format(loc["error.unknown"], e.Exception.Message),
            loc["error.title"], MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}

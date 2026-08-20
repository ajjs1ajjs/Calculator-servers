using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using ResourceCalculator.Avalonia.Dialogs;
using ResourceCalculator.Avalonia.Views;
using ResourceCalculator.Data;
using ResourceCalculator.Interfaces;
using ResourceCalculator.Localization;
using ResourceCalculator.Services;
using ResourceCalculator.ViewModels;

namespace ResourceCalculator.Avalonia;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;
    public static Window? MainWindow { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
                LogError((args.ExceptionObject as Exception)?.Message ?? "unknown");
            Dispatcher.UIThread.UnhandledException += (_, args) =>
            {
                LogError(args.Exception.Message);
                args.Handled = true;
            };

            var sc = new ServiceCollection();

            sc.AddSingleton<ILocalizationService>(_ => LocalizationService.Instance);
            sc.AddTransient<IDataService, DataService>();
            sc.AddTransient<ICalculationHistoryService, CalculationHistoryService>();
            sc.AddTransient<IValidationEngine, ValidationEngine>();
            sc.AddSingleton<SizingMatrix>();
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
            sc.AddSingleton<IDialogService>(sp =>
                new AvaloniaDialogService(sp.GetRequiredService<AccessService>(),
                    () => MainWindow));
            sc.AddSingleton<IFileSaveService>(sp => (IFileSaveService)sp.GetRequiredService<IDialogService>());
            sc.AddSingleton<IThemeService, AvaloniaThemeService>();

            Services = sc.BuildServiceProvider();

            var mainWindow = new MainWindow
            {
                DataContext = Services.GetRequiredService<MainViewModel>()
            };
            MainWindow = mainWindow;
            desktop.MainWindow = mainWindow;
            mainWindow.Show();

            _ = CheckForUpdatesAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private async System.Threading.Tasks.Task CheckForUpdatesAsync()
    {
        try
        {
            var update = await Services.GetRequiredService<IUpdateCheckService>().CheckForUpdateAsync().ConfigureAwait(false);
            if (update is null) return;

            var loc = LocalizationService.Instance;
            var currentVersion = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "?";
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var dialogs = Services.GetRequiredService<IDialogService>();
                if (await dialogs.ConfirmAsync(
                    string.Format(loc["update.message"], update.Version, currentVersion),
                    loc["update.title"]))
                {
                    Process.Start(new ProcessStartInfo(update.DownloadUrl) { UseShellExecute = true });
                }
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Update check crashed: {ex.Message}");
        }
    }

    private static void LogError(string message)
    {
        Debug.WriteLine($"Unhandled exception: {message}");
        try
        {
            var logPath = Path.Combine(AppContext.BaseDirectory, "error.log");
            File.AppendAllText(logPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}\n\n");
        }
        catch { /* logging must never crash the handler itself */ }
    }
}
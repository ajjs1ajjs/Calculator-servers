using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
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
            sc.AddSingleton<ISelfUpdateService, SelfUpdateService>();
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

            _ = CheckForUpdatesAsync(silent: true);
        }

        base.OnFrameworkInitializationCompleted();
    }

    internal async System.Threading.Tasks.Task CheckForUpdatesAsync(bool silent)
    {
        try
        {
            var result = await Services.GetRequiredService<IUpdateCheckService>().CheckForUpdateAsync().ConfigureAwait(false);

            var loc = LocalizationService.Instance;
            var currentVersion = DisplayVersion();
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var dialogs = Services.GetRequiredService<IDialogService>();
                switch (result.Status)
                {
                    case UpdateCheckStatus.UpdateAvailable:
                        if (await dialogs.ConfirmAsync(
                            string.Format(loc["update.message"], result.Update!.Version, currentVersion),
                            loc["update.title"]))
                        {
                            await StartUpdateAsync(result.Update.Version, result.Update.DownloadUrl);
                        }
                        break;

                    case UpdateCheckStatus.NoUpdate:
                        if (!silent)
                            await dialogs.InfoAsync(loc["update.none"], loc["update.title"]);
                        break;

                    case UpdateCheckStatus.Failed:
                        if (!silent)
                            await dialogs.ErrorAsync(loc["update.failed"], loc["update.title"]);
                        break;
                }
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Update check crashed: {ex.Message}");
        }
    }

    private async Task StartUpdateAsync(string version, string downloadUrl)
    {
        var mainWindow = MainWindow;
        if (mainWindow is null) return;

        var progressDialog = new UpdateProgressDialog(version);
        progressDialog.Show(mainWindow);

        var updateService = Services.GetRequiredService<ISelfUpdateService>();
        updateService.DownloadUrl = downloadUrl;
        var cts = CancellationTokenSource.CreateLinkedTokenSource(progressDialog.GetCancellationToken());

        updateService.Progress += (bytes, total) =>
        {
            Dispatcher.UIThread.Post(() => progressDialog.SetProgress(bytes, total));
        };

        var updateResult = await updateService.UpdateAsync(cts.Token);

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (updateResult.Status == SelfUpdateStatus.Completed)
            {
                progressDialog.SetCompleted();
                Environment.Exit(0);
            }
            else
            {
                progressDialog.SetError(updateResult.Error ?? "Unknown error");
            }
        });
    }

    // Поточна версія застосунку для показу: чистий вигляд (без суфікса SourceLink +<sha>).
    private static string DisplayVersion()
    {
        var informational = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (string.IsNullOrWhiteSpace(informational)) return "?";
        var plus = informational.IndexOf('+');
        return plus > 0 ? informational[..plus] : informational;
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
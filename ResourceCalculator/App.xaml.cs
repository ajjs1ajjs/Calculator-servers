using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using ResourceCalculator.Dialogs;
using ResourceCalculator.Interfaces;
using ResourceCalculator.Localization;
using ResourceCalculator.Services;
using ResourceCalculator.ViewModels;
using ResourceCalculator.Views;

namespace ResourceCalculator;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        ResourceCalculator.Themes.ThemeService.Initialize();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        base.OnFrameworkInitializationCompleted();

        Dispatcher.UIThread.UnhandledException += OnUIThreadUnhandledException;
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
        sc.AddSingleton<IDialogService>(sp =>
            new DialogService(sp.GetRequiredService<AccessService>(),
                () => (ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow));
        sc.AddSingleton<IFileSaveService>(sp => (IFileSaveService)sp.GetRequiredService<IDialogService>());
        sc.AddSingleton<IThemeService, ThemeService>();
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

        Services = sc.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = new MainWindow();
            mainWindow.DataContext = Services.GetRequiredService<MainViewModel>();
            desktop.MainWindow = mainWindow;
            mainWindow.Show();

            _ = CheckForUpdatesAsync(silent: true).ContinueWith(t =>
            {
                if (t.IsFaulted)
                    Debug.WriteLine($"Update check crashed: {t.Exception?.InnerException?.Message}");
            }, TaskContinuationOptions.OnlyOnFaulted);
        }
    }

    private static string DisplayVersion()
    {
        var informational = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (string.IsNullOrWhiteSpace(informational)) return "?";
        var plus = informational.IndexOf('+');
        return plus > 0 ? informational[..plus] : informational;
    }

    internal async Task CheckForUpdatesAsync(bool silent)
    {
        UpdateCheckResult update;
        try
        {
            update = await Services.GetRequiredService<IUpdateCheckService>()
                .CheckForUpdateAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Update check crashed: {ex}");
            update = new UpdateCheckResult(UpdateCheckStatus.Failed);
        }

        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop) return;

        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var mainWindow = desktop.MainWindow;
            if (mainWindow == null) return;
            
            var loc = LocalizationService.Instance;
            switch (update.Status)
            {
                case UpdateCheckStatus.UpdateAvailable:
                    // Повністю автоматичне оновлення всередині програми:
                    // жодних переходів у браузер / на сторінку GitHub.
                    var availableDialog = new UpdateAvailableDialog(
                        update.Update!.Version, DisplayVersion(),
                        update.Update.ReleaseNotes, update.Update.SizeBytes);
                    var accepted = await availableDialog.ShowDialog<bool>(mainWindow);
                    if (accepted)
                        await StartUpdateAsync(update.Update);
                    break;

                case UpdateCheckStatus.NoUpdate:
                    if (!silent)
                        await MessageBox.Show(mainWindow, loc["update.none"], loc["update.title"], MessageBoxButtons.OK, MessageBoxImage.Information);
                    break;

                case UpdateCheckStatus.Failed:
                    if (!silent)
                        await MessageBox.Show(mainWindow, loc["update.failed"], loc["update.title"], MessageBoxButtons.OK, MessageBoxImage.Warning);
                    break;
            }
        });
    }

    // Цикл спроб: після помилки діалог пропонує «Спробувати ще» або «Закрити».
    private async Task StartUpdateAsync(UpdateInfo info)
    {
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop) return;
        var mainWindow = desktop.MainWindow;
        if (mainWindow is null) return;

        var updateService = Services.GetRequiredService<ISelfUpdateService>();

        while (true)
        {
            var progressDialog = new UpdateProgressDialog(info.Version);
            // Поки триває завантаження — закриття через X заборонено: інакше
            // SetCompleted/SetError прилетіли б у вже закрите вікно (use-after-close).
            // Прапорець живе в діалозі (кнопки Скасувати/Закрити/Retry відкривають його самі).
            progressDialog.AllowClose = false;
            var showTask = progressDialog.ShowDialog<bool>(mainWindow);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(progressDialog.GetCancellationToken());

            DownloadProgressHandler onProgress = (bytes, total) =>
                Dispatcher.UIThread.Post(() => progressDialog.SetProgress(bytes, total));
            updateService.Progress += onProgress;

            SelfUpdateResult updateResult;
            try
            {
                updateResult = await updateService.UpdateAsync(info.DownloadUrl, cts.Token);
            }
            catch (Exception ex)
            {
                updateResult = new SelfUpdateResult(SelfUpdateStatus.Failed, ex.Message);
            }
            finally
            {
                updateService.Progress -= onProgress;
            }

            if (updateResult.Status == SelfUpdateStatus.Completed)
            {
                await Dispatcher.UIThread.InvokeAsync(() => progressDialog.SetCompleted());
                // Даємо користувачу побачити «Оновлення встановлено», потім вихід:
                // bat-скрипт підміняє exe і запускає нову версію.
                await Task.Delay(1500);
                Environment.Exit(0);
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                progressDialog.AllowClose = true;
                progressDialog.SetError(updateResult.Error ?? "Unknown error");
            });
            await showTask;
            if (!progressDialog.RetryRequested)
                return;
        }
    }

    private void OnUIThreadUnhandledException(object? sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Debug.WriteLine($"Unhandled exception: {e.Exception}");
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ResourceCalculator");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "error.log"), $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {e.Exception}\n\n");
        }
        catch { }
        var loc = LocalizationService.Instance;
        _ = MessageBox.Show((ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow,
            string.Format(loc["error.unknown"], e.Exception.Message),
            loc["error.title"], MessageBoxButtons.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}

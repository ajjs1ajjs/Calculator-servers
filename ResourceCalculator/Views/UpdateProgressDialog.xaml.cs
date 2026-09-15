using System;
using System.Threading;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ResourceCalculator.Localization;

namespace ResourceCalculator.Views;

public partial class UpdateProgressDialog : Window
{
    private CancellationTokenSource? _cts;

    /// <summary>True, якщо користувач натиснув «Спробувати ще» (діалог закрито для повтору).</summary>
    public bool RetryRequested { get; private set; }

    /// <summary>Закриття через X/Скасувати під час завантаження блокується ззовні
    /// (App ставить false, доки триває UpdateAsync — захист від use-after-close).</summary>
    public bool AllowClose { get; set; } = true;

    public UpdateProgressDialog()
    {
        InitializeComponent();
        Closing += (_, e) => e.Cancel = !AllowClose;
    }

    public UpdateProgressDialog(string version)
    {
        InitializeComponent();
        Closing += (_, e) => e.Cancel = !AllowClose;
        var loc = LocalizationService.Instance;
        TxtVersion.Text = $"ITE.ResourceCalculator · {version}";
        TxtStage.Text = loc["update.downloading"];
        TxtStatus.Text = "";
        TxtPercent.Text = "";
    }

    public void SetProgress(long bytesReceived, long totalBytes)
    {
        var loc = LocalizationService.Instance;
        if (totalBytes > 0)
        {
            var percent = (double)bytesReceived / totalBytes * 100;
            ProgressBar.IsIndeterminate = false;
            ProgressBar.Value = percent;
            TxtPercent.Text = $"{percent:F0}%";
            TxtStatus.Text = string.Format(loc["update.downloadedOf"],
                FormatBytes(bytesReceived), FormatBytes(totalBytes), percent.ToString("F0"));
        }
        else
        {
            ProgressBar.IsIndeterminate = true;
            TxtPercent.Text = "";
            TxtStatus.Text = $"{FormatBytes(bytesReceived)}";
        }
    }

    public void SetInstalling()
    {
        var loc = LocalizationService.Instance;
        ProgressBar.IsIndeterminate = true;
        TxtStage.Text = loc["update.installing"];
        TxtStatus.Text = "";
        TxtPercent.Text = "";
        BtnCancel.IsVisible = false;
    }

    public void SetCompleted()
    {
        var loc = LocalizationService.Instance;
        ProgressBar.IsIndeterminate = false;
        ProgressBar.Value = 100;
        TxtPercent.Text = "100%";
        TxtStage.Text = loc["update.completed"];
        BtnCancel.IsVisible = false;
        BtnRetry.IsVisible = false;
        BtnClose.IsVisible = false;
    }

    public void SetError(string message)
    {
        ProgressBar.IsIndeterminate = false;
        // ex.Message може нести локальні шляхи/URL і бути довільним за розміром:
        // показуємо стислу безпечну версію, деталі — лише в лозі.
        var safe = new string((message ?? "").Where(c => c == '\n' || c == '\t' || !char.IsControl(c)).ToArray()).Trim();
        if (safe.Length > 300) safe = safe[..300] + "…";
        TxtStage.Text = $"✕ {(string.IsNullOrEmpty(safe) ? "Unknown error" : safe)}";
        TxtPercent.Text = "";
        BtnCancel.IsVisible = false;
        BtnRetry.IsVisible = true;
        BtnClose.IsVisible = true;
    }

    public CancellationToken GetCancellationToken()
    {
        _cts = new CancellationTokenSource();
        return _cts.Token;
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        try { _cts?.Cancel(); } catch (ObjectDisposedException) { }
        AllowClose = true;
        Close(false);
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        AllowClose = true;
        Close(false);
    }

    private void BtnRetry_Click(object sender, RoutedEventArgs e)
    {
        RetryRequested = true;
        AllowClose = true;
        Close(true);
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
    }
}

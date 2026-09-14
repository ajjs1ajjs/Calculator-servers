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

    public UpdateProgressDialog()
    {
        InitializeComponent();
    }

    public UpdateProgressDialog(string version)
    {
        InitializeComponent();
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
        TxtStage.Text = $"✕ {message}";
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
        Close(false);
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close(false);

    private void BtnRetry_Click(object sender, RoutedEventArgs e)
    {
        RetryRequested = true;
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

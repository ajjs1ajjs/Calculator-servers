using System;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ResourceCalculator.Avalonia.Views;

public partial class UpdateProgressDialog : Window
{
    private CancellationTokenSource? _cts;

    public UpdateProgressDialog(string version)
    {
        InitializeComponent();
        TxtVersion.Text = $"Версія {version}";
        TxtStatus.Text = "Завантаження...";
    }

    public void SetProgress(long bytesReceived, long totalBytes)
    {
        if (totalBytes > 0)
        {
            var percent = (double)bytesReceived / totalBytes * 100;
            ProgressBar.Value = percent;
            TxtStatus.Text = $"{FormatBytes(bytesReceived)} / {FormatBytes(totalBytes)}";
        }
        else
        {
            ProgressBar.IsIndeterminate = true;
            TxtStatus.Text = $"{FormatBytes(bytesReceived)} завантажено";
        }
    }

    public void SetCompleted()
    {
        ProgressBar.Value = 100;
        TxtStatus.Text = "Оновлення застосовано. Перезапуск...";
        BtnCancel.IsVisible = false;
    }

    public void SetError(string message)
    {
        TxtStatus.Text = $"Помилка: {message}";
        BtnCancel.Content = "Закрити";
    }

    public CancellationToken GetCancellationToken()
    {
        _cts = new CancellationTokenSource();
        return _cts.Token;
    }

    private void BtnCancel_Click(object? sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        Close();
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
    }
}

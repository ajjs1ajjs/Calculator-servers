using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ResourceCalculator.Views;

public partial class UpdateAvailableDialog : Window
{
    public UpdateAvailableDialog()
    {
        InitializeComponent();
    }

    public UpdateAvailableDialog(string newVersion, string currentVersion, string? releaseNotes, long sizeBytes)
    {
        InitializeComponent();
        TxtSubtitle.Text = $"ITE.ResourceCalculator";
        TxtCurrent.Text = currentVersion;
        TxtNew.Text = newVersion;
        TxtSize.Text = sizeBytes > 0 ? $"· {FormatBytes(sizeBytes)}" : "";
        TxtNotes.Text = string.IsNullOrWhiteSpace(releaseNotes) ? "—" : releaseNotes.Trim();
    }

    private void BtnUpdate_Click(object sender, RoutedEventArgs e) => Close(true);

    private void BtnLater_Click(object sender, RoutedEventArgs e) => Close(false);

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
    }
}

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
        // Рядки з мережі (tag/body релізу) йдуть у UI як звичайний текст, але чистимо:
        // керуючі/bidi-символи (спуфінг) геть, довжини обрізаємо проти layout-abuse.
        TxtCurrent.Text = CleanVersion(currentVersion);
        TxtNew.Text = CleanVersion(newVersion);
        TxtSize.Text = sizeBytes > 0 ? $"· {FormatBytes(sizeBytes)}" : "";
        TxtNotes.Text = string.IsNullOrWhiteSpace(releaseNotes) ? "—" : CleanNotes(releaseNotes);
    }

    internal static string CleanVersion(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "?";
        var t = new string(s.Where(c => !char.IsControl(c)).ToArray()).Trim();
        if (t.Length > 32) t = t[..32] + "…";
        return string.IsNullOrEmpty(t) ? "?" : t;
    }

    internal static string CleanNotes(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "—";
        // Дозволяємо \n \t, решту керуючих + bidi-override прибираємо.
        var t = new string(s.Where(c => c == '\n' || c == '\t' || (!char.IsControl(c) && !IsBidi(c))).ToArray());
        t = t.Trim();
        if (t.Length > 4000) t = t[..4000] + "…";
        return string.IsNullOrEmpty(t) ? "—" : t;
    }

    private static bool IsBidi(char c) =>
        c is '\u202A' or '\u202B' or '\u202C' or '\u202D' or '\u202E'
            or '\u2066' or '\u2067' or '\u2068' or '\u2069' or '\u200E' or '\u200F';

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

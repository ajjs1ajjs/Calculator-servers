using System.Windows;

namespace AIResourceCalculator.Services;

public static class ThemeService
{
    private static bool _isDark;

    public static bool IsDark
    {
        get => _isDark;
        set
        {
            if (_isDark == value) return;
            _isDark = value;
            ApplyTheme();
        }
    }

    public static void Toggle()
    {
        _isDark = !_isDark;
        ApplyTheme();
    }

    private static void ApplyTheme()
    {
        var app = Application.Current;
        if (app == null) return;

        var uri = new Uri($"Themes/{(_isDark ? "DarkTheme" : "LightTheme")}.xaml", UriKind.RelativeOrAbsolute);
        var dict = new ResourceDictionary { Source = uri };

        var existing = app.Resources.MergedDictionaries
            .FirstOrDefault(d => d.Source != null &&
                (d.Source.OriginalString.Contains("LightTheme") || d.Source.OriginalString.Contains("DarkTheme")));

        if (existing != null)
        {
            var index = app.Resources.MergedDictionaries.IndexOf(existing);
            app.Resources.MergedDictionaries.RemoveAt(index);
            app.Resources.MergedDictionaries.Insert(index, dict);
        }
        else
        {
            app.Resources.MergedDictionaries.Add(dict);
        }
    }
}

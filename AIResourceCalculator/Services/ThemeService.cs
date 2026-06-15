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

        var dict = new ResourceDictionary
        {
            Source = new Uri($"Themes/{(_isDark ? "DarkTheme" : "LightTheme")}.xaml",
                UriKind.RelativeOrAbsolute)
        };

        app.Resources.MergedDictionaries.Clear();
        app.Resources.MergedDictionaries.Add(dict);
    }
}

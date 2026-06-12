using System.Windows;
using System.Windows.Media;

namespace AIResourceCalculator.Services;

public static class ThemeManager
{
    private static bool _isDark;

    public static bool IsDark => _isDark;

    public static void Toggle()
    {
        _isDark = !_isDark;
        var uri = _isDark
            ? new Uri("/Themes/DarkTheme.xaml", UriKind.Relative)
            : new Uri("/Themes/LightTheme.xaml", UriKind.Relative);
        var app = Application.Current;
        app.Resources.MergedDictionaries.Clear();
        app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = uri });
    }

    public static SolidColorBrush BgPage => Get("BgPage");
    public static SolidColorBrush BgSurface => Get("BgSurface");
    public static SolidColorBrush BgCard => Get("BgCard");
    public static SolidColorBrush BgAccent => Get("BgAccent");
    public static SolidColorBrush TextPrimary => Get("TextPrimary");
    public static SolidColorBrush TextSecondary => Get("TextSecondary");

    public static Color BgPageColor => ((SolidColorBrush)Get("BgPage")).Color;
    public static Color BgSurfaceColor => ((SolidColorBrush)Get("BgSurface")).Color;
    public static Color BgCardColor => ((SolidColorBrush)Get("BgCard")).Color;
    public static Color TextPrimaryColor => ((SolidColorBrush)Get("TextPrimary")).Color;
    public static Color TextSecondaryColor => ((SolidColorBrush)Get("TextSecondary")).Color;

    private static SolidColorBrush Get(string key)
    {
        var app = Application.Current;
        return (SolidColorBrush)(app.Resources.Contains(key) ? app.Resources[key] : new SolidColorBrush(Colors.Gray));
    }
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;

namespace ResourceCalculator.Themes;

// Тільки світла тема (Catppuccin Latte): темної теми і перемикача немає.
// Володіє обома словниками: спільні стилі (AppStyles) і світла палітра.
public static class ThemeService
{
    private static Styles? _appStyles;
    private static ResourceDictionary? _themePalette;

    public static bool IsDark => false;

    public static void Initialize()
    {
        var app = Application.Current;
        if (app == null) return;
        try
        {
            _appStyles = AvaloniaXamlLoader.Load(
                new Uri("avares://ITE.ResourceCalculator/Themes/AppStyles.xaml")) as Styles;
            if (_appStyles != null && !app.Styles.Contains(_appStyles))
                app.Styles.Add(_appStyles);
        }
        catch { }
        SetDark(false);
    }

    public static void SetDark(bool dark)
    {
        var app = Application.Current;
        if (app == null) return;
        try
        {
            var dict = AvaloniaXamlLoader.Load(
                new Uri("avares://ITE.ResourceCalculator/Themes/LightTheme.xaml")) as ResourceDictionary;
            if (dict == null) return;
            var merged = app.Resources.MergedDictionaries;
            if (_themePalette != null)
            {
                var idx = merged.IndexOf(_themePalette);
                if (idx >= 0)
                {
                    merged[idx] = dict;
                    _themePalette = dict;
                    return;
                }
            }
            merged.Add(dict);
            _themePalette = dict;
        }
        catch { }
    }
}
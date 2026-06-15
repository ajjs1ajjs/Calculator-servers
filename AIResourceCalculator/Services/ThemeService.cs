using System.Windows;

namespace AIResourceCalculator.Services;

public static class ThemeService
{
    public static bool IsDark { get; private set; }

    public static void Toggle()
    {
        IsDark = !IsDark;
        ApplyTheme();
    }

    private static void ApplyTheme()
    {
        var app = Application.Current;
        if (app == null) return;

        var themeName = IsDark ? "DarkTheme" : "LightTheme";
        var dict = new ResourceDictionary
        {
            Source = new Uri($"/AIResourceCalculator;component/Themes/{themeName}.xaml", UriKind.Relative)
        };

        for (int i = 0; i < app.Resources.MergedDictionaries.Count; i++)
        {
            var src = app.Resources.MergedDictionaries[i].Source;
            if (src != null && (src.OriginalString.Contains("LightTheme") || src.OriginalString.Contains("DarkTheme")))
            {
                app.Resources.MergedDictionaries[i] = dict;
                return;
            }
        }
        app.Resources.MergedDictionaries.Add(dict);
    }
}

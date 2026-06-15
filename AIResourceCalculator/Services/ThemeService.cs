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

        var dict = new ResourceDictionary
        {
            Source = new Uri($"Themes/{(IsDark ? "DarkTheme" : "LightTheme")}.xaml", UriKind.Relative)
        };

        var existingIdx = -1;
        for (int i = 0; i < app.Resources.MergedDictionaries.Count; i++)
        {
            var src = app.Resources.MergedDictionaries[i].Source;
            if (src != null && (src.OriginalString.Contains("LightTheme") || src.OriginalString.Contains("DarkTheme")))
            {
                existingIdx = i;
                break;
            }
        }

        if (existingIdx >= 0)
            app.Resources.MergedDictionaries[existingIdx] = dict;
        else
            app.Resources.MergedDictionaries.Add(dict);
    }
}

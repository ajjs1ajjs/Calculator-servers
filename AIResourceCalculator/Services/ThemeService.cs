using System.IO;
using System.Text.Json;
using System.Windows;

namespace AIResourceCalculator.Services;

public static class ThemeService
{
    private static readonly string ThemeConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AIResourceCalculator", "theme.json");

    public static bool IsDark { get; private set; }

    public static void Initialize()
    {
        IsDark = LoadThemeSetting();
        ApplyTheme();
    }

    public static void Toggle()
    {
        IsDark = !IsDark;
        SaveThemeSetting();
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

        for (int i = app.Resources.MergedDictionaries.Count - 1; i >= 0; i--)
        {
            var src = app.Resources.MergedDictionaries[i].Source;
            if (src != null && (src.OriginalString.Contains("LightTheme") || src.OriginalString.Contains("DarkTheme")))
            {
                app.Resources.MergedDictionaries.RemoveAt(i);
            }
        }
        app.Resources.MergedDictionaries.Add(dict);
    }

    private static bool LoadThemeSetting()
    {
        try
        {
            if (File.Exists(ThemeConfigPath))
            {
                var json = File.ReadAllText(ThemeConfigPath);
                var settings = JsonSerializer.Deserialize<ThemeSettings>(json);
                return settings?.IsDark ?? false;
            }
        }
        catch { }
        return false;
    }

    private static void SaveThemeSetting()
    {
        try
        {
            var dir = Path.GetDirectoryName(ThemeConfigPath);
            if (dir != null && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(new ThemeSettings { IsDark = IsDark });
            File.WriteAllText(ThemeConfigPath, json);
        }
        catch { }
    }

    private class ThemeSettings
    {
        public bool IsDark { get; set; }
    }
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.Styling;

namespace ResourceCalculator.Avalonia.Themes;

// Перемикання світлої/темної палітри в реальному часі: App.axaml реєструє палітру
// (LightTheme.axaml) першим merged-словником в Application.Resources — тут ми лише замінюємо
// цей перший словник на інший (DarkTheme.axaml). Решта UI підхоплює зміну через DynamicResource.
public static class ThemeService
{
    public static bool IsDark { get; private set; }

    public static void SetDark(bool dark)
    {
        IsDark = dark;
        var app = Application.Current;
        var dict = app?.Resources.MergedDictionaries;
        if (dict is not { Count: > 0 }) return;

        var uri = new Uri($"avares://ITE.ResourceCalculator/Themes/{(dark ? "DarkTheme" : "LightTheme")}.axaml");
        dict[0] = new ResourceInclude(uri);
    }
}
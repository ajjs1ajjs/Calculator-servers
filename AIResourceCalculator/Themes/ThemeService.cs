using System.Windows;

namespace AIResourceCalculator.Themes;

// Перемикання світлої/темної палітри в реальному часі: App.xaml реєструє палітру (LightTheme.xaml)
// першим merged-словником — тут ми лише замінюємо цей перший словник на інший (DarkTheme.xaml),
// решта UI підхоплює зміну автоматично через DynamicResource (перезавантажувати вікна не потрібно).
public static class ThemeService
{
    public static bool IsDark { get; private set; }

    public static void SetDark(bool dark)
    {
        IsDark = dark;
        var app = Application.Current;
        if (app == null) return;

        var uri = new Uri(dark ? "Themes/DarkTheme.xaml" : "Themes/LightTheme.xaml", UriKind.Relative);
        app.Resources.MergedDictionaries[0] = new ResourceDictionary { Source = uri };
    }
}

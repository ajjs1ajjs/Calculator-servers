using ResourceCalculator.Interfaces;
using ResourceCalculator.Themes;

namespace ResourceCalculator.Dialogs;

public class ThemeService : IThemeService
{
    public bool IsDark => Themes.ThemeService.IsDark;
    public void SetDark(bool dark) => Themes.ThemeService.SetDark(dark);
}

using ResourceCalculator.Interfaces;
using ResourceCalculator.Themes;

namespace ResourceCalculator.Dialogs;

// WPF-реалізація перемикання теми (динамічна підміна першого merged-словника).
public class WpfThemeService : IThemeService
{
    public bool IsDark => ThemeService.IsDark;
    public void SetDark(bool dark) => ThemeService.SetDark(dark);
}
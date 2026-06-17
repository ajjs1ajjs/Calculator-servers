namespace AIResourceCalculator.Interfaces;

public interface IThemeService
{
    bool IsDark { get; }
    void Initialize();
    void Toggle();
}

namespace ResourceCalculator.Interfaces;

// Перемикання світлої/темної теми без залежності від UI-фреймворку.
public interface IThemeService
{
    bool IsDark { get; }
    void SetDark(bool dark);
}
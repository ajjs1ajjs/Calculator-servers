using ResourceCalculator.Interfaces;

namespace ResourceCalculator.Services;

// Поведінка за замовчуванням, коли UI-хост не надав свою реалізацію діалогів
// (наприклад, у тестах): повідомлення виводимо в консоль, парольні діалоги — «скасовано».
public class DefaultDialogService : IDialogService
{
    public bool Confirm(string message, string title)
    {
        System.Diagnostics.Debug.WriteLine($"Dialog: {title} — {message}");
        return false;
    }

    public void Info(string message, string title)
        => System.Diagnostics.Debug.WriteLine($"Info: {title} — {message}");

    public void Error(string message, string title)
        => System.Diagnostics.Debug.WriteLine($"Error: {title} — {message}");

    public bool ShowPasswordDialog() => false;
    public void ShowChangePasswordDialog() { }
}
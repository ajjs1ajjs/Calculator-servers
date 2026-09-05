using ResourceCalculator.Interfaces;

namespace ResourceCalculator.Services;

// Поведінка за замовчуванням, коли UI-хост не надав свою реалізацію діалогів
// (наприклад, у тестах): повідомлення виводимо в консоль, парольні діалоги — «скасовано».
public class DefaultDialogService : IDialogService
{
    public Task<bool> ConfirmAsync(string message, string title)
    {
        System.Diagnostics.Debug.WriteLine($"Dialog: {title} — {message}");
        return Task.FromResult(false);
    }

    public Task InfoAsync(string message, string title)
    {
        System.Diagnostics.Debug.WriteLine($"Info: {title} — {message}");
        return Task.CompletedTask;
    }

    public Task ErrorAsync(string message, string title)
    {
        System.Diagnostics.Debug.WriteLine($"Error: {title} — {message}");
        return Task.CompletedTask;
    }

    public Task<bool> ShowPasswordDialogAsync() => Task.FromResult(false);
    public Task ShowChangePasswordDialogAsync() => Task.CompletedTask;
}
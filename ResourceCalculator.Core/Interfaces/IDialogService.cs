namespace ResourceCalculator.Interfaces;

// Абстракції UI-діалогів, щоб ViewModels лишались незалежними від UI-фреймворку
// (WPF на Windows, Avalonia на Linux/macOS). Методи асинхронні, бо модальні діалоги
// Avalonia (ShowDialog) повертають Task.
public interface IDialogService
{
    // Так/Ні (підтвердження).
    Task<bool> ConfirmAsync(string message, string title);
    // Інформаційне повідомлення.
    Task InfoAsync(string message, string title);
    // Повідомлення про помилку.
    Task ErrorAsync(string message, string title);
    // Розблокування матриці паролем. true = розблоковано.
    Task<bool> ShowPasswordDialogAsync();
    // Зміна пароля матриці.
    Task ShowChangePasswordDialogAsync();
}
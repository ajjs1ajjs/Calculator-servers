namespace ResourceCalculator.Interfaces;

// Абстракції UI-діалогів, щоб ViewModels лишались незалежними від UI-фреймворку
// (WPF на Windows, Avalonia на Linux/macOS).
public interface IDialogService
{
    // Так/Ні (підтвердження).
    bool Confirm(string message, string title);
    // Інформаційне повідомлення.
    void Info(string message, string title);
    // Повідомлення про помилку.
    void Error(string message, string title);
    // Розблокування матриці паролем. true = розблоковано.
    bool ShowPasswordDialog();
    // Зміна пароля матриці.
    void ShowChangePasswordDialog();
}
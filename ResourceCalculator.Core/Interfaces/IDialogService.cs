namespace ResourceCalculator.Interfaces;

// Абстракції UI-діалогів, щоб ViewModels лишались незалежними від UI-фреймворку
// і піддавались тестуванню без вікон. Методи асинхронні: виклик іде з async-команд
// ViewModel, а реалізація може показувати модальне вікно.
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
}
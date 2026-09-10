namespace ResourceCalculator.Interfaces;

// Вибір файлу для збереження (Excel/PDF). Повертає шлях або null, якщо користувач скасував.
// Асинхронний, щоб виклик із async-команд ViewModel не блокував UI-потік.
public interface IFileSaveService
{
    Task<string?> PickSavePathAsync(string defaultFileName, string filterDescription, string extension);
}
namespace ResourceCalculator.Interfaces;

// Вибір файлу для збереження (Excel/PDF). Повертає шлях або null, якщо користувач скасував.
// Асинхронний, бо Avalonia StorageProvider.SaveFilePickerAsync повертає Task.
public interface IFileSaveService
{
    Task<string?> PickSavePathAsync(string defaultFileName, string filterDescription, string extension);
}
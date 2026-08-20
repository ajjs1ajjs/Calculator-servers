namespace ResourceCalculator.Interfaces;

// Вибір файлу для збереження (Excel/PDF). Повертає шлях або null, якщо користувач скасував.
public interface IFileSaveService
{
    string? PickSavePath(string defaultFileName, string filterDescription, string extension);
}
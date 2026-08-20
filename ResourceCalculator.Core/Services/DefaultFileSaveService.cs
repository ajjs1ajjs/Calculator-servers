using ResourceCalculator.Interfaces;

namespace ResourceCalculator.Services;

// Тестова/без-UI реалізація вибору файлу: повертає null (скасування).
public class DefaultFileSaveService : IFileSaveService
{
    public string? PickSavePath(string defaultFileName, string filterDescription, string extension) => null;
}
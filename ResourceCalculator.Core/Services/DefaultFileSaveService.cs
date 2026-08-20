using ResourceCalculator.Interfaces;

namespace ResourceCalculator.Services;

// Тестова/без-UI реалізація вибору файлу: повертає null (скасування).
public class DefaultFileSaveService : IFileSaveService
{
    public Task<string?> PickSavePathAsync(string defaultFileName, string filterDescription, string extension)
        => Task.FromResult<string?>(null);
}
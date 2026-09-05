namespace ResourceCalculator.Interfaces;

public record UpdateInfo(string Version, string DownloadUrl);

// Результат перевірки оновлень: окремо позначаємо збій мережі/API, щоб ручна перевірка
// могла показати користувачу «не вдалося перевірити» замість мовчазного ігнору.
public enum UpdateCheckStatus { NoUpdate, UpdateAvailable, Failed }

public record UpdateCheckResult(UpdateCheckStatus Status, UpdateInfo? Update = null);

public interface IUpdateCheckService
{
    Task<UpdateCheckResult> CheckForUpdateAsync();
}
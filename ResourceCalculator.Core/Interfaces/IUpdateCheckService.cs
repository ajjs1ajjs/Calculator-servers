namespace ResourceCalculator.Interfaces;

// Sha256 — очікуваний хеш ассета з тієї самої відповіді GitHub API, у якій знайдено
// сам URL. Раніше SelfUpdateService ходив за digest окремим запитом до /releases/latest
// уже після завантаження: це був другий удар по rate-limit (60/год на IP) і вікно, в яке
// «latest» міг стати іншим релізом — тоді перевірка падала на коректному файлі.
public record UpdateInfo(string Version, string DownloadUrl, string? ReleaseNotes = null, long SizeBytes = 0, string? Sha256 = null);

// Результат перевірки оновлень: окремо позначаємо збій мережі/API, щоб ручна перевірка
// могла показати користувачу «не вдалося перевірити» замість мовчазного ігнору.
public enum UpdateCheckStatus { NoUpdate, UpdateAvailable, Failed }

public record UpdateCheckResult(UpdateCheckStatus Status, UpdateInfo? Update = null);

public interface IUpdateCheckService
{
    Task<UpdateCheckResult> CheckForUpdateAsync();
}
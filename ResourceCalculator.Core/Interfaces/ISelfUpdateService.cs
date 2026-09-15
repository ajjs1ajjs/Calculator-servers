namespace ResourceCalculator.Interfaces;

public delegate void DownloadProgressHandler(long bytesReceived, long totalBytes);

public enum SelfUpdateStatus { InProgress, Completed, Failed }

public record SelfUpdateResult(SelfUpdateStatus Status, string? Error = null);

public interface ISelfUpdateService
{
    event DownloadProgressHandler? Progress;
    // URL передається параметром, а не mutable-властивістю синглтона:
    // паралельні перевірки оновлень не можуть перехрестити завантаження.
    Task<SelfUpdateResult> UpdateAsync(string downloadUrl, CancellationToken cancellationToken = default);
}

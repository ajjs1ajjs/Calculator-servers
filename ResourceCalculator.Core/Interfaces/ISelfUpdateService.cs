namespace ResourceCalculator.Interfaces;

public delegate void DownloadProgressHandler(long bytesReceived, long totalBytes);

public enum SelfUpdateStatus { InProgress, Completed, Failed }

public record SelfUpdateResult(SelfUpdateStatus Status, string? Error = null);

public interface ISelfUpdateService
{
    event DownloadProgressHandler? Progress;
    string? LatestVersion { get; }
    string? DownloadUrl { get; set; }
    Task<SelfUpdateResult> UpdateAsync(CancellationToken cancellationToken = default);
}

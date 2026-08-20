using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using ResourceCalculator.Interfaces;

namespace ResourceCalculator.Services;

/// <summary>
/// Перевіряє GitHub Releases репозиторію на наявність версії, новішої за поточну.
/// Мережеві/парсинг-помилки (немає інтернету, rate limit, повільна мережа) не кидаються —
/// повертається Failed зі записом у лог, щоб ручна перевірка могла показати користувачу.
/// </summary>
public class UpdateCheckService : IUpdateCheckService
{
    private const string LatestReleaseUrl =
        "https://api.github.com/repos/ajjs1ajjs/Calculator-servers/releases/latest";

    private static readonly HttpClient Http = CreateHttpClient();

    private static HttpClient CreateHttpClient()
    {
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2)
        };
        // 15с замість 5с: GitHub API іноді повільний, короткий таймаут тихо вбивав перевірку.
        var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("ITE.ResourceCalculator", GetCurrentVersion()));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }

    public async Task<UpdateCheckResult> CheckForUpdateAsync()
    {
        // Ретраї з невеликим бек-офом: після публікації релізу /releases/latest кілька хвилин
        // кешує старий стан, а rate-limit (60/год на IP) теж зникає сам.
        const int attempts = 3;
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            try
            {
                using var response = await Http.GetAsync(LatestReleaseUrl).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    LogCheck($"HTTP {(int)response.StatusCode}");
                    if (attempt < attempts) { await DelayBackoff(attempt); continue; }
                    return new UpdateCheckResult(UpdateCheckStatus.Failed);
                }

                using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                using var json = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);
                var root = json.RootElement;

                var tagName = root.GetProperty("tag_name").GetString();
                var releaseUrl = root.GetProperty("html_url").GetString();
                if (string.IsNullOrWhiteSpace(tagName) || string.IsNullOrWhiteSpace(releaseUrl))
                    return new UpdateCheckResult(UpdateCheckStatus.Failed);

                var latestVersion = ParseVersion(tagName);
                var currentVersion = ParseVersion(GetCurrentVersion());
                if (latestVersion is null || currentVersion is null)
                    return new UpdateCheckResult(UpdateCheckStatus.Failed);

                if (latestVersion > currentVersion)
                    return new UpdateCheckResult(UpdateCheckStatus.UpdateAvailable, new UpdateInfo(tagName, releaseUrl));

                return new UpdateCheckResult(UpdateCheckStatus.NoUpdate);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException or InvalidOperationException)
            {
                LogCheck($"attempt {attempt}: {ex.GetType().Name}: {ex.Message}");
                if (attempt < attempts) { await DelayBackoff(attempt); continue; }
                return new UpdateCheckResult(UpdateCheckStatus.Failed);
            }
        }
        return new UpdateCheckResult(UpdateCheckStatus.Failed);
    }

    private static Task DelayBackoff(int attempt) =>
        Task.Delay(TimeSpan.FromSeconds(attempt));

    private static void LogCheck(string message)
    {
        Debug.WriteLine($"Update check: {message}");
        try
        {
            var logPath = Path.Combine(AppContext.BaseDirectory, "update-check.log");
            File.AppendAllText(logPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}\n");
        }
        catch { /* logging must never break the app */ }
    }

    private static string GetCurrentVersion() =>
        Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "0.0.0";

    private static Version? ParseVersion(string raw)
    {
        var trimmed = raw.TrimStart('v', 'V');
        var plusIndex = trimmed.IndexOf('+');
        if (plusIndex >= 0) trimmed = trimmed[..plusIndex];
        return Version.TryParse(trimmed, out var version) ? version : null;
    }
}
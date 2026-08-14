using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using ResourceCalculator.Interfaces;

namespace ResourceCalculator.Services;

/// <summary>
/// Перевіряє GitHub Releases репозиторію на наявність версії, новішої за поточну.
/// Мережеві/парсинг-помилки (немає інтернету, rate limit) навмисно проковтуються —
/// це фонова перевірка, вона не повинна заважати роботі застосунку.
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
        var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("ITE.ResourceCalculator", GetCurrentVersion()));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }

    public async Task<UpdateInfo?> CheckForUpdateAsync()
    {
        try
        {
            using var response = await Http.GetAsync(LatestReleaseUrl).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return null;

            using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            using var json = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);
            var root = json.RootElement;

            var tagName = root.GetProperty("tag_name").GetString();
            var releaseUrl = root.GetProperty("html_url").GetString();
            if (string.IsNullOrWhiteSpace(tagName) || string.IsNullOrWhiteSpace(releaseUrl)) return null;

            var latestVersion = ParseVersion(tagName);
            var currentVersion = ParseVersion(GetCurrentVersion());
            if (latestVersion is null || currentVersion is null) return null;

            return latestVersion > currentVersion ? new UpdateInfo(tagName, releaseUrl) : null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            Debug.WriteLine($"Update check failed: {ex.Message}");
            return null;
        }
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

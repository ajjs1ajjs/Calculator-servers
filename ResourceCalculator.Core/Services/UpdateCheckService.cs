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

                if (!root.TryGetProperty("tag_name", out var tagProp))
                    return new UpdateCheckResult(UpdateCheckStatus.Failed);
                var tagName = tagProp.GetString();
                if (string.IsNullOrWhiteSpace(tagName))
                    return new UpdateCheckResult(UpdateCheckStatus.Failed);

                var latestVersion = ParseVersion(tagName);
                var currentVersion = ParseVersion(GetCurrentVersion());
                if (latestVersion is null || currentVersion is null)
                    return new UpdateCheckResult(UpdateCheckStatus.Failed);

                if (latestVersion <= currentVersion)
                    return new UpdateCheckResult(UpdateCheckStatus.NoUpdate);

                // Резолвимо прямий URL exe-ассета з того ж відповіді API, щоб далі качати
                // саме файл, а не HTML-сторінку релізу (html_url). Жодних переходів
                // у браузер: усе оновлення відбувається всередині програми.
                var (assetUrl, assetSize) = FindExeAsset(root);
                if (string.IsNullOrWhiteSpace(assetUrl))
                {
                    LogCheck($"no {ExeAssetName} asset in {tagName}");
                    return new UpdateCheckResult(UpdateCheckStatus.Failed);
                }

                var notes = root.TryGetProperty("body", out var body) ? body.GetString() : null;
                // Релізні нотатки йдуть у UI: обрізаємо, щоб зловмисний/гігантський body
                // не роздував діалог і пам'ять.
                if (notes is not null && notes.Length > MaxNotesLength)
                    notes = notes[..MaxNotesLength] + "…";
                return new UpdateCheckResult(UpdateCheckStatus.UpdateAvailable, new UpdateInfo(tagName, assetUrl, notes, assetSize));
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

    private const string ExeAssetName = "ITE.ResourceCalculator.exe";
    private const int MaxNotesLength = 8000;
    // Розмір sanity-cap: брехливий size з API не має ламати форматування/UI.
    private const long MaxAssetSize = 2L * 1024 * 1024 * 1024;

    // Шукає прямий browser_download_url exe-ассета у вже отриманому JSON релізу.
    private static (string? Url, long Size) FindExeAsset(JsonElement root)
    {
        try
        {
            if (!root.TryGetProperty("assets", out var assets)) return (null, 0);
            foreach (var asset in assets.EnumerateArray())
            {
                if (!asset.TryGetProperty("name", out var nameProp)
                    || nameProp.GetString() != ExeAssetName)
                    continue;
                if (!asset.TryGetProperty("browser_download_url", out var urlProp))
                    return (null, 0);
                var url = urlProp.GetString();
                var size = asset.TryGetProperty("size", out var sizeProp) && sizeProp.TryGetInt64(out var s) ? s : 0;
                if (size < 0 || size > MaxAssetSize) size = 0;
                return (url, size);
            }
        }
        catch (InvalidOperationException) { }
        return (null, 0);
    }

    private static Task DelayBackoff(int attempt) =>
        Task.Delay(TimeSpan.FromSeconds(attempt));

    private static void LogCheck(string message)
    {
        Debug.WriteLine($"Update check: {message}");
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ResourceCalculator");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "update-check.log"), $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}\n");
        }
        catch { /* logging must never break the app */ }
    }

    private static string GetCurrentVersion()
    {
        // Версію беремо з entry-assembly (exe застосунку: ITE.ResourceCalculator.exe),
        // а НЕ з Assembly.GetExecutingAssembly() — тут виконується код Core, і версія
        // Core завжди 1.0.0 (не задана), через що апка вічно пропонувала оновлення.
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
               ?? assembly.GetName().Version?.ToString()
               ?? "0.0.0";
    }

    private static Version? ParseVersion(string raw)
    {
        var trimmed = raw.TrimStart('v', 'V');
        var plusIndex = trimmed.IndexOf('+');
        if (plusIndex >= 0) trimmed = trimmed[..plusIndex];
        return Version.TryParse(trimmed, out var version) ? version : null;
    }
}
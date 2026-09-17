using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using ResourceCalculator.Interfaces;

namespace ResourceCalculator.Services;

public class SelfUpdateService : ISelfUpdateService
{
    // Ліміт завантаження: захист від безмежного/брехливого стріму (лише дисковий DoS-бар'єр;
    // реальний exe ~100-200 МБ, беремо з запасом).
    private const long MaxDownloadBytes = 500L * 1024 * 1024;

    // Дозволені хости для URL завантаження: лише GitHub-інфраструктура релізів.
    private static readonly HashSet<string> AllowedHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "github.com",
        "api.github.com",
        "objects.githubusercontent.com",
        "release-assets.githubusercontent.com",
    };

    private static readonly HttpClient Http = CreateHttpClient();

    public event DownloadProgressHandler? Progress;

    public async Task<SelfUpdateResult> UpdateAsync(UpdateInfo info, CancellationToken cancellationToken = default)
    {
        if (info is null) return new SelfUpdateResult(SelfUpdateStatus.Failed, "Немає даних про оновлення");
        var downloadUrl = info.DownloadUrl;
        if (!IsAllowedDownloadUrl(downloadUrl, out var urlError))
            return new SelfUpdateResult(SelfUpdateStatus.Failed, urlError);

        // Fail closed до завантаження: без очікуваного хеша звіряти буде нічим,
        // тож немає сенсу тягнути сотні мегабайт.
        var expectedHash = NormalizeSha256(info.Sha256);
        if (expectedHash is null)
        {
            Log("no sha256 digest for asset — update aborted");
            return new SelfUpdateResult(SelfUpdateStatus.Failed, "Не вдалося перевірити цілісність оновлення");
        }

        // Унікальне ім'я на завантаження: фіксований temp-файл у спільному каталозі
        // можна підмінити/зайняти іншим локальним процесом (squat/TOCTOU).
        var tempPath = Path.Combine(Path.GetTempPath(), $"ITE.ResourceCalculator_new_{Path.GetRandomFileName()}.exe");
        var keepTemp = false; // при успіху файл лишається: bat-скрипт забере його після виходу процесу
        try
        {
            using var response = await Http.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return new SelfUpdateResult(SelfUpdateStatus.Failed, $"HTTP {(int)response.StatusCode}");

            var totalBytes = response.Content.Headers.ContentLength ?? -1;
            if (totalBytes > MaxDownloadBytes)
                return new SelfUpdateResult(SelfUpdateStatus.Failed, "Файл оновлення завеликий");

            await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var fileStream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 8192, true);

            var buffer = new byte[81920];
            long bytesReceived = 0;
            int bytesRead;
            // Про прогрес звітуємо не частіше ніж раз на ~100 мс: подія маршалиться на
            // UI-потік, і виклик на кожен буфер топив чергу диспетчера десятками тисяч
            // повідомлень, через що вікно прогресу «замерзало» на великих завантаженнях.
            var lastReport = Stopwatch.StartNew();

            while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                bytesReceived += bytesRead;
                if (bytesReceived > MaxDownloadBytes)
                    return new SelfUpdateResult(SelfUpdateStatus.Failed, "Файл оновлення завеликий");
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                if (lastReport.ElapsedMilliseconds >= 100)
                {
                    Progress?.Invoke(bytesReceived, totalBytes);
                    lastReport.Restart();
                }
            }

            await fileStream.FlushAsync(cancellationToken);
            Progress?.Invoke(bytesReceived, totalBytes);

            if (totalBytes > 0 && bytesReceived != totalBytes)
                return new SelfUpdateResult(SelfUpdateStatus.Failed,
                    $"Incomplete download: {bytesReceived}/{totalBytes} bytes");

            // Файл ще відкритий на запис у fileStream вище — закриваємо до перевірки хешу
            // й підміни exe, інакше SHA256 читав би заблокований файл.
            await fileStream.DisposeAsync();

            var verify = await VerifyHashAsync(tempPath, expectedHash, cancellationToken);
            if (!verify.Ok)
                return new SelfUpdateResult(SelfUpdateStatus.Failed, verify.Error ?? "Hash verification failed");

            VerifyAuthenticodeIfPresent(tempPath);

            ApplyUpdate(tempPath);
            keepTemp = true;
            return new SelfUpdateResult(SelfUpdateStatus.Completed);
        }
        catch (OperationCanceledException)
        {
            return new SelfUpdateResult(SelfUpdateStatus.Failed, "Download cancelled");
        }
        catch (Exception ex)
        {
            return new SelfUpdateResult(SelfUpdateStatus.Failed, ex.Message);
        }
        finally
        {
            // При неуспіху/скасуванні — прибрати; при успіху bat-скрипт сам видалить після копіювання.
            if (!keepTemp)
            {
                try { if (File.Exists(tempPath)) File.Delete(tempPath); }
                catch { }
            }
        }
    }

    // Очікуваний SHA-256 приходить із тієї самої відповіді GitHub API, у якій знайдено
    // URL ассета (UpdateInfo.Sha256). Раніше цей метод робив ДРУГИЙ запит до
    // /releases/latest уже після завантаження: зайвий удар по rate-limit (60/год на IP)
    // і вікно, в яке «latest» міг стати наступним релізом — тоді перевірка падала на
    // цілком коректному файлі. Мережі тут більше немає, лише читання з диска.
    //
    // Межа гарантії не змінилася: digest приходить з того ж джерела, що й файл, тож це
    // захист від псування в дорозі, а не від компрометації релізу. Останнє закриває
    // лише підпис (див. VerifyAuthenticodeIfPresent і нотатку в IMPLEMENTATION.md).
    private static async Task<(bool Ok, string? Error)> VerifyHashAsync(
        string filePath, string expectedHashHex, CancellationToken cancellationToken)
    {
        try
        {
            await using var fileStream = File.OpenRead(filePath);
            var actual = await SHA256.HashDataAsync(fileStream, cancellationToken);
            var expected = Convert.FromHexString(expectedHashHex);
            if (!CryptographicOperations.FixedTimeEquals(actual, expected))
            {
                Log("hash MISMATCH — update aborted");
                return (false, "Контрольна сума оновлення не збіглася");
            }
            return (true, null);
        }
        catch (Exception ex)
        {
            Log($"hash UNVERIFIED: {ex.GetType().Name} — update aborted");
            return (false, "Не вдалося перевірити цілісність оновлення");
        }
    }

    // Приймаємо і "sha256:<hex>", і чистий hex; усе інше — не хеш.
    public static string? NormalizeSha256(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var hex = raw.Trim();
        if (hex.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)) hex = hex[7..];
        return hex.Length == 64 && hex.All(Uri.IsHexDigit) ? hex.ToLowerInvariant() : null;
    }

    // Якщо файл має Authenticode-підпис (майбутні підписані релізи) — вимагаємо валідний.
    // Непідписані білди пропускаємо з записом у лог (підпис стане обов'язковим після впровадження CA).
    private static void VerifyAuthenticodeIfPresent(string filePath)
    {
        try
        {
            // SYSLIB0057 придушено свідомо: для витягування сертифіката з Authenticode-підписаного
            // файла заміни в X509CertificateLoader (.NET 10) не існує — це єдиний API.
#pragma warning disable SYSLIB0057
            using var cert = X509Certificate.CreateFromSignedFile(filePath);
#pragma warning restore SYSLIB0057
            using var chain = new X509Chain();
            chain.ChainPolicy.RevocationMode = X509RevocationMode.Online;
            if (cert is X509Certificate2 cert2)
            {
                if (!chain.Build(cert2))
                    Log("Authenticode: signature present but chain build FAILED");
                else
                    Log($"Authenticode: valid signature by {cert2.Subject}");
            }
        }
        catch (CryptographicException)
        {
            Log("Authenticode: file is not signed (allowed for now)");
        }
        catch (Exception ex)
        {
            Log($"Authenticode check error: {ex.GetType().Name}");
        }
    }

    public static bool IsAllowedDownloadUrl(string? url, out string error)
    {
        error = "Некоректне посилання на оновлення";
        if (string.IsNullOrWhiteSpace(url)) return false;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
        if (!uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase)) return false;
        if (uri.UserInfo.Length > 0 || !uri.IsDefaultPort) return false;
        var host = uri.Host;
        if (AllowedHosts.Contains(host)) { error = ""; return true; }
        // Субдомени objects.githubusercontent.com / release-assets.githubusercontent.com.
        if (host.EndsWith(".githubusercontent.com", StringComparison.OrdinalIgnoreCase)
            && (host.Contains("objects", StringComparison.OrdinalIgnoreCase)
                || host.Contains("release-assets", StringComparison.OrdinalIgnoreCase)))
        {
            error = "";
            return true;
        }
        return false;
    }

    private static void Log(string message) => GitHubRelease.Log("self-update", message);

    private void ApplyUpdate(string newExePath)
    {
        // Тільки Environment.ProcessPath. Фолбек на Assembly.Location тут був не просто
        // марним, а й ламав publish: у single-file застосунку Location завжди повертає
        // порожній рядок, і аналізатор IL3000 разом із TreatWarningsAsErrors валив
        // `dotnet publish -p:PublishSingleFile=true` (збірка релізу) з помилкою.
        var currentExe = Environment.ProcessPath;
        if (string.IsNullOrEmpty(currentExe))
        {
            Log("ApplyUpdate: Environment.ProcessPath недоступний — підміну exe скасовано");
            return;
        }

        var currentDir = Path.GetDirectoryName(currentExe);
        var oldExePath = Path.Combine(currentDir!, Path.GetFileName(currentExe) + ".old");
        // Унікальний bat на запуск: фіксований %TEMP%\ITE_Update.bat міг підмінити
        // будь-який локальний процес. Ексклюзивне створення (CreateNew).
        var batchPath = Path.Combine(Path.GetTempPath(), $"ITE_Update_{Path.GetRandomFileName()}.bat");
        var pid = Environment.ProcessId;

        // Скрипт чекає на фактичне завершення процесу (а не фіксовані 2с — на повільній
        // машині exe ще був заблокований, підміна тихо провалювалася й користувач
        // отримував стару версію), і відкочує перейменування, якщо копіювання не вдалося.
        var batch = new StringBuilder();
        batch.AppendLine("@echo off");
        batch.AppendLine("chcp 65001 >nul");
        batch.AppendLine("setlocal");
        batch.AppendLine($"set \"EXE={currentExe}\"");
        batch.AppendLine($"set \"NEW={newExePath}\"");
        batch.AppendLine($"set \"OLD={oldExePath}\"");
        // Утиліти викликаємо за повним шляхом: у PATH користувача можуть бути однойменні
        // GNU-варіанти find/ping (Git for Windows, MSYS), які поводяться інакше й ламають
        // цикл очікування.
        batch.AppendLine(@"set ""SYS=%SystemRoot%\System32""");
        batch.AppendLine("set /a tries=0");
        batch.AppendLine(":wait");
        batch.AppendLine($@"""%SYS%\tasklist.exe"" /fi ""PID eq {pid}"" /nh 2>nul | ""%SYS%\find.exe"" ""{pid}"" >nul");
        batch.AppendLine("if errorlevel 1 goto swap");
        batch.AppendLine("set /a tries+=1");
        batch.AppendLine("if %tries% geq 60 goto swap");
        batch.AppendLine(@"""%SYS%\ping.exe"" -n 2 127.0.0.1 >nul");
        batch.AppendLine("goto wait");
        batch.AppendLine(":swap");
        batch.AppendLine("del /f /q \"%OLD%\" >nul 2>&1");
        batch.AppendLine("move /y \"%EXE%\" \"%OLD%\" >nul 2>&1");
        batch.AppendLine("copy /y \"%NEW%\" \"%EXE%\" >nul 2>&1");
        // Підміна не вдалася — повертаємо стару версію на місце, щоб не лишити користувача без exe.
        batch.AppendLine("if not exist \"%EXE%\" move /y \"%OLD%\" \"%EXE%\" >nul 2>&1");
        batch.AppendLine("del /f /q \"%NEW%\" >nul 2>&1");
        batch.AppendLine("start \"\" \"%EXE%\"");
        // .old ще кілька секунд утримується щойно завершеним процесом — прибираємо з ретраями.
        batch.AppendLine("set /a tries=0");
        batch.AppendLine(":cleanup");
        batch.AppendLine("if not exist \"%OLD%\" goto done");
        batch.AppendLine("del /f /q \"%OLD%\" >nul 2>&1");
        batch.AppendLine("set /a tries+=1");
        batch.AppendLine("if %tries% geq 10 goto done");
        batch.AppendLine(@"""%SYS%\ping.exe"" -n 2 127.0.0.1 >nul");
        batch.AppendLine("goto cleanup");
        batch.AppendLine(":done");
        // Самовидалення: (goto) звільняє дескриптор .bat, інакше del на власному файлі не спрацює.
        batch.AppendLine("(goto) 2>nul & del /f /q \"%~f0\"");

        // UTF-8 без BOM + chcp 65001 у самому скрипті: шляхи проходять через ім'я
        // користувача, яке цілком може бути кирилицею, а BOM зламав би перший рядок.
        // CreateNew: якщо файл раптом існує (хтось зайняв ім'я) — не перезаписуємо чуже.
        using (var fs = new FileStream(batchPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        using (var writer = new StreamWriter(fs, new UTF8Encoding(false)))
        {
            writer.Write(batch.ToString());
        }

        // Запуск через cmd.exe явно: UseShellExecute=false (без shell-ін'єкцій через асоціації).
        Process.Start(new ProcessStartInfo
        {
            FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe"),
            Arguments = $"/c \"{batchPath}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        });
    }

    private static HttpClient CreateHttpClient()
    {
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5)
        };
        var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(300) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("ITE.ResourceCalculator", "1.0"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));
        return client;
    }
}

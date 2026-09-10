using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.Json;
using ResourceCalculator.Interfaces;

namespace ResourceCalculator.Services;

public class SelfUpdateService : ISelfUpdateService
{
    private static readonly string ReleasesUrl = "https://api.github.com/repos/ajjs1ajjs/Calculator-servers/releases/latest";

    private static readonly HttpClient Http = CreateHttpClient();

    public event DownloadProgressHandler? Progress;
    public string? DownloadUrl { get; set; }

    public async Task<SelfUpdateResult> UpdateAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(DownloadUrl))
            return new SelfUpdateResult(SelfUpdateStatus.Failed, "No download URL");

        try
        {
            var tempPath = Path.Combine(Path.GetTempPath(), "ITE.ResourceCalculator_new.exe");
            if (File.Exists(tempPath))
                File.Delete(tempPath);

            using var response = await Http.GetAsync(DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return new SelfUpdateResult(SelfUpdateStatus.Failed, $"HTTP {(int)response.StatusCode}");

            var totalBytes = response.Content.Headers.ContentLength ?? -1;
            await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            var buffer = new byte[81920];
            long bytesReceived = 0;
            int bytesRead;
            // Про прогрес звітуємо не частіше ніж раз на ~100 мс: подія маршалиться на
            // UI-потік, і виклик на кожен буфер топив чергу диспетчера десятками тисяч
            // повідомлень, через що вікно прогресу «замерзало» на великих завантаженнях.
            var lastReport = Stopwatch.StartNew();

            while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                bytesReceived += bytesRead;
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

            if (!await VerifyHashAsync(tempPath, cancellationToken))
                return new SelfUpdateResult(SelfUpdateStatus.Failed, "Hash verification failed");

            ApplyUpdate(tempPath);
            return new SelfUpdateResult(SelfUpdateStatus.Completed);
        }
        catch (OperationCanceledException)
        {
            var tempPath = Path.Combine(Path.GetTempPath(), "ITE.ResourceCalculator_new.exe");
            if (File.Exists(tempPath)) File.Delete(tempPath);
            return new SelfUpdateResult(SelfUpdateStatus.Failed, "Download cancelled");
        }
        catch (Exception ex)
        {
            return new SelfUpdateResult(SelfUpdateStatus.Failed, ex.Message);
        }
    }

    private async Task<bool> VerifyHashAsync(string filePath, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await Http.GetAsync(ReleasesUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                // Немає з чим звіряти — файл уже прийшов по HTTPS із GitHub, тож пускаємо далі,
                // але лишаємо слід у логу: тиха відсутність перевірки не має бути непомітною.
                Log($"hash not verified: releases API HTTP {(int)response.StatusCode}");
                return true;
            }

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var assets = json.RootElement.GetProperty("assets");

            foreach (var asset in assets.EnumerateArray())
            {
                var name = asset.GetProperty("name").GetString();
                if (name == "ITE.ResourceCalculator.exe")
                {
                    var expectedHash = asset.GetProperty("digest").GetString() ?? "";
                    if (expectedHash.StartsWith("sha256:"))
                        expectedHash = expectedHash[7..];

                    if (!string.IsNullOrEmpty(expectedHash))
                    {
                        using var sha = System.Security.Cryptography.SHA256.Create();
                        await using var fileStream = File.OpenRead(filePath);
                        var hash = Convert.ToHexString(sha.ComputeHash(fileStream)).ToLowerInvariant();
                        if (hash != expectedHash.ToLowerInvariant())
                        {
                            Log($"hash mismatch: got {hash}, expected {expectedHash.ToLowerInvariant()}");
                            return false;
                        }
                    }
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            Log($"hash not verified: {ex.GetType().Name}: {ex.Message}");
        }
        return true;
    }

    private static void Log(string message)
    {
        Debug.WriteLine($"Self-update: {message}");
        try
        {
            File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "update-check.log"),
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} self-update: {message}" + Environment.NewLine);
        }
        catch { /* logging must never break the update */ }
    }

    private void ApplyUpdate(string newExePath)
    {
        var currentExe = Environment.ProcessPath ?? Assembly.GetExecutingAssembly().Location;
        if (string.IsNullOrEmpty(currentExe)) return;

        var currentDir = Path.GetDirectoryName(currentExe);
        var oldExePath = Path.Combine(currentDir!, Path.GetFileName(currentExe) + ".old");
        var batchPath = Path.Combine(Path.GetTempPath(), "ITE_Update.bat");
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
        File.WriteAllText(batchPath, batch.ToString(), new UTF8Encoding(false));

        Process.Start(new ProcessStartInfo
        {
            FileName = batchPath,
            UseShellExecute = true,
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

using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
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

            if (!VerifyAuthenticode(tempPath, out var signError))
            {
                Log($"signature REJECTED: {signError}");
                return new SelfUpdateResult(SelfUpdateStatus.Failed, signError);
            }

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

    // Пін сертифіката підпису: SHA-256 відбиток. Сертифікат самопідписаний
    // (внутрішній інструмент, публічної CA немає), тому довіру дає не системне
    // сховище, а саме цей пін — оновлення приймається ЛИШЕ від нього.
    // Зміна сертифіката = зміна цієї константи + новий PFX у секретах репозиторію.
    public const string SigningCertSha256Thumbprint =
        "BA28C80D64940AE5453C4FD1DD4560AC5C27E0BF6DEDF3C580822A7B9788328E";

    // Обов'язкова перевірка Authenticode завантаженого exe. Раніше була
    // «перевіримо, якщо підпис є» з лише записом у лог — тобто непідписаний або
    // підписаний будь-ким файл проходив далі.
    //
    // Два незалежні бар'єри, обидва мусять пройти:
    //   1) WinVerifyTrust — стандартний API Windows: підтверджує, що підпис
    //      математично валідний і що файл не змінювали після підписання
    //      (модифікований байт → TRUST_E_BAD_DIGEST).
    //   2) Пін відбитка сертифіката підписанта — щоб валідний підпис ЧУЖИМ
    //      сертифікатом (у т.ч. виданим публічною CA) не вважався нашим.
    //
    // CERT_E_UNTRUSTEDROOT приймається свідомо й лише разом з (2): самопідписаний
    // корінь за визначенням не лежить у Trusted Root на машинах користувачів, і
    // ставити його туди не треба — довіру несе пін. Будь-який інший код помилки
    // (немає підпису, зіпсований дайджест, протермінований сертифікат) — відмова.
    public static bool VerifyAuthenticode(string filePath, out string error)
    {
        if (!OperatingSystem.IsWindows())
        {
            // Fail closed: якщо перевірити підпис нечим — exe не підміняємо.
            error = "Перевірка підпису оновлення доступна лише у Windows";
            return false;
        }

        string signerThumbprint;
        try
        {
            // Заміни в X509CertificateLoader для витягування сертифіката з
            // Authenticode-підписаного файла не існує — це єдиний API.
#pragma warning disable SYSLIB0057
            var rawCert = X509Certificate.CreateFromSignedFile(filePath).GetRawCertData();
#pragma warning restore SYSLIB0057
            using var signer = X509CertificateLoader.LoadCertificate(rawCert);
            signerThumbprint = Convert.ToHexString(signer.GetCertHash(HashAlgorithmName.SHA256));
        }
        catch (CryptographicException)
        {
            error = "Оновлення не підписане — установка скасована";
            return false;
        }
        catch (Exception ex)
        {
            error = $"Не вдалося прочитати підпис оновлення ({ex.GetType().Name})";
            return false;
        }

        if (!FixedTimeEqualsHex(signerThumbprint, SigningCertSha256Thumbprint))
        {
            Log($"signature thumbprint mismatch: {signerThumbprint}");
            error = "Оновлення підписане невідомим сертифікатом — установка скасована";
            return false;
        }

        var status = WinVerifyTrustFile(filePath);
        if (status == 0 || status == CERT_E_UNTRUSTEDROOT)
        {
            Log($"signature OK (WinVerifyTrust 0x{status:X8}, pinned cert)");
            error = "";
            return true;
        }

        error = status switch
        {
            TRUST_E_NOSIGNATURE => "Оновлення не підписане — установка скасована",
            TRUST_E_BAD_DIGEST => "Підпис оновлення не відповідає файлу — установка скасована",
            CERT_E_EXPIRED => "Сертифікат підпису оновлення протермінований — установка скасована",
            CERT_E_REVOKED => "Сертифікат підпису оновлення відкликано — установка скасована",
            _ => $"Підпис оновлення не пройшов перевірку (0x{status:X8}) — установка скасована"
        };
        return false;
    }

    // --- WinVerifyTrust (wintrust.dll) ---
    private const int CERT_E_UNTRUSTEDROOT = unchecked((int)0x800B0109);
    private const int TRUST_E_NOSIGNATURE = unchecked((int)0x800B0100);
    private const int TRUST_E_BAD_DIGEST = unchecked((int)0x80096010);
    private const int CERT_E_EXPIRED = unchecked((int)0x800B0101);
    private const int CERT_E_REVOKED = unchecked((int)0x800B010C);

    private const uint WTD_UI_NONE = 2;
    private const uint WTD_REVOKE_NONE = 0;
    private const uint WTD_CHOICE_FILE = 1;
    private const uint WTD_STATEACTION_VERIFY = 1;
    private const uint WTD_STATEACTION_CLOSE = 2;
    private const uint WTD_SAFER_FLAG = 0x100;

    private static int WinVerifyTrustFile(string filePath)
    {
        // Ревокацію свідомо не перевіряємо: у самопідписаного сертифіката немає ні
        // CRL, ні OCSP, тож така перевірка або впала б, або полізла в мережу під час
        // оновлення. Відкликання тут реалізується зміною піна + новим релізом.
        var action = new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");
        var pathPtr = Marshal.StringToHGlobalUni(filePath);
        var fileInfoPtr = Marshal.AllocHGlobal(Marshal.SizeOf<WINTRUST_FILE_INFO>());
        var dataPtr = Marshal.AllocHGlobal(Marshal.SizeOf<WINTRUST_DATA>());
        try
        {
            var fileInfo = new WINTRUST_FILE_INFO
            {
                cbStruct = (uint)Marshal.SizeOf<WINTRUST_FILE_INFO>(),
                pcwszFilePath = pathPtr,
                hFile = IntPtr.Zero,
                pgKnownSubject = IntPtr.Zero
            };
            var data = new WINTRUST_DATA
            {
                cbStruct = (uint)Marshal.SizeOf<WINTRUST_DATA>(),
                dwUIChoice = WTD_UI_NONE,
                fdwRevocationChecks = WTD_REVOKE_NONE,
                dwUnionChoice = WTD_CHOICE_FILE,
                pFile = fileInfoPtr,
                dwStateAction = WTD_STATEACTION_VERIFY,
                dwProvFlags = WTD_SAFER_FLAG
            };
            Marshal.StructureToPtr(fileInfo, fileInfoPtr, false);
            Marshal.StructureToPtr(data, dataPtr, false);

            var result = WinVerifyTrust(IntPtr.Zero, ref action, dataPtr);

            // Закриття стану обов'язкове — інакше течуть хендли провайдера довіри.
            data = Marshal.PtrToStructure<WINTRUST_DATA>(dataPtr);
            data.dwStateAction = WTD_STATEACTION_CLOSE;
            Marshal.StructureToPtr(data, dataPtr, false);
            WinVerifyTrust(IntPtr.Zero, ref action, dataPtr);
            return result;
        }
        finally
        {
            Marshal.FreeHGlobal(dataPtr);
            Marshal.FreeHGlobal(fileInfoPtr);
            Marshal.FreeHGlobal(pathPtr);
        }
    }

    [DllImport("wintrust.dll", ExactSpelling = true, SetLastError = false)]
    private static extern int WinVerifyTrust(IntPtr hwnd, ref Guid pgActionID, IntPtr pWVTData);

    [StructLayout(LayoutKind.Sequential)]
    private struct WINTRUST_FILE_INFO
    {
        public uint cbStruct;
        public IntPtr pcwszFilePath;
        public IntPtr hFile;
        public IntPtr pgKnownSubject;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WINTRUST_DATA
    {
        public uint cbStruct;
        public IntPtr pPolicyCallbackData;
        public IntPtr pSIPClientData;
        public uint dwUIChoice;
        public uint fdwRevocationChecks;
        public uint dwUnionChoice;
        public IntPtr pFile;
        public uint dwStateAction;
        public IntPtr hWVTStateData;
        public IntPtr pwszURLReference;
        public uint dwProvFlags;
        public uint dwUIContext;
        public IntPtr pSignatureSettings;
    }

    // Порівняння hex-відбитків за сталий час, без урахування регістру.
    public static bool FixedTimeEqualsHex(string a, string b)
    {
        if (a is null || b is null || a.Length != b.Length) return false;
        var ab = Encoding.ASCII.GetBytes(a.ToUpperInvariant());
        var bb = Encoding.ASCII.GetBytes(b.ToUpperInvariant());
        return CryptographicOperations.FixedTimeEquals(ab, bb);
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

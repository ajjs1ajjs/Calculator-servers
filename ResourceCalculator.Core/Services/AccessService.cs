using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;

namespace ResourceCalculator.Services;

// Захист чутливих даних матриці (діапазони, формули, вузли — впливають на кінцевий результат).
// Пароль зберігається як PBKDF2-SHA256 хеш з сіллю у settings.json у %LOCALAPPDATA%\ResourceCalculator.
// Вбудованого дефолтного пароля НЕМАЄ: за відсутності settings.json доступ заборонено,
// а перший запуск вимагає створення пароля через UI (режим налаштування PasswordDialog).
// Легасі-хеші SHA-256 (single-round) приймаються лише для міграції: після успішної
// перевірки пароль одразу перехешовується у PBKDF2.
public class AccessService
{
    // Контакти підтримки для відновлення доступу, якщо пароль забуто.
    // Телефон свідомо не показуємо в UI — лише пошти (клікабельні, з копіюванням).
    public const string DevEmail1 = "yaroslav.andreichuk@gmail.com";
    public const string DevEmail2 = "andreichuk.y@it-enterprise.com";
    public const string DevContacts = $"Email: {DevEmail1} · {DevEmail2}";

    public const int MinPasswordLength = 12;
    private const int Pbkdf2Iterations = 210_000;
    private const int SaltBytes = 16;
    private const int MaxFailedAttempts = 10;

    private readonly string _settingsPath;
    private readonly string _dataDir;

    private int _failedAttempts;
    private DateTime _lockoutUntilUtc = DateTime.MinValue;

    public string SettingsPath => _settingsPath;

    public AccessService(string? dataDir = null)
    {
        _dataDir = dataDir ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ResourceCalculator", "data");
        _settingsPath = Path.Combine(_dataDir, "settings.json");
    }

    // Повертає true, якщо пароль уже встановлено/ініціалізовано.
    public bool IsPasswordSet => File.Exists(_settingsPath);

    // Гарантує існування каталогу даних. Пароль НЕ створює: за відсутності
    // settings.json доступ заборонено (fail closed), UI пропонує його створити.
    public void EnsureInitialized()
    {
        Directory.CreateDirectory(_dataDir);
    }

    // Залишок блокування після невдалих спроб (для UI-затримки).
    public TimeSpan LockoutRemaining
    {
        get
        {
            var rest = _lockoutUntilUtc - DateTime.UtcNow;
            return rest > TimeSpan.Zero ? rest : TimeSpan.Zero;
        }
    }

    public bool Verify(string password)
    {
        if (string.IsNullOrEmpty(password)) return false;
        if (IsLockedOut()) return false;
        // Fail closed: без файла налаштувань жоден пароль не приймається.
        if (!File.Exists(_settingsPath))
        {
            RegisterFailure();
            return false;
        }

        try
        {
            var json = File.ReadAllText(_settingsPath);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("MatrixPasswordHash", out var hashEl)
                || !doc.RootElement.TryGetProperty("MatrixPasswordSalt", out var saltEl))
            {
                RegisterFailure();
                return false;
            }
            var storedHash = hashEl.GetString() ?? "";
            var salt = saltEl.GetString() ?? "";
            // Легасі-файли не мають поля Iterations — їх приймаємо лише для міграції.
            int storedIter = 0;
            var isLegacy = !doc.RootElement.TryGetProperty("Iterations", out var iterEl)
                || !iterEl.TryGetInt32(out storedIter) || storedIter <= 0;

            bool ok;
            if (isLegacy)
            {
                // Легасі-формат: single-round SHA-256. Приймаємо лише для міграції.
                ok = FixedTimeEquals(LegacyHash(password, salt), storedHash);
                if (ok) SetPassword(password); // мовчазний апгрейд до PBKDF2
            }
            else
            {
                ok = FixedTimeEquals(Pbkdf2Hash(password, salt, storedIter), storedHash);
            }

            if (ok) ResetFailures();
            else RegisterFailure();
            return ok;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"AccessService.Verify failed: {ex.Message}");
            RegisterFailure();
            return false;
        }
    }

    // Перевірка без immutable string-копій пароля в купі: SecureString → pinned char[] → UTF-8.
    public bool Verify(SecureString password)
    {
        if (password is null) return false;
        IntPtr bstr = IntPtr.Zero;
        char[]? chars = null;
        byte[]? bytes = null;
        try
        {
            bstr = Marshal.SecureStringToBSTR(password);
            int len = Marshal.ReadInt32(bstr, -4) / 2;
            if (len <= 0) return false;
            chars = new char[len];
            for (int i = 0; i < len; i++) chars[i] = (char)Marshal.ReadInt16(bstr, i * 2);
            bytes = Encoding.UTF8.GetBytes(chars);
            return VerifyBytes(bytes);
        }
        finally
        {
            if (chars is not null) CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(chars.AsSpan()));
            if (bytes is not null) CryptographicOperations.ZeroMemory(bytes);
            if (bstr != IntPtr.Zero) Marshal.ZeroFreeBSTR(bstr);
        }
    }

    private bool VerifyBytes(byte[] passwordBytes)
    {
        if (IsLockedOut()) return false;
        if (!File.Exists(_settingsPath))
        {
            RegisterFailure();
            return false;
        }
        try
        {
            var json = File.ReadAllText(_settingsPath);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("MatrixPasswordHash", out var hashEl)
                || !doc.RootElement.TryGetProperty("MatrixPasswordSalt", out var saltEl))
            {
                RegisterFailure();
                return false;
            }
            var storedHash = hashEl.GetString() ?? "";
            var salt = saltEl.GetString() ?? "";
            int storedIter = 0;
            var isLegacy = !doc.RootElement.TryGetProperty("Iterations", out var iterEl)
                || !iterEl.TryGetInt32(out storedIter) || storedIter <= 0;
            if (isLegacy)
            {
                // Легасі-міграція: одноразово декодуємо для перевірки старим хешем,
                // одразу перехешовуємо у PBKDF2. Далі — лише байтовий шлях.
                var transient = Encoding.UTF8.GetString(passwordBytes);
                var okLegacy = FixedTimeEquals(LegacyHash(transient, salt), storedHash);
                if (okLegacy) SetPassword(transient);
                else RegisterFailure();
                return okLegacy;
            }
            var saltBytes = Convert.FromBase64String(salt);
            var derived = Rfc2898DeriveBytes.Pbkdf2(passwordBytes, saltBytes, storedIter, HashAlgorithmName.SHA256, 32);
            var ok = FixedTimeEquals(Convert.ToBase64String(derived), storedHash);
            if (ok) ResetFailures();
            else RegisterFailure();
            return ok;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"AccessService.Verify failed: {ex.Message}");
            RegisterFailure();
            return false;
        }
    }

    // Встановлює НОВИЙ пароль (перший запуск або перегенерація). Кидає виняток
    // при слабкому паролі або помилці запису — мовчазних no-op більше немає.
    public void SetPassword(string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);
        if (password.Length < MinPasswordLength)
            throw new ArgumentException($"Пароль має містити щонайменше {MinPasswordLength} символів.", nameof(password));

        Directory.CreateDirectory(_dataDir);
        var salt = Convert.ToBase64String(RandomNumberGenerator.GetBytes(SaltBytes));
        var hash = Pbkdf2Hash(password, salt, Pbkdf2Iterations);
        var payload = new Dictionary<string, object>
        {
            ["MatrixPasswordHash"] = hash,
            ["MatrixPasswordSalt"] = salt,
            ["Iterations"] = Pbkdf2Iterations
        };
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        var tmp = _settingsPath + ".tmp";
        File.WriteAllText(tmp, json);
        RestrictToCurrentUser(tmp);
        File.Move(tmp, _settingsPath, overwrite: true);
        RestrictToCurrentUser(_settingsPath);
        ResetFailures();
    }

    // Повертає підказку з контактами підтримки для відновлення доступу.
    public string GetPasswordHint()
        => "Пароль не збережено у програмі. Для відновлення доступу зверніться: " + DevContacts;

    private bool IsLockedOut() => DateTime.UtcNow < _lockoutUntilUtc;

    private void RegisterFailure()
    {
        _failedAttempts++;
        // Прогресивна затримка 1с → 2с → 4с … до 30с; після ліміту — жорстке блокування на 5 хв.
        if (_failedAttempts >= MaxFailedAttempts)
        {
            _lockoutUntilUtc = DateTime.UtcNow.AddMinutes(5);
            _failedAttempts = 0;
        }
        else
        {
            var delaySec = Math.Min(30, 1 << Math.Min(_failedAttempts, 5));
            _lockoutUntilUtc = DateTime.UtcNow.AddSeconds(delaySec);
        }
    }

    private void ResetFailures()
    {
        _failedAttempts = 0;
        _lockoutUntilUtc = DateTime.MinValue;
    }

    private static string Pbkdf2Hash(string password, string saltBase64, int iterations)
    {
        var salt = Convert.FromBase64String(saltBase64);
        var derived = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password), salt, iterations, HashAlgorithmName.SHA256, 32);
        return Convert.ToBase64String(derived);
    }

    private static string LegacyHash(string password, string salt)
    {
        var bytes = Encoding.UTF8.GetBytes(salt + ":" + password);
        var hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }

    private static bool FixedTimeEquals(string a, string b)
    {
        var ab = Encoding.UTF8.GetBytes(a);
        var bb = Encoding.UTF8.GetBytes(b);
        if (ab.Length != bb.Length) return false;
        return CryptographicOperations.FixedTimeEquals(ab, bb);
    }

    // Файл із хешем — тільки поточному користувачеві (ускладнює офлайн-перебір з інших обліковок).
    private static void RestrictToCurrentUser(string path)
    {
        if (!OperatingSystem.IsWindows()) return;
        try
        {
            var fileInfo = new FileInfo(path);
            var security = fileInfo.GetAccessControl();
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            var identity = WindowsIdentity.GetCurrent().User;
            if (identity is not null)
            {
                security.AddAccessRule(new FileSystemAccessRule(
                    identity, FileSystemRights.FullControl, AccessControlType.Allow));
            }
            fileInfo.SetAccessControl(security);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"AccessService.RestrictToCurrentUser failed: {ex.Message}");
        }
    }
}

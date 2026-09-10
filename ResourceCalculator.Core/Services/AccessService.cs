using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ResourceCalculator.Services;

// Захист чутливих даних матриці (діапазони, формули, вузли — впливають на кінцевий результат).
// Пароль зберігається як SHA-256 хеш з сіллю у settings.json у %LOCALAPPDATA%\ResourceCalculator.
// Пароль статичний, встановлюється розробником при білді (DefaultPassword). Користувачі не можуть його змінити/перегенерувати.
public class AccessService
{
    // Контакти розробника для відновлення доступу, якщо пароль забуто.
    public const string DevEmail1 = "yaroslav.andreichuk@gmail.com";
    public const string DevEmail2 = "andreichuk.y@it-enterprise.com";
    public const string DevPhone = "+380979454941";
    public const string DevContacts = $"Email: {DevEmail1} · {DevEmail2}\nТелефон: {DevPhone}";

    // Пароль за замовчуванням (захист від випадкового редагування матриці).
    // ⚠️ Увага: дефолтовий пароль має бути змінений користувачем після першого запуску.
    // Рекомендується змінити через UI (кнопка "Змінити пароль") або використовувати
    #warning "Default matrix password is well-known - users MUST change it via UI after first run"
    public const string DefaultPassword = "yF2jrX7inC4w";

    private readonly string _settingsPath;
    private readonly string _dataDir;

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

    public void EnsureInitialized()
    {
        if (!File.Exists(_settingsPath))
            SetPassword(DefaultPassword);
    }

    public bool Verify(string password)
    {
        if (string.IsNullOrEmpty(password)) return false;
        if (!File.Exists(_settingsPath)) return password == DefaultPassword;

        try
        {
            var json = File.ReadAllText(_settingsPath);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("MatrixPasswordHash", out var hashEl)
                || !doc.RootElement.TryGetProperty("MatrixPasswordSalt", out var saltEl))
                return false;
            var storedHash = hashEl.GetString() ?? "";
            var salt = saltEl.GetString() ?? "";
            return FixedTimeEquals(Hash(password, salt), storedHash);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"AccessService.Verify failed: {ex.Message}");
            return false;
        }
    }

    public void SetPassword(string password)
    {
        try
        {
            var dir = Path.GetDirectoryName(_settingsPath);
            if (dir != null && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            var salt = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
            var hash = Hash(password, salt);
            var payload = new Dictionary<string, string>
            {
                ["MatrixPasswordHash"] = hash,
                ["MatrixPasswordSalt"] = salt
            };
            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
            var tmp = _settingsPath + ".tmp";
            File.WriteAllText(tmp, json);
            File.Move(tmp, _settingsPath, overwrite: true);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"AccessService.SetPassword failed: {ex.Message}");
        }
    }

    // Повертає підказку з контактами розробника для отримання паролю.
    public string GetPasswordHint()
        => "Пароль встановлено розробником. Для отримання зверніться: " + DevContacts;

    private static string Hash(string password, string salt)
    {
        var bytes = Encoding.UTF8.GetBytes(salt + ":" + password);
        var hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }

    private static bool FixedTimeEquals(string a, string b)
        => CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
}

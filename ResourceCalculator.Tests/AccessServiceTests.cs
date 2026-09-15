using System.IO;
using System.Text.Json;
using ResourceCalculator.Services;

namespace ResourceCalculator.Tests;

public class AccessServiceTests
{
    private const string GoodPassword = "correct-horse-1234";

    private static AccessService NewService(out string dir)
    {
        dir = Path.Combine(Path.GetTempPath(), "rc-access-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return new AccessService(dir);
    }

    [Fact]
    public void EnsureInitialized_DoesNotCreatePassword_FailClosed()
    {
        var svc = NewService(out var dir);
        try
        {
            svc.EnsureInitialized();
            Assert.False(svc.IsPasswordSet);
            // Fail closed: без файла налаштувань жоден пароль не приймається.
            Assert.False(svc.Verify("anything"));
            Assert.False(svc.Verify(""));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void SetPassword_RoundTrips_WithPbkdf2Iterations()
    {
        var svc = NewService(out var dir);
        try
        {
            svc.SetPassword(GoodPassword);
            Assert.True(svc.IsPasswordSet);
            // Окремий інстанс читає той самий файл (без in-memory стану).
            var svc2 = new AccessService(dir);
            Assert.True(svc2.Verify(GoodPassword));
            Assert.False(svc2.Verify("wrong-password-0000"));

            var json = File.ReadAllText(Path.Combine(dir, "settings.json"));
            using var doc = JsonDocument.Parse(json);
            Assert.True(doc.RootElement.TryGetProperty("Iterations", out var iter));
            Assert.True(iter.GetInt32() >= 100_000);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void SetPassword_RejectsWeakPasswords()
    {
        var svc = NewService(out var dir);
        try
        {
            Assert.Throws<ArgumentException>(() => svc.SetPassword("short"));
            Assert.Throws<ArgumentException>(() => svc.SetPassword(""));
            Assert.False(svc.IsPasswordSet);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void Verify_LegacySha256_MigratesToPbkdf2()
    {
        var svc = NewService(out var dir);
        try
        {
            // Легасі-файл: single-round SHA-256 без поля Iterations.
            var salt = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16));
            using var sha = System.Security.Cryptography.SHA256.Create();
            var hash = Convert.ToBase64String(sha.ComputeHash(
                System.Text.Encoding.UTF8.GetBytes(salt + ":" + GoodPassword)));
            File.WriteAllText(Path.Combine(dir, "settings.json"),
                JsonSerializer.Serialize(new { MatrixPasswordHash = hash, MatrixPasswordSalt = salt }));

            var svc2 = new AccessService(dir);
            Assert.True(svc2.Verify(GoodPassword));
            // Після успіху файл перехешовано у PBKDF2.
            var json = File.ReadAllText(Path.Combine(dir, "settings.json"));
            using var doc = JsonDocument.Parse(json);
            Assert.True(doc.RootElement.TryGetProperty("Iterations", out _));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void Verify_ThrottlesRepeatedFailures()
    {
        var svc = NewService(out var dir);
        try
        {
            svc.SetPassword(GoodPassword);
            Assert.False(svc.Verify("wrong-password-0000"));
            // Наступна спроба одразу — заблокована прогресивною затримкою,
            // навіть із правильним паролем.
            Assert.True(svc.LockoutRemaining > TimeSpan.Zero);
            Assert.False(svc.Verify(GoodPassword));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void DevContacts_ContainsEmailAndPhone()
    {
        Assert.Contains("yaroslav.andreichuk@gmail.com", AccessService.DevContacts);
        Assert.Contains("+380979454941", AccessService.DevContacts);
    }

    [Fact]
    public void GetPasswordHint_DoesNotLeakShippedSecret()
    {
        var svc = NewService(out var dir);
        try
        {
            var hint = svc.GetPasswordHint();
            Assert.Contains("yaroslav.andreichuk@gmail.com", hint);
            Assert.Contains("andreichuk.y@it-enterprise.com", hint);
            Assert.Contains("+380979454941", hint);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
}

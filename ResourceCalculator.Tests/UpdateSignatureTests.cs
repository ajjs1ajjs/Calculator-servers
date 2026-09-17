using System.IO;
using System.Runtime.InteropServices;
using ResourceCalculator.Services;

namespace ResourceCalculator.Tests;

// Перевірка підпису оновлення. Раніше вона була нефатальною («перевіримо, якщо
// підпис є, інакше пишемо в лог») — тобто непідписаний або підписаний будь-ким
// exe проходив далі й підміняв робочу програму. Тепер fail closed.
public class UpdateSignatureTests
{
    [Fact]
    public void PinnedThumbprint_IsFullUppercaseSha256Hex()
    {
        var pin = SelfUpdateService.SigningCertSha256Thumbprint;
        Assert.Equal(64, pin.Length);
        Assert.All(pin, c => Assert.True(Uri.IsHexDigit(c), $"не hex: '{c}'"));
        Assert.Equal(pin.ToUpperInvariant(), pin);
    }

    [Theory]
    [InlineData("ABCD", "ABCD", true)]
    [InlineData("abcd", "ABCD", true)]   // регістр hex не має значення
    [InlineData("ABCD", "ABCE", false)]
    [InlineData("ABCD", "ABC", false)]   // різна довжина
    [InlineData("", "", true)]
    public void FixedTimeEqualsHex_ComparesCaseInsensitively(string a, string b, bool expected)
    {
        Assert.Equal(expected, SelfUpdateService.FixedTimeEqualsHex(a, b));
    }

    [Fact]
    public void FixedTimeEqualsHex_NullIsNeverEqual()
    {
        Assert.False(SelfUpdateService.FixedTimeEqualsHex(null!, "ABCD"));
        Assert.False(SelfUpdateService.FixedTimeEqualsHex("ABCD", null!));
    }

    // Найважливіший негативний кейс: звичайний непідписаний файл мусить бути
    // відкинутий, а не «пропущений з записом у лог», як було раніше.
    [Fact]
    public void VerifyAuthenticode_UnsignedFile_IsRejected()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

        var path = Path.Combine(Path.GetTempPath(), "rc-unsigned-" + Guid.NewGuid().ToString("N") + ".exe");
        File.WriteAllBytes(path, new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00 });
        try
        {
            var ok = SelfUpdateService.VerifyAuthenticode(path, out var error);
            Assert.False(ok);
            Assert.Contains("не підписане", error);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void VerifyAuthenticode_MissingFile_IsRejected()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

        var path = Path.Combine(Path.GetTempPath(), "rc-absent-" + Guid.NewGuid().ToString("N") + ".exe");
        var ok = SelfUpdateService.VerifyAuthenticode(path, out var error);
        Assert.False(ok);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    // Підписаний exe присутній лише після `dotnet publish` + signtool, тож тест
    // умовний: у CI він перевіряє реальний артефакт, локально без підпису — мовчить.
    // Саме він ловить найгірший сценарій: пін у коді розійшовся з сертифікатом,
    // яким CI підписує реліз, і жоден користувач не зможе оновитися.
    [Fact]
    public void VerifyAuthenticode_SignedReleaseExe_IsAccepted()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

        var exe = Environment.GetEnvironmentVariable("ITE_SIGNED_EXE");
        if (string.IsNullOrWhiteSpace(exe)) return;   // локальний прогін без підпису

        // Якщо змінну задали, але файла немає — це помилка конфігурації кроку CI,
        // а не причина тихо пройти: інакше друкарська помилка у шляху перетворила б
        // головний гейт релізу на no-op.
        Assert.True(File.Exists(exe), $"ITE_SIGNED_EXE вказує на неіснуючий файл: {exe}");

        var ok = SelfUpdateService.VerifyAuthenticode(exe, out var error);
        Assert.True(ok, $"підписаний реліз відкинуто: {error}");
    }
}

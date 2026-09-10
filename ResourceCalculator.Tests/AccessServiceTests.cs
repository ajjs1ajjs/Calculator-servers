using System.IO;
using ResourceCalculator.Services;

namespace ResourceCalculator.Tests;

public class AccessServiceTests
{
    private static AccessService NewService(out string dir)
    {
        dir = Path.Combine(Path.GetTempPath(), "rc-access-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return new AccessService(dir);
    }

    [Fact]
    public void EnsureInitialized_SetsDefaultPassword()
    {
        var svc = NewService(out var dir);
        try
        {
            svc.EnsureInitialized();
            Assert.True(svc.IsPasswordSet);
            Assert.True(svc.Verify(AccessService.DefaultPassword));
            Assert.False(svc.Verify("wrong-password"));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void Verify_UnknownFile_AcceptsOnlyDefaultPassword()
    {
        var svc = NewService(out var dir);
        try
        {
            Assert.False(svc.IsPasswordSet);
            Assert.True(svc.Verify(AccessService.DefaultPassword)); // до ініціалізації — лише дефолт
            Assert.False(svc.Verify("nope"));
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
    public void GetPasswordHint_ReturnsContacts()
    {
        var svc = NewService(out var dir);
        try
        {
            var hint = svc.GetPasswordHint();
            Assert.Contains("yaroslav.andreichuk@gmail.com", hint);
            Assert.Contains("andreichuk.y@it-enterprise.com", hint);
            Assert.Contains("+380979454941", hint);
            Assert.Contains("Пароль встановлено розробником", hint);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
}
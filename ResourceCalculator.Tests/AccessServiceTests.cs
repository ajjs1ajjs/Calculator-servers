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
    public void ChangePassword_RequiresCurrentPassword()
    {
        var svc = NewService(out var dir);
        try
        {
            svc.EnsureInitialized();
            Assert.False(svc.ChangePassword("wrong", "NewPass123"));

            Assert.True(svc.ChangePassword(AccessService.DefaultPassword, "NewPass123"));
            Assert.True(svc.Verify("NewPass123"));
            Assert.False(svc.Verify(AccessService.DefaultPassword));
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
    public void RegeneratePassword_CreatesStrongUniquePassword_AndSetsIt()
    {
        var svc = NewService(out var dir);
        try
        {
            svc.EnsureInitialized();
            var p1 = svc.RegeneratePassword();
            var p2 = svc.RegeneratePassword();

            Assert.False(string.IsNullOrWhiteSpace(p1));
            Assert.True(p1.Length >= 12);
            Assert.NotEqual(p1, p2);
            Assert.False(svc.Verify(p1));   // попередній пароль уже неактивний
            Assert.True(svc.Verify(p2));    // останній згенерований пароль активний
            Assert.False(svc.Verify(AccessService.DefaultPassword));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
}

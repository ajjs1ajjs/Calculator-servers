using System.IO;
using ResourceCalculator.Data;
using ResourceCalculator.Models;
using ResourceCalculator.Services;

namespace ResourceCalculator.Tests;

// Регресійні тести аудиту безпеки: валідація матриці, Excel-санітизація,
// allowlist URL оновлень, вироджена математика звірки.
public class SecurityRegressionTests
{
    [Fact]
    public void MatrixValidator_RejectsNegativeCpuAndInvertedRanges()
    {
        var m = new SizingMatrix();
        m.MsSqlRanges.Add(new UserLoadRange { MinUsers = 100, MaxUsers = 50, Cpu = -2 });
        var errors = MatrixValidator.Validate(m);
        Assert.Contains(errors, e => e.Contains("MinUsers") && e.Contains("MaxUsers"));
        Assert.Contains(errors, e => e.Contains("CPU"));
    }

    [Fact]
    public void MatrixValidator_RejectsNaNAndOverlaps()
    {
        var m = new SizingMatrix();
        m.MsSqlRanges.Clear();
        m.MsSqlRanges.Add(new UserLoadRange { MinUsers = 1, MaxUsers = 100, Cpu = double.NaN });
        m.MsSqlRanges.Add(new UserLoadRange { MinUsers = 50, MaxUsers = 200, Cpu = 2 });
        var errors = MatrixValidator.Validate(m);
        Assert.Contains(errors, e => e.Contains("CPU"));
        Assert.Contains(errors, e => e.Contains("перетинаються"));
    }

    [Fact]
    public void MatrixValidator_RejectsZeroPageFileRounding()
    {
        var m = new SizingMatrix();
        m.Engine.PageFileRounding = 0;
        var errors = MatrixValidator.Validate(m);
        Assert.Contains(errors, e => e.Contains("PageFileRounding"));
    }

    [Fact]
    public void MatrixValidator_AcceptsDefaults()
    {
        var errors = MatrixValidator.Validate(new SizingMatrix());
        Assert.Empty(errors);
    }

    [Fact]
    public void MatrixManager_Sync_RejectsInvalidGrids_WithoutMutating()
    {
        var data = new FakeDataService();
        var manager = new MatrixManager(data, new SizingMatrix());
        var badRanges = new List<UserLoadRange>
        {
            new() { MinUsers = 10, MaxUsers = 5, Cpu = 2 }
        };
        var errors = manager.SyncGridsToMatrix(
            badRanges, new(), new(), new(), new(), new(), new(), new(), new(), new EngineSettings());
        Assert.NotEmpty(errors);
    }

    [Theory]
    [InlineData("=cmd|'/c calc'!A0", "'=cmd|'/c calc'!A0")]
    [InlineData("+123", "'+123")]
    [InlineData("-5", "'-5")]
    [InlineData("@SUM(A1)", "'@SUM(A1)")]
    [InlineData("  =HYPERLINK(\"http://x\")", "'  =HYPERLINK(\"http://x\")")]
    [InlineData("Звичайна назва", "Звичайна назва")]
    [InlineData("", "")]
    public void Xl_NeutralizesFormulaInjection(string input, string expected)
    {
        Assert.Equal(expected, ConfigExportService.Xl(input));
    }

    [Theory]
    [InlineData("https://github.com/ajjs1ajjs/Calculator-servers/releases/download/v1/x.exe", true)]
    [InlineData("https://objects.githubusercontent.com/abc/x.exe", true)]
    [InlineData("https://release-assets.githubusercontent.com/abc/x.exe", true)]
    [InlineData("http://github.com/x.exe", false)]
    [InlineData("https://evil.com/x.exe", false)]
    [InlineData("https://github.com.evil.com/x.exe", false)]
    [InlineData("https://github.com:8443/x.exe", false)]
    [InlineData("https://user:pass@github.com/x.exe", false)]
    [InlineData("file:///C:/x.exe", false)]
    [InlineData("", false)]
    public void IsAllowedDownloadUrl_AllowsOnlyGitHubHttps(string url, bool expected)
    {
        Assert.Equal(expected, SelfUpdateService.IsAllowedDownloadUrl(url, out _));
    }

    [Fact]
    public void GetSeverity_DegenerateInputs_ReturnUnknown()
    {
        var engine = new ValidationEngine();
        var req = new ResourceRequirement { TotalCpu = double.NaN };
        var alloc = new ResourceRequirement { TotalCpu = 10 };
        var results = engine.Validate(req, alloc);
        var cpu = results.First(r => r.ResourceName.Contains("CPU") || r.Unit == "cores");
        Assert.Equal("UNKNOWN", cpu.Severity);
    }

    [Fact]
    public void DataService_CorruptMatrix_Preserved_NotDeleted()
    {
        var dir = Path.Combine(Path.GetTempPath(), "rc-matrix-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "matrix.json"), "{not json");
            var svc = new DataService(dir);
            var loaded = svc.LoadMatrix();
            Assert.NotNull(loaded);
            // Битий файл збережено з міткою, а не тихо видалено.
            Assert.True(Directory.GetFiles(dir, "matrix.json.corrupt.*").Length == 1);
            Assert.False(File.Exists(Path.Combine(dir, "matrix.json")));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    private sealed class FakeDataService : ResourceCalculator.Interfaces.IDataService
    {
        public void SaveMatrix(SizingMatrix matrix) { }
        public SizingMatrix LoadMatrix() => new();
        public void ClearMatrix() { }
    }
}

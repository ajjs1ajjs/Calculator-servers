using AIResourceCalculator.Data;
using AIResourceCalculator.Models;

namespace AIResourceCalculator.Tests;

public class DocumentRequirementsTests
{
    [Fact]
    public void ForUsers_PicksMatchingRange()
    {
        var row = DocumentRequirements.ForUsers(100);
        Assert.NotNull(row);
        Assert.Equal(8, row!.CpuCores);
        Assert.Equal(48, row.RamRecGb);
        Assert.Equal(500, row.Iops);
    }

    [Fact]
    public void Compare_FlagsBelowRequirement()
    {
        var req = new ResourceRequirement { UserCount = 100 };
        // Розрахований вузол БД нижчий за вимоги документа (CPU 4 < 8).
        req.Infrastructure.Add(new InfrastructureNode { Name = "SQL Server", Cpu = 4, RamGb = 16, NodeCount = 1, Iops = 200, Latency = 8 });
        var config = new ProjectConfig { UserCount = 100, DatabaseType = DatabaseType.MsSql };

        var items = DocumentRequirements.Compare(req, config);

        Assert.NotEmpty(items);
        var cpu = items.First(i => i.Metric.StartsWith("CPU"));
        Assert.Equal("Нижче вимог", cpu.Status);
    }

    [Fact]
    public void Compare_PassesWhenMeetsRequirement()
    {
        var req = new ResourceRequirement { UserCount = 100 };
        req.Infrastructure.Add(new InfrastructureNode { Name = "SQL Server", Cpu = 8, RamGb = 48, NodeCount = 1, Iops = 500, Latency = 5 });
        var config = new ProjectConfig { UserCount = 100, DatabaseType = DatabaseType.MsSql };

        var items = DocumentRequirements.Compare(req, config);

        Assert.All(items, i => Assert.Equal("Відповідає", i.Status));
    }

    [Fact]
    public void Compare_NonSqlDatabase_ReturnsEmpty()
    {
        var req = new ResourceRequirement { UserCount = 100 };
        req.Infrastructure.Add(new InfrastructureNode { Name = "PostgreSQL", Cpu = 8, RamGb = 48, NodeCount = 1 });
        var config = new ProjectConfig { UserCount = 100, DatabaseType = DatabaseType.PostgreSQL };

        Assert.Empty(DocumentRequirements.Compare(req, config));
    }
}

using AIResourceCalculator.Models;
using AIResourceCalculator.Services;

namespace AIResourceCalculator.Tests;

// Кожне середовище рахується рушієм окремо (див. MainViewModel); EnvironmentScaler відповідає
// лише за резерв під бекап на вузлі БД.
public class EnvironmentScalerTests
{
    private static readonly EnvironmentSettings Settings = new()
    {
        BackupRetentionDays = 7, BackupCompression = 0.5
    };

    [Fact]
    public void BackupReserve_IsRetentionTimesCompressedDataSize_NotFullDisk()
    {
        // Резерв рахується від ОБСЯГУ ДАНИХ (20 ГБ), а не від усього диска БД:
        // 20 × (1 − 0.5) × 7 = 70 ГБ.
        Assert.Equal(70, EnvironmentScaler.BackupReserveGb(20, Settings));
    }

    [Fact]
    public void BackupReserve_ZeroData_NoReserve()
    {
        Assert.Equal(0, EnvironmentScaler.BackupReserveGb(0, Settings));
    }

    [Fact]
    public void AddBackupReserve_GrowsDbDiskByReserve_AndAnnotates()
    {
        var env = new ResourceRequirement { DeploymentType = DeploymentType.Kubernetes };
        env.Infrastructure.Add(new InfrastructureNode { Name = "SQL Server", Cpu = 2, RamGb = 8, NodeCount = 1, StorageGb = 150 });
        env.TotalStorageGb = env.Infrastructure.Sum(n => n.TotalStorageGb);
        var before = env.TotalStorageGb;

        EnvironmentScaler.AddBackupReserve(env, 70);

        var db = env.Infrastructure.First(n => n.Name.Contains("SQL"));
        Assert.Equal(70, db.StorageGb4);
        Assert.Equal(before + 70, env.TotalStorageGb);
        Assert.Contains("бекап", db.Notes);
    }

    [Fact]
    public void AddBackupReserve_ZeroReserve_NoChange()
    {
        var env = new ResourceRequirement { DeploymentType = DeploymentType.Windows };
        env.Infrastructure.Add(new InfrastructureNode { Name = "SQL Server", Cpu = 4, RamGb = 16, NodeCount = 1, StorageGb = 300 });
        env.TotalStorageGb = env.Infrastructure.Sum(n => n.TotalStorageGb);
        var before = env.TotalStorageGb;

        EnvironmentScaler.AddBackupReserve(env, 0);

        Assert.Equal(before, env.TotalStorageGb);
    }
}

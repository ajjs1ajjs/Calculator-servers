using AIResourceCalculator.Models;
using AIResourceCalculator.Services;

namespace AIResourceCalculator.Tests;

public class EnvironmentScalerTests
{
    private static ResourceRequirement BuildProd()
    {
        var req = new ResourceRequirement
        {
            UserCount = 100, DeploymentType = DeploymentType.Kubernetes, LoadProfile = LoadProfile.Basic
        };
        // SQL: split disks 100+150+300+200 = 750 ГБ.
        req.Infrastructure.Add(new InfrastructureNode
        {
            Name = "SQL Server", Cpu = 8, RamGb = 48, NodeCount = 1,
            StorageGb = 100, StorageGb2 = 150, StorageGb3 = 300, StorageGb4 = 200, Iops = 800
        });
        req.Infrastructure.Add(new InfrastructureNode { Name = "Master Node", Cpu = 4, RamGb = 6, NodeCount = 1, StorageGb = 100 });
        req.Infrastructure.Add(new InfrastructureNode { Name = "Worker Node", Cpu = 8, RamGb = 32, NodeCount = 3, StorageGb = 200 });
        req.TotalCpu = req.Infrastructure.Sum(n => n.Cpu * n.NodeCount);
        req.TotalRamGb = req.Infrastructure.Sum(n => n.RamGb * n.NodeCount);
        req.TotalStorageGb = req.Infrastructure.Sum(n => n.TotalStorageGb);
        return req;
    }

    private static readonly EnvironmentSettings Settings = new()
    {
        BackupRetentionDays = 7, BackupCompression = 0.5, TestScaleFactor = 0.5, PredProdMultiplier = 1.2
    };

    [Fact]
    public void BackupReserve_IsRetentionTimesCompressedDbSize()
    {
        var prod = BuildProd();
        // 750 ГБ даних БД × (1 − 0.5) × 7 днів = 2625 ГБ.
        Assert.Equal(2625, EnvironmentScaler.BackupReserveGb(prod, Settings));
    }

    [Fact]
    public void Test_ReducesPowerButNeverDisk()
    {
        var prod = BuildProd();
        var reserve = EnvironmentScaler.BackupReserveGb(prod, Settings);
        var test = EnvironmentScaler.ScaleFromProd(prod, Settings.TestScaleFactor, reserve);

        // Потужність нижча за PROD.
        Assert.True(test.TotalCpu < prod.TotalCpu);
        Assert.True(test.TotalRamGb < prod.TotalRamGb);
        // Диск НЕ менший за PROD (навпаки — більший на бекап-резерв).
        Assert.True(test.TotalStorageGb >= prod.TotalStorageGb);
    }

    [Fact]
    public void Test_AddsBackupReserveToDatabaseNode()
    {
        var prod = BuildProd();
        var reserve = EnvironmentScaler.BackupReserveGb(prod, Settings);
        var test = EnvironmentScaler.ScaleFromProd(prod, Settings.TestScaleFactor, reserve);

        var db = test.Infrastructure.First(n => n.Name.Contains("SQL"));
        // Початковий StorageGb4 = 200, плюс резерв 2625.
        Assert.Equal(200 + reserve, db.StorageGb4);
        Assert.Contains("бекап", db.Notes);
    }

    [Fact]
    public void PredProd_IsMorePowerfulThanTest_SameDisk()
    {
        var prod = BuildProd();
        var reserve = EnvironmentScaler.BackupReserveGb(prod, Settings);
        var test = EnvironmentScaler.ScaleFromProd(prod, Settings.TestScaleFactor, reserve);
        var pred = EnvironmentScaler.ScaleFromProd(prod, Settings.TestScaleFactor * Settings.PredProdMultiplier, reserve);

        Assert.True(pred.TotalCpu >= test.TotalCpu);
        Assert.True(pred.TotalRamGb >= test.TotalRamGb);
        // Диск однаковий (PROD + той самий бекап-резерв) і не менший за PROD.
        Assert.Equal(test.TotalStorageGb, pred.TotalStorageGb);
        Assert.True(pred.TotalStorageGb >= prod.TotalStorageGb);
    }

    [Fact]
    public void AddBackupReserve_GrowsDevDiskByReserve()
    {
        var dev = new ResourceRequirement { DeploymentType = DeploymentType.Kubernetes };
        dev.Infrastructure.Add(new InfrastructureNode { Name = "SQL Server", Cpu = 2, RamGb = 8, NodeCount = 1, StorageGb = 150 });
        dev.TotalStorageGb = dev.Infrastructure.Sum(n => n.TotalStorageGb);
        var before = dev.TotalStorageGb;

        EnvironmentScaler.AddBackupReserve(dev, 500);

        Assert.Equal(before + 500, dev.TotalStorageGb);
    }
}

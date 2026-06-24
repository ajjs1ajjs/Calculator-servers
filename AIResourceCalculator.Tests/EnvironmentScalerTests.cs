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
        req.TotalIops = 800;
        return req;
    }

    private static readonly EnvironmentSettings Settings = new()
    {
        BackupRetentionDays = 7, BackupCompression = 0.5, TestScaleFactor = 0.5, PredProdMultiplier = 1.2
    };

    [Fact]
    public void BackupReserve_IsRetentionTimesCompressedDataSize_NotFullDisk()
    {
        // Резерв рахується від ОБСЯГУ ДАНИХ (20 ГБ), а не від усього диска БД (750 ГБ):
        // 20 × (1 − 0.5) × 7 = 70 ГБ.
        Assert.Equal(70, EnvironmentScaler.BackupReserveGb(20, Settings));
    }

    [Fact]
    public void BackupReserve_ZeroData_NoReserve()
    {
        Assert.Equal(0, EnvironmentScaler.BackupReserveGb(0, Settings));
    }

    [Fact]
    public void Test_ReducesPower_KeepsDbDataDisks()
    {
        var prod = BuildProd();
        var reserve = EnvironmentScaler.BackupReserveGb(20, Settings);
        var test = EnvironmentScaler.ScaleFromProd(prod, Settings.TestScaleFactor);
        EnvironmentScaler.AddBackupReserve(test, reserve);

        // Потужність нижча за PROD (менше ВМ/ядер/пам'яті).
        Assert.True(test.TotalCpu < prod.TotalCpu);
        Assert.True(test.TotalRamGb < prod.TotalRamGb);

        // Диски ДАНИХ вузла БД зберігаються (не зменшуються), плюс додається бекап-резерв.
        var prodDb = prod.Infrastructure.First(n => n.Name.Contains("SQL"));
        var testDb = test.Infrastructure.First(n => n.Name.Contains("SQL"));
        Assert.True(testDb.DiskPerNodeGb >= prodDb.DiskPerNodeGb);
        Assert.Equal(prodDb.DiskPerNodeGb + reserve, testDb.DiskPerNodeGb);
    }

    [Fact]
    public void Test_AddsBackupReserveToDatabaseNode()
    {
        var prod = BuildProd();
        var reserve = EnvironmentScaler.BackupReserveGb(20, Settings);
        var test = EnvironmentScaler.ScaleFromProd(prod, Settings.TestScaleFactor);
        EnvironmentScaler.AddBackupReserve(test, reserve);

        var db = test.Infrastructure.First(n => n.Name.Contains("SQL"));
        // Початковий StorageGb4 = 200, плюс резерв.
        Assert.Equal(200 + reserve, db.StorageGb4);
        Assert.Contains("бекап", db.Notes);
    }

    [Fact]
    public void PredProd_IsMorePowerfulThanTest_SameDisk()
    {
        var prod = BuildProd();
        var reserve = EnvironmentScaler.BackupReserveGb(20, Settings);
        var test = EnvironmentScaler.ScaleFromProd(prod, Settings.TestScaleFactor);
        EnvironmentScaler.AddBackupReserve(test, reserve);
        var pred = EnvironmentScaler.ScaleFromProd(prod, Settings.TestScaleFactor * Settings.PredProdMultiplier);
        EnvironmentScaler.AddBackupReserve(pred, reserve);

        Assert.True(pred.TotalCpu >= test.TotalCpu);
        Assert.True(pred.TotalRamGb >= test.TotalRamGb);
        // Диск однаковий (та сама к-сть ВМ + той самий бекап-резерв).
        Assert.Equal(test.TotalStorageGb, pred.TotalStorageGb);
    }

    [Fact]
    public void Scale_KeepsDbIopsNotSummed()
    {
        var prod = BuildProd();
        var test = EnvironmentScaler.ScaleFromProd(prod, Settings.TestScaleFactor);
        // IOPS не сумуються між дисками — лишається IOPS вузла БД, як у PROD.
        Assert.Equal(prod.TotalIops, test.TotalIops);
    }

    [Fact]
    public void AddBackupReserve_GrowsDevDiskByReserve()
    {
        var dev = new ResourceRequirement { DeploymentType = DeploymentType.Kubernetes };
        dev.Infrastructure.Add(new InfrastructureNode { Name = "SQL Server", Cpu = 2, RamGb = 8, NodeCount = 1, StorageGb = 150 });
        dev.TotalStorageGb = dev.Infrastructure.Sum(n => n.TotalStorageGb);
        var before = dev.TotalStorageGb;

        EnvironmentScaler.AddBackupReserve(dev, 70);

        Assert.Equal(before + 70, dev.TotalStorageGb);
    }
}

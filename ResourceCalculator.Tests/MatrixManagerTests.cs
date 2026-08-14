using ResourceCalculator.Data;
using ResourceCalculator.Interfaces;
using ResourceCalculator.Models;
using ResourceCalculator.Services;

namespace ResourceCalculator.Tests;

public class MatrixManagerTests
{
    // Стаб, що повертає "стару" матрицю без політики модулів (App Server не обов'язковий, LMS увімкнено).
    private sealed class StaleDataService : IDataService
    {
        public SizingMatrix LoadMatrix()
        {
            var m = new SizingMatrix();
            foreach (var mod in m.DocumentFlowModules)
            {
                mod.IsMandatory = false;                  // старі дані не знали про IsMandatory
                if (mod.Name is "LMS" or "HR Portal") mod.IsEnabled = true; // помилково увімкнені
            }
            return m;
        }
        public void SaveMatrix(SizingMatrix matrix) { }
        public void ClearMatrix() { }
    }

    [Fact]
    public void Load_RestoresModulePolicy_FromCode()
    {
        var manager = new MatrixManager(new StaleDataService(), new SizingMatrix());

        foreach (var name in new[] { "App Server", "ROBOT", "Web" })
        {
            var mod = manager.Matrix.DocumentFlowModules.First(m => m.Name == name);
            Assert.True(mod.IsMandatory, $"{name} має бути обов'язковим");
            Assert.True(mod.IsEnabled);
        }

        foreach (var name in new[] { "LMS", "HR Portal" })
        {
            var mod = manager.Matrix.DocumentFlowModules.First(m => m.Name == name);
            Assert.False(mod.IsMandatory);
            Assert.False(mod.IsEnabled);
        }
    }

    // Синхронізація грідів редактора матриці: усі діапазони, вузли та налаштування рушія
    // потрапляють у матрицю й далі використовуються розрахунком.
    [Fact]
    public void SyncGridsToMatrix_PersistsAllRangesNodesAndEngineSettings()
    {
        var manager = new MatrixManager(new EmptyDataService(), new SizingMatrix());

        var appRange = new UserLoadRange { MinUsers = 1, MaxUsers = 10, Cpu = 2, RamRec = 4, InstanceCount = 1, Ghz = 2.4 };
        var pgRange = new UserLoadRange { MinUsers = 1, MaxUsers = 25, Cpu = 1, RamRec = 2, Iops = 100 };
        var sqlNode = new InfrastructureNode { Name = "SQL Server", Cpu = 8, RamGb = 32, Os = "Windows Server 2022", StorageGb = 300 };
        var windowsApp = new InfrastructureNode { Name = "Сервери додатків", Cpu = 4, RamGb = 16 };
        var reporting = new InfrastructureNode { Name = "Сервер звітів", Cpu = 2, RamGb = 4 };

        manager.SyncGridsToMatrix(
            new List<UserLoadRange> { appRange },
            new List<UserLoadRange>(),
            new List<UserLoadRange> { appRange },
            new List<UserLoadRange>(),
            new List<UserLoadRange>(),
            new List<UserLoadRange>(),
            new List<UserLoadRange> { pgRange },
            new List<UserLoadRange>(),
            new List<ServiceComponent>(),
            new List<InfrastructureNode> { sqlNode },
            new List<InfrastructureNode> { windowsApp },
            new List<InfrastructureNode> { reporting },
            new EngineSettings { SmartIdCpuPerReplica = 0.5, PageFileMultiplier = 6 });

        Assert.Equal(1, manager.Matrix.AppServerRanges.Count);
        Assert.Equal(2.4, manager.Matrix.AppServerRanges[0].Ghz);
        Assert.Equal(1, manager.Matrix.PostgresRanges.Count);
        Assert.Equal(100, manager.Matrix.PostgresRanges[0].Iops);
        Assert.NotNull(manager.Matrix.DefaultK8sSql);
        Assert.Equal(8, manager.Matrix.DefaultK8sSql!.Cpu);
        Assert.NotNull(manager.Matrix.DefaultWindowsApp);
        Assert.Equal("Сервери додатків", manager.Matrix.DefaultWindowsApp!.Name);
        Assert.NotNull(manager.Matrix.DefaultReportingServer);
        Assert.NotNull(manager.Matrix.Engine);
        Assert.Equal(0.5, manager.Matrix.Engine!.SmartIdCpuPerReplica);
        Assert.Equal(6, manager.Matrix.Engine.PageFileMultiplier);
    }

    private sealed class EmptyDataService : IDataService
    {
        public SizingMatrix LoadMatrix() => new();
        public void SaveMatrix(SizingMatrix matrix) { }
        public void ClearMatrix() { }
    }
}

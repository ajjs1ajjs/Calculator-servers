using ResourceCalculator.Data;
using ResourceCalculator.Interfaces;
using ResourceCalculator.Models;
using ResourceCalculator.Services;

namespace ResourceCalculator.Tests;

// Регресія: раніше MatrixManager роздавав відредаговані вузли по слотах матриці
// через Name.Contains(...). Назва "Веб сервери (IIS)" містить "сервер", тому
// потрапляла у слот сервера додатків раніше, ніж перевірялася гілка Web: правки
// веб-вузла губилися, а сервер додатків отримував чужу назву й диски.
public class MatrixNodeSlotTests
{
    private sealed class EmptyDataService : IDataService
    {
        public SizingMatrix LoadMatrix() => new();
        public void SaveMatrix(SizingMatrix matrix) { }
        public void ClearMatrix() { }
    }

    private static List<string> Sync(MatrixManager m,
        List<InfrastructureNode> k8s, List<InfrastructureNode> win, List<InfrastructureNode> opt)
        => m.SyncGridsToMatrix(
            new List<UserLoadRange>(), new List<UserLoadRange>(), new List<UserLoadRange>(),
            new List<UserLoadRange>(), new List<UserLoadRange>(),
            new List<ServiceComponent>(), k8s, win, opt, new EngineSettings());

    [Fact]
    public void WindowsGrid_KeepsAppAndWebNodesApart()
    {
        var manager = new MatrixManager(new EmptyDataService(), new SizingMatrix());
        var sql = new InfrastructureNode { Slot = NodeSlot.WindowsSql, Name = "SQL Server", Cpu = 8 };
        var app = new InfrastructureNode { Slot = NodeSlot.WindowsApp, Name = "Сервери додатків", Cpu = 4, StorageGb = 111 };
        var web = new InfrastructureNode { Slot = NodeSlot.WindowsWeb, Name = "Веб сервери (IIS)", Cpu = 2, StorageGb = 222 };

        var errors = Sync(manager, new List<InfrastructureNode>(),
            new List<InfrastructureNode> { sql, app, web }, new List<InfrastructureNode>());

        Assert.Empty(errors);
        Assert.Equal("Сервери додатків", manager.Matrix.DefaultWindowsApp!.Name);
        Assert.Equal(111, manager.Matrix.DefaultWindowsApp!.StorageGb);
        Assert.Equal("Веб сервери (IIS)", manager.Matrix.DefaultWindowsWeb!.Name);
        Assert.Equal(222, manager.Matrix.DefaultWindowsWeb!.StorageGb);
    }

    // Перейменування вузла в гріді не має переносити його в інший слот.
    [Fact]
    public void RenamedNode_StaysInItsSlot()
    {
        var manager = new MatrixManager(new EmptyDataService(), new SizingMatrix());
        var web = new InfrastructureNode { Slot = NodeSlot.WindowsWeb, Name = "SQL-балансир App", StorageGb = 777 };

        Sync(manager, new List<InfrastructureNode>(),
            new List<InfrastructureNode> { web }, new List<InfrastructureNode>());

        Assert.Equal(777, manager.Matrix.DefaultWindowsWeb!.StorageGb);
        Assert.NotEqual(777, manager.Matrix.DefaultWindowsSql!.StorageGb);
        Assert.NotEqual(777, manager.Matrix.DefaultWindowsApp!.StorageGb);
    }

    [Fact]
    public void K8sAndOptionalGrids_MapToTheirOwnSlots()
    {
        var manager = new MatrixManager(new EmptyDataService(), new SizingMatrix());
        var k8s = new List<InfrastructureNode>
        {
            new() { Slot = NodeSlot.K8sSql, Name = "SQL Server", Cpu = 6 },
            new() { Slot = NodeSlot.K8sMaster, Name = "Master node", Cpu = 2 },
            new() { Slot = NodeSlot.K8sWorker, Name = "Worker-node", Cpu = 16 },
        };
        var opt = new List<InfrastructureNode>
        {
            new() { Slot = NodeSlot.ReportingServer, Name = "Сервер звітів", Cpu = 3 },
            new() { Slot = NodeSlot.HaProxy, Name = "HAProxy", Cpu = 1 },
        };

        Sync(manager, k8s, new List<InfrastructureNode>(), opt);

        Assert.Equal(6, manager.Matrix.DefaultK8sSql!.Cpu);
        Assert.Equal(2, manager.Matrix.DefaultK8sMaster!.Cpu);
        Assert.Equal(16, manager.Matrix.DefaultK8sWorker!.Cpu);
        Assert.Equal(3, manager.Matrix.DefaultReportingServer!.Cpu);
        Assert.Equal(1, manager.Matrix.DefaultHaProxy!.Cpu);
    }

    // Дефолти коду мусять мати слоти — інакше редактор матриці не зміг би повернути правки.
    [Fact]
    public void CodeDefaults_HaveSlotsAssigned()
    {
        var m = new SizingMatrix();
        Assert.Equal(NodeSlot.K8sSql, m.DefaultK8sSql!.Slot);
        Assert.Equal(NodeSlot.K8sMaster, m.DefaultK8sMaster!.Slot);
        Assert.Equal(NodeSlot.K8sWorker, m.DefaultK8sWorker!.Slot);
        Assert.Equal(NodeSlot.WindowsSql, m.DefaultWindowsSql!.Slot);
        Assert.Equal(NodeSlot.WindowsApp, m.DefaultWindowsApp!.Slot);
        Assert.Equal(NodeSlot.WindowsWeb, m.DefaultWindowsWeb!.Slot);
        Assert.Equal(NodeSlot.ReportingServer, m.DefaultReportingServer!.Slot);
        Assert.Equal(NodeSlot.HaProxy, m.DefaultHaProxy!.Slot);
        Assert.Empty(MatrixValidator.Validate(m));
    }

    // Файл матриці з переставленими слотами приймати не можна.
    [Fact]
    public void Validator_RejectsWrongSlot()
    {
        var m = new SizingMatrix();
        m.DefaultWindowsWeb!.Slot = NodeSlot.WindowsApp;
        Assert.Contains(MatrixValidator.Validate(m), e => e.Contains("Windows Web"));
    }
}

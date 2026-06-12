using AIResourceCalculator.Models;

namespace AIResourceCalculator.Data;

public class SizingMatrix
{
    public List<UserLoadRange> MsSqlRanges { get; set; } = new()
    {
        new() { MinUsers = 1, MaxUsers = 10, Cpu = 2, RamMin = 4, RamRec = 8, Iops = 200, Latency = 8 },
        new() { MinUsers = 11, MaxUsers = 25, Cpu = 4, RamMin = 8, RamRec = 12, Iops = 250, Latency = 7 },
        new() { MinUsers = 26, MaxUsers = 50, Cpu = 6, RamMin = 16, RamRec = 24, Iops = 300, Latency = 5 },
        new() { MinUsers = 51, MaxUsers = 100, Cpu = 8, RamMin = 32, RamRec = 48, Iops = 500, Latency = 4 },
        new() { MinUsers = 101, MaxUsers = 200, Cpu = 10, RamMin = 64, RamRec = 96, Iops = 800, Latency = 3 },
    };
    public List<UserLoadRange> MsSqlPerformanceRanges { get; set; } = new()
    {
        new() { MinUsers = 1, MaxUsers = 10, Cpu = 2, RamMin = 4, RamRec = 8, Iops = 200, Latency = 8 },
        new() { MinUsers = 11, MaxUsers = 25, Cpu = 4, RamMin = 8, RamRec = 12, Iops = 250, Latency = 7 },
        new() { MinUsers = 26, MaxUsers = 50, Cpu = 4, RamMin = 16, RamRec = 24, Iops = 300, Latency = 5 },
        new() { MinUsers = 51, MaxUsers = 100, Cpu = 6, RamMin = 32, RamRec = 48, Iops = 500, Latency = 4 },
        new() { MinUsers = 101, MaxUsers = 200, Cpu = 6, RamMin = 48, RamRec = 64, Iops = 800, Latency = 3 },
    };
    public List<UserLoadRange> AppServerRanges { get; set; } = new()
    {
        new() { MinUsers = 1, MaxUsers = 10, InstanceCount = 1, Ghz = 2, Cpu = 4, Iops = 250, RamMin = 6, RamRec = 8 },
        new() { MinUsers = 11, MaxUsers = 25, InstanceCount = 1, Ghz = 2.4, Cpu = 4, Iops = 250, RamMin = 12, RamRec = 16 },
        new() { MinUsers = 26, MaxUsers = 50, InstanceCount = 1, Ghz = 2.4, Cpu = 4, Iops = 500, RamMin = 16, RamRec = 24 },
        new() { MinUsers = 51, MaxUsers = 100, InstanceCount = 2, Ghz = 2.4, Cpu = 4, Iops = 500, RamMin = 16, RamRec = 24 },
        new() { MinUsers = 101, MaxUsers = 200, InstanceCount = 4, Ghz = 2.4, Cpu = 4, Iops = 500, RamMin = 16, RamRec = 24 },
    };
    public List<UserLoadRange> WebServerRanges { get; set; } = new()
    {
        new() { MinUsers = 1, MaxUsers = 50, InstanceCount = 1, Ghz = 2, Cpu = 4, Iops = 200, RamMin = 4, RamRec = 6 },
        new() { MinUsers = 51, MaxUsers = 100, InstanceCount = 1, Ghz = 2, Cpu = 4, Iops = 200, RamMin = 6, RamRec = 8 },
        new() { MinUsers = 101, MaxUsers = 200, InstanceCount = 1, Ghz = 2.2, Cpu = 4, Iops = 200, RamMin = 8, RamRec = 14 },
        new() { MinUsers = 201, MaxUsers = 400, InstanceCount = 1, Ghz = 2.2, Cpu = 4, Iops = 200, RamMin = 14, RamRec = 18 },
        new() { MinUsers = 401, MaxUsers = 800, InstanceCount = 2, Ghz = 2.2, Cpu = 4, Iops = 200, RamMin = 14, RamRec = 18 },
    };

    public List<ServiceComponent> K8sBasicComponents { get; set; } = new();
    public List<ServiceComponent> K8sPerformanceComponents { get; set; } = new();

    public InfrastructureNode DefaultK8sSql { get; set; } = new() { Name = "SQL Server", Os = "Windows Server 2022", Cpu = 4, RamGb = 12, NodeCount = 1, StorageGb = 300, StorageType = "SSD" };
    public InfrastructureNode DefaultK8sMaster { get; set; } = new() { Name = "Master Node", Os = "Ubuntu 24.04", Cpu = 4, RamGb = 6, NodeCount = 1, StorageGb = 100, StorageType = "SSD" };
    public InfrastructureNode DefaultK8sWorker { get; set; } = new() { Name = "Worker Node", Os = "Ubuntu 24.04", Cpu = 8, RamGb = 32, NodeCount = 1, StorageGb = 200, StorageType = "SSD" };

    public InfrastructureNode DefaultWindowsSql { get; set; } = new() { Name = "SQL Server", Os = "Windows Server 2022", Cpu = 4, RamGb = 12, NodeCount = 1, StorageGb = 300, StorageType = "SSD" };
    public InfrastructureNode DefaultWindowsApp { get; set; } = new() { Name = "App Server", Os = "Windows Server 2022", Cpu = 4, RamGb = 16, NodeCount = 1, StorageGb = 150, StorageType = "SSD" };
    public InfrastructureNode DefaultWindowsWeb { get; set; } = new() { Name = "Web Server (IIS)", Os = "Windows Server 2022", Cpu = 4, RamGb = 8, NodeCount = 1, StorageGb = 150, StorageType = "SSD" };

    public List<UserLoadRange> AppServerPerformanceRanges { get; set; } = new();
    public List<UserLoadRange> WebServerPerformanceRanges { get; set; } = new();
}

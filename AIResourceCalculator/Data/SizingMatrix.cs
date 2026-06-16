using AIResourceCalculator.Models;

namespace AIResourceCalculator.Data;

public class SizingMatrix
{
    public List<UserLoadRange> MsSqlRanges { get; set; } = new()
    {
        new() { MinUsers = 1, MaxUsers = 10,    Cpu = 2,  RamMin = 4,   RamRec = 8,    Iops = 200,   Latency = 8 },
        new() { MinUsers = 11, MaxUsers = 25,   Cpu = 4,  RamMin = 8,   RamRec = 12,   Iops = 250,   Latency = 7 },
        new() { MinUsers = 26, MaxUsers = 50,   Cpu = 6,  RamMin = 16,  RamRec = 24,   Iops = 300,   Latency = 5 },
        new() { MinUsers = 51, MaxUsers = 100,  Cpu = 8,  RamMin = 32,  RamRec = 48,   Iops = 500,   Latency = 4 },
        new() { MinUsers = 101, MaxUsers = 200, Cpu = 10, RamMin = 64,  RamRec = 96,   Iops = 800,   Latency = 3 },
        new() { MinUsers = 201, MaxUsers = 350, Cpu = 12, RamMin = 112, RamRec = 168,  Iops = 1400,  Latency = 2 },
        new() { MinUsers = 351, MaxUsers = 500, Cpu = 16, RamMin = 168, RamRec = 240,  Iops = 2000,  Latency = 1 },
        new() { MinUsers = 501, MaxUsers = 1000, Cpu = 20, RamMin = 240, RamRec = 384, Iops = 4000,  Latency = 0.6 },
        new() { MinUsers = 1001, MaxUsers = 2000, Cpu = 22, RamMin = 384, RamRec = 576, Iops = 12000, Latency = 0.2 },
        new() { MinUsers = 2001, MaxUsers = 3000, Cpu = 24, RamMin = 576, RamRec = 768, Iops = 24000, Latency = 0.1 },
        new() { MinUsers = 3001, MaxUsers = 4000, Cpu = 28, RamMin = 768, RamRec = 960, Iops = 36000, Latency = 0.1 },
        new() { MinUsers = 4001, MaxUsers = 5000, Cpu = 32, RamMin = 960, RamRec = 1152, Iops = 48000, Latency = 0.1 },
    };
    public List<UserLoadRange> MsSqlPerformanceRanges { get; set; } = new()
    {
        new() { MinUsers = 1, MaxUsers = 10,    Cpu = 2,  RamMin = 4,   RamRec = 8,    Iops = 200,   Latency = 8 },
        new() { MinUsers = 11, MaxUsers = 25,   Cpu = 4,  RamMin = 8,   RamRec = 12,   Iops = 250,   Latency = 7 },
        new() { MinUsers = 26, MaxUsers = 50,   Cpu = 4,  RamMin = 16,  RamRec = 24,   Iops = 300,   Latency = 5 },
        new() { MinUsers = 51, MaxUsers = 100,  Cpu = 6,  RamMin = 32,  RamRec = 48,   Iops = 500,   Latency = 4 },
        new() { MinUsers = 101, MaxUsers = 200, Cpu = 6,  RamMin = 48,  RamRec = 64,   Iops = 800,   Latency = 3 },
        new() { MinUsers = 201, MaxUsers = 350, Cpu = 8,  RamMin = 64,  RamRec = 96,   Iops = 1400,  Latency = 2 },
        new() { MinUsers = 351, MaxUsers = 500, Cpu = 8,  RamMin = 96,  RamRec = 128,  Iops = 2000,  Latency = 1 },
        new() { MinUsers = 501, MaxUsers = 1000, Cpu = 12, RamMin = 128, RamRec = 192, Iops = 4000,  Latency = 0.6 },
        new() { MinUsers = 1001, MaxUsers = 2000, Cpu = 12, RamMin = 192, RamRec = 378, Iops = 12000, Latency = 0.2 },
        new() { MinUsers = 2001, MaxUsers = 3000, Cpu = 16, RamMin = 378, RamRec = 512, Iops = 36000, Latency = 0.1 },
        new() { MinUsers = 3001, MaxUsers = 4000, Cpu = 16, RamMin = 512, RamRec = 768, Iops = 64000, Latency = 0.1 },
        new() { MinUsers = 4001, MaxUsers = 5000, Cpu = 24, RamMin = 768, RamRec = 1024, Iops = 128000, Latency = 0.1 },
    };
    public List<UserLoadRange> AppServerRanges { get; set; } = new()
    {
        new() { MinUsers=1, MaxUsers=10, InstanceCount=1, Ghz=2.0, Cpu=4, Iops=250, RamMin=6, RamRec=8 },
        new() { MinUsers=11, MaxUsers=25, InstanceCount=1, Ghz=2.4, Cpu=4, Iops=250, RamMin=12, RamRec=16 },
        new() { MinUsers=26, MaxUsers=50, InstanceCount=1, Ghz=2.4, Cpu=4, Iops=500, RamMin=16, RamRec=24 },
        new() { MinUsers=51, MaxUsers=100, InstanceCount=2, Ghz=2.4, Cpu=4, Iops=500, RamMin=16, RamRec=24 },
        new() { MinUsers=101, MaxUsers=200, InstanceCount=4, Ghz=2.4, Cpu=4, Iops=500, RamMin=16, RamRec=24 },
        new() { MinUsers=201, MaxUsers=350, InstanceCount=7, Ghz=2.4, Cpu=4, Iops=500, RamMin=16, RamRec=24 },
        new() { MinUsers=351, MaxUsers=500, InstanceCount=10, Ghz=2.4, Cpu=4, Iops=500, RamMin=16, RamRec=24 },
        new() { MinUsers=501, MaxUsers=1000, InstanceCount=20, Ghz=2.4, Cpu=4, Iops=500, RamMin=16, RamRec=24 },
        new() { MinUsers=1001, MaxUsers=2000, InstanceCount=40, Ghz=2.4, Cpu=4, Iops=500, RamMin=16, RamRec=24 },
        new() { MinUsers=2001, MaxUsers=3000, InstanceCount=60, Ghz=2.4, Cpu=4, Iops=500, RamMin=16, RamRec=24 },
        new() { MinUsers=3001, MaxUsers=4000, InstanceCount=80, Ghz=2.4, Cpu=4, Iops=500, RamMin=16, RamRec=24 },
        new() { MinUsers=4001, MaxUsers=5000, InstanceCount=100, Ghz=2.4, Cpu=4, Iops=500, RamMin=16, RamRec=24 },
    };
    public List<UserLoadRange> WebServerRanges { get; set; } = new()
    {
        new() { MinUsers=1, MaxUsers=50, InstanceCount=1, Ghz=2.0, Cpu=4, Iops=200, RamMin=4, RamRec=6 },
        new() { MinUsers=51, MaxUsers=100, InstanceCount=1, Ghz=2.0, Cpu=4, Iops=200, RamMin=6, RamRec=8 },
        new() { MinUsers=101, MaxUsers=200, InstanceCount=1, Ghz=2.2, Cpu=4, Iops=200, RamMin=8, RamRec=14 },
        new() { MinUsers=201, MaxUsers=400, InstanceCount=1, Ghz=2.2, Cpu=4, Iops=200, RamMin=14, RamRec=18 },
        new() { MinUsers=401, MaxUsers=800, InstanceCount=2, Ghz=2.2, Cpu=4, Iops=200, RamMin=14, RamRec=18 },
        new() { MinUsers=801, MaxUsers=1600, InstanceCount=4, Ghz=2.2, Cpu=4, Iops=200, RamMin=14, RamRec=18 },
        new() { MinUsers=1601, MaxUsers=3200, InstanceCount=8, Ghz=2.2, Cpu=4, Iops=200, RamMin=14, RamRec=18 },
        new() { MinUsers=3201, MaxUsers=6400, InstanceCount=16, Ghz=2.2, Cpu=4, Iops=200, RamMin=14, RamRec=18 },
        new() { MinUsers=6401, MaxUsers=12800, InstanceCount=32, Ghz=2.2, Cpu=4, Iops=200, RamMin=14, RamRec=18 },
    };
    public List<UserLoadRange> AppServerPerformanceRanges { get; set; } = new()
    {
        new() { MinUsers=1, MaxUsers=10, InstanceCount=1, Ghz=2.4, Cpu=4, Iops=250, RamMin=6, RamRec=8 },
        new() { MinUsers=11, MaxUsers=25, InstanceCount=1, Ghz=2.4, Cpu=4, Iops=250, RamMin=12, RamRec=16 },
        new() { MinUsers=26, MaxUsers=50, InstanceCount=1, Ghz=2.4, Cpu=4, Iops=1200, RamMin=24, RamRec=32 },
        new() { MinUsers=51, MaxUsers=100, InstanceCount=2, Ghz=2.4, Cpu=4, Iops=1200, RamMin=24, RamRec=32 },
        new() { MinUsers=101, MaxUsers=200, InstanceCount=3, Ghz=2.4, Cpu=4, Iops=1200, RamMin=24, RamRec=32 },
        new() { MinUsers=201, MaxUsers=350, InstanceCount=5, Ghz=2.4, Cpu=4, Iops=1200, RamMin=24, RamRec=32 },
        new() { MinUsers=351, MaxUsers=500, InstanceCount=7, Ghz=2.4, Cpu=4, Iops=1200, RamMin=24, RamRec=32 },
        new() { MinUsers=501, MaxUsers=1000, InstanceCount=14, Ghz=2.4, Cpu=4, Iops=1200, RamMin=24, RamRec=32 },
        new() { MinUsers=1001, MaxUsers=2000, InstanceCount=27, Ghz=2.4, Cpu=4, Iops=1200, RamMin=24, RamRec=32 },
        new() { MinUsers=2001, MaxUsers=3000, InstanceCount=40, Ghz=2.4, Cpu=4, Iops=1200, RamMin=24, RamRec=32 },
        new() { MinUsers=3001, MaxUsers=4000, InstanceCount=54, Ghz=2.4, Cpu=4, Iops=1200, RamMin=24, RamRec=32 },
        new() { MinUsers=4001, MaxUsers=5000, InstanceCount=67, Ghz=2.4, Cpu=4, Iops=1200, RamMin=24, RamRec=32 },
    };
    public List<UserLoadRange> WebServerPerformanceRanges { get; set; } = new()
    {
        new() { MinUsers=1, MaxUsers=50, InstanceCount=1, Ghz=2.0, Cpu=4, Iops=200, RamMin=4, RamRec=6 },
        new() { MinUsers=51, MaxUsers=100, InstanceCount=1, Ghz=2.0, Cpu=4, Iops=200, RamMin=6, RamRec=8 },
        new() { MinUsers=101, MaxUsers=200, InstanceCount=1, Ghz=2.2, Cpu=4, Iops=200, RamMin=8, RamRec=14 },
        new() { MinUsers=201, MaxUsers=400, InstanceCount=1, Ghz=2.2, Cpu=4, Iops=200, RamMin=14, RamRec=18 },
        new() { MinUsers=401, MaxUsers=800, InstanceCount=2, Ghz=2.2, Cpu=4, Iops=200, RamMin=14, RamRec=18 },
        new() { MinUsers=801, MaxUsers=1600, InstanceCount=4, Ghz=2.2, Cpu=4, Iops=200, RamMin=14, RamRec=18 },
        new() { MinUsers=1601, MaxUsers=3200, InstanceCount=8, Ghz=2.2, Cpu=4, Iops=200, RamMin=14, RamRec=18 },
        new() { MinUsers=3201, MaxUsers=6400, InstanceCount=16, Ghz=2.2, Cpu=4, Iops=200, RamMin=14, RamRec=18 },
        new() { MinUsers=6401, MaxUsers=12800, InstanceCount=32, Ghz=2.2, Cpu=4, Iops=200, RamMin=14, RamRec=18 },
    };

    public List<ProjectModule> Modules { get; set; } = new();

    public InfrastructureNode? DefaultK8sSql { get; set; }
    public InfrastructureNode? DefaultK8sMaster { get; set; }
    public InfrastructureNode? DefaultK8sWorker { get; set; }
    public InfrastructureNode? DefaultWindowsSql { get; set; }
    public InfrastructureNode? DefaultWindowsApp { get; set; }
    public InfrastructureNode? DefaultWindowsWeb { get; set; }
}

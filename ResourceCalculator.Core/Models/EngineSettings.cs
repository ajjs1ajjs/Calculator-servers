namespace ResourceCalculator.Models;

// Налаштування рушія розрахунку, редаговані через матрицю та збережені в matrix.json —
// щоб змінювати поведінку розрахунку без перекомпіляції/передеплою застосунку.
public class EngineSettings
{
    // Центральний SmartID (SSO) — ресурс на 1 репліку (на кожні 25 користувачів), один на систему.
    public double SmartIdCpuPerReplica { get; set; } = 0.2;
    public double SmartIdRamPerReplicaGb { get; set; } = 0.5;

    // HR Portal load-test sizing (cold peak + 25% headroom):
    // GraphQL 0.5 CPU / 0.5 GB, SmartID 1.25 CPU / 0.5 GB,
    // ROBOT 10.58 CPU / 2.13 GB per pod at the 1000-user scenario.
    public double HrPortalGraphqlCpuPerReplica { get; set; } = 0.5;
    public double HrPortalGraphqlRamPerReplicaGb { get; set; } = 0.5;
    public double HrPortalSmartIdCpuPerReplica { get; set; } = 1.25;
    public double HrPortalSmartIdRamPerReplicaGb { get; set; } = 0.5;
    public double HrPortalRobotCpuPerReplica { get; set; } = 10.58;
    public double HrPortalRobotRamPerReplicaGb { get; set; } = 2.13;

    // Профілі читання/запису дисків за документом D-AD-ADM-E:
    //  • сервер БД — 50r/50w; сервери додатків — 30r/70w; веб-сервери — 70r/30w;
    //  • вузли Kubernetes (master/worker) — 30r/70w.
    public string DbIopsProfile { get; set; } = "50r/50w";
    public string AppServerIopsProfile { get; set; } = "30r/70w";
    public string WebServerIopsProfile { get; set; } = "70r/30w";
    public string K8sIopsProfile { get; set; } = "30r/70w";

    // Worker-вузол: ресурси за замовчуванням, коли матриця не задає специфікації.
    public double DefaultWorkerCpu { get; set; } = 8;
    public double DefaultWorkerRamGb { get; set; } = 32;
    public int DefaultWorkerIops { get; set; } = 500;
    public double DefaultWorkerLatency { get; set; } = 5;

    // Master node/etcd — за офіційним etcd sizing guide: SSD, fsync latency < 10мс.
    public int EtcdIops { get; set; } = 1000;
    public string EtcdIopsProfile { get; set; } = "10r/90w";
    public int EtcdThroughputMiBs { get; set; } = 50;
    public double EtcdLatency { get; set; } = 10;

    // Середній розмір I/O-блока контейнерного навантаження worker-вузла (для оцінки MiB/s з IOPS).
    public int AvgBlockSizeKb { get; set; } = 16;

    // App/Web servers: MiB/s і латентність, коли матриця їх не задає.
    public int AppServerThroughputMiBs { get; set; } = 100;
    public double AppServerLatency { get; set; } = 10;
    public int WebServerThroughputMiBs { get; set; } = 80;
    public double WebServerLatency { get; set; } = 10;

    // Ліміти MS SQL Standard (128 ГБ ОЗП, 24 ядра) — понад це потрібна Enterprise.
    public double MsSqlStandardMaxRamGb { get; set; } = 128;
    public double MsSqlStandardMaxCores { get; set; } = 24;

    // Коефіцієнт файла підкачки Windows app/web сервера = RAM × n, округлено вгору до кратного 10.
    public double PageFileMultiplier { get; set; } = 4;
    public int PageFileRounding { get; set; } = 10;

    public EngineSettings Clone() => (EngineSettings)MemberwiseClone();
}

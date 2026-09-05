namespace ResourceCalculator.Models;

public class UserLoadRange
{
    public int MinUsers { get; set; }
    public int MaxUsers { get; set; }
    public double Cpu { get; set; }
    public double RamMin { get; set; }
    public double RamRec { get; set; }
    public int Iops { get; set; }
    public double Latency { get; set; }
    public int InstanceCount { get; set; }
    public double Ghz { get; set; }
    // Пропускна здатність диска, MiB/s (послідовні операції) — для вузла БД.
    public int ThroughputMiBs { get; set; }
    // Профіль навантаження диска (співвідношення читання/запису), напр. "50r/50w".
    public string IopsProfile { get; set; } = "50r/50w";

    public UserLoadRange Clone() => (UserLoadRange)MemberwiseClone();
}

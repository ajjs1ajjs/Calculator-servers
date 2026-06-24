namespace AIResourceCalculator.Models;

public class ServiceComponent
{
    public string Name { get; set; } = "";
    // Cpu/RamGb — СУМАРНІ ресурси компонента (на 1 репліку × кількість реплік).
    public double Cpu { get; set; }
    public double RamGb { get; set; }
    // Ресурси на ОДНУ репліку (щоб у звітах було видно, де сума, а де на под).
    public double CpuPerReplica { get; set; }
    public double RamPerReplicaGb { get; set; }
    public int Replicas { get; set; }
    public int FixedReplicas { get; set; }
    public int Instances { get; set; }
    public bool HasLocalSql { get; set; }
    public bool HasRedis { get; set; }
    public string Notes { get; set; } = "";
    public string Category { get; set; } = "";
    public ReplicaFormula Formula { get; set; } = ReplicaFormula.Fixed;
}

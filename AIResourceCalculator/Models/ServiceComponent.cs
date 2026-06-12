namespace AIResourceCalculator.Models;

public class ServiceComponent
{
    public string Name { get; set; } = "";
    public double Cpu { get; set; }
    public double RamGb { get; set; }
    public int Replicas { get; set; }
    public int Instances { get; set; }
    public bool HasLocalSql { get; set; }
    public bool HasRedis { get; set; }
    public string Notes { get; set; } = "";
    public string Category { get; set; } = "";
}

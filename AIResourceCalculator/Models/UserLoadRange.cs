namespace AIResourceCalculator.Models;

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
}

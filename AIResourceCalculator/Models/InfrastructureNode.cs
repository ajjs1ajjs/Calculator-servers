namespace AIResourceCalculator.Models;

public class InfrastructureNode
{
    public string Name { get; set; } = "";
    public string Os { get; set; } = "";
    public double Cpu { get; set; }
    public double RamGb { get; set; }
    public int NodeCount { get; set; }
    public string StorageType { get; set; } = "SSD";
    public int StorageGb { get; set; }
    public string StorageType2 { get; set; } = "";
    public int StorageGb2 { get; set; }
    public string StorageType3 { get; set; } = "";
    public int StorageGb3 { get; set; }
    public string StorageType4 { get; set; } = "";
    public int StorageGb4 { get; set; }
    public double MinVersion { get; set; }
    public int Iops { get; set; }
    public string IopsProfile { get; set; } = "";
    public double Latency { get; set; }
    public int PageFileGb { get; set; }
    public string PageFileType { get; set; } = "";
    public string Notes { get; set; } = "";
    public int TotalStorageGb => (StorageGb + StorageGb2 + StorageGb3 + StorageGb4) * NodeCount;
}

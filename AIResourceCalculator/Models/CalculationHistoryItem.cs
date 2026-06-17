namespace AIResourceCalculator.Models;

public class CalculationHistoryItem
{
    public DateTime Timestamp { get; set; }
    public ProjectConfig Config { get; set; } = new();
    public double TotalCpu { get; set; }
    public double TotalRamGb { get; set; }
    public double TotalStorageGb { get; set; }
    public double TotalIops { get; set; }
    public int TotalNodes { get; set; }

    public string DisplayText()
    {
        var ts = Timestamp.ToString("dd.MM HH:mm");
        return $"{Config.UserCount} users | {Config.DeploymentType} | {TotalCpu:F1} CPU / {TotalRamGb:F1} GB | {ts}";
    }
}

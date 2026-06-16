using System.IO;
using System.Text.Json;
using AIResourceCalculator.Models;

namespace AIResourceCalculator.Services;

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

public static class CalculationHistoryService
{
    private static readonly string HistoryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AIResourceCalculator", "history.json");
    private const int MaxHistory = 20;

    public static List<CalculationHistoryItem> LoadHistory()
    {
        try
        {
            if (File.Exists(HistoryPath))
            {
                var json = File.ReadAllText(HistoryPath);
                return JsonSerializer.Deserialize<List<CalculationHistoryItem>>(json) ?? new();
            }
        }
        catch { }
        return new();
    }

    public static void SaveToHistory(ProjectConfig config, ResourceRequirement req)
    {
        var history = LoadHistory();
        history.Insert(0, new CalculationHistoryItem
        {
            Timestamp = DateTime.Now,
            Config = config,
            TotalCpu = req.TotalCpu,
            TotalRamGb = req.TotalRamGb,
            TotalStorageGb = req.TotalStorageGb,
            TotalIops = req.TotalIops,
            TotalNodes = req.Infrastructure.Sum(n => n.NodeCount)
        });

        if (history.Count > MaxHistory) history = history.Take(MaxHistory).ToList();

        try
        {
            var dir = Path.GetDirectoryName(HistoryPath);
            if (dir != null && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(HistoryPath, JsonSerializer.Serialize(history, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}

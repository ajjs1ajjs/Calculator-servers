using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using AIResourceCalculator.Interfaces;
using AIResourceCalculator.Models;

namespace AIResourceCalculator.Services;

public class CalculationHistoryService : ICalculationHistoryService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    private static readonly JsonSerializerOptions WriteOptions = new(JsonOptions)
    {
        WriteIndented = true
    };

    private readonly string HistoryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AIResourceCalculator", "history.json");
    private const int MaxHistory = 20;

    public List<CalculationHistoryItem> LoadHistory()
    {
        try
        {
            if (File.Exists(HistoryPath))
            {
                var json = File.ReadAllText(HistoryPath);
                return JsonSerializer.Deserialize<List<CalculationHistoryItem>>(json, JsonOptions) ?? new();
            }
        }
        catch (Exception ex) { Debug.WriteLine($"CalculationHistoryService.LoadHistory failed: {ex.Message}"); }
        return new();
    }

    public void SaveToHistory(ProjectConfig config, ResourceRequirement req)
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
            File.WriteAllText(HistoryPath, JsonSerializer.Serialize(history, WriteOptions));
        }
        catch (Exception ex) { Debug.WriteLine($"CalculationHistoryService.SaveToHistory failed: {ex.Message}"); }
    }
}

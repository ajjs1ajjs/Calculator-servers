using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using ResourceCalculator.Interfaces;
using ResourceCalculator.Models;

namespace ResourceCalculator.Services;

public class CalculationHistoryService : ICalculationHistoryService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        MaxDepth = 8
    };

    private static readonly JsonSerializerOptions WriteOptions = new(JsonOptions)
    {
        WriteIndented = true
    };

    private readonly string HistoryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ResourceCalculator", "history.json");
    private const int MaxHistory = 20;
    // Ліміт файла історії: захист від OOM на навмисно роздутому history.json.
    private const long MaxHistoryBytes = 1L * 1024 * 1024;

    public List<CalculationHistoryItem> LoadHistory()
    {
        try
        {
            if (File.Exists(HistoryPath))
            {
                if (new FileInfo(HistoryPath).Length > MaxHistoryBytes)
                {
                    Debug.WriteLine("CalculationHistoryService.LoadHistory: file too large, ignoring");
                    return new();
                }
                var json = File.ReadAllText(HistoryPath);
                var items = JsonSerializer.Deserialize<List<CalculationHistoryItem>>(json, JsonOptions);
                if (items is null) return new();
                // Невалідні записи пропускаємо поштучно, а не весь файл.
                var valid = new List<CalculationHistoryItem>(items.Count);
                foreach (var item in items.Take(MaxHistory))
                {
                    if (IsValidItem(item)) valid.Add(item);
                }
                return valid;
            }
        }
        catch (Exception ex) { Debug.WriteLine($"CalculationHistoryService.LoadHistory failed: {ex.Message}"); }
        return new();
    }

    public void SaveToHistory(ProjectConfig config, ResourceRequirement req)
    {
        if (config is null || req?.Infrastructure is null) return;
        var history = LoadHistory();
        history.Insert(0, new CalculationHistoryItem
        {
            Timestamp = DateTime.UtcNow,
            Config = config,
            TotalCpu = FiniteOrZero(req.TotalCpu),
            TotalRamGb = FiniteOrZero(req.TotalRamGb),
            TotalStorageGb = FiniteOrZero(req.TotalStorageGb),
            TotalIops = FiniteOrZero(req.TotalIops),
            TotalNodes = req.Infrastructure.Where(n => n is not null).Sum(n => Math.Max(0, n.NodeCount))
        });

        if (history.Count > MaxHistory) history = history.Take(MaxHistory).ToList();

        try
        {
            var dir = Path.GetDirectoryName(HistoryPath);
            if (dir != null && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            // Атомарний запис (tmp+Move), як у DataService: крах посеред запису
            // не має лишати обірваний history.json.
            var tmp = HistoryPath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(history, WriteOptions));
            File.Move(tmp, HistoryPath, overwrite: true);
        }
        catch (Exception ex) { Debug.WriteLine($"CalculationHistoryService.SaveToHistory failed: {ex.Message}"); }
    }

    private static bool IsValidItem(CalculationHistoryItem? item)
    {
        if (item is null) return false;
        if (!double.IsFinite(item.TotalCpu) || !double.IsFinite(item.TotalRamGb)
            || !double.IsFinite(item.TotalStorageGb) || !double.IsFinite(item.TotalIops))
            return false;
        if (item.TotalNodes < 0 || item.TotalNodes > 1_000_000) return false;
        return true;
    }

    private static double FiniteOrZero(double v) => double.IsFinite(v) ? v : 0;
}

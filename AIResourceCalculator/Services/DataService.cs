using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using AIResourceCalculator.Data;
using AIResourceCalculator.Interfaces;

namespace AIResourceCalculator.Services;

public class DataService : IDataService
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

    private readonly string DataDir;
    private readonly string MatrixPath;

    public DataService()
    {
        DataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AIResourceCalculator", "data");
        MatrixPath = Path.Combine(DataDir, "matrix.json");
        Directory.CreateDirectory(DataDir);
    }

    public void SaveMatrix(SizingMatrix matrix)
    {
        try
        {
            var dir = Path.GetDirectoryName(MatrixPath);
            if (dir != null && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(matrix, WriteOptions);
            File.WriteAllText(MatrixPath, json);
        }
        catch (Exception ex) { Debug.WriteLine($"DataService.SaveMatrix failed: {ex.Message}"); }
    }

    public SizingMatrix LoadMatrix()
    {
        if (!File.Exists(MatrixPath))
            return new SizingMatrix();
        try
        {
            var json = File.ReadAllText(MatrixPath);

            // Явна перевірка ПРИСУТНОСТІ поля SchemaVersion: старі збереження без цього поля
            // через ініціалізатор властивості (= CurrentSchemaVersion) інакше виглядали б як
            // поточна версія і не відкидалися б, перебиваючи дефолти коду (звідси хибні MiB/s тощо).
            using (var probe = JsonDocument.Parse(json))
            {
                bool hasVersion = probe.RootElement.TryGetProperty(nameof(SizingMatrix.SchemaVersion), out var sv)
                    && sv.ValueKind == JsonValueKind.Number;
                if (!hasVersion || sv.GetInt32() < SizingMatrix.CurrentSchemaVersion)
                {
                    ClearMatrix();
                    return new SizingMatrix();
                }
            }

            var loaded = JsonSerializer.Deserialize<SizingMatrix>(json, JsonOptions);
            if (loaded == null || loaded.SchemaVersion < SizingMatrix.CurrentSchemaVersion)
            {
                ClearMatrix();
                return new SizingMatrix();
            }
            return loaded;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"DataService.LoadMatrix failed: {ex.Message}");
            return new SizingMatrix();
        }
    }

    public void ClearMatrix()
    {
        try
        {
            if (File.Exists(MatrixPath))
                File.Delete(MatrixPath);
        }
        catch (Exception ex) { Debug.WriteLine($"DataService.ClearMatrix failed: {ex.Message}"); }
    }
}

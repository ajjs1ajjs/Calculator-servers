using System.Diagnostics;
using System.IO;
using System.Text.Json;
using AIResourceCalculator.Data;
using AIResourceCalculator.Interfaces;

namespace AIResourceCalculator.Services;

public class DataService : IDataService
{
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
            var json = JsonSerializer.Serialize(matrix, new JsonSerializerOptions { WriteIndented = true });
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
            return JsonSerializer.Deserialize<SizingMatrix>(json) ?? new SizingMatrix();
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

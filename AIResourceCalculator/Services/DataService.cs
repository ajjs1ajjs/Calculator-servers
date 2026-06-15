using System.IO;
using System.Text.Json;
using AIResourceCalculator.Data;

namespace AIResourceCalculator.Services;

public static class DataService
{
    private static readonly string DataDir;
    private static readonly string MatrixPath;

    static DataService()
    {
        DataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AIResourceCalculator", "data");
        MatrixPath = Path.Combine(DataDir, "matrix.json");
        Directory.CreateDirectory(DataDir);
    }

    public static void SaveMatrix(SizingMatrix matrix)
    {
        var json = JsonSerializer.Serialize(matrix, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(MatrixPath, json);
    }

    public static SizingMatrix LoadMatrix()
    {
        if (!File.Exists(MatrixPath))
            return new SizingMatrix();
        try
        {
            var json = File.ReadAllText(MatrixPath);
            return JsonSerializer.Deserialize<SizingMatrix>(json) ?? new SizingMatrix();
        }
        catch
        {
            return new SizingMatrix();
        }
    }

    public static void ClearMatrix()
    {
        if (File.Exists(MatrixPath))
            File.Delete(MatrixPath);
    }
}

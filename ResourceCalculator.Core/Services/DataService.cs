using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using ResourceCalculator.Data;
using ResourceCalculator.Interfaces;

namespace ResourceCalculator.Services;

public class DataService : IDataService
{
    // Ліміт розміру файла: захист від OOM на навмисно роздутому matrix.json.
    private const long MaxMatrixBytes = 5L * 1024 * 1024;
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
            "ResourceCalculator", "data");
        MatrixPath = Path.Combine(DataDir, "matrix.json");
        Directory.CreateDirectory(DataDir);
    }

    // Конструктор з явним шляхом — для тестів (щоб не чіпати реальний профіль користувача).
    public DataService(string dataDir)
    {
        DataDir = dataDir;
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
            var tmp = MatrixPath + ".tmp";
            File.WriteAllText(tmp, json);
            if (File.Exists(MatrixPath))
            {
                var backup = MatrixPath + ".bak";
                File.Copy(MatrixPath, backup, overwrite: true);
            }
            File.Move(tmp, MatrixPath, overwrite: true);
        }
        catch (Exception ex) { Debug.WriteLine($"DataService.SaveMatrix failed: {ex.Message}"); }
    }

    public SizingMatrix LoadMatrix()
    {
        if (!File.Exists(MatrixPath))
            return new SizingMatrix();
        try
        {
            var fileInfo = new FileInfo(MatrixPath);
            if (fileInfo.Length > MaxMatrixBytes)
            {
                PreserveCorrupt("перевищено ліміт розміру");
                return new SizingMatrix();
            }
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
            // Значення з файла не довіряємо: від'ємні CPU, NaN, нескінченності й абсурдні
            // порядки ламали б розрахунок і звірку. Невалідне — в карантин, не видалення.
            var errors = MatrixValidator.Validate(loaded);
            if (errors.Count > 0)
            {
                PreserveCorrupt($"невалідні значення: {string.Join("; ", errors.Take(3))}");
                return new SizingMatrix();
            }
            return loaded;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"DataService.LoadMatrix failed: {ex.Message}");
            PreserveCorrupt("помилка читання");
            return new SizingMatrix();
        }
    }

    // Битий/невалідний файл зберігаємо з міткою часу замість тихого видалення —
    // інакше підміна або збій диска маскувалися б під «чисті дефолти».
    private void PreserveCorrupt(string reason)
    {
        try
        {
            if (!File.Exists(MatrixPath)) return;
            var stamped = MatrixPath + $".corrupt.{DateTime.Now:yyyyMMdd-HHmmss}";
            File.Move(MatrixPath, stamped);
            Debug.WriteLine($"DataService: corrupt matrix preserved as {stamped} ({reason})");
        }
        catch (Exception ex) { Debug.WriteLine($"DataService.PreserveCorrupt failed: {ex.Message}"); }
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

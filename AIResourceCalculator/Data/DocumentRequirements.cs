using AIResourceCalculator.Models;

namespace AIResourceCalculator.Data;

// Канонічні (незмінні) вимоги до сервера БД із документа D-AD-ADM-E «Вимоги до технічного та
// системного програмного забезпечення». Слугують ЕТАЛОНОМ для звірки розрахунку — на відміну
// від матриці, яку користувач може редагувати. Значення взято з таблиці «Вимоги до сервера БД»
// (за кількістю конкурентних ліцензій).
public class DocRequirementRow
{
    public int MinUsers { get; init; }
    public int MaxUsers { get; init; }
    public double CpuCores { get; init; }
    public double RamMinGb { get; init; }
    public double RamRecGb { get; init; }
    public int Iops { get; init; }
    public double LatencyMs { get; init; }
}

// Один рядок порівняння «розрахунок vs документ».
public class DocComparisonItem
{
    public string Metric { get; set; } = "";
    public string Document { get; set; } = "";
    public string Calculated { get; set; } = "";
    public string Status { get; set; } = "";
}

public static class DocumentRequirements
{
    public const string Source = "D-AD-ADM-E «Вимоги до технічного та системного програмного забезпечення»";

    // Таблиця сервера БД (MS SQL Server) за кількістю конкурентних ліцензій.
    public static readonly List<DocRequirementRow> SqlServer = new()
    {
        new() { MinUsers = 1,    MaxUsers = 10,   CpuCores = 2,  RamMinGb = 4,   RamRecGb = 8,    Iops = 200,   LatencyMs = 10 },
        new() { MinUsers = 11,   MaxUsers = 25,   CpuCores = 4,  RamMinGb = 8,   RamRecGb = 12,   Iops = 250,   LatencyMs = 10 },
        new() { MinUsers = 26,   MaxUsers = 50,   CpuCores = 6,  RamMinGb = 16,  RamRecGb = 24,   Iops = 300,   LatencyMs = 8 },
        new() { MinUsers = 51,   MaxUsers = 100,  CpuCores = 8,  RamMinGb = 32,  RamRecGb = 48,   Iops = 500,   LatencyMs = 5 },
        new() { MinUsers = 101,  MaxUsers = 200,  CpuCores = 10, RamMinGb = 64,  RamRecGb = 96,   Iops = 800,   LatencyMs = 3 },
        new() { MinUsers = 201,  MaxUsers = 350,  CpuCores = 12, RamMinGb = 112, RamRecGb = 168,  Iops = 1400,  LatencyMs = 2 },
        new() { MinUsers = 351,  MaxUsers = 500,  CpuCores = 16, RamMinGb = 168, RamRecGb = 240,  Iops = 2000,  LatencyMs = 2 },
        new() { MinUsers = 501,  MaxUsers = 1000, CpuCores = 20, RamMinGb = 240, RamRecGb = 384,  Iops = 4000,  LatencyMs = 0.6 },
        new() { MinUsers = 1001, MaxUsers = 2000, CpuCores = 22, RamMinGb = 384, RamRecGb = 576,  Iops = 12000, LatencyMs = 0.2 },
        new() { MinUsers = 2001, MaxUsers = 3000, CpuCores = 24, RamMinGb = 576, RamRecGb = 768,  Iops = 24000, LatencyMs = 0.1 },
        new() { MinUsers = 3001, MaxUsers = 4000, CpuCores = 28, RamMinGb = 768, RamRecGb = 960,  Iops = 36000, LatencyMs = 0.1 },
        new() { MinUsers = 4001, MaxUsers = 5000, CpuCores = 32, RamMinGb = 960, RamRecGb = 1152, Iops = 48000, LatencyMs = 0.1 },
    };

    public static DocRequirementRow? ForUsers(int users)
        => SqlServer.FirstOrDefault(r => users >= r.MinUsers && users <= r.MaxUsers)
           ?? SqlServer.LastOrDefault();

    // Звірка розрахованого вузла БД з еталоном документа. Лише для MS SQL Server
    // (таблиця документа описує саме його). Для інших СУБД повертає порожній список.
    public static List<DocComparisonItem> Compare(ResourceRequirement req, ProjectConfig config)
    {
        var items = new List<DocComparisonItem>();
        if (config.DatabaseType != DatabaseType.MsSql) return items;

        var doc = ForUsers(config.UserCount);
        if (doc == null) return items;

        var db = req.Infrastructure.FirstOrDefault(n =>
            n.Name.Contains("SQL", StringComparison.OrdinalIgnoreCase));
        if (db == null) return items;

        items.Add(Make("CPU, ядер", doc.CpuCores, db.Cpu, higherIsOk: true));
        items.Add(Make("RAM, ГБ (рек.)", doc.RamRecGb, db.RamGb, higherIsOk: true));
        items.Add(Make("IOPS", doc.Iops, db.Iops, higherIsOk: true));
        items.Add(MakeLatency(doc.LatencyMs, db.Latency));
        return items;
    }

    private static DocComparisonItem Make(string metric, double docVal, double calcVal, bool higherIsOk)
    {
        string status = higherIsOk
            ? (calcVal + 1e-6 >= docVal ? "Відповідає" : "Нижче вимог")
            : (calcVal - 1e-6 <= docVal ? "Відповідає" : "Вище вимог");
        return new DocComparisonItem
        {
            Metric = metric,
            Document = $"≥ {Trim(docVal)}",
            Calculated = Trim(calcVal),
            Status = status
        };
    }

    private static DocComparisonItem MakeLatency(double docVal, double calcVal)
        => new()
        {
            Metric = "Затримка, мс",
            Document = $"≤ {Trim(docVal)}",
            Calculated = Trim(calcVal),
            Status = calcVal <= docVal + 1e-6 ? "Відповідає" : "Вище вимог"
        };

    private static string Trim(double v) => v % 1 == 0 ? ((int)v).ToString() : v.ToString("0.#");
}

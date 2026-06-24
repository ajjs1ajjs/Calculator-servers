using AIResourceCalculator.Models;

namespace AIResourceCalculator.Services;

// Виводить похідні середовища (TEST/PredProd) з порахованого PROD та рахує бекап-резерв.
// DEV рахується рушієм окремо (менша к-сть ліцензій) — тут лише додається бекап-резерв.
public static class EnvironmentScaler
{
    private static bool IsDb(InfrastructureNode n) =>
        n.Name.Contains("SQL", StringComparison.OrdinalIgnoreCase)
        || n.Name.Contains("PostgreSQL", StringComparison.OrdinalIgnoreCase)
        || n.Name.Contains("Oracle", StringComparison.OrdinalIgnoreCase);

    // Резерв під вивантаження бекапу: retention днів × (1 − стиснення) × ОБСЯГ ДАНИХ БД.
    // База рахується від РЕЛЯЦІЙНИХ даних (а не від усього виділеного диска) — інакше резерв
    // безпідставно роздувається (терабайти під бекап бази в 10-20 ГБ).
    public static int BackupReserveGb(int dbDataGb, EnvironmentSettings s)
    {
        dbDataGb = Math.Max(0, dbDataGb);
        if (dbDataGb <= 0) return 0;
        var compression = Math.Clamp(s.BackupCompression, 0, 0.95);
        var perBackup = dbDataGb * (1.0 - compression);
        return (int)Math.Ceiling(perBackup * Math.Max(1, s.BackupRetentionDays));
    }

    // TEST/PredProd: масштабуємо ПОТУЖНІСТЬ (ноди/ВМ/ядра/пам'ять), але НЕ диск (диск ≥ PROD).
    // Бекап-резерв додається окремо (однаково для всіх середовищ) у MainViewModel.
    public static ResourceRequirement ScaleFromProd(ResourceRequirement prod, double powerFactor)
    {
        powerFactor = Math.Clamp(powerFactor, 0.1, 1.0);
        var req = new ResourceRequirement
        {
            UserCount = prod.UserCount,
            DeploymentType = prod.DeploymentType,
            LoadProfile = prod.LoadProfile
        };

        foreach (var n in prod.Infrastructure)
        {
            var c = n.Clone();
            if (n.NodeCount > 1)
                // Горизонтально масштабовані (worker/app/web) — менше ВМ, специфікація вузла без змін.
                c.NodeCount = Math.Max(1, (int)Math.Round(n.NodeCount * powerFactor));
            else
            {
                // Поодинокі вузли (master/БД) — менше ядер/пам'яті, але з розумним мінімумом.
                c.Cpu = Math.Max(2, Math.Round(n.Cpu * powerFactor));
                c.RamGb = Math.Max(4, Math.Round(n.RamGb * powerFactor));
            }
            // Диски лишаємо як у PROD (ніколи не менше).
            req.Infrastructure.Add(c);
        }

        // Лічильники ролей. Master беремо з PROD: масштабування потужності не змінює к-сть
        // керуючих вузлів. Worker рахуємо за ЗАЛИШКОВИМ принципом (усі ноди, крім БД, master і
        // GPU), бо app/web-вузли Windows названі українською («Сервери додатків») — пошук
        // підрядка "Worker" у назві давав 0. Назви master/GPU задає рушій англійською, тож
        // зіставлення з ними надійне.
        req.MasterNodeCount = prod.MasterNodeCount;
        req.WorkerNodeCount = req.Infrastructure
            .Where(n => !IsDb(n)
                && !n.Name.Contains("Master", StringComparison.OrdinalIgnoreCase)
                && !n.Name.Contains("GPU", StringComparison.OrdinalIgnoreCase))
            .Sum(n => n.NodeCount);
        req.PodCpu = Math.Round(prod.PodCpu * powerFactor, 1);
        req.PodRamGb = Math.Round(prod.PodRamGb * powerFactor, 1);
        Recalculate(req);
        // IOPS не сумуються — визначальним лишається вузол БД (як у PROD).
        req.TotalIops = prod.TotalIops;
        req.TotalLatency = prod.TotalLatency;
        return req;
    }

    // Додає окремий диск під бекап на вузол БД. Застосовується до КОЖНОГО середовища (вкл. PROD).
    public static void AddBackupReserve(ResourceRequirement env, int backupReserveGb)
    {
        ApplyBackupReserve(env, backupReserveGb);
        Recalculate(env);
    }

    private static void ApplyBackupReserve(ResourceRequirement req, int backupReserveGb)
    {
        if (backupReserveGb <= 0) return;
        var db = req.Infrastructure.FirstOrDefault(IsDb);
        if (db == null) return;
        db.StorageGb4 += backupReserveGb;
        if (string.IsNullOrWhiteSpace(db.StorageType4)) db.StorageType4 = "SATA";
        // У Windows-розгортанні бекап ширший (БД + веб-папка + клієнт + мережева); у Linux — лише БД.
        var scope = req.DeploymentType == DeploymentType.Kubernetes
            ? "лише БД"
            : "БД + веб/клієнт/мережева";
        var note = $"+{backupReserveGb} ГБ резерв під бекап ({scope})";
        db.Notes = string.IsNullOrWhiteSpace(db.Notes) ? note : $"{db.Notes}; {note}";
    }

    private static void Recalculate(ResourceRequirement req)
    {
        req.TotalCpu = req.Infrastructure.Sum(n => n.Cpu * n.NodeCount);
        req.TotalRamGb = req.Infrastructure.Sum(n => n.RamGb * n.NodeCount);
        req.TotalStorageGb = req.Infrastructure.Sum(n => n.TotalStorageGb);
    }
}

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

    // Резерв під вивантаження бекапу з ПРОДу: днів × (1 − стиснення) × обсяг дисків БД ПРОДу.
    public static int BackupReserveGb(ResourceRequirement prod, EnvironmentSettings s)
    {
        var dbDataGb = prod.Infrastructure.Where(IsDb).Sum(n => n.DiskPerNodeGb * Math.Max(1, n.NodeCount));
        if (dbDataGb <= 0) return 0;
        var compression = Math.Clamp(s.BackupCompression, 0, 0.95);
        var perBackup = dbDataGb * (1.0 - compression);
        return (int)Math.Ceiling(perBackup * Math.Max(1, s.BackupRetentionDays));
    }

    // TEST/PredProd: масштабуємо ПОТУЖНІСТЬ (ноди/ВМ/ядра/пам'ять), але НЕ диск
    // (диск ≥ PROD), і додаємо бекап-резерв окремим диском на вузлі БД.
    public static ResourceRequirement ScaleFromProd(
        ResourceRequirement prod, double powerFactor, int backupReserveGb)
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
            c.Iops = (int)Math.Round(n.Iops * powerFactor);
            // Диски лишаємо як у PROD (ніколи не менше).
            req.Infrastructure.Add(c);
        }

        ApplyBackupReserve(req, backupReserveGb);

        req.WorkerNodeCount = req.Infrastructure.Where(n => n.Name.Contains("Worker", StringComparison.OrdinalIgnoreCase)).Sum(n => n.NodeCount);
        req.MasterNodeCount = req.Infrastructure.Where(n => n.Name.Contains("Master", StringComparison.OrdinalIgnoreCase)).Sum(n => n.NodeCount);
        req.PodCpu = Math.Round(prod.PodCpu * powerFactor, 1);
        req.PodRamGb = Math.Round(prod.PodRamGb * powerFactor, 1);
        Recalculate(req);
        req.TotalIops = req.Infrastructure.Sum(n => n.Iops);
        req.TotalLatency = prod.TotalLatency;
        return req;
    }

    // DEV рахується рушієм на меншій к-сті ліцензій; додаємо лише бекап-резерв з ПРОДу.
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
        var note = $"+{backupReserveGb} ГБ резерв під бекап ПРОДу";
        db.Notes = string.IsNullOrWhiteSpace(db.Notes) ? note : $"{db.Notes}; {note}";
    }

    private static void Recalculate(ResourceRequirement req)
    {
        req.TotalCpu = req.Infrastructure.Sum(n => n.Cpu * n.NodeCount);
        req.TotalRamGb = req.Infrastructure.Sum(n => n.RamGb * n.NodeCount);
        req.TotalStorageGb = req.Infrastructure.Sum(n => n.TotalStorageGb);
    }
}

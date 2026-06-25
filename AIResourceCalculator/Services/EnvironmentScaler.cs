using AIResourceCalculator.Models;

namespace AIResourceCalculator.Services;

// Рахує резерв під бекап і додає його до вузла БД середовища.
// Кожне середовище (PROD/DEV/TEST/PreProd) рахується рушієм окремо (див. MainViewModel);
// масштабування PROD більше не застосовується.
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

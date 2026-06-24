namespace AIResourceCalculator.Models;

// Налаштування похідних середовищ. PROD — база; решта виводяться з нього.
//  • DEV  — окрема (менша) кількість ліцензій, рахується рушієм заново;
//  • TEST — творчо зменшений PROD (TestScaleFactor потужності), але диск ≥ PROD + бекап-резерв;
//  • PredProd — як TEST, але × PredProdMultiplier (на 20% потужніший за замовчуванням).
public class EnvironmentSettings
{
    public bool IncludeDev { get; set; }
    public bool IncludeTest { get; set; }
    public bool IncludePredProd { get; set; }

    public int DevUserCount { get; set; } = 10;

    // Частка потужності TEST відносно PROD (0.5 = 50%).
    public double TestScaleFactor { get; set; } = 0.5;
    // Множник PredProd відносно TEST (1.2 = +20%).
    public double PredProdMultiplier { get; set; } = 1.2;

    // Бекап-резерв на не-prod середовищах: retention днів × (1 − стиснення) × обсяг даних БД ПРОДу.
    public int BackupRetentionDays { get; set; } = 7;
    // Частка стиснення бекапу (0.5 = стиснення на 50%, тобто бекап = 50% від БД).
    public double BackupCompression { get; set; } = 0.5;

    public bool AnyDerived => IncludeDev || IncludeTest || IncludePredProd;
}

// Один порахований звіт середовища (PROD/DEV/TEST/PredProd) + його людська назва.
public class EnvironmentReport
{
    public DeployEnvironment Environment { get; set; }
    public string Name { get; set; } = "";
    public int UserCount { get; set; }
    public ResourceRequirement Requirement { get; set; } = new();

    // Плоскі властивості для прив'язки в порівняльній таблиці.
    public double Cpu => Math.Round(Requirement.TotalCpu, 1);
    public double RamGb => Math.Round(Requirement.TotalRamGb, 1);
    public int StorageGb => Requirement.TotalStorageGb;
    public int Iops => Requirement.TotalIops;
    public int Nodes => Requirement.Infrastructure.Sum(n => n.NodeCount);
}

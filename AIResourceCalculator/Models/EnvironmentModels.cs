namespace AIResourceCalculator.Models;

// Налаштування похідних середовищ. PROD — база; кожне додаткове середовище рахується
// рушієм ОКРЕМО за власною кількістю користувачів (як у Excel-табличці), а не масштабуванням
// PROD. Це дає правильні мінімальні DEV/TEST та незалежний контроль PreProd.
public class EnvironmentSettings
{
    public bool IncludeDev { get; set; }
    public bool IncludeTest { get; set; }
    public bool IncludePredProd { get; set; }

    // Власна кількість користувачів (ліцензій) кожного похідного середовища.
    public int DevUserCount { get; set; } = 10;
    public int TestUserCount { get; set; } = 25;
    public int PredProdUserCount { get; set; } = 50;

    public bool AnyDerived => IncludeDev || IncludeTest || IncludePredProd;
}

// Рядок таблиці «к-сть користувачів модуля по середовищах»: для одного модуля (LMS/HR/ForceBPM)
// окремі к-сті користувачів у DEV/TEST/PreProd. PROD бере к-сть із полів модуля на вкладці.
public class EnvModuleCount
{
    public string ModuleName { get; set; } = "";
    public int DevUsers { get; set; }
    public int TestUsers { get; set; }
    public int PredProdUsers { get; set; }

    // Увімкнення модуля ОКРЕМО для кожного середовища (незалежно від PROD).
    public bool DevEnabled { get; set; } = true;
    public bool TestEnabled { get; set; } = true;
    public bool PredProdEnabled { get; set; } = true;

    public int CountFor(DeployEnvironment env) => env switch
    {
        DeployEnvironment.Dev => DevUsers,
        DeployEnvironment.Test => TestUsers,
        DeployEnvironment.PredProd => PredProdUsers,
        _ => 0
    };

    public bool EnabledFor(DeployEnvironment env) => env switch
    {
        DeployEnvironment.Dev => DevEnabled,
        DeployEnvironment.Test => TestEnabled,
        DeployEnvironment.PredProd => PredProdEnabled,
        _ => true
    };
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

    // Перелік ВМ середовища (для розбивки у звіті/UI).
    public IEnumerable<InfrastructureNode> Vms => Requirement.Infrastructure.Where(n => n.NodeCount > 0);

    // Компоненти (поди) середовища — для окремої розбивки DEV/TEST/PreProd у звіті/UI.
    public IEnumerable<ServiceComponent> Components => Requirement.Components.Where(c => c.Cpu > 0);
    public bool HasComponents => Components.Any();
    // Підсумок ресурсів подів середовища.
    public double ComponentsCpu => Math.Round(Components.Sum(c => c.Cpu), 1);
    public double ComponentsRamGb => Math.Round(Components.Sum(c => c.RamGb), 1);
}

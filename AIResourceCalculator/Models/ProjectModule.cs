namespace AIResourceCalculator.Models;

public enum ReplicaFormula
{
    Fixed,              // фіксована кількість реплік
    Per25Users,         // Ceiling(користувачі / 25)
    Per100Users,        // Ceiling(користувачі / 100)
    Per50Users,         // Ceiling(користувачі / 50)
    Per100Plus1000,     // 1 + Int(користувачі/100) + Int(користувачі/1000)
    Per50Plus500,       // 1 + Int(користувачі/50) + Int(користувачі/500)
    OnePlusPer100       // 1 + Int(користувачі/100)
}

// Єдине джерело правди для розрахунку кількості реплік за формулою.
public static class ReplicaMath
{
    public static int Resolve(ReplicaFormula formula, int fixedReplicas, int userCount)
    {
        if (userCount < 0) userCount = 0;
        return formula switch
        {
            ReplicaFormula.Fixed => fixedReplicas,
            ReplicaFormula.Per25Users => (int)Math.Ceiling(userCount / 25.0),
            ReplicaFormula.Per100Users => (int)Math.Ceiling(userCount / 100.0),
            ReplicaFormula.Per50Users => (int)Math.Ceiling(userCount / 50.0),
            ReplicaFormula.Per100Plus1000 => 1 + (int)(userCount / 100.0) + (int)(userCount / 1000.0),
            ReplicaFormula.Per50Plus500 => 1 + (int)(userCount / 50.0) + (int)(userCount / 500.0),
            ReplicaFormula.OnePlusPer100 => 1 + (int)(userCount / 100.0),
            _ => Math.Max(1, fixedReplicas)
        };
    }
}

public class ModuleComponent
{
    public string Name { get; set; } = "";
    public double Cpu { get; set; }
    public double RamGb { get; set; }
    public double PerfCpu { get; set; }
    public double PerfRamGb { get; set; }
    public int FixedReplicas { get; set; }
    public ReplicaFormula Formula { get; set; } = ReplicaFormula.Fixed;
    public bool HasLocalSql { get; set; }
    public bool HasRedis { get; set; }
    public string Notes { get; set; } = "";

    public ModuleComponent Clone() => new()
    {
        Name = Name, Cpu = Cpu, RamGb = RamGb, PerfCpu = PerfCpu, PerfRamGb = PerfRamGb,
        FixedReplicas = FixedReplicas, Formula = Formula, HasLocalSql = HasLocalSql,
        HasRedis = HasRedis, Notes = Notes
    };
}

public class ProjectModule
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public bool IsEnabled { get; set; } = true;
    public bool IsKubernetesOnly { get; set; }
    // Обов'язковий сервіс (App Server / ROBOT / Web) — завжди ввімкнений, не виноситься у вибір.
    public bool IsMandatory { get; set; }
    public List<ModuleComponent> Components { get; set; } = new();

    public ProjectModule Clone() => new()
    {
        Name = Name, Description = Description, IsEnabled = IsEnabled,
        IsKubernetesOnly = IsKubernetesOnly, IsMandatory = IsMandatory,
        Components = Components.Select(c => c.Clone()).ToList()
    };

    public (double cpu, double ram) CalculateReplicas(int userCount, LoadProfile profile = LoadProfile.Basic)
    {
        if (userCount < 0) userCount = 0;
        double totalCpu = 0, totalRam = 0;

        foreach (var comp in Components)
        {
            int replicas = ReplicaMath.Resolve(comp.Formula, comp.FixedReplicas, userCount);
            if (replicas == 0) replicas = 1;

            var cpu = profile == LoadProfile.Performance && comp.PerfCpu > 0 ? comp.PerfCpu : comp.Cpu;
            var ram = profile == LoadProfile.Performance && comp.PerfRamGb > 0 ? comp.PerfRamGb : comp.RamGb;
            totalCpu += cpu * replicas;
            totalRam += ram * replicas;
        }

        return (totalCpu, totalRam);
    }
}

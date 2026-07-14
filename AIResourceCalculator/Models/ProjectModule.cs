using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AIResourceCalculator.Models;

public enum ReplicaFormula
{
    Fixed,              // фіксована кількість реплік
    Per25Users,         // Ceiling(користувачі / 25)
    Per100Users,        // Ceiling(користувачі / 100)
    Per50Users,         // Ceiling(користувачі / 50)
    Per100Plus1000,     // 1 + Int(користувачі/100) + Int(користувачі/1000)
    Per50Plus500,       // 1 + Int(користувачі/50) + Int(користувачі/500)
    OnePlusPer100,      // 1 + Int(користувачі/100)
    Per1000Users,       // Ceiling(користувачі / 1000)
    LmsGraphqlLoadTest  // Табличні точки з навантажувального тесту LMS-GraphQL (LMS_LT_results.pdf)
}

// Єдине джерело правди для розрахунку кількості реплік за формулою.
// auxUsers — допоміжна к-сть користувачів для формул із перехресним зв'язком (за Excel
// ROBOT і WS масштабуються ще й від к-сті користувачів HR Portal): у Per100Plus1000 та
// Per50Plus500 «великі» доданки (/1000, /500) рахуються від auxUsers. Якщо auxUsers < 0 —
// використовується userCount (зворотна сумісність).
public static class ReplicaMath
{
    public static int Resolve(ReplicaFormula formula, int fixedReplicas, int userCount, int auxUsers = -1)
    {
        if (userCount < 0) userCount = 0;
        if (auxUsers < 0) auxUsers = userCount;
        return formula switch
        {
            ReplicaFormula.Fixed => fixedReplicas,
            ReplicaFormula.Per25Users => (int)Math.Ceiling(userCount / 25.0),
            ReplicaFormula.Per100Users => (int)Math.Ceiling(userCount / 100.0),
            ReplicaFormula.Per50Users => (int)Math.Ceiling(userCount / 50.0),
            // ROBOT: 1 + int(ліцензій/100) + int(HR/1000)
            ReplicaFormula.Per100Plus1000 => 1 + (int)(userCount / 100.0) + (int)(auxUsers / 1000.0),
            // WS: 1 + int(ліцензій/50) + int(HR/500)
            ReplicaFormula.Per50Plus500 => 1 + (int)(userCount / 50.0) + (int)(auxUsers / 500.0),
            ReplicaFormula.OnePlusPer100 => 1 + (int)(userCount / 100.0),
            // HR-GraphQL: легке навантаження на сесію — 1 репліка на кожні 1000 користувачів
            // (не на 100, як типові поди), бо HR Portal — самообслуговуючий портал з рідкісними
            // короткими сесіями, а не постійним активним навантаженням.
            ReplicaFormula.Per1000Users => (int)Math.Ceiling(userCount / 1000.0),
            ReplicaFormula.LmsGraphqlLoadTest => LmsGraphqlReplicas(userCount),
            _ => Math.Max(1, fixedReplicas)
        };
    }

    // Реальні точки з навантажувального тесту LMS-GraphQL (LMS_LT_results.pdf): 50→1, 100→2,
    // 150→3, 200→5, 250→7 репліки. Не лягає на жодну з наявних формул (Per25Users/Per50Users
    // тощо давали б удвічі більше реплік, ніж реально знадобилось), тож — таблиця точних значень
    // з екстраполяцією за межами 250 користувачів тим самим темпом, що й останній крок тесту
    // (200→250: +2 репліки на кожні 50 користувачів).
    private static readonly (int Users, int Replicas)[] LmsGraphqlBreakpoints =
    {
        (50, 1), (100, 2), (150, 3), (200, 5), (250, 7)
    };

    private static int LmsGraphqlReplicas(int userCount)
    {
        foreach (var (users, replicas) in LmsGraphqlBreakpoints)
            if (userCount <= users) return replicas;

        var last = LmsGraphqlBreakpoints[^1];
        var extra = (int)Math.Ceiling((userCount - last.Users) / 50.0) * 2;
        return last.Replicas + extra;
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

public class ProjectModule : INotifyPropertyChanged
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    // Сповіщає про зміну (UI прив'язує чекбокс), щоб ViewModel реагувала — напр. ввімкнення
    // Kubernetes-only модуля (ForceBPM) блокує вибір Windows-розгортання.
    private bool _isEnabled = true;
    public bool IsEnabled
    {
        get => _isEnabled;
        set { if (_isEnabled == value) return; _isEnabled = value; OnPropertyChanged(); }
    }
    public bool IsKubernetesOnly { get; set; }
    // Обов'язковий сервіс (App Server / ROBOT / Web) — завжди ввімкнений, не виноситься у вибір.
    public bool IsMandatory { get; set; }
    // Чи має модуль власну к-сть користувачів (LMS/HR Portal — так; ForceBPM — ні, він масштабується
    // від загальної к-сті за формулами). Керує показом поля к-сті в UI.
    public bool HasOwnUserCount { get; set; } = true;
    // За замовчуванням Kubernetes-only модулі (ForceBPM) керуються типом розгортання автоматично,
    // тож їхня галочка заблокована. У Гібриді MainViewModel знімає це обмеження (AllowManualToggle),
    // бо там ForceBPM — не обов'язковий компонент і користувач може свідомо його прибрати.
    public bool AllowManualToggle { get; set; }
    public bool IsUserToggleable => !IsKubernetesOnly || AllowManualToggle;
    // Окрема кількість користувачів для цього модуля (напр., LMS/HR Portal використовує не вся
    // компанія). 0 = брати загальну кількість користувачів проєкту. Понад загальну не піднімається.
    public int UserCount { get; set; }
    public List<ModuleComponent> Components { get; set; } = new();

    // Ефективна кількість користувачів модуля: власна (якщо задана), інакше загальна.
    // cap=true (для похідних середовищ) обмежує власну к-сть загальною кількістю середовища;
    // cap=false (PROD, як у Excel) — модуль масштабується за власною к-стю незалежно (напр.
    // LMS 7500 при 50 ліцензіях головної системи).
    public int EffectiveUsers(int projectUsers, bool cap = false)
        => UserCount > 0 ? (cap ? Math.Min(UserCount, projectUsers) : UserCount) : projectUsers;

    public ProjectModule Clone() => new()
    {
        Name = Name, Description = Description, IsEnabled = IsEnabled,
        IsKubernetesOnly = IsKubernetesOnly, IsMandatory = IsMandatory, UserCount = UserCount,
        HasOwnUserCount = HasOwnUserCount,
        Components = Components.Select(c => c.Clone()).ToList()
    };

    public (double cpu, double ram) CalculateReplicas(int userCount, LoadProfile profile = LoadProfile.Basic, int auxUsers = -1)
    {
        if (userCount < 0) userCount = 0;
        double totalCpu = 0, totalRam = 0;

        foreach (var comp in Components)
        {
            int replicas = ReplicaMath.Resolve(comp.Formula, comp.FixedReplicas, userCount, auxUsers);
            if (replicas == 0) replicas = 1;

            var cpu = profile == LoadProfile.Performance && comp.PerfCpu > 0 ? comp.PerfCpu : comp.Cpu;
            var ram = profile == LoadProfile.Performance && comp.PerfRamGb > 0 ? comp.PerfRamGb : comp.RamGb;
            totalCpu += cpu * replicas;
            totalRam += ram * replicas;
        }

        return (totalCpu, totalRam);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

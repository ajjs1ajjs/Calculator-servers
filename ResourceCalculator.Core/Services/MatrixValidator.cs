using ResourceCalculator.Data;
using ResourceCalculator.Models;

namespace ResourceCalculator.Services;

// Пост-завантажувальна валідація матриці: matrix.json пишуть люди/інструменти,
// тож після десеріалізації перевіряємо форму, а не лише SchemaVersion.
// Повертає людиночитані помилки; порожній список = матриця придатна.
public static class MatrixValidator
{
    private const int MaxRanges = 1000;
    private const int MaxModules = 500;
    private const int MaxComponentsPerModule = 1000;
    private const int MaxUsers = 100_000_000;
    private const double MaxCpu = 1_000_000;
    private const double MaxRamGb = 10_000_000;
    private const int MaxIops = 1_000_000_000;
    private const int MaxNodeCount = 100_000;
    private const int MaxTextLength = 500;

    public static List<string> Validate(SizingMatrix m)
    {
        var errors = new List<string>();
        if (m is null) { errors.Add("Матриця відсутня."); return errors; }

        ValidateRanges(errors, "MS SQL", m.MsSqlRanges);
        ValidateRanges(errors, "App Server", m.AppServerRanges);
        ValidateRanges(errors, "Web Server", m.WebServerRanges);
        ValidateRanges(errors, "PostgreSQL", m.PostgresRanges);
        ValidateRanges(errors, "Oracle", m.OracleRanges);

        ValidateModules(errors, m.Modules);
        ValidateModules(errors, m.DocumentFlowModules);

        ValidateNode(errors, "K8s SQL", m.DefaultK8sSql, NodeSlot.K8sSql);
        ValidateNode(errors, "K8s Master", m.DefaultK8sMaster, NodeSlot.K8sMaster);
        ValidateNode(errors, "K8s Worker", m.DefaultK8sWorker, NodeSlot.K8sWorker);
        ValidateNode(errors, "Windows SQL", m.DefaultWindowsSql, NodeSlot.WindowsSql);
        ValidateNode(errors, "Windows App", m.DefaultWindowsApp, NodeSlot.WindowsApp);
        ValidateNode(errors, "Windows Web", m.DefaultWindowsWeb, NodeSlot.WindowsWeb);
        ValidateNode(errors, "Сервер звітів", m.DefaultReportingServer, NodeSlot.ReportingServer);
        ValidateNode(errors, "HAProxy", m.DefaultHaProxy, NodeSlot.HaProxy);

        ValidateEngine(errors, m.Engine);
        return errors;
    }

    private static void ValidateRanges(List<string> errors, string name, List<UserLoadRange>? ranges)
    {
        if (ranges is null) { errors.Add($"{name}: список діапазонів відсутній."); return; }
        if (ranges.Count > MaxRanges) { errors.Add($"{name}: забагато діапазонів ({ranges.Count})."); return; }
        int prevMax = -1;
        for (int i = 0; i < ranges.Count; i++)
        {
            var r = ranges[i];
            var tag = $"{name}[{i}]";
            if (r is null) { errors.Add($"{tag}: порожній запис."); continue; }
            if (r.MinUsers < 0 || r.MaxUsers < 0 || r.MinUsers > MaxUsers || r.MaxUsers > MaxUsers)
                errors.Add($"{tag}: користувачі поза межами 0–{MaxUsers}.");
            if (r.MinUsers > r.MaxUsers)
                errors.Add($"{tag}: MinUsers ({r.MinUsers}) > MaxUsers ({r.MaxUsers}).");
            if (r.MinUsers <= prevMax)
                errors.Add($"{tag}: діапазони перетинаються або не відсортовано.");
            prevMax = Math.Max(prevMax, r.MaxUsers);
            if (!IsFiniteNonNeg(r.Cpu, MaxCpu)) errors.Add($"{tag}: CPU поза межами.");
            if (!IsFiniteNonNeg(r.RamMin, MaxRamGb) || !IsFiniteNonNeg(r.RamRec, MaxRamGb))
                errors.Add($"{tag}: RAM поза межами.");
            if (!IsFiniteNonNeg(r.Ghz, 1000)) errors.Add($"{tag}: частота поза межами.");
            if (r.Iops < 0 || r.Iops > MaxIops) errors.Add($"{tag}: IOPS поза межами.");
            if (r.InstanceCount < 0 || r.InstanceCount > MaxNodeCount) errors.Add($"{tag}: к-сть поза межами.");
            if (r.ThroughputMiBs < 0 || r.ThroughputMiBs > MaxIops) errors.Add($"{tag}: MiB/s поза межами.");
            if (!IsFiniteNonNeg(r.Latency, 1_000_000)) errors.Add($"{tag}: латентність поза межами.");
        }
    }

    private static void ValidateModules(List<string> errors, List<ProjectModule>? modules)
    {
        if (modules is null) return;
        if (modules.Count > MaxModules) { errors.Add($"Забагато модулів ({modules.Count})."); return; }
        for (int i = 0; i < modules.Count; i++)
        {
            var mod = modules[i];
            if (mod is null) { errors.Add($"Модуль[{i}]: порожній запис."); continue; }
            if (string.IsNullOrWhiteSpace(mod.Name) || mod.Name.Length > 200)
                errors.Add($"Модуль[{i}]: порожня або задовга назва.");
            if (mod.Components is null) { errors.Add($"Модуль '{mod.Name}': компоненти відсутні."); continue; }
            if (mod.Components.Count > MaxComponentsPerModule)
                errors.Add($"Модуль '{mod.Name}': забагато компонентів.");
            foreach (var c in mod.Components)
            {
                if (c is null) { errors.Add($"Модуль '{mod.Name}': порожній компонент."); continue; }
                if (!IsFiniteNonNeg(c.Cpu, MaxCpu) || !IsFiniteNonNeg(c.PerfCpu, MaxCpu)
                    || !IsFiniteNonNeg(c.RamGb, MaxRamGb) || !IsFiniteNonNeg(c.PerfRamGb, MaxRamGb))
                    errors.Add($"Модуль '{mod.Name}', '{c.Name}': CPU/RAM поза межами.");
                if (c.FixedReplicas < 0 || c.FixedReplicas > MaxNodeCount)
                    errors.Add($"Модуль '{mod.Name}', '{c.Name}': репліки поза межами.");
            }
        }
    }

    private static void ValidateNode(List<string> errors, string role, InfrastructureNode? n, NodeSlot expectedSlot)
    {
        if (n is null) return;
        // Slot — інваріант коду, а не даних: саме за ним правки гріда повертаються
        // у свій слот матриці. Файл із чужим/порожнім Slot приймати не можна, інакше
        // редактор матриці тихо писав би значення в неправильний вузол.
        if (n.Slot != expectedSlot)
            errors.Add($"{role}: очікувався Slot={expectedSlot}, у файлі {n.Slot}.");
        if (n.Name is not null && n.Name.Length > 200) errors.Add($"{role}: задовга назва.");
        if (!IsFiniteNonNeg(n.Cpu, MaxCpu) || !IsFiniteNonNeg(n.Ghz, 1000) || !IsFiniteNonNeg(n.RamGb, MaxRamGb))
            errors.Add($"{role}: CPU/RAM поза межами.");
        if (n.NodeCount < 0 || n.NodeCount > MaxNodeCount) errors.Add($"{role}: к-сть вузлів поза межами.");
        if (n.StorageGb < 0 || n.StorageGb2 < 0 || n.StorageGb3 < 0 || n.StorageGb4 < 0
            || n.StorageGb > MaxRamGb || n.StorageGb2 > MaxRamGb || n.StorageGb3 > MaxRamGb || n.StorageGb4 > MaxRamGb)
            errors.Add($"{role}: диски поза межами.");
        if (n.Iops < 0 || n.Iops > MaxIops) errors.Add($"{role}: IOPS поза межами.");
        if (!IsFiniteNonNeg(n.ThroughputMiBs, MaxIops) || !IsFiniteNonNeg(n.Latency, 1_000_000)
            || n.PageFileGb < 0 || n.PageFileGb > MaxRamGb)
            errors.Add($"{role}: IOPS/латентність/pagefile поза межами.");
        foreach (var s in new[] { n.Os, n.DbVersion, n.Notes, n.IopsProfile })
            if (s is not null && s.Length > MaxTextLength) errors.Add($"{role}: задовге текстове поле.");
    }

    private static void ValidateEngine(List<string> errors, EngineSettings? e)
    {
        if (e is null) { errors.Add("Налаштування рушія відсутні."); return; }
        if (e.PageFileRounding == 0) errors.Add("PageFileRounding не може бути нулем (ділення).");
        if (!IsFiniteNonNeg(e.PageFileMultiplier, 1000)) errors.Add("PageFileMultiplier поза межами.");
        if (!IsFiniteNonNeg(e.DefaultWorkerCpu, MaxCpu) || !IsFiniteNonNeg(e.DefaultWorkerRamGb, MaxRamGb))
            errors.Add("Параметри worker-вузла поза межами.");
        if (e.DefaultWorkerIops < 0 || e.DefaultWorkerIops > MaxIops) errors.Add("Worker IOPS поза межами.");
        if (!IsFiniteNonNeg(e.MsSqlStandardMaxRamGb, MaxRamGb)
            || !IsFiniteNonNeg(e.MsSqlStandardMaxCores, MaxCpu))
            errors.Add("Ліміти SQL Server поза межами.");
        if (!IsFiniteNonNeg(e.SmartIdCpuPerReplica, MaxCpu)
            || !IsFiniteNonNeg(e.SmartIdRamPerReplicaGb, MaxRamGb))
            errors.Add("Параметри SmartID поза межами.");
    }

    private static bool IsFiniteNonNeg(double v, double max)
        => double.IsFinite(v) && v >= 0 && v <= max;
}

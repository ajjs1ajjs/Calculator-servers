using ResourceCalculator.Models;

namespace ResourceCalculator.Services;

// Спільні для Excel- і PDF-звіту helpers: назви, підписи, безпечний запис тексту.
// Винесено з ConfigExportService, щоб PdfReportBuilder і ExcelReportBuilder не
// дублювали форматування й лишалися незалежними один від одного.
internal static class ReportCommon
{
    internal static string DbName(DatabaseType db) => db switch
    {
        DatabaseType.PostgreSQL => "PostgreSQL",
        DatabaseType.Oracle => "Oracle 19c",
        _ => "MS SQL Server"
    };

    internal static string DeployName(DeploymentType d) => d switch
    {
        DeploymentType.Kubernetes => "Kubernetes",
        DeploymentType.Windows => "Windows",
        _ => "Гібрид (K8s + Windows)"
    };

    // Пояснення (UI/Excel/PDF), чому середовища з близькою к-стю користувачів мають однакові поди.
    internal const string PodScalingNote =
        "Поди масштабуються блоками (на кожні 25/50/100 користувачів, мінімум 1 репліка), тож середовища " +
        "з близькою кількістю користувачів можуть мати однакові компоненти — відмінності проявляються у вузлі БД.";

    // Кількість worker-вузлів K8s (саме на них лягають поди), окремо від Windows-VM.
    internal static int WorkerNodes(ResourceRequirement req)
        => req.Infrastructure.Where(n => n.Name.Contains("Worker", StringComparison.OrdinalIgnoreCase))
                             .Sum(n => n.NodeCount);

    internal static int TotalPods(ResourceRequirement req)
        => req.Components.Where(c => c.Cpu > 0).Sum(c => c.Replicas);

    // Текст про те, як поди розподіляються по worker-вузлах (а не лише перелік реплік).
    internal static string PodDistribution(ResourceRequirement req)
    {
        var pods = TotalPods(req);
        if (pods <= 0) return "";
        var workers = WorkerNodes(req);
        if (workers <= 0) return $"Подів усього: {pods}.";
        var perNode = (int)Math.Ceiling((double)pods / workers);
        return $"Подів усього: {pods} на {workers} worker-вузлах (~{perNode} подів/вузол). " +
               $"Запит подів: {req.PodCpu:F1} CPU / {req.PodRamGb:F1} ГБ — фізичні вузли провіжиняться з округленням угору + master/БД.";
    }

    internal static string ReportTitle(ProjectConfig config)
        => $"Розрахунок інфраструктури — {config.UserCount} користувачів, {DeployName(config.DeploymentType)}";

    // Захист від Excel formula injection: рядки з матриці/конфігу (назви, ОС, примітки)
    // пишуться у .xlsx як текст. Значення з початковими = + - @ Excel виконав би як формулу
    // на машині отримувача. Префікс-апостроф лишає текст текстом.
    public static string Xl(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var t = s.TrimStart();
        if (t.StartsWith('=') || t.StartsWith('+') || t.StartsWith('-') || t.StartsWith('@'))
            return "'" + s;
        return s;
    }

    internal static string Truncate(string? s, int max) =>
        string.IsNullOrEmpty(s) || s.Length <= max ? s ?? "" : s[..max] + "…";

    // Зрозуміле призначення сервера за його назвою (для не-ІТ читачів звіту).
    public static string NodeRole(string name)
    {
        var n = name.ToLowerInvariant();
        if (n.Contains("sql") || n.Contains("postgre") || n.Contains("oracle"))
            return "Сервер бази даних — зберігає й обробляє всі дані системи";
        if (n.Contains("master"))
            return "Керуючий вузол кластера Kubernetes — координує роботу (без застосунку)";
        if (n.Contains("worker"))
            return "Робочий вузол Kubernetes — виконує застосунок у контейнерах";
        if (n.Contains("gpu"))
            return "Вузол з відеокартою — перекодування відео (LMS)";
        if (n.Contains("iis") || n.Contains("веб") || n.Contains("web"))
            return "Веб-сервер — приймає запити користувачів із браузера";
        if (n.Contains("звіт") || n.Contains("report"))
            return "Сервер звітів (Reporting Services) — формує звіти системи";
        if (n.Contains("haproxy") || n.Contains("балансув"))
            return "Балансувальник навантаження — розподіляє запити між серверами";
        if (n.Contains("додатк") || n.Contains("app"))
            return "Сервер застосунків — виконує бізнес-логіку системи";
        return "—";
    }

}

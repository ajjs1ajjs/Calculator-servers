using System.Text;
using System.Xml.Linq;
using System.Globalization;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using AIResourceCalculator.Models;

namespace AIResourceCalculator.Services;

// Формує звіти про пораховані ресурси: HTML (швидкий перегляд), XML (структурований, для імпорту)
// та Excel (.xlsx — для тендерних/переддоговірних документів).
// Прив'язки до хмарних провайдерів немає — лише обчислені вимоги (CPU/RAM/диски/IOPS).
public class ConfigExportService
{
    private static string DbName(DatabaseType db) => db switch
    {
        DatabaseType.PostgreSQL => "PostgreSQL",
        DatabaseType.Oracle => "Oracle 19c",
        _ => "MS SQL Server"
    };

    private static string DeployName(DeploymentType d) => d switch
    {
        DeploymentType.Kubernetes => "Kubernetes",
        DeploymentType.Windows => "Windows",
        _ => "Гібрид (K8s + Windows)"
    };

    private static string ProductName(ProductType t) => t == ProductType.DocumentFlow ? "Документообіг" : "Стандарт";

    // Зрозуміла назва профілю навантаження (замість англ. Basic/Performance).
    private static string ProfileName(LoadProfile p) => p == LoadProfile.Performance
        ? "Продуктивний (підвищене навантаження)"
        : "Базовий (звичайне навантаження)";

    // Кількість worker-вузлів K8s (саме на них лягають поди), окремо від Windows-VM.
    private static int WorkerNodes(ResourceRequirement req)
        => req.Infrastructure.Where(n => n.Name.Contains("Worker", StringComparison.OrdinalIgnoreCase))
                             .Sum(n => n.NodeCount);

    private static int TotalPods(ResourceRequirement req)
        => req.Components.Where(c => c.Cpu > 0).Sum(c => c.Replicas);

    // Текст про те, як поди розподіляються по worker-вузлах (а не лише перелік реплік).
    private static string PodDistribution(ResourceRequirement req)
    {
        var pods = TotalPods(req);
        if (pods <= 0) return "";
        var workers = WorkerNodes(req);
        if (workers <= 0) return $"Подів усього: {pods}.";
        var perNode = (int)Math.Ceiling((double)pods / workers);
        return $"Подів усього: {pods} на {workers} worker-вузлах (~{perNode} подів/вузол). " +
               $"Запит подів: {req.PodCpu:F1} CPU / {req.PodRamGb:F1} ГБ — фізичні вузли провіжиняться з округленням угору + master/БД.";
    }

    // ───────────────────────────── HTML ─────────────────────────────
    public string ExportHtml(ResourceRequirement req, ProjectConfig config,
        IReadOnlyList<EnvironmentReport>? environments = null,
        IEnumerable<UserLoadRange>? matrixRanges = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html><head><meta charset='UTF-8'>");
        sb.AppendLine("<style>body{font-family:Arial;margin:40px;background:#eff1f5;color:#4c4f69;line-height:1.5}h1{color:#1e66f5}h2{color:#1e66f5;margin-top:28px;border-bottom:2px solid #ccd0da;padding-bottom:4px}table{border-collapse:collapse;width:100%;margin:10px 0}th,td{border:1px solid #acb0be;padding:8px;text-align:left}th{background:#1e66f5;color:white}.kpi{display:flex;gap:12px;flex-wrap:wrap}.kpi-box{color:white;padding:14px;border-radius:6px;flex:1;min-width:120px}.kpi-box h3{margin:0 0 4px}.kpi-box .v{font-size:24px;font-weight:bold}.kpi-box small{display:block;font-size:11px;opacity:.9;margin-top:4px}tfoot td{font-weight:bold;background:#dce0e8}.note{background:#dce0e8;border-left:4px solid #1e66f5;padding:10px 14px;margin:12px 0;border-radius:4px}.intro{font-size:14px}ul.gloss{background:#fff;border:1px solid #ccd0da;border-radius:6px;padding:12px 28px}ul.gloss li{margin:4px 0}</style>");
        sb.AppendLine("</head><body>");
        sb.AppendLine($"<h1>{SanitizeHtml(ReportTitle(config))}</h1>");
        sb.AppendLine($"<p class='intro'>Цей документ описує, <b>яке обладнання (сервери) потрібно підготувати</b> для роботи системи на " +
            $"<b>{config.UserCount}</b> користувачів. Продукт: <b>{ProductName(config.ProductType)}</b>, " +
            $"тип розгортання: <b>{SanitizeHtml(DeployName(config.DeploymentType))}</b>, " +
            $"профіль навантаження: <b>{ProfileName(config.LoadProfile)}</b>, " +
            $"база даних: <b>{DbName(config.DatabaseType)}</b>. " +
            "Нижче — підсумкові потреби, перелік серверів (віртуальних машин) з поясненням їхнього призначення, вимоги до дисків та звірка з офіційними вимогами.</p>");
        sb.AppendLine("<div class='kpi'>");
        sb.AppendLine($"<div class='kpi-box' style='background:#1e66f5'><h3>CPU</h3><span class='v'>{req.TotalCpu:F1}</span><small>ядер процесора всього</small></div>");
        sb.AppendLine($"<div class='kpi-box' style='background:#40a02b'><h3>RAM</h3><span class='v'>{req.TotalRamGb:F1} ГБ</span><small>оперативної пам'яті всього</small></div>");
        sb.AppendLine($"<div class='kpi-box' style='background:#fe640b'><h3>Диски</h3><span class='v'>{req.TotalStorageGb} ГБ</span><small>дискового простору всього</small></div>");
        sb.AppendLine($"<div class='kpi-box' style='background:#8839ef'><h3>IOPS (БД)</h3><span class='v'>{req.TotalIops}</span><small>швидкодія диска сервера БД</small></div>");
        sb.AppendLine($"<div class='kpi-box' style='background:#d20f39'><h3>ВМ</h3><span class='v'>{req.Infrastructure.Sum(n => n.NodeCount)}</span><small>серверів (віртуальних машин)</small></div>");
        sb.AppendLine("</div>");
        var dist = PodDistribution(req);
        if (dist.Length > 0)
            sb.AppendLine($"<p><i>{SanitizeHtml(dist)}</i></p>");

        if (environments != null && environments.Count > 1)
        {
            sb.AppendLine("<h2>Інфраструктура за середовищами</h2>");
            sb.AppendLine("<p class='intro'>Інфраструктура — це перелік серверів (віртуальних машин), які потрібно " +
                "підготувати. Система може мати кілька середовищ: <b>PROD</b> — робоче (для всіх користувачів), " +
                "<b>DEV</b> — для розробки, <b>TEST</b> — для тестування, <b>PreProd</b> — попередній прогін перед випуском. " +
                "Кожне середовище рахується окремо за власною кількістю користувачів. " +
                "Нижче — зведення, а далі — перелік серверів і компонентів для кожного середовища окремо.</p>");
            sb.AppendLine("<table><tr><th>Середовище</th><th>Користувачів</th><th>Модулі (користувачів)</th><th>CPU (ядер)</th><th>RAM, ГБ</th><th>Диски, ГБ</th><th>IOPS (БД)</th><th>Серверів (ВМ)</th></tr>");
            foreach (var e in environments)
                sb.AppendLine($"<tr><td>{SanitizeHtml(e.Name)}</td><td>{e.UserCount}</td><td>{SanitizeHtml(e.ModulesInfo)}</td><td>{e.Cpu:F1}</td><td>{e.RamGb:F1}</td><td>{e.StorageGb}</td><td>{e.Iops}</td><td>{e.Nodes}</td></tr>");
            sb.AppendLine("</table>");

            // Розбивка ВМ та компонентів для кожного середовища.
            foreach (var e in environments)
            {
                sb.AppendLine($"<h3>Середовище {SanitizeHtml(e.Name)} — сервери (користувачів: {e.UserCount})</h3>");
                if (!string.IsNullOrEmpty(e.ModulesInfo))
                    sb.AppendLine($"<p class='intro'><b>Модулі (користувачів):</b> {SanitizeHtml(e.ModulesInfo)}</p>");
                AppendInfraTableHtml(sb, e.Requirement);
                AppendComponentsTableHtml(sb, e.Requirement, $"Компоненти (поди) середовища {e.Name}");
            }
        }
        else
        {
            sb.AppendLine("<h2>Інфраструктура — сервери (віртуальні машини)</h2>");
            sb.AppendLine("<p class='intro'>Інфраструктура — це перелік серверів (віртуальних машин), які потрібно " +
                "підготувати, із їхнім призначенням і ресурсами (середовище PROD).</p>");
            AppendInfraTableHtml(sb, req);
        }

        AppendGlossaryHtml(sb);

        // Компоненти PROD показуємо лише коли НЕ розбивали по середовищах (інакше вони вже вище).
        if (environments == null || environments.Count <= 1)
            AppendComponentsTableHtml(sb, req, "Компоненти (поди)");

        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    // Таблиця компонентів (подів) одного середовища. Нічого не виводить, якщо подів немає.
    private static void AppendComponentsTableHtml(System.Text.StringBuilder sb, ResourceRequirement req, string title)
    {
        var comps = req.Components.Where(c => c.Cpu > 0).ToList();
        if (comps.Count == 0) return;
        sb.AppendLine($"<h3>{SanitizeHtml(title)}</h3><table><tr><th>Назва</th><th>Категорія</th><th>CPU/репліку</th><th>RAM/репліку</th><th>Реплік</th><th>CPU разом</th><th>RAM разом</th></tr>");
        foreach (var c in comps)
            sb.AppendLine($"<tr><td>{SanitizeHtml(c.Name)}</td><td>{SanitizeHtml(c.Category)}</td><td>{c.CpuPerReplica:F2}</td><td>{c.RamPerReplicaGb:F2} GB</td><td>{c.Replicas}</td><td>{c.Cpu:F1}</td><td>{c.RamGb:F1} GB</td></tr>");
        sb.AppendLine($"<tfoot><tr><td>Разом</td><td></td><td></td><td></td><td>{comps.Sum(c => c.Replicas)}</td><td>{comps.Sum(c => c.Cpu):F1}</td><td>{comps.Sum(c => c.RamGb):F1} GB</td></tr></tfoot>");
        sb.AppendLine("</table>");
    }

    private static string ReportTitle(ProjectConfig config)
        => $"Розрахунок інфраструктури — {config.UserCount} користувачів, {DeployName(config.DeploymentType)}";

    // Таблиця інфраструктури (ВМ) з призначенням, версією СУБД та підсумковим рядком.
    private static void AppendInfraTableHtml(System.Text.StringBuilder sb, ResourceRequirement req)
    {
        sb.AppendLine("<table><tr><th>Сервер (ВМ)</th><th>CPU (ядер)</th><th>RAM, ГБ</th><th>К-сть</th><th>Тип диску</th><th>Диск на 1 сервер</th><th>Диск разом</th><th>Page file</th><th>IOPS</th><th>Профіль IOPS</th><th>MiB/s</th><th>Затримка, мс</th><th>Призначення</th><th>ОС</th><th>Версія СУБД</th><th>Примітки</th></tr>");
        foreach (var i in req.Infrastructure.Where(n => n.NodeCount > 0))
            sb.AppendLine($"<tr><td>{SanitizeHtml(i.Name)}</td><td>{i.Cpu}</td><td>{i.RamGb}</td><td>{i.NodeCount}</td><td>{SanitizeHtml(i.StorageType)}</td><td>{i.DiskPerNodeGb} GB</td><td>{i.TotalStorageGb} GB</td><td>{(i.PageFileGb > 0 ? i.PageFileGb + " GB" : "")}</td><td>{(i.Iops > 0 ? i.Iops.ToString() : "")}</td><td>{SanitizeHtml(i.IopsProfile)}</td><td>{(i.ThroughputMiBs > 0 ? i.ThroughputMiBs.ToString() : "")}</td><td>{(i.Latency > 0 ? Trim(i.Latency) : "")}</td><td>{SanitizeHtml(NodeRole(i.Name))}</td><td>{SanitizeHtml(i.Os)}</td><td>{SanitizeHtml(i.DbVersion)}</td><td>{SanitizeHtml(i.Notes)}</td></tr>");
        sb.AppendLine($"<tfoot><tr><td>Разом</td><td>{req.TotalCpu:F1}</td><td>{req.TotalRamGb:F1}</td><td>{req.Infrastructure.Sum(n => n.NodeCount)}</td><td></td><td></td><td>{req.TotalStorageGb} GB</td><td></td><td></td><td></td><td></td><td></td><td></td><td></td><td></td><td></td></tr></tfoot>");
        sb.AppendLine("</table>");
    }

    // Глосарій основних термінів — щоб звіт був зрозумілий без ІТ-фаху.
    private static void AppendGlossaryHtml(System.Text.StringBuilder sb)
    {
        sb.AppendLine("<h2>Пояснення показників</h2>");
        sb.AppendLine("<ul class='gloss'>");
        sb.AppendLine("<li><b>CPU (ядер)</b> — обчислювальна потужність процесора. Більше користувачів — більше ядер.</li>");
        sb.AppendLine("<li><b>RAM</b> — оперативна пам'ять. Найважливіша для сервера бази даних.</li>");
        sb.AppendLine("<li><b>Диски</b> — обсяг сховища (у ГБ) під операційну систему, дані, журнали та резервні копії.</li>");
        sb.AppendLine("<li><b>IOPS</b> — швидкодія диска (операцій за секунду). Вказується для сервера БД як найвибагливішого; " +
            "значення різних дисків не додаються між собою.</li>");
        sb.AppendLine("<li><b>Профіль IOPS</b> — співвідношення операцій читання/запису (напр. «50r/50w» — порівну).</li>");
        sb.AppendLine("<li><b>MiB/s</b> — пропускна здатність диска (мегабайти за секунду, послідовні операції).</li>");
        sb.AppendLine("<li><b>Затримка</b> — час відповіді диска в мілісекундах; що менше, то краще.</li>");
        sb.AppendLine("<li><b>Page file</b> — файл підкачки (резерв пам'яті на диску) для серверів застосунків/веб.</li>");
        sb.AppendLine("<li><b>ВМ (сервер)</b> — окрема віртуальна машина, яку треба створити.</li>");
        sb.AppendLine("</ul>");
    }

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
        if (n.Contains("додатк") || n.Contains("app"))
            return "Сервер застосунків — виконує бізнес-логіку системи";
        return "—";
    }

    // ───────────────────────────── XML ─────────────────────────────
    public string ExportXml(ResourceRequirement req, ProjectConfig config,
        IReadOnlyList<EnvironmentReport>? environments = null,
        IEnumerable<UserLoadRange>? matrixRanges = null)
    {
        static XAttribute D(string name, double v) => new(name, v.ToString("0.##", CultureInfo.InvariantCulture));

        XElement Node(InfrastructureNode n) => new("Node",
            new XAttribute("name", n.Name),
            new XAttribute("os", n.Os),
            new XAttribute("dbVersion", n.DbVersion),
            D("cpu", n.Cpu),
            D("ramGb", n.RamGb),
            new XAttribute("count", n.NodeCount),
            new XAttribute("storageType", n.StorageType),
            new XAttribute("diskPerNodeGb", n.DiskPerNodeGb),
            new XAttribute("diskTotalGb", n.TotalStorageGb),
            new XAttribute("pageFileGb", n.PageFileGb),
            new XAttribute("pageFileType", n.PageFileType),
            new XAttribute("iops", n.Iops),
            new XAttribute("iopsProfile", n.IopsProfile),
            new XAttribute("throughputMiBs", n.ThroughputMiBs),
            D("latencyMs", n.Latency),
            new XAttribute("notes", n.Notes));

        var infra = new XElement("Infrastructure",
            req.Infrastructure.Where(n => n.NodeCount > 0).Select(Node));

        var comps = new XElement("Components",
            req.Components.Where(c => c.Cpu > 0).Select(c => new XElement("Component",
                new XAttribute("name", c.Name),
                new XAttribute("category", c.Category),
                D("cpuPerReplica", c.CpuPerReplica),
                D("ramPerReplicaGb", c.RamPerReplicaGb),
                new XAttribute("replicas", c.Replicas),
                D("cpuTotal", c.Cpu),
                D("ramTotalGb", c.RamGb))));

        XElement? envs = null;
        if (environments != null && environments.Count > 1)
        {
            envs = new XElement("Environments",
                environments.Select(e => new XElement("Environment",
                    new XAttribute("name", e.Name),
                    new XAttribute("users", e.UserCount),
                    D("cpu", e.Cpu),
                    D("ramGb", e.RamGb),
                    new XAttribute("storageGb", e.StorageGb),
                    new XAttribute("iops", e.Iops),
                    new XAttribute("vms", e.Nodes),
                    // Розбивка ВМ кожного середовища.
                    new XElement("Infrastructure",
                        e.Requirement.Infrastructure.Where(n => n.NodeCount > 0).Select(Node)))));
        }

        var doc = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement("ResourceReport",
                new XAttribute("project", config.ProjectName),
                new XAttribute("users", config.UserCount),
                new XAttribute("deployment", config.DeploymentType.ToString()),
                new XAttribute("profile", config.LoadProfile.ToString()),
                new XAttribute("database", DbName(config.DatabaseType)),
                new XElement("Totals",
                    D("cpu", req.TotalCpu),
                    D("ramGb", req.TotalRamGb),
                    new XAttribute("storageGb", req.TotalStorageGb),
                    // IOPS визначаються вузлом БД (не сумою дисків).
                    new XAttribute("iopsDb", req.TotalIops),
                    new XAttribute("vms", req.Infrastructure.Sum(n => n.NodeCount))),
                new XElement("PodRequests",
                    D("cpu", req.PodCpu),
                    D("ramGb", req.PodRamGb),
                    new XAttribute("totalPods", TotalPods(req)),
                    new XAttribute("workerNodes", WorkerNodes(req))),
                envs,
                infra,
                comps));

        return doc.Declaration + Environment.NewLine + doc;
    }

    // ───────────────────────────── Excel ─────────────────────────────
    public byte[] ExportExcel(ResourceRequirement req, ProjectConfig config,
        IReadOnlyList<EnvironmentReport>? environments = null,
        IEnumerable<UserLoadRange>? matrixRanges = null)
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        using var pkg = new ExcelPackage();

        BuildSummarySheet(pkg, req, config);
        bool multiEnv = environments != null && environments.Count > 1;
        if (multiEnv)
        {
            BuildEnvironmentsSheet(pkg, environments!);
            BuildEnvironmentVmsSheet(pkg, environments!);
            BuildEnvironmentComponentsSheet(pkg, environments!);
        }
        BuildInfrastructureSheet(pkg, req);
        // Компоненти PROD окремо лише коли не було розбивки по середовищах.
        if (!multiEnv) BuildComponentsSheet(pkg, req);

        return pkg.GetAsByteArray();
    }

    // Аркуш із розбивкою ВМ для кожного середовища (PROD/DEV/TEST/PreProd).
    private static void BuildEnvironmentVmsSheet(ExcelPackage pkg, IReadOnlyList<EnvironmentReport> environments)
    {
        var ws = pkg.Workbook.Worksheets.Add("ВМ по середовищах");
        string[] headers = { "Середовище", "Сервер (ВМ)", "CPU (ядер)", "RAM (ГБ)", "К-сть",
            "Диск/сервер (ГБ)", "Диск разом (ГБ)", "IOPS", "Профіль IOPS", "MiB/s", "Затримка (мс)",
            "Призначення", "ОС", "Версія СУБД", "Примітки" };
        WriteHeader(ws, headers);
        int row = 2;
        foreach (var e in environments)
        {
            foreach (var n in e.Requirement.Infrastructure.Where(x => x.NodeCount > 0))
            {
                ws.Cells[row, 1].Value = e.Name;
                ws.Cells[row, 2].Value = n.Name;
                ws.Cells[row, 3].Value = n.Cpu;
                ws.Cells[row, 4].Value = n.RamGb;
                ws.Cells[row, 5].Value = n.NodeCount;
                ws.Cells[row, 6].Value = n.DiskPerNodeGb;
                ws.Cells[row, 7].Value = n.TotalStorageGb;
                ws.Cells[row, 8].Value = n.Iops > 0 ? n.Iops : (object)"";
                ws.Cells[row, 9].Value = n.IopsProfile;
                ws.Cells[row, 10].Value = n.ThroughputMiBs > 0 ? n.ThroughputMiBs : (object)"";
                ws.Cells[row, 11].Value = n.Latency > 0 ? n.Latency : (object)"";
                ws.Cells[row, 12].Value = NodeRole(n.Name);
                ws.Cells[row, 13].Value = n.Os;
                ws.Cells[row, 14].Value = n.DbVersion;
                ws.Cells[row, 15].Value = n.Notes;
                ws.Cells[row, 3, row, 11].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                row++;
            }
        }
        StyleTable(ws);
    }

    // Аркуш із компонентами (подами): ЗВЕДЕНИЙ вигляд — один рядок на компонент, а середовища
    // йдуть СТОВПЦЯМИ зліва направо (Реплік/CPU/RAM на кожне), щоб зручно порівнювати по горизонталі.
    private static void BuildEnvironmentComponentsSheet(ExcelPackage pkg, IReadOnlyList<EnvironmentReport> environments)
    {
        var envs = environments.Where(e => e.Components.Any()).ToList();
        if (envs.Count == 0) return;
        var ws = pkg.Workbook.Worksheets.Add("Компоненти по середовищах");

        // Унікальні компоненти в порядку першої появи (PROD першим).
        var order = new List<(string Cat, string Name)>();
        var seen = new HashSet<string>();
        foreach (var e in envs)
            foreach (var c in e.Components)
                if (seen.Add(c.Category + "|" + c.Name)) order.Add((c.Category, c.Name));

        // Дворядкова шапка: над кожним середовищем — його назва (об'єднано на 3 стовпці).
        var blue = System.Drawing.Color.FromArgb(30, 102, 245);
        void Head(ExcelRange cell, string text, bool merge = false)
        {
            cell.Value = text;
            cell.Style.Font.Bold = true;
            cell.Style.Font.Color.SetColor(System.Drawing.Color.White);
            cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
            cell.Style.Fill.BackgroundColor.SetColor(blue);
            cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            if (merge) cell.Merge = true;
        }
        Head(ws.Cells[1, 1, 2, 1], "Назва", merge: true);
        Head(ws.Cells[1, 2, 2, 2], "Категорія", merge: true);
        for (int i = 0; i < envs.Count; i++)
        {
            int c0 = 3 + i * 3;
            Head(ws.Cells[1, c0, 1, c0 + 2], envs[i].Name, merge: true);
            Head(ws.Cells[2, c0], "Реплік");
            Head(ws.Cells[2, c0 + 1], "CPU");
            Head(ws.Cells[2, c0 + 2], "RAM");
        }

        int row = 3;
        foreach (var (cat, name) in order)
        {
            ws.Cells[row, 1].Value = name;
            ws.Cells[row, 2].Value = cat;
            for (int i = 0; i < envs.Count; i++)
            {
                int c0 = 3 + i * 3;
                var comp = envs[i].Components.FirstOrDefault(x => x.Category == cat && x.Name == name);
                if (comp != null)
                {
                    ws.Cells[row, c0].Value = comp.Replicas;
                    ws.Cells[row, c0 + 1].Value = Math.Round(comp.Cpu, 1);
                    ws.Cells[row, c0 + 2].Value = Math.Round(comp.RamGb, 1);
                }
                ws.Cells[row, c0, row, c0 + 2].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            }
            row++;
        }
        // Підсумковий рядок «Разом» по кожному середовищу.
        ws.Cells[row, 1].Value = "Разом";
        for (int i = 0; i < envs.Count; i++)
        {
            int c0 = 3 + i * 3;
            ws.Cells[row, c0].Value = envs[i].Components.Sum(c => c.Replicas);
            ws.Cells[row, c0 + 1].Value = Math.Round(envs[i].Components.Sum(c => c.Cpu), 1);
            ws.Cells[row, c0 + 2].Value = Math.Round(envs[i].Components.Sum(c => c.RamGb), 1);
            ws.Cells[row, c0, row, c0 + 2].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
        }
        int lastCol = 2 + envs.Count * 3;
        ws.Cells[row, 1, row, lastCol].Style.Font.Bold = true;
        ws.Cells[row, 1, row, lastCol].Style.Fill.PatternType = ExcelFillStyle.Solid;
        ws.Cells[row, 1, row, lastCol].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(220, 224, 232));

        StyleTable(ws);
    }

    private static void BuildEnvironmentsSheet(ExcelPackage pkg, IReadOnlyList<EnvironmentReport> environments)
    {
        var ws = pkg.Workbook.Worksheets.Add("Середовища");
        string[] headers = { "Середовище", "Користувачів", "Модулі (користувачів)", "CPU", "RAM (ГБ)", "Диски (ГБ)", "IOPS (БД)", "ВМ (серверів)" };
        WriteHeader(ws, headers);

        int row = 2;
        foreach (var e in environments)
        {
            ws.Cells[row, 1].Value = e.Name;
            ws.Cells[row, 2].Value = e.UserCount;
            ws.Cells[row, 3].Value = e.ModulesInfo;
            ws.Cells[row, 4].Value = e.Cpu;
            ws.Cells[row, 5].Value = e.RamGb;
            ws.Cells[row, 6].Value = e.StorageGb;
            ws.Cells[row, 7].Value = e.Iops;
            ws.Cells[row, 8].Value = e.Nodes;
            ws.Cells[row, 2].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            ws.Cells[row, 4, row, 8].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            row++;
        }
        StyleTable(ws);
    }

    private static void BuildSummarySheet(ExcelPackage pkg, ResourceRequirement req, ProjectConfig config)
    {
        var ws = pkg.Workbook.Worksheets.Add("Підсумок");
        int r = 1;
        ws.Cells[r, 1].Value = ReportTitle(config);
        ws.Cells[r, 1, r, 2].Merge = true;
        ws.Cells[r, 1].Style.Font.Size = 14;
        ws.Cells[r, 1].Style.Font.Bold = true;
        r++;
        ws.Cells[r, 1].Value = $"Документ описує, яке обладнання (сервери) потрібно підготувати для роботи системи на {config.UserCount} користувачів.";
        ws.Cells[r, 1, r, 2].Merge = true;
        ws.Cells[r, 1].Style.WrapText = true;
        ws.Cells[r, 1].Style.Font.Italic = true;
        r += 2;

        void Section(string title)
        {
            ws.Cells[r, 1].Value = title;
            ws.Cells[r, 1, r, 2].Merge = true;
            ws.Cells[r, 1].Style.Font.Bold = true;
            ws.Cells[r, 1].Style.Font.Size = 12;
            ws.Cells[r, 1].Style.Font.Color.SetColor(System.Drawing.Color.FromArgb(30, 102, 245));
            r++;
        }

        void Kv(string k, object v)
        {
            ws.Cells[r, 1].Value = k;
            ws.Cells[r, 1].Style.Font.Bold = true;
            ws.Cells[r, 2].Value = v;
            r++;
        }

        Section("Параметри");
        Kv("Користувачів", config.UserCount);
        Kv("Продукт", ProductName(config.ProductType));
        Kv("Тип розгортання", DeployName(config.DeploymentType));
        Kv("Профіль навантаження", ProfileName(config.LoadProfile));
        Kv("База даних (СКБД)", DbName(config.DatabaseType));
        r++;
        Section("Підсумкові потреби (середовище PROD)");
        Kv("Всього CPU (ядер процесора)", Math.Round(req.TotalCpu, 1));
        Kv("Всього RAM (оперативної пам'яті), ГБ", Math.Round(req.TotalRamGb, 1));
        Kv("Всього диски, ГБ", req.TotalStorageGb);
        Kv("IOPS сервера БД (швидкодія диска)", req.TotalIops);
        var dbMib = req.Infrastructure.FirstOrDefault(n =>
            n.Name.Contains("SQL", StringComparison.OrdinalIgnoreCase)
            || n.Name.Contains("PostgreSQL", StringComparison.OrdinalIgnoreCase)
            || n.Name.Contains("Oracle", StringComparison.OrdinalIgnoreCase))?.ThroughputMiBs ?? 0;
        Kv("Пропускна здатність БД, MiB/s", dbMib);
        Kv("Всього серверів (ВМ)", req.Infrastructure.Sum(n => n.NodeCount));
        if (req.PodCpu > 0)
        {
            r++;
            Section("Контейнери (поди) Kubernetes");
            Kv("Сумарний запит подів, CPU", Math.Round(req.PodCpu, 1));
            Kv("Сумарний запит подів, RAM (ГБ)", Math.Round(req.PodRamGb, 1));
            Kv("Подів усього", TotalPods(req));
            Kv("Worker-вузлів (на них працюють поди)", WorkerNodes(req));
            r++;
            ws.Cells[r, 1].Value = PodDistribution(req);
            ws.Cells[r, 1, r, 2].Merge = true;
            ws.Cells[r, 1].Style.WrapText = true;
            ws.Cells[r, 1].Style.Font.Italic = true;
        }
        ws.Cells[ws.Dimension.Address].AutoFitColumns();
        ws.Column(1).Width = 36;
        ws.Column(2).Width = 28;
    }

    private static void BuildInfrastructureSheet(ExcelPackage pkg, ResourceRequirement req)
    {
        var ws = pkg.Workbook.Worksheets.Add("Інфраструктура");
        // Підпис: ця таблиця — середовище PROD (перелік серверів/ВМ для розгортання).
        ws.Cells[1, 1].Value = "Інфраструктура (сервери/ВМ) — середовище PROD";
        ws.Cells[1, 1, 1, 16].Merge = true;
        ws.Cells[1, 1].Style.Font.Bold = true;
        ws.Cells[1, 1].Style.Font.Size = 12;
        ws.Cells[1, 1].Style.Font.Color.SetColor(System.Drawing.Color.FromArgb(30, 102, 245));

        // Порядок колонок: ідентифікатор + числові характеристики спершу, описові
        // (Призначення/ОС/Версія СУБД) — у кінці перед примітками.
        string[] headers = { "Сервер (ВМ)", "CPU (ядер)", "RAM (ГБ)", "К-сть", "Тип диску",
            "Диск/сервер (ГБ)", "Диск разом (ГБ)", "Page file (ГБ)", "IOPS", "Профіль IOPS", "MiB/s", "Затримка (мс)",
            "Призначення", "ОС", "Версія СУБД", "Примітки" };
        WriteHeader(ws, headers, headerRow: 2);

        int row = 3;
        foreach (var n in req.Infrastructure.Where(x => x.NodeCount > 0))
        {
            ws.Cells[row, 1].Value = n.Name;
            ws.Cells[row, 2].Value = n.Cpu;
            ws.Cells[row, 3].Value = n.RamGb;
            ws.Cells[row, 4].Value = n.NodeCount;
            ws.Cells[row, 5].Value = n.StorageType;
            ws.Cells[row, 6].Value = n.DiskPerNodeGb;
            ws.Cells[row, 7].Value = n.TotalStorageGb;
            ws.Cells[row, 8].Value = n.PageFileGb > 0 ? n.PageFileGb : (object)"";
            ws.Cells[row, 9].Value = n.Iops > 0 ? n.Iops : (object)"";
            ws.Cells[row, 10].Value = n.IopsProfile;
            ws.Cells[row, 11].Value = n.ThroughputMiBs > 0 ? n.ThroughputMiBs : (object)"";
            ws.Cells[row, 12].Value = n.Latency > 0 ? n.Latency : (object)"";
            ws.Cells[row, 13].Value = NodeRole(n.Name);
            ws.Cells[row, 14].Value = n.Os;
            ws.Cells[row, 15].Value = n.DbVersion;
            ws.Cells[row, 16].Value = n.Notes;
            ws.Cells[row, 2, row, 12].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            row++;
        }
        // Підсумковий рядок.
        ws.Cells[row, 1].Value = "Разом";
        ws.Cells[row, 2].Value = Math.Round(req.TotalCpu, 1);
        ws.Cells[row, 3].Value = Math.Round(req.TotalRamGb, 1);
        ws.Cells[row, 4].Value = req.Infrastructure.Sum(n => n.NodeCount);
        ws.Cells[row, 7].Value = req.TotalStorageGb;
        ws.Cells[row, 2, row, 12].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
        ws.Cells[row, 1, row, 16].Style.Font.Bold = true;
        ws.Cells[row, 1, row, 16].Style.Fill.PatternType = ExcelFillStyle.Solid;
        ws.Cells[row, 1, row, 16].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(220, 224, 232));

        StyleTable(ws, headerRow: 2);
    }

    private static void BuildComponentsSheet(ExcelPackage pkg, ResourceRequirement req)
    {
        var comps = req.Components.Where(c => c.Cpu > 0).ToList();
        if (comps.Count == 0) return;

        var ws = pkg.Workbook.Worksheets.Add("Компоненти");
        string[] headers = { "Назва", "Категорія", "CPU/репліку", "RAM/репліку (ГБ)",
            "Реплік", "CPU разом", "RAM разом (ГБ)" };
        WriteHeader(ws, headers);

        int row = 2;
        foreach (var c in comps)
        {
            ws.Cells[row, 1].Value = c.Name;
            ws.Cells[row, 2].Value = c.Category;
            ws.Cells[row, 3].Value = Math.Round(c.CpuPerReplica, 2);
            ws.Cells[row, 4].Value = Math.Round(c.RamPerReplicaGb, 2);
            ws.Cells[row, 5].Value = c.Replicas;
            ws.Cells[row, 6].Value = Math.Round(c.Cpu, 1);
            ws.Cells[row, 7].Value = Math.Round(c.RamGb, 1);
            ws.Cells[row, 3, row, 7].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            row++;
        }
        ws.Cells[row, 1].Value = "Разом";
        ws.Cells[row, 5].Value = comps.Sum(c => c.Replicas);
        ws.Cells[row, 6].Value = Math.Round(comps.Sum(c => c.Cpu), 1);
        ws.Cells[row, 7].Value = Math.Round(comps.Sum(c => c.RamGb), 1);
        ws.Cells[row, 1, row, 7].Style.Font.Bold = true;
        ws.Cells[row, 1, row, 7].Style.Fill.PatternType = ExcelFillStyle.Solid;
        ws.Cells[row, 1, row, 7].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(220, 224, 232));

        StyleTable(ws);
    }

    // Тонкі рамки на всю таблицю + автоширина. Числа лишаються чорними (типовий колір).
    private static void StyleTable(ExcelWorksheet ws, int headerRow = 1)
    {
        var dim = ws.Dimension;
        if (dim == null) return;
        var cells = ws.Cells[dim.Address];
        cells.Style.Border.Top.Style = ExcelBorderStyle.Thin;
        cells.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
        cells.Style.Border.Left.Style = ExcelBorderStyle.Thin;
        cells.Style.Border.Right.Style = ExcelBorderStyle.Thin;
        cells.AutoFitColumns();
    }

    private static void WriteHeader(ExcelWorksheet ws, string[] headers, int headerRow = 1)
    {
        for (int c = 0; c < headers.Length; c++)
        {
            var cell = ws.Cells[headerRow, c + 1];
            cell.Value = headers[c];
            cell.Style.Font.Bold = true;
            cell.Style.Font.Color.SetColor(System.Drawing.Color.White);
            cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
            cell.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(30, 102, 245));
        }
    }

    private static string Trim(double v) => v % 1 == 0 ? ((int)v).ToString() : v.ToString("0.#");

    public static string SanitizeHtml(string input)
        => string.IsNullOrEmpty(input) ? "" : input.Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}

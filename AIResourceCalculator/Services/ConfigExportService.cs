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
        IReadOnlyList<EnvironmentReport>? environments = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html><head><meta charset='UTF-8'>");
        sb.AppendLine("<style>body{font-family:Arial;margin:40px;background:#eff1f5;color:#4c4f69}h1{color:#1e66f5}table{border-collapse:collapse;width:100%;margin:15px 0}th,td{border:1px solid #acb0be;padding:8px;text-align:left}th{background:#1e66f5;color:white}.kpi{display:flex;gap:15px}.kpi-box{color:white;padding:15px;border-radius:6px;flex:1}tfoot td{font-weight:bold;background:#dce0e8}</style>");
        sb.AppendLine("</head><body>");
        sb.AppendLine($"<h1>{SanitizeHtml(ReportTitle(config))}</h1>");
        sb.AppendLine($"<p>Користувачів: {config.UserCount} | Розгортання: {SanitizeHtml(DeployName(config.DeploymentType))} | Профіль: {config.LoadProfile} | СКБД: {DbName(config.DatabaseType)} | Обсяг даних БД: {config.DbDataSizeGb} ГБ</p>");
        sb.AppendLine("<div class='kpi'>");
        sb.AppendLine($"<div class='kpi-box' style='background:#1e66f5'><h3>CPU</h3><p>{req.TotalCpu:F1}</p></div>");
        sb.AppendLine($"<div class='kpi-box' style='background:#40a02b'><h3>RAM</h3><p>{req.TotalRamGb:F1} GB</p></div>");
        sb.AppendLine($"<div class='kpi-box' style='background:#fe640b'><h3>Диски</h3><p>{req.TotalStorageGb} GB</p></div>");
        sb.AppendLine($"<div class='kpi-box' style='background:#8839ef'><h3>IOPS (БД)</h3><p>{req.TotalIops}</p></div>");
        sb.AppendLine($"<div class='kpi-box' style='background:#d20f39'><h3>ВМ (серверів)</h3><p>{req.Infrastructure.Sum(n => n.NodeCount)}</p></div>");
        sb.AppendLine("</div>");
        var dist = PodDistribution(req);
        if (dist.Length > 0)
            sb.AppendLine($"<p><i>{SanitizeHtml(dist)}</i></p>");

        AppendDocComparisonHtml(sb, req, config);

        if (environments != null && environments.Count > 1)
        {
            sb.AppendLine("<h2>Порівняння середовищ</h2><table><tr><th>Середовище</th><th>Ліцензій</th><th>CPU</th><th>RAM (ГБ)</th><th>Диски (ГБ)</th><th>IOPS (БД)</th><th>ВМ (серверів)</th></tr>");
            foreach (var e in environments)
                sb.AppendLine($"<tr><td>{SanitizeHtml(e.Name)}</td><td>{e.UserCount}</td><td>{e.Cpu:F1}</td><td>{e.RamGb:F1}</td><td>{e.StorageGb}</td><td>{e.Iops}</td><td>{e.Nodes}</td></tr>");
            sb.AppendLine("</table>");

            // Розбивка ВМ для кожного середовища.
            foreach (var e in environments)
            {
                sb.AppendLine($"<h3>ВМ середовища {SanitizeHtml(e.Name)} (ліцензій: {e.UserCount})</h3>");
                AppendInfraTableHtml(sb, e.Requirement);
            }
        }
        else
        {
            sb.AppendLine("<h2>Інфраструктура (ВМ)</h2>");
            AppendInfraTableHtml(sb, req);
        }

        var comps = req.Components.Where(c => c.Cpu > 0).ToList();
        if (comps.Count > 0)
        {
            sb.AppendLine("<h2>Компоненти (поди)</h2><table><tr><th>Назва</th><th>Категорія</th><th>CPU/репліку</th><th>RAM/репліку</th><th>Реплік</th><th>CPU разом</th><th>RAM разом</th></tr>");
            foreach (var c in comps)
                sb.AppendLine($"<tr><td>{SanitizeHtml(c.Name)}</td><td>{SanitizeHtml(c.Category)}</td><td>{c.CpuPerReplica:F2}</td><td>{c.RamPerReplicaGb:F2} GB</td><td>{c.Replicas}</td><td>{c.Cpu:F1}</td><td>{c.RamGb:F1} GB</td></tr>");
            sb.AppendLine($"<tfoot><tr><td>Разом</td><td></td><td></td><td></td><td>{comps.Sum(c => c.Replicas)}</td><td>{comps.Sum(c => c.Cpu):F1}</td><td>{comps.Sum(c => c.RamGb):F1} GB</td></tr></tfoot>");
            sb.AppendLine("</table>");
        }
        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    private static string ReportTitle(ProjectConfig config)
        => $"Розрахунок інфраструктури — {config.UserCount} користувачів, {DeployName(config.DeploymentType)}";

    // Таблиця інфраструктури (ВМ) з колонкою версії СУБД та підсумковим рядком.
    private static void AppendInfraTableHtml(System.Text.StringBuilder sb, ResourceRequirement req)
    {
        sb.AppendLine("<table><tr><th>Вузол</th><th>ОС</th><th>Версія СУБД</th><th>CPU</th><th>RAM</th><th>К-сть</th><th>Тип диску</th><th>Диск/вузол</th><th>Диск разом</th><th>Page file</th><th>IOPS</th><th>Затримка, мс</th><th>Примітки</th></tr>");
        foreach (var i in req.Infrastructure.Where(n => n.NodeCount > 0))
            sb.AppendLine($"<tr><td>{SanitizeHtml(i.Name)}</td><td>{SanitizeHtml(i.Os)}</td><td>{SanitizeHtml(string.IsNullOrEmpty(i.DbVersion) ? "—" : i.DbVersion)}</td><td>{i.Cpu}</td><td>{i.RamGb}</td><td>{i.NodeCount}</td><td>{SanitizeHtml(i.StorageType)}</td><td>{i.DiskPerNodeGb} GB</td><td>{i.TotalStorageGb} GB</td><td>{(i.PageFileGb > 0 ? i.PageFileGb + " GB" : "—")}</td><td>{(i.Iops > 0 ? i.Iops.ToString() : "—")}</td><td>{(i.Latency > 0 ? Trim(i.Latency) : "—")}</td><td>{SanitizeHtml(i.Notes)}</td></tr>");
        sb.AppendLine($"<tfoot><tr><td>Разом</td><td></td><td></td><td>{req.TotalCpu:F1}</td><td>{req.TotalRamGb:F1}</td><td>{req.Infrastructure.Sum(n => n.NodeCount)}</td><td></td><td></td><td>{req.TotalStorageGb} GB</td><td></td><td></td><td></td><td></td></tr></tfoot>");
        sb.AppendLine("</table>");
    }

    // Звірка розрахунку з вимогами документа D-AD-ADM-E (лише для MS SQL Server).
    private static void AppendDocComparisonHtml(System.Text.StringBuilder sb, ResourceRequirement req, ProjectConfig config)
    {
        var items = Data.DocumentRequirements.Compare(req, config);
        if (items.Count == 0) return;
        sb.AppendLine($"<h2>Звірка з вимогами ({SanitizeHtml(Data.DocumentRequirements.Source)})</h2>");
        sb.AppendLine("<table><tr><th>Показник (сервер БД)</th><th>За документом</th><th>Розрахунок</th><th>Статус</th></tr>");
        foreach (var it in items)
        {
            var color = it.Status == "Відповідає" ? "#40a02b" : "#d20f39";
            sb.AppendLine($"<tr><td>{SanitizeHtml(it.Metric)}</td><td>{SanitizeHtml(it.Document)}</td><td>{SanitizeHtml(it.Calculated)}</td><td style='color:{color};font-weight:bold'>{SanitizeHtml(it.Status)}</td></tr>");
        }
        sb.AppendLine("</table>");
    }

    // ───────────────────────────── XML ─────────────────────────────
    public string ExportXml(ResourceRequirement req, ProjectConfig config,
        IReadOnlyList<EnvironmentReport>? environments = null)
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

        var docItems = Data.DocumentRequirements.Compare(req, config);
        XElement? docCompare = docItems.Count == 0 ? null : new XElement("DocumentComparison",
            new XAttribute("source", Data.DocumentRequirements.Source),
            docItems.Select(it => new XElement("Item",
                new XAttribute("metric", it.Metric),
                new XAttribute("document", it.Document),
                new XAttribute("calculated", it.Calculated),
                new XAttribute("status", it.Status))));

        var doc = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement("ResourceReport",
                new XAttribute("project", config.ProjectName),
                new XAttribute("users", config.UserCount),
                new XAttribute("deployment", config.DeploymentType.ToString()),
                new XAttribute("profile", config.LoadProfile.ToString()),
                new XAttribute("database", DbName(config.DatabaseType)),
                new XAttribute("dbDataSizeGb", config.DbDataSizeGb),
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
                docCompare,
                envs,
                infra,
                comps));

        return doc.Declaration + Environment.NewLine + doc;
    }

    // ───────────────────────────── Excel ─────────────────────────────
    public byte[] ExportExcel(ResourceRequirement req, ProjectConfig config,
        IReadOnlyList<EnvironmentReport>? environments = null)
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        using var pkg = new ExcelPackage();

        BuildSummarySheet(pkg, req, config);
        BuildDocComparisonSheet(pkg, req, config);
        if (environments != null && environments.Count > 1)
        {
            BuildEnvironmentsSheet(pkg, environments);
            BuildEnvironmentVmsSheet(pkg, environments);
        }
        BuildInfrastructureSheet(pkg, req);
        BuildComponentsSheet(pkg, req);

        return pkg.GetAsByteArray();
    }

    // Окремий аркуш зі звіркою розрахунку з вимогами документа (лише для MS SQL Server).
    private static void BuildDocComparisonSheet(ExcelPackage pkg, ResourceRequirement req, ProjectConfig config)
    {
        var items = Data.DocumentRequirements.Compare(req, config);
        if (items.Count == 0) return;
        var ws = pkg.Workbook.Worksheets.Add("Звірка з вимогами");
        ws.Cells[1, 1].Value = Data.DocumentRequirements.Source;
        ws.Cells[1, 1, 1, 4].Merge = true;
        ws.Cells[1, 1].Style.Font.Bold = true;
        string[] headers = { "Показник (сервер БД)", "За документом", "Розрахунок", "Статус" };
        for (int c = 0; c < headers.Length; c++)
        {
            var cell = ws.Cells[2, c + 1];
            cell.Value = headers[c];
            cell.Style.Font.Bold = true;
            cell.Style.Font.Color.SetColor(System.Drawing.Color.White);
            cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
            cell.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(30, 102, 245));
        }
        int row = 3;
        foreach (var it in items)
        {
            ws.Cells[row, 1].Value = it.Metric;
            ws.Cells[row, 2].Value = it.Document;
            ws.Cells[row, 3].Value = it.Calculated;
            ws.Cells[row, 4].Value = it.Status;
            ws.Cells[row, 4].Style.Font.Color.SetColor(it.Status == "Відповідає"
                ? System.Drawing.Color.FromArgb(64, 160, 43)
                : System.Drawing.Color.FromArgb(210, 15, 57));
            row++;
        }
        ws.Cells[ws.Dimension.Address].AutoFitColumns();
    }

    // Аркуш із розбивкою ВМ для кожного середовища (PROD/DEV/TEST/PreProd).
    private static void BuildEnvironmentVmsSheet(ExcelPackage pkg, IReadOnlyList<EnvironmentReport> environments)
    {
        var ws = pkg.Workbook.Worksheets.Add("ВМ по середовищах");
        string[] headers = { "Середовище", "Вузол", "ОС", "Версія СУБД", "CPU", "RAM (ГБ)", "К-сть",
            "Диск/вузол (ГБ)", "Диск разом (ГБ)", "IOPS", "Примітки" };
        WriteHeader(ws, headers);
        int row = 2;
        foreach (var e in environments)
        {
            foreach (var n in e.Requirement.Infrastructure.Where(x => x.NodeCount > 0))
            {
                ws.Cells[row, 1].Value = e.Name;
                ws.Cells[row, 2].Value = n.Name;
                ws.Cells[row, 3].Value = n.Os;
                ws.Cells[row, 4].Value = string.IsNullOrEmpty(n.DbVersion) ? "—" : n.DbVersion;
                ws.Cells[row, 5].Value = n.Cpu;
                ws.Cells[row, 6].Value = n.RamGb;
                ws.Cells[row, 7].Value = n.NodeCount;
                ws.Cells[row, 8].Value = n.DiskPerNodeGb;
                ws.Cells[row, 9].Value = n.TotalStorageGb;
                ws.Cells[row, 10].Value = n.Iops > 0 ? n.Iops : (object)"—";
                ws.Cells[row, 11].Value = n.Notes;
                row++;
            }
        }
        ws.Cells[ws.Dimension.Address].AutoFitColumns();
    }

    private static void BuildEnvironmentsSheet(ExcelPackage pkg, IReadOnlyList<EnvironmentReport> environments)
    {
        var ws = pkg.Workbook.Worksheets.Add("Середовища");
        string[] headers = { "Середовище", "Ліцензій", "CPU", "RAM (ГБ)", "Диски (ГБ)", "IOPS (БД)", "ВМ (серверів)" };
        WriteHeader(ws, headers);

        int row = 2;
        foreach (var e in environments)
        {
            ws.Cells[row, 1].Value = e.Name;
            ws.Cells[row, 2].Value = e.UserCount;
            ws.Cells[row, 3].Value = e.Cpu;
            ws.Cells[row, 4].Value = e.RamGb;
            ws.Cells[row, 5].Value = e.StorageGb;
            ws.Cells[row, 6].Value = e.Iops;
            ws.Cells[row, 7].Value = e.Nodes;
            row++;
        }
        ws.Cells[ws.Dimension.Address].AutoFitColumns();
    }

    private static void BuildSummarySheet(ExcelPackage pkg, ResourceRequirement req, ProjectConfig config)
    {
        var ws = pkg.Workbook.Worksheets.Add("Підсумок");
        int r = 1;
        ws.Cells[r, 1].Value = $"{config.ProjectName} — Звіт про ресурси";
        ws.Cells[r, 1, r, 2].Merge = true;
        ws.Cells[r, 1].Style.Font.Size = 14;
        ws.Cells[r, 1].Style.Font.Bold = true;
        r += 2;

        void Kv(string k, object v)
        {
            ws.Cells[r, 1].Value = k;
            ws.Cells[r, 1].Style.Font.Bold = true;
            ws.Cells[r, 2].Value = v;
            r++;
        }

        Kv("Користувачів", config.UserCount);
        Kv("Розгортання", DeployName(config.DeploymentType));
        Kv("Профіль навантаження", config.LoadProfile.ToString());
        Kv("СКБД", DbName(config.DatabaseType));
        Kv("Обсяг даних БД (ГБ)", config.DbDataSizeGb);
        r++;
        Kv("Всього CPU (ядер)", Math.Round(req.TotalCpu, 1));
        Kv("Всього RAM (ГБ)", Math.Round(req.TotalRamGb, 1));
        Kv("Всього диски (ГБ)", req.TotalStorageGb);
        Kv("IOPS (БД)", req.TotalIops);
        Kv("Всього ВМ (серверів)", req.Infrastructure.Sum(n => n.NodeCount));
        if (req.PodCpu > 0)
        {
            r++;
            Kv("Запит подів CPU", Math.Round(req.PodCpu, 1));
            Kv("Запит подів RAM (ГБ)", Math.Round(req.PodRamGb, 1));
            Kv("Подів усього", TotalPods(req));
            Kv("Worker-вузлів", WorkerNodes(req));
            r++;
            ws.Cells[r, 1].Value = PodDistribution(req);
            ws.Cells[r, 1, r, 2].Merge = true;
            ws.Cells[r, 1].Style.WrapText = true;
        }
        ws.Cells[ws.Dimension.Address].AutoFitColumns();
        ws.Column(1).Width = 26;
    }

    private static void BuildInfrastructureSheet(ExcelPackage pkg, ResourceRequirement req)
    {
        var ws = pkg.Workbook.Worksheets.Add("Інфраструктура");
        string[] headers = { "Вузол", "ОС", "Версія СУБД", "CPU", "RAM (ГБ)", "К-сть", "Тип диску",
            "Диск/вузол (ГБ)", "Диск разом (ГБ)", "Page file (ГБ)", "IOPS", "Затримка (мс)", "Примітки" };
        WriteHeader(ws, headers);

        int row = 2;
        foreach (var n in req.Infrastructure.Where(x => x.NodeCount > 0))
        {
            ws.Cells[row, 1].Value = n.Name;
            ws.Cells[row, 2].Value = n.Os;
            ws.Cells[row, 3].Value = string.IsNullOrEmpty(n.DbVersion) ? "—" : n.DbVersion;
            ws.Cells[row, 4].Value = n.Cpu;
            ws.Cells[row, 5].Value = n.RamGb;
            ws.Cells[row, 6].Value = n.NodeCount;
            ws.Cells[row, 7].Value = n.StorageType;
            ws.Cells[row, 8].Value = n.DiskPerNodeGb;
            ws.Cells[row, 9].Value = n.TotalStorageGb;
            ws.Cells[row, 10].Value = n.PageFileGb > 0 ? n.PageFileGb : (object)"—";
            ws.Cells[row, 11].Value = n.Iops > 0 ? n.Iops : (object)"—";
            ws.Cells[row, 12].Value = n.Latency > 0 ? n.Latency : (object)"—";
            ws.Cells[row, 13].Value = n.Notes;
            row++;
        }
        // Підсумковий рядок.
        ws.Cells[row, 1].Value = "Разом";
        ws.Cells[row, 4].Value = Math.Round(req.TotalCpu, 1);
        ws.Cells[row, 5].Value = Math.Round(req.TotalRamGb, 1);
        ws.Cells[row, 6].Value = req.Infrastructure.Sum(n => n.NodeCount);
        ws.Cells[row, 9].Value = req.TotalStorageGb;
        ws.Cells[row, 1, row, 13].Style.Font.Bold = true;
        ws.Cells[row, 1, row, 13].Style.Fill.PatternType = ExcelFillStyle.Solid;
        ws.Cells[row, 1, row, 13].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(220, 224, 232));

        ws.Cells[ws.Dimension.Address].AutoFitColumns();
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
            row++;
        }
        ws.Cells[row, 1].Value = "Разом";
        ws.Cells[row, 5].Value = comps.Sum(c => c.Replicas);
        ws.Cells[row, 6].Value = Math.Round(comps.Sum(c => c.Cpu), 1);
        ws.Cells[row, 7].Value = Math.Round(comps.Sum(c => c.RamGb), 1);
        ws.Cells[row, 1, row, 7].Style.Font.Bold = true;
        ws.Cells[row, 1, row, 7].Style.Fill.PatternType = ExcelFillStyle.Solid;
        ws.Cells[row, 1, row, 7].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(220, 224, 232));

        ws.Cells[ws.Dimension.Address].AutoFitColumns();
    }

    private static void WriteHeader(ExcelWorksheet ws, string[] headers)
    {
        for (int c = 0; c < headers.Length; c++)
        {
            var cell = ws.Cells[1, c + 1];
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

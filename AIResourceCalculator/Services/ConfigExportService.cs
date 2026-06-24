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
        sb.AppendLine($"<h1>{SanitizeHtml(config.ProjectName)} — Звіт про ресурси</h1>");
        sb.AppendLine($"<p>Користувачів: {config.UserCount} | Розгортання: {SanitizeHtml(DeployName(config.DeploymentType))} | Профіль: {config.LoadProfile} | СКБД: {DbName(config.DatabaseType)}</p>");
        sb.AppendLine("<div class='kpi'>");
        sb.AppendLine($"<div class='kpi-box' style='background:#1e66f5'><h3>CPU</h3><p>{req.TotalCpu:F1}</p></div>");
        sb.AppendLine($"<div class='kpi-box' style='background:#40a02b'><h3>RAM</h3><p>{req.TotalRamGb:F1} GB</p></div>");
        sb.AppendLine($"<div class='kpi-box' style='background:#fe640b'><h3>Диски</h3><p>{req.TotalStorageGb} GB</p></div>");
        sb.AppendLine($"<div class='kpi-box' style='background:#8839ef'><h3>IOPS</h3><p>{req.TotalIops}</p></div>");
        sb.AppendLine("</div>");
        var dist = PodDistribution(req);
        if (dist.Length > 0)
            sb.AppendLine($"<p><i>{SanitizeHtml(dist)}</i></p>");

        if (environments != null && environments.Count > 1)
        {
            sb.AppendLine("<h2>Середовища</h2><table><tr><th>Середовище</th><th>Ліцензій</th><th>CPU</th><th>RAM (ГБ)</th><th>Диски (ГБ)</th><th>IOPS</th><th>Вузли</th></tr>");
            foreach (var e in environments)
                sb.AppendLine($"<tr><td>{SanitizeHtml(e.Name)}</td><td>{e.UserCount}</td><td>{e.Cpu:F1}</td><td>{e.RamGb:F1}</td><td>{e.StorageGb}</td><td>{e.Iops}</td><td>{e.Nodes}</td></tr>");
            sb.AppendLine("</table>");
        }

        sb.AppendLine("<h2>Інфраструктура</h2><table><tr><th>Вузол</th><th>ОС</th><th>CPU</th><th>RAM</th><th>К-сть</th><th>Тип диску</th><th>Диск/вузол</th><th>Диск разом</th><th>Page file</th><th>IOPS</th><th>Затримка, мс</th><th>Примітки</th></tr>");
        foreach (var i in req.Infrastructure.Where(n => n.NodeCount > 0))
            sb.AppendLine($"<tr><td>{SanitizeHtml(i.Name)}</td><td>{SanitizeHtml(i.Os)}</td><td>{i.Cpu}</td><td>{i.RamGb}</td><td>{i.NodeCount}</td><td>{SanitizeHtml(i.StorageType)}</td><td>{i.DiskPerNodeGb} GB</td><td>{i.TotalStorageGb} GB</td><td>{(i.PageFileGb > 0 ? i.PageFileGb + " GB" : "—")}</td><td>{(i.Iops > 0 ? i.Iops.ToString() : "—")}</td><td>{(i.Latency > 0 ? Trim(i.Latency) : "—")}</td><td>{SanitizeHtml(i.Notes)}</td></tr>");
        sb.AppendLine($"<tfoot><tr><td>Разом</td><td></td><td>{req.TotalCpu:F1}</td><td>{req.TotalRamGb:F1}</td><td>{req.Infrastructure.Sum(n => n.NodeCount)}</td><td></td><td></td><td>{req.TotalStorageGb} GB</td><td></td><td></td><td></td><td></td></tr></tfoot>");
        sb.AppendLine("</table>");

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

    // ───────────────────────────── XML ─────────────────────────────
    public string ExportXml(ResourceRequirement req, ProjectConfig config,
        IReadOnlyList<EnvironmentReport>? environments = null)
    {
        static XAttribute D(string name, double v) => new(name, v.ToString("0.##", CultureInfo.InvariantCulture));

        var infra = new XElement("Infrastructure",
            req.Infrastructure.Where(n => n.NodeCount > 0).Select(n => new XElement("Node",
                new XAttribute("name", n.Name),
                new XAttribute("os", n.Os),
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
                new XAttribute("notes", n.Notes))));

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
                    new XAttribute("nodes", e.Nodes))));
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
                    new XAttribute("iops", req.TotalIops),
                    new XAttribute("nodes", req.Infrastructure.Sum(n => n.NodeCount))),
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
        IReadOnlyList<EnvironmentReport>? environments = null)
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        using var pkg = new ExcelPackage();

        BuildSummarySheet(pkg, req, config);
        if (environments != null && environments.Count > 1)
            BuildEnvironmentsSheet(pkg, environments);
        BuildInfrastructureSheet(pkg, req);
        BuildComponentsSheet(pkg, req);

        return pkg.GetAsByteArray();
    }

    private static void BuildEnvironmentsSheet(ExcelPackage pkg, IReadOnlyList<EnvironmentReport> environments)
    {
        var ws = pkg.Workbook.Worksheets.Add("Середовища");
        string[] headers = { "Середовище", "Ліцензій", "CPU", "RAM (ГБ)", "Диски (ГБ)", "IOPS", "Вузли" };
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
        r++;
        Kv("Всього CPU (ядер)", Math.Round(req.TotalCpu, 1));
        Kv("Всього RAM (ГБ)", Math.Round(req.TotalRamGb, 1));
        Kv("Всього диски (ГБ)", req.TotalStorageGb);
        Kv("Всього IOPS", req.TotalIops);
        Kv("Всього вузлів", req.Infrastructure.Sum(n => n.NodeCount));
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
        string[] headers = { "Вузол", "ОС", "CPU", "RAM (ГБ)", "К-сть", "Тип диску",
            "Диск/вузол (ГБ)", "Диск разом (ГБ)", "Page file (ГБ)", "IOPS", "Затримка (мс)", "Примітки" };
        WriteHeader(ws, headers);

        int row = 2;
        foreach (var n in req.Infrastructure.Where(x => x.NodeCount > 0))
        {
            ws.Cells[row, 1].Value = n.Name;
            ws.Cells[row, 2].Value = n.Os;
            ws.Cells[row, 3].Value = n.Cpu;
            ws.Cells[row, 4].Value = n.RamGb;
            ws.Cells[row, 5].Value = n.NodeCount;
            ws.Cells[row, 6].Value = n.StorageType;
            ws.Cells[row, 7].Value = n.DiskPerNodeGb;
            ws.Cells[row, 8].Value = n.TotalStorageGb;
            ws.Cells[row, 9].Value = n.PageFileGb > 0 ? n.PageFileGb : (object)"—";
            ws.Cells[row, 10].Value = n.Iops > 0 ? n.Iops : (object)"—";
            ws.Cells[row, 11].Value = n.Latency > 0 ? n.Latency : (object)"—";
            ws.Cells[row, 12].Value = n.Notes;
            row++;
        }
        // Підсумковий рядок.
        ws.Cells[row, 1].Value = "Разом";
        ws.Cells[row, 3].Value = Math.Round(req.TotalCpu, 1);
        ws.Cells[row, 4].Value = Math.Round(req.TotalRamGb, 1);
        ws.Cells[row, 5].Value = req.Infrastructure.Sum(n => n.NodeCount);
        ws.Cells[row, 8].Value = req.TotalStorageGb;
        ws.Cells[row, 1, row, 12].Style.Font.Bold = true;
        ws.Cells[row, 1, row, 12].Style.Fill.PatternType = ExcelFillStyle.Solid;
        ws.Cells[row, 1, row, 12].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(220, 224, 232));

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

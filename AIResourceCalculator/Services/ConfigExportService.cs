using OfficeOpenXml;
using OfficeOpenXml.Style;
using AIResourceCalculator.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AIResourceCalculator.Services;

// Формує два звіти про пораховані ресурси: Excel (.xlsx) та PDF — для тендерних/переддоговірних
// документів. Прив'язки до хмарних провайдерів немає — лише обчислені вимоги (CPU/RAM/диски/IOPS).
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

    // Пояснення (UI/Excel/PDF), чому середовища з близькою к-стю користувачів мають однакові поди.
    private const string PodScalingNote =
        "Поди масштабуються блоками (на кожні 25/50/100 користувачів, мінімум 1 репліка), тож середовища " +
        "з близькою кількістю користувачів можуть мати однакові компоненти — відмінності проявляються у вузлі БД.";

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

    private static string ReportTitle(ProjectConfig config)
        => $"Розрахунок інфраструктури — {config.UserCount} користувачів, {DeployName(config.DeploymentType)}";

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

    // ───────────────────────────── PDF ─────────────────────────────
    // Кольорова палітра звіту (узгоджена з UI-темою Catppuccin Latte).
    private const string PdfAccent = "#1E66F5";
    private const string PdfGreen  = "#40A02B";
    private const string PdfOrange = "#FE640B";
    private const string PdfPurple = "#8839EF";
    private const string PdfRed    = "#D20F39";
    private const string PdfInk    = "#3C3F58";
    private const string PdfMuted  = "#6C6F85";
    private const string PdfBorder = "#BCC0CC";
    private const string PdfZebra  = "#F2F4FA";
    private const string PdfHeadBg = "#E6ECFE";

    public byte[] ExportPdf(ResourceRequirement req, ProjectConfig config,
        IReadOnlyList<EnvironmentReport>? environments = null,
        IEnumerable<UserLoadRange>? matrixRanges = null)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        bool multiEnv = environments != null && environments.Count > 1;

        var pdf = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(26);
                page.DefaultTextStyle(t => t.FontSize(9).FontColor(PdfInk).FontFamily("Segoe UI", "Arial"));

                page.Header().Element(h => ComposePdfHeader(h, config));
                page.Content().PaddingTop(10).Column(col =>
                {
                    col.Spacing(12);
                    col.Item().Element(c => ComposePdfKpis(c, req));
                    col.Item().Element(c => ComposePdfParams(c, config));

                    if (multiEnv)
                    {
                        col.Item().Element(c => PdfSectionTitle(c, "Зведення за середовищами"));
                        col.Item().Element(c => ComposePdfEnvSummary(c, environments!));
                        col.Item().Text(PodScalingNote).FontSize(8).Italic().FontColor(PdfMuted);
                        foreach (var e in environments!)
                        {
                            col.Item().PaddingTop(4).Element(c => PdfSectionTitle(c,
                                $"Середовище {e.Name} — сервери (користувачів: {e.UserCount})"));
                            if (!string.IsNullOrEmpty(e.ModulesInfo))
                                col.Item().Text($"Модулі: {e.ModulesInfo}").FontSize(8).Italic().FontColor(PdfMuted);
                            col.Item().Element(c => ComposePdfInfraTable(c, e.Requirement));
                            col.Item().Element(c => ComposePdfComponents(c, e.Requirement,
                                $"Компоненти (поди) середовища {e.Name}"));
                        }
                    }
                    else
                    {
                        col.Item().Element(c => PdfSectionTitle(c, "Інфраструктура — сервери (віртуальні машини)"));
                        col.Item().Element(c => ComposePdfInfraTable(c, req));
                        col.Item().Element(c => ComposePdfComponents(c, req));
                    }

                    col.Item().PaddingTop(4).Element(c => PdfSectionTitle(c, "Пояснення показників"));
                    col.Item().Element(ComposePdfGlossary);
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.DefaultTextStyle(s => s.FontSize(8).FontColor(PdfMuted));
                    t.Span("Розрахунок апаратних ресурсів IT-Enterprise · сторінка ");
                    t.CurrentPageNumber();
                    t.Span(" / ");
                    t.TotalPages();
                });
            });
        });

        return pdf.GeneratePdf();
    }

    private static void ComposePdfHeader(IContainer c, ProjectConfig config)
    {
        c.BorderBottom(2).BorderColor(PdfAccent).PaddingBottom(6).Column(col =>
        {
            col.Item().Text(ReportTitle(config)).FontSize(16).Bold().FontColor(PdfAccent);
            col.Item().Text($"Продукт: {ProductName(config.ProductType)}  ·  Розгортання: {DeployName(config.DeploymentType)}  ·  " +
                            $"Профіль: {ProfileName(config.LoadProfile)}  ·  СКБД: {DbName(config.DatabaseType)}")
                .FontSize(9).FontColor(PdfMuted);
        });
    }

    private static void ComposePdfKpis(IContainer c, ResourceRequirement req)
    {
        void Kpi(IContainer box, string color, string value, string label) =>
            box.Background(color).CornerRadius(6).Padding(8).Column(x =>
            {
                x.Item().Text(value).FontSize(17).Bold().FontColor("#FFFFFF");
                x.Item().Text(label).FontSize(8).FontColor("#FFFFFF");
            });

        c.Row(row =>
        {
            row.Spacing(8);
            row.RelativeItem().Element(b => Kpi(b, PdfAccent, $"{req.TotalCpu:0.#}", "CPU — ядер всього"));
            row.RelativeItem().Element(b => Kpi(b, PdfGreen, $"{req.TotalRamGb:0.#} ГБ", "RAM — пам'яті всього"));
            row.RelativeItem().Element(b => Kpi(b, PdfOrange, $"{req.TotalStorageGb} ГБ", "Диски — сховища всього"));
            row.RelativeItem().Element(b => Kpi(b, PdfPurple, $"{req.TotalIops}", "IOPS — швидкодія БД"));
            row.RelativeItem().Element(b => Kpi(b, PdfRed, $"{req.Infrastructure.Sum(n => n.NodeCount)}", "ВМ — серверів всього"));
        });
    }

    private static void ComposePdfParams(IContainer c, ProjectConfig config)
    {
        c.Text(t =>
        {
            t.Span("Документ описує, яке обладнання (сервери) потрібно підготувати для роботи системи на ");
            t.Span($"{config.UserCount}").Bold();
            t.Span(" користувачів. Нижче — підсумкові потреби, перелік серверів з призначенням і ресурсами та пояснення показників.");
        });
    }

    private static void PdfSectionTitle(IContainer c, string text) =>
        c.BorderBottom(1).BorderColor(PdfBorder).PaddingBottom(3)
         .Text(text).FontSize(12).Bold().FontColor(PdfAccent);

    private static void ComposePdfEnvSummary(IContainer c, IReadOnlyList<EnvironmentReport> envs)
    {
        c.Table(t =>
        {
            t.ColumnsDefinition(d =>
            {
                d.RelativeColumn(1.2f); d.RelativeColumn(1); d.RelativeColumn(3);
                d.RelativeColumn(0.9f); d.RelativeColumn(1); d.RelativeColumn(1); d.RelativeColumn(1); d.RelativeColumn(1);
            });
            t.Header(h =>
            {
                PdfHead(h.Cell(), "Середовище"); PdfHead(h.Cell(), "Користувачів"); PdfHead(h.Cell(), "Модулі (користувачів)");
                PdfHead(h.Cell(), "CPU"); PdfHead(h.Cell(), "RAM, ГБ"); PdfHead(h.Cell(), "Диски, ГБ");
                PdfHead(h.Cell(), "IOPS (БД)"); PdfHead(h.Cell(), "ВМ");
            });
            int i = 0;
            foreach (var e in envs)
            {
                bool z = i++ % 2 == 1;
                PdfData(t.Cell(), e.Name, z, bold: true); PdfData(t.Cell(), e.UserCount.ToString(), z, center: true);
                PdfData(t.Cell(), e.ModulesInfo, z); PdfData(t.Cell(), $"{e.Cpu:0.#}", z, center: true);
                PdfData(t.Cell(), $"{e.RamGb:0.#}", z, center: true); PdfData(t.Cell(), e.StorageGb.ToString(), z, center: true);
                PdfData(t.Cell(), e.Iops.ToString(), z, center: true); PdfData(t.Cell(), e.Nodes.ToString(), z, center: true);
            }
        });
    }

    private static void ComposePdfInfraTable(IContainer c, ResourceRequirement req)
    {
        var nodes = req.Infrastructure.Where(n => n.NodeCount > 0).ToList();
        c.Table(t =>
        {
            t.ColumnsDefinition(d =>
            {
                d.RelativeColumn(2.1f);  // Сервер
                d.RelativeColumn(2.7f);  // Призначення
                d.RelativeColumn(0.6f);  // CPU
                d.RelativeColumn(0.7f);  // RAM
                d.RelativeColumn(0.6f);  // К-сть
                d.RelativeColumn(1.1f);  // Диск
                d.RelativeColumn(0.8f);  // PageFile
                d.RelativeColumn(1.3f);  // IOPS (профіль)
                d.RelativeColumn(0.7f);  // MiB/s
                d.RelativeColumn(0.8f);  // Затримка
                d.RelativeColumn(1.3f);  // ОС
                d.RelativeColumn(2.0f);  // СУБД / примітки
            });
            t.Header(h =>
            {
                PdfHead(h.Cell(), "Сервер (ВМ)"); PdfHead(h.Cell(), "Призначення");
                PdfHead(h.Cell(), "CPU"); PdfHead(h.Cell(), "RAM"); PdfHead(h.Cell(), "К-сть");
                PdfHead(h.Cell(), "Диск (тип · ГБ)"); PdfHead(h.Cell(), "PageFile");
                PdfHead(h.Cell(), "IOPS (профіль)"); PdfHead(h.Cell(), "MiB/s"); PdfHead(h.Cell(), "Затр., мс");
                PdfHead(h.Cell(), "ОС"); PdfHead(h.Cell(), "СУБД / примітки");
            });
            int i = 0;
            foreach (var n in nodes)
            {
                bool z = i++ % 2 == 1;
                var disk = n.DiskPerNodeGb > 0 ? $"{n.StorageType} · {n.TotalStorageGb}" : "—";
                var iops = n.Iops > 0 ? $"{n.Iops}\n{n.IopsProfile}" : "";
                var sub = string.Join("\n", new[] { n.DbVersion, n.Notes }.Where(s => !string.IsNullOrWhiteSpace(s)));
                PdfData(t.Cell(), n.Name, z, bold: true);
                PdfData(t.Cell(), NodeRole(n.Name), z);
                PdfData(t.Cell(), $"{n.Cpu:0.#}", z, center: true);
                PdfData(t.Cell(), $"{n.RamGb:0.#}", z, center: true);
                PdfData(t.Cell(), n.NodeCount.ToString(), z, center: true);
                PdfData(t.Cell(), disk, z, center: true);
                PdfData(t.Cell(), n.PageFileGb > 0 ? n.PageFileGb.ToString() : "", z, center: true);
                PdfData(t.Cell(), iops, z, center: true);
                PdfData(t.Cell(), n.ThroughputMiBs > 0 ? n.ThroughputMiBs.ToString() : "", z, center: true);
                PdfData(t.Cell(), n.Latency > 0 ? $"{n.Latency:0.#}" : "", z, center: true);
                PdfData(t.Cell(), n.Os, z);
                PdfData(t.Cell(), sub, z);
            }
            // Підсумок.
            PdfTotal(t.Cell(), "Разом"); PdfTotal(t.Cell(), "");
            PdfTotal(t.Cell(), $"{req.TotalCpu:0.#}", center: true);
            PdfTotal(t.Cell(), $"{req.TotalRamGb:0.#}", center: true);
            PdfTotal(t.Cell(), req.Infrastructure.Sum(n => n.NodeCount).ToString(), center: true);
            PdfTotal(t.Cell(), $"{req.TotalStorageGb}", center: true);
            PdfTotal(t.Cell(), ""); PdfTotal(t.Cell(), ""); PdfTotal(t.Cell(), ""); PdfTotal(t.Cell(), "");
            PdfTotal(t.Cell(), ""); PdfTotal(t.Cell(), "");
        });
    }

    private static void ComposePdfComponents(IContainer c, ResourceRequirement req, string title = "Компоненти (поди)")
    {
        var comps = req.Components.Where(x => x.Cpu > 0).ToList();
        if (comps.Count == 0) { c.Text(""); return; }
        c.Column(col =>
        {
            col.Spacing(4);
            col.Item().PaddingTop(4).Element(x => PdfSectionTitle(x, title));
            col.Item().Table(t =>
            {
                t.ColumnsDefinition(d =>
                {
                    d.RelativeColumn(2.6f); d.RelativeColumn(1.6f);
                    d.RelativeColumn(1); d.RelativeColumn(1); d.RelativeColumn(0.8f); d.RelativeColumn(1); d.RelativeColumn(1);
                });
                t.Header(h =>
                {
                    PdfHead(h.Cell(), "Назва"); PdfHead(h.Cell(), "Категорія");
                    PdfHead(h.Cell(), "CPU/репл."); PdfHead(h.Cell(), "RAM/репл."); PdfHead(h.Cell(), "Реплік");
                    PdfHead(h.Cell(), "CPU разом"); PdfHead(h.Cell(), "RAM разом");
                });
                int i = 0;
                foreach (var x in comps)
                {
                    bool z = i++ % 2 == 1;
                    PdfData(t.Cell(), x.Name, z); PdfData(t.Cell(), x.Category, z);
                    PdfData(t.Cell(), $"{x.CpuPerReplica:0.##}", z, center: true);
                    PdfData(t.Cell(), $"{x.RamPerReplicaGb:0.##}", z, center: true);
                    PdfData(t.Cell(), x.Replicas.ToString(), z, center: true);
                    PdfData(t.Cell(), $"{x.Cpu:0.##}", z, center: true);
                    PdfData(t.Cell(), $"{x.RamGb:0.##}", z, center: true);
                }
                PdfTotal(t.Cell(), "Разом"); PdfTotal(t.Cell(), "");
                PdfTotal(t.Cell(), ""); PdfTotal(t.Cell(), "");
                PdfTotal(t.Cell(), comps.Sum(x => x.Replicas).ToString(), center: true);
                PdfTotal(t.Cell(), $"{comps.Sum(x => x.Cpu):0.##}", center: true);
                PdfTotal(t.Cell(), $"{comps.Sum(x => x.RamGb):0.##}", center: true);
            });
        });
    }

    private static void ComposePdfGlossary(IContainer c)
    {
        (string, string)[] items =
        {
            ("CPU (ядер)", "обчислювальна потужність процесора"),
            ("RAM", "оперативна пам'ять; найважливіша для сервера БД"),
            ("Диски", "обсяг сховища під ОС, дані, журнали та копії"),
            ("IOPS", "швидкодія диска; вказується для сервера БД (не сумується між дисками)"),
            ("Профіль IOPS", "співвідношення читання/запису (напр. 50r/50w)"),
            ("MiB/s", "пропускна здатність диска (послідовні операції)"),
            ("Затримка", "час відповіді диска, мс; менше — краще"),
            ("PageFile", "файл підкачки для серверів застосунків/веб"),
        };
        c.Column(col =>
        {
            foreach (var (term, desc) in items)
                col.Item().Text(t =>
                {
                    t.DefaultTextStyle(s => s.FontSize(8));
                    t.Span($"{term} — ").Bold().FontColor(PdfInk);
                    t.Span(desc).FontColor(PdfMuted);
                });
        });
    }

    private static void PdfHead(IContainer c, string text) =>
        c.Background(PdfAccent).BorderColor(PdfBorder).Border(0.5f).PaddingVertical(3).PaddingHorizontal(4)
         .Text(text).FontSize(8).Bold().FontColor("#FFFFFF");

    private static void PdfData(IContainer c, string text, bool zebra, bool center = false, bool bold = false)
    {
        var cell = c.Background(zebra ? PdfZebra : "#FFFFFF").BorderColor(PdfBorder).Border(0.5f)
                    .PaddingVertical(2).PaddingHorizontal(4);
        if (center) cell = cell.AlignCenter();
        var span = cell.Text(text ?? "").FontSize(8);
        if (bold) span.Bold();
    }

    private static void PdfTotal(IContainer c, string text, bool center = false)
    {
        var cell = c.Background(PdfHeadBg).BorderColor(PdfBorder).Border(0.5f).PaddingVertical(3).PaddingHorizontal(4);
        if (center) cell = cell.AlignCenter();
        cell.Text(text ?? "").FontSize(8).Bold().FontColor(PdfInk);
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
        BuildInfrastructureSheet(pkg, req, multiEnv ? environments : null);
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
                // Числові/кодові стовпці — по центру; власне числа — ще й жирним.
                ws.Cells[row, 3, row, 11].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                ws.Cells[row, 3, row, 8].Style.Font.Bold = true;
                ws.Cells[row, 10, row, 11].Style.Font.Bold = true;
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
        // Кожне середовище займає 3 стовпці даних + 1 порожній стовпець-розділювач (крім останнього),
        // щоб таблиці середовищ візуально не зливались в одну.
        int Col0(int i) => 3 + i * 4;

        Head(ws.Cells[1, 1, 2, 1], "Назва", merge: true);
        Head(ws.Cells[1, 2, 2, 2], "Категорія", merge: true);
        for (int i = 0; i < envs.Count; i++)
        {
            int c0 = Col0(i);
            Head(ws.Cells[1, c0, 1, c0 + 2], envs[i].Name, merge: true);
            Head(ws.Cells[2, c0], "Реплік");
            Head(ws.Cells[2, c0 + 1], "CPU");
            Head(ws.Cells[2, c0 + 2], "RAM");
        }

        int row = 3;
        foreach (var (cat, name) in order)
        {
            ws.Cells[row, 1].Value = name;   // назва — зліва (типово)
            ws.Cells[row, 2].Value = cat;    // категорія — зліва (типово)
            for (int i = 0; i < envs.Count; i++)
            {
                int c0 = Col0(i);
                var comp = envs[i].Components.FirstOrDefault(x => x.Category == cat && x.Name == name);
                if (comp != null)
                {
                    ws.Cells[row, c0].Value = comp.Replicas;
                    ws.Cells[row, c0 + 1].Value = Math.Round(comp.Cpu, 2);
                    ws.Cells[row, c0 + 2].Value = Math.Round(comp.RamGb, 2);
                }
                // Числа — по центру і жирним.
                ws.Cells[row, c0, row, c0 + 2].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                ws.Cells[row, c0, row, c0 + 2].Style.Font.Bold = true;
            }
            row++;
        }
        // Підсумковий рядок «Разом» по кожному середовищу.
        ws.Cells[row, 1].Value = "Разом";
        for (int i = 0; i < envs.Count; i++)
        {
            int c0 = Col0(i);
            ws.Cells[row, c0].Value = envs[i].Components.Sum(c => c.Replicas);
            ws.Cells[row, c0 + 1].Value = Math.Round(envs[i].Components.Sum(c => c.Cpu), 2);
            ws.Cells[row, c0 + 2].Value = Math.Round(envs[i].Components.Sum(c => c.RamGb), 2);
            ws.Cells[row, c0, row, c0 + 2].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
        }

        // Заливка/жирний підсумку — посегментно (Назва+Категорія та по 3 стовпці кожного середовища),
        // щоб стовпці-розділювачі лишались порожніми.
        var grey = System.Drawing.Color.FromArgb(220, 224, 232);
        void FillTotal(ExcelRange r)
        {
            r.Style.Font.Bold = true;
            r.Style.Fill.PatternType = ExcelFillStyle.Solid;
            r.Style.Fill.BackgroundColor.SetColor(grey);
        }
        FillTotal(ws.Cells[row, 1, row, 2]);
        for (int i = 0; i < envs.Count; i++)
            FillTotal(ws.Cells[row, Col0(i), row, Col0(i) + 2]);

        // Рамки — теж посегментно (стовпці-розділювачі без рамок), потім автоширина і вузькі розділювачі.
        void Border(ExcelRange r)
        {
            r.Style.Border.Top.Style = ExcelBorderStyle.Thin;
            r.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
            r.Style.Border.Left.Style = ExcelBorderStyle.Thin;
            r.Style.Border.Right.Style = ExcelBorderStyle.Thin;
        }
        Border(ws.Cells[1, 1, row, 2]);
        for (int i = 0; i < envs.Count; i++)
            Border(ws.Cells[1, Col0(i), row, Col0(i) + 2]);

        // Примітка: чому середовища з близькою к-стю користувачів мають однакові компоненти.
        int noteRow = row + 2;
        int lastCol = Col0(envs.Count - 1) + 2;
        ws.Cells[noteRow, 1, noteRow, lastCol].Merge = true;
        ws.Cells[noteRow, 1].Value = PodScalingNote;
        ws.Cells[noteRow, 1].Style.Font.Italic = true;
        ws.Cells[noteRow, 1].Style.Font.Color.SetColor(System.Drawing.Color.FromArgb(108, 111, 133));
        ws.Cells[noteRow, 1].Style.WrapText = true;

        ws.Cells[1, 1, row, lastCol].AutoFitColumns();
        for (int i = 0; i < envs.Count - 1; i++)
            ws.Column(Col0(i) + 3).Width = 2.5; // вузький порожній стовпець-відступ
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
            // Числа — по центру і жирним; «Модулі (користувачів)» (стовпець 3) — текст, зліва.
            ws.Cells[row, 2].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            ws.Cells[row, 4, row, 8].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            ws.Cells[row, 2].Style.Font.Bold = true;
            ws.Cells[row, 4, row, 8].Style.Font.Bold = true;
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
            // Числові значення — жирним чорним (текстові, як назва продукту, лишаємо звичайними).
            if (v is int or long or double or decimal)
                ws.Cells[r, 2].Style.Font.Bold = true;
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

    private static void BuildInfrastructureSheet(ExcelPackage pkg, ResourceRequirement req,
        IReadOnlyList<EnvironmentReport>? environments = null)
    {
        var ws = pkg.Workbook.Worksheets.Add("Інфраструктура");

        if (environments != null && environments.Count > 1)
        {
            // По одному блоку-таблиці на кожне середовище (PROD/DEV/TEST/PreProd) — згори вниз.
            int row = 1;
            foreach (var e in environments)
            {
                row = WriteInfraBlock(ws, e.Requirement, row, $"Інфраструктура (сервери/ВМ) — середовище {e.Name}");
                row += 2; // порожні рядки-відступ між середовищами, щоб таблиці не зливались
            }
        }
        else
        {
            WriteInfraBlock(ws, req, 1, "Інфраструктура (сервери/ВМ) — середовище PROD");
        }

        ws.Cells[ws.Dimension.Address].AutoFitColumns();
    }

    // Один блок таблиці інфраструктури (для одного середовища), починаючи з рядка startRow.
    // Повертає номер наступного вільного рядка (одразу після підсумкового «Разом»).
    private static int WriteInfraBlock(ExcelWorksheet ws, ResourceRequirement req, int startRow, string title)
    {
        // Підпис середовища над таблицею.
        ws.Cells[startRow, 1].Value = title;
        ws.Cells[startRow, 1, startRow, 16].Merge = true;
        ws.Cells[startRow, 1].Style.Font.Bold = true;
        ws.Cells[startRow, 1].Style.Font.Size = 12;
        ws.Cells[startRow, 1].Style.Font.Color.SetColor(System.Drawing.Color.FromArgb(30, 102, 245));

        // Порядок колонок: ідентифікатор + числові характеристики спершу, описові
        // (Призначення/ОС/Версія СУБД) — у кінці перед примітками.
        string[] headers = { "Сервер (ВМ)", "CPU (ядер)", "RAM (ГБ)", "К-сть", "Тип диску",
            "Диск/сервер (ГБ)", "Диск разом (ГБ)", "Page file (ГБ)", "IOPS", "Профіль IOPS", "MiB/s", "Затримка (мс)",
            "Призначення", "ОС", "Версія СУБД", "Примітки" };
        int headerRow = startRow + 1;
        WriteHeader(ws, headers, headerRow);

        int row = headerRow + 1;
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
            // Числові/кодові стовпці — по центру; власне числа — ще й жирним. Назви/опис — зліва (типово).
            ws.Cells[row, 2, row, 12].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            ws.Cells[row, 2, row, 4].Style.Font.Bold = true;
            ws.Cells[row, 6, row, 9].Style.Font.Bold = true;
            ws.Cells[row, 11, row, 12].Style.Font.Bold = true;
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

        // Тонкі рамки лише на таблицю (від шапки до підсумку), щоб порожні рядки-відступ лишались чистими.
        var block = ws.Cells[headerRow, 1, row, 16];
        block.Style.Border.Top.Style = ExcelBorderStyle.Thin;
        block.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
        block.Style.Border.Left.Style = ExcelBorderStyle.Thin;
        block.Style.Border.Right.Style = ExcelBorderStyle.Thin;

        return row + 1;
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
            ws.Cells[row, 6].Value = Math.Round(c.Cpu, 2);
            ws.Cells[row, 7].Value = Math.Round(c.RamGb, 2);
            // Числа — по центру і жирним; Назва/Категорія — зліва (типово).
            ws.Cells[row, 3, row, 7].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            ws.Cells[row, 3, row, 7].Style.Font.Bold = true;
            row++;
        }
        ws.Cells[row, 1].Value = "Разом";
        ws.Cells[row, 5].Value = comps.Sum(c => c.Replicas);
        ws.Cells[row, 6].Value = Math.Round(comps.Sum(c => c.Cpu), 2);
        ws.Cells[row, 7].Value = Math.Round(comps.Sum(c => c.RamGb), 2);
        ws.Cells[row, 1, row, 7].Style.Font.Bold = true;
        ws.Cells[row, 1, row, 7].Style.Fill.PatternType = ExcelFillStyle.Solid;
        ws.Cells[row, 1, row, 7].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(220, 224, 232));

        StyleTable(ws);
    }

    // Рамки на всю таблицю + чергування рядків (зебра) + автоширина з обмеженням + закріплення
    // шапки та автофільтр. Робить просту таблицю охайною й зручною для перегляду.
    private static void StyleTable(ExcelWorksheet ws, int headerRow = 1)
    {
        var dim = ws.Dimension;
        if (dim == null) return;
        var cells = ws.Cells[dim.Address];
        cells.Style.Border.Top.Style = ExcelBorderStyle.Thin;
        cells.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
        cells.Style.Border.Left.Style = ExcelBorderStyle.Thin;
        cells.Style.Border.Right.Style = ExcelBorderStyle.Thin;
        cells.Style.VerticalAlignment = ExcelVerticalAlignment.Center;

        // Зебра — світло-блакитна заливка парних рядків даних (краще читається).
        var zebra = System.Drawing.Color.FromArgb(242, 244, 250);
        for (int r = headerRow + 1; r <= dim.End.Row; r++)
        {
            if ((r - headerRow) % 2 == 0)
            {
                var rng = ws.Cells[r, dim.Start.Column, r, dim.End.Column];
                rng.Style.Fill.PatternType = ExcelFillStyle.Solid;
                rng.Style.Fill.BackgroundColor.SetColor(zebra);
            }
        }

        cells.AutoFitColumns();
        // Обмежуємо надто широкі стовпці (довгі назви/примітки переносяться).
        for (int c = dim.Start.Column; c <= dim.End.Column; c++)
        {
            if (ws.Column(c).Width > 46)
            {
                ws.Column(c).Width = 46;
                ws.Cells[headerRow, c, dim.End.Row, c].Style.WrapText = true;
            }
        }

        // Закріплюємо шапку та вмикаємо автофільтр (зручно гортати й фільтрувати).
        ws.View.FreezePanes(headerRow + 1, 1);
        ws.Cells[headerRow, dim.Start.Column, dim.End.Row, dim.End.Column].AutoFilter = true;
    }

    private static void WriteHeader(ExcelWorksheet ws, string[] headers, int headerRow = 1)
    {
        for (int c = 0; c < headers.Length; c++)
        {
            var cell = ws.Cells[headerRow, c + 1];
            cell.Value = headers[c];
            cell.Style.Font.Bold = true;
            cell.Style.Font.Color.SetColor(System.Drawing.Color.White);
            cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            cell.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
            cell.Style.WrapText = true;
            cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
            cell.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(30, 102, 245));
        }
        ws.Row(headerRow).Height = 26;
    }

}

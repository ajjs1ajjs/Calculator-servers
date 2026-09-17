using ResourceCalculator.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using static ResourceCalculator.Services.ReportCommon;

namespace ResourceCalculator.Services;

// PDF-звіт (QuestPDF) — для тендерних/переддоговірних документів.
internal sealed class PdfReportBuilder
{
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
                                col.Item().Text($"Модулі: {Truncate(e.ModulesInfo, 2000)}").FontSize(8).Italic().FontColor(PdfMuted);
                            col.Item().Element(c => ComposePdfInfraTable(c, e.Requirement));
                            if (config.IncludeComponentsInReport)
                                col.Item().Element(c => ComposePdfComponents(c, e.Requirement,
                                    $"Компоненти (поди) середовища {e.Name}"));
                        }
                    }
                    else
                    {
                        col.Item().Element(c => PdfSectionTitle(c, "Інфраструктура — сервери (віртуальні машини)"));
                        col.Item().Element(c => ComposePdfInfraTable(c, req));
                        if (config.IncludeComponentsInReport)
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
            col.Item().Text($"Розгортання: {DeployName(config.DeploymentType)}  ·  СКБД: {DbName(config.DatabaseType)}")
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

    // Диск розписаний по частинах (OS/Logs/MainData/Content) прямо колонками таблиці — це і є
    // вимоги до дисків, без окремої секції/аркуша.
    private static string DiskCell(string type, int gb) => gb > 0 ? $"{type}\n{gb}" : "";

    private static void ComposePdfInfraTable(IContainer c, ResourceRequirement req)
    {
        var nodes = req.Infrastructure.Where(n => n.NodeCount > 0).ToList();
        c.Table(t =>
        {
            t.ColumnsDefinition(d =>
            {
                d.RelativeColumn(1.7f);  // Сервер
                d.RelativeColumn(2.0f);  // Призначення
                d.RelativeColumn(0.5f);  // CPU
                d.RelativeColumn(0.6f);  // ГГц
                d.RelativeColumn(0.6f);  // RAM
                d.RelativeColumn(0.5f);  // К-сть
                d.RelativeColumn(0.9f);  // Диск ОС
                d.RelativeColumn(0.9f);  // Диск Logs/TempDB
                d.RelativeColumn(0.9f);  // Диск MainData
                d.RelativeColumn(0.9f);  // Диск Content
                d.RelativeColumn(0.8f);  // Диск підкачки
                d.RelativeColumn(0.8f);  // Диск разом
                d.RelativeColumn(1.2f);  // IOPS (профіль)
                d.RelativeColumn(0.7f);  // MiB/s
                d.RelativeColumn(0.7f);  // Затримка
                d.RelativeColumn(1.1f);  // ОС
                d.RelativeColumn(1.6f);  // СУБД / примітки
            });
            t.Header(h =>
            {
                PdfHead(h.Cell(), "Сервер (ВМ)"); PdfHead(h.Cell(), "Призначення");
                PdfHead(h.Cell(), "CPU"); PdfHead(h.Cell(), "ГГц"); PdfHead(h.Cell(), "RAM"); PdfHead(h.Cell(), "К-сть");
                PdfHead(h.Cell(), "Диск ОС"); PdfHead(h.Cell(), "Logs/TempDB"); PdfHead(h.Cell(), "MainData"); PdfHead(h.Cell(), "Content");
                PdfHead(h.Cell(), "Підкачка"); PdfHead(h.Cell(), "Разом, ГБ");
                PdfHead(h.Cell(), "IOPS (профіль)"); PdfHead(h.Cell(), "MiB/s"); PdfHead(h.Cell(), "Затр., мс");
                PdfHead(h.Cell(), "ОС"); PdfHead(h.Cell(), "СУБД / примітки");
            });
            int i = 0;
            foreach (var n in nodes)
            {
                bool z = i++ % 2 == 1;
                var iops = n.Iops > 0 ? $"{n.Iops}\n{n.IopsProfile}" : "";
                var sub = string.Join("\n", new[] { n.DbVersion, n.Notes }.Where(s => !string.IsNullOrWhiteSpace(s)));
                PdfData(t.Cell(), n.Name, z, bold: true);
                PdfData(t.Cell(), NodeRole(n.Name), z);
                PdfData(t.Cell(), $"{n.Cpu:0.#}", z, center: true);
                PdfData(t.Cell(), n.Ghz > 0 ? $"{n.Ghz:0.#}" : "", z, center: true);
                PdfData(t.Cell(), $"{n.RamGb:0.#}", z, center: true);
                PdfData(t.Cell(), n.NodeCount.ToString(), z, center: true);
                PdfData(t.Cell(), DiskCell(n.StorageType, n.StorageGb), z, center: true);
                PdfData(t.Cell(), DiskCell(n.StorageType2, n.StorageGb2), z, center: true);
                PdfData(t.Cell(), DiskCell(n.StorageType3, n.StorageGb3), z, center: true);
                PdfData(t.Cell(), DiskCell(n.StorageType4, n.StorageGb4), z, center: true);
                PdfData(t.Cell(), n.PageFileGb > 0 ? n.PageFileGb.ToString() : "", z, center: true);
                PdfData(t.Cell(), n.TotalStorageGb.ToString(), z, center: true);
                PdfData(t.Cell(), iops, z, center: true);
                PdfData(t.Cell(), n.ThroughputMiBs > 0 ? n.ThroughputMiBs.ToString() : "", z, center: true);
                PdfData(t.Cell(), n.Latency > 0 ? $"{n.Latency:0.#}" : "", z, center: true);
                PdfData(t.Cell(), n.Os, z);
                PdfData(t.Cell(), sub, z);
            }
            // Підсумок.
            PdfTotal(t.Cell(), "Разом"); PdfTotal(t.Cell(), "");
            PdfTotal(t.Cell(), $"{req.TotalCpu:0.#}", center: true);
            PdfTotal(t.Cell(), "", center: true);
            PdfTotal(t.Cell(), $"{req.TotalRamGb:0.#}", center: true);
            PdfTotal(t.Cell(), req.Infrastructure.Sum(n => n.NodeCount).ToString(), center: true);
            PdfTotal(t.Cell(), ""); PdfTotal(t.Cell(), ""); PdfTotal(t.Cell(), ""); PdfTotal(t.Cell(), "");
            PdfTotal(t.Cell(), "");
            PdfTotal(t.Cell(), $"{req.TotalStorageGb}", center: true);
            PdfTotal(t.Cell(), ""); PdfTotal(t.Cell(), ""); PdfTotal(t.Cell(), "");
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
            ("Диск підкачки", "окремий диск під файл підкачки для серверів застосунків/веб"),
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

}

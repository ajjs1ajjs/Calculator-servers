using OfficeOpenXml;
using OfficeOpenXml.Style;
using ResourceCalculator.Models;
using static ResourceCalculator.Services.ReportCommon;

namespace ResourceCalculator.Services;

// Excel-звіт (EPPlus) — для тендерних/переддоговірних документів.
internal sealed class ExcelReportBuilder
{
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
            if (config.IncludeComponentsInReport) BuildEnvironmentComponentsSheet(pkg, environments!);
        }
        // Вимоги до дисків (OS/Logs/MainData/Content) — колонками прямо в таблиці "Інфраструктура",
        // не окремим аркушем.
        BuildInfrastructureSheet(pkg, req, multiEnv ? environments : null, config.UserCount);
        // Компоненти PROD окремо лише коли не було розбивки по середовищах.
        if (!multiEnv && config.IncludeComponentsInReport) BuildComponentsSheet(pkg, req);

        return pkg.GetAsByteArray();
    }

    // Аркуш із розбивкою ВМ для кожного середовища (PROD/DEV/TEST/PreProd) — окремим блоком-таблицею
    // на кожне середовище (підпис → шапка → рядки → підсумок → рамка + відступ), щоб середовища
    // візуально не зливались в одну суцільну таблицю. Стиль збігається з аркушем «Інфраструктура».
    private static void BuildEnvironmentVmsSheet(ExcelPackage pkg, IReadOnlyList<EnvironmentReport> environments)
    {
        var ws = pkg.Workbook.Worksheets.Add("ВМ по середовищах");
        int row = 1;
        foreach (var e in environments)
        {
            row = WriteEnvVmBlock(ws, e, row);
            row += 2; // порожні рядки-відступ між середовищами, щоб таблиці не зливались
        }
        if (ws.Dimension == null) return;
        ws.Cells[ws.Dimension.Address].AutoFitColumns();
        // Обмежуємо надто широкі стовпці (довгі назви/примітки переносяться) — як у StyleTable.
        for (int c = ws.Dimension.Start.Column; c <= ws.Dimension.End.Column; c++)
        {
            if (ws.Column(c).Width > 46)
            {
                ws.Column(c).Width = 46;
                ws.Cells[1, c, ws.Dimension.End.Row, c].Style.WrapText = true;
            }
        }
    }

    // Один блок таблиці ВМ для одного середовища, починаючи з рядка startRow. Стиль повторює блоки
    // аркуша «Інфраструктура»: підпис (синій) → шапка → рядки ВМ → підсумок «Разом» (сірий) → рамка.
    // Повертає номер наступного вільного рядка.
    private static int WriteEnvVmBlock(ExcelWorksheet ws, EnvironmentReport e, int startRow)
    {
        const int cols = 15;
        // Підпис середовища над таблицею (із к-стю користувачів).
        ws.Cells[startRow, 1].Value = Xl($"Середовище {e.Name} — користувачів: {e.UserCount}");
        ws.Cells[startRow, 1, startRow, cols].Merge = true;
        ws.Cells[startRow, 1].Style.Font.Bold = true;
        ws.Cells[startRow, 1].Style.Font.Size = 12;
        ws.Cells[startRow, 1].Style.Font.Color.SetColor(System.Drawing.Color.FromArgb(30, 102, 245));

        // «Середовище» більше не окрема колонка — воно у підписі блоку (звідси й зручніше).
        string[] headers = { "Сервер (ВМ)", "CPU (ядер)", "RAM (ГБ)", "К-сть",
            "Диск/сервер (ГБ)", "Диск разом (ГБ)", "Диск підкачки (ГБ)", "IOPS", "Профіль IOPS", "MiB/s", "Затримка (мс)",
            "Призначення", "ОС", "Версія СУБД", "Примітки" };
        int headerRow = startRow + 1;
        WriteHeader(ws, headers, headerRow);
        // Вужчі колонки (диски/IOPS) переносять довгі заголовки у 3 рядки — типової висоти 26pt
        // не вистачає, і текст обрізається (напр. "Диск/сервер" → "Диск/се").
        ws.Row(headerRow).Height = 44;

        int row = headerRow + 1;
        foreach (var n in e.Requirement.Infrastructure.Where(x => x.NodeCount > 0))
        {
            ws.Cells[row, 1].Value = Xl(n.Name);
            ws.Cells[row, 2].Value = n.Cpu;
            ws.Cells[row, 3].Value = n.RamGb;
            ws.Cells[row, 4].Value = n.NodeCount;
            ws.Cells[row, 5].Value = n.DiskPerNodeGb;
            ws.Cells[row, 6].Value = n.TotalStorageGb;
            if (n.PageFileNotApplicable)
                WriteNaCell(ws, row, 7);
            else
                ws.Cells[row, 7].Value = n.PageFileGb > 0 ? n.PageFileGb : (object)"";
            if (n.IopsNotApplicable)
            {
                WriteNaCell(ws, row, 8);
                WriteNaCell(ws, row, 9);
                WriteNaCell(ws, row, 10);
                WriteNaCell(ws, row, 11);
            }
            else
            {
                ws.Cells[row, 8].Value = n.Iops > 0 ? n.Iops : (object)"";
                ws.Cells[row, 9].Value = Xl(n.IopsProfile);
                ws.Cells[row, 10].Value = n.ThroughputMiBs > 0 ? n.ThroughputMiBs : (object)"";
                ws.Cells[row, 11].Value = n.Latency > 0 ? n.Latency : (object)"";
            }
            ws.Cells[row, 12].Value = NodeRole(n.Name);
            ws.Cells[row, 13].Value = Xl(n.Os);
            ws.Cells[row, 14].Value = Xl(n.DbVersion);
            ws.Cells[row, 15].Value = Xl(n.Notes);
            // Числові/кодові стовпці — по центру; власне числа — ще й жирним. Назви/опис — зліва (типово).
            ws.Cells[row, 2, row, 11].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            ws.Cells[row, 2, row, 7].Style.Font.Bold = true;
            ws.Cells[row, 9, row, 11].Style.Font.Bold = true;
            row++;
        }
        // Підсумковий рядок.
        ws.Cells[row, 1].Value = "Разом";
        ws.Cells[row, 2].Value = Math.Round(e.Requirement.TotalCpu, 1);
        ws.Cells[row, 3].Value = Math.Round(e.Requirement.TotalRamGb, 1);
        ws.Cells[row, 4].Value = e.Requirement.Infrastructure.Sum(n => n.NodeCount);
        ws.Cells[row, 6].Value = e.Requirement.TotalStorageGb;
        ws.Cells[row, 2, row, 11].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
        ws.Cells[row, 1, row, cols].Style.Font.Bold = true;
        ws.Cells[row, 1, row, cols].Style.Fill.PatternType = ExcelFillStyle.Solid;
        ws.Cells[row, 1, row, cols].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(220, 224, 232));

        // Тонкі рамки лише на таблицю (від шапки до підсумку), щоб порожні рядки-відступ лишались чистими.
        var block = ws.Cells[headerRow, 1, row, cols];
        block.Style.Border.Top.Style = ExcelBorderStyle.Thin;
        block.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
        block.Style.Border.Left.Style = ExcelBorderStyle.Thin;
        block.Style.Border.Right.Style = ExcelBorderStyle.Thin;

        return row + 1;
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
            cell.Value = Xl(text);
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
            ws.Cells[row, 1].Value = Xl(name);   // назва — зліва (типово)
            ws.Cells[row, 2].Value = Xl(cat);    // категорія — зліва (типово)
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
            ws.Cells[row, 1].Value = Xl(e.Name);
            ws.Cells[row, 2].Value = e.UserCount;
            ws.Cells[row, 3].Value = Xl(e.ModulesInfo);
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
        // Об'єднана комірка не авторозширюється під AutoFit — переносимо текст і даємо висоту,
        // щоб довга назва (напр. «… Гібрид (K8s + Windows)») була видна повністю.
        ws.Cells[r, 1].Style.WrapText = true;
        ws.Cells[r, 1].Style.VerticalAlignment = ExcelVerticalAlignment.Center;
        ws.Row(r).Height = 36;
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
        Kv("Тип розгортання", DeployName(config.DeploymentType));
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
        IReadOnlyList<EnvironmentReport>? environments, int userCount)
    {
        var ws = pkg.Workbook.Worksheets.Add("Інфраструктура");

        if (environments != null && environments.Count > 1)
        {
            // По одному блоку-таблиці на кожне середовище (PROD/DEV/TEST/PreProd) — згори вниз.
            int row = 1;
            foreach (var e in environments)
            {
                row = WriteInfraBlock(ws, e.Requirement, row, Xl($"Інфраструктура (сервери/ВМ) — середовище {e.Name} для {e.UserCount} користувачів"));
                row += 2; // порожні рядки-відступ між середовищами, щоб таблиці не зливались
            }
        }
        else
        {
            WriteInfraBlock(ws, req, 1, Xl($"Інфраструктура (сервери/ВМ) — середовище PROD для {userCount} користувачів"));
        }

        ws.Cells[ws.Dimension.Address].AutoFitColumns();
    }

    // Довге тире для показників, які свідомо не застосовні до ролі вузла (а не просто не порахували).
    // Без контрастної сірої заливки — лише тонка світло-сіра рамка, щоб клітинка не "рябіла" на тлі решти таблиці.
    private static void WriteNaCell(ExcelWorksheet ws, int row, int col)
    {
        var cell = ws.Cells[row, col];
        cell.Value = "—";
        cell.Style.Font.Color.SetColor(System.Drawing.Color.FromArgb(108, 111, 133));
        cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
        cell.Style.Border.Top.Style = ExcelBorderStyle.Hair;
        cell.Style.Border.Bottom.Style = ExcelBorderStyle.Hair;
        cell.Style.Border.Left.Style = ExcelBorderStyle.Hair;
        cell.Style.Border.Right.Style = ExcelBorderStyle.Hair;
        cell.Style.Border.Top.Color.SetColor(System.Drawing.Color.FromArgb(230, 232, 237));
        cell.Style.Border.Bottom.Color.SetColor(System.Drawing.Color.FromArgb(230, 232, 237));
        cell.Style.Border.Left.Color.SetColor(System.Drawing.Color.FromArgb(230, 232, 237));
        cell.Style.Border.Right.Color.SetColor(System.Drawing.Color.FromArgb(230, 232, 237));
    }

    // Один блок таблиці інфраструктури (для одного середовища), починаючи з рядка startRow.
    // Повертає номер наступного вільного рядка (одразу після підсумкового «Разом»).
    private static int WriteInfraBlock(ExcelWorksheet ws, ResourceRequirement req, int startRow, string title)
    {
        // Підпис середовища над таблицею.
        ws.Cells[startRow, 1].Value = title;
        ws.Cells[startRow, 1, startRow, 20].Merge = true;
        ws.Cells[startRow, 1].Style.Font.Bold = true;
        ws.Cells[startRow, 1].Style.Font.Size = 12;
        ws.Cells[startRow, 1].Style.Font.Color.SetColor(System.Drawing.Color.FromArgb(30, 102, 245));

        // Порядок колонок: ідентифікатор + числові характеристики спершу (диск розписаний по
        // частинах — OS/Logs/MainData/Content — замість одного сукупного числа, це і є вимоги
        // до дисків, вбудовані прямо в таблицю інфраструктури), описові — у кінці перед примітками.
        string[] headers = { "Сервер (ВМ)", "CPU (ядер)", "Частота, ГГц", "RAM (ГБ)", "К-сть", "Тип диску",
            "Диск ОС (ГБ)", "Диск Logs/TempDB (ГБ)", "Диск MainData (ГБ)", "Диск Content (ГБ)",
            "Диск підкачки (ГБ)", "Диск разом (ГБ)", "IOPS", "Профіль IOPS", "MiB/s", "Затримка (мс)",
            "Призначення", "ОС", "Версія СУБД", "Примітки" };
        int headerRow = startRow + 1;
        WriteHeader(ws, headers, headerRow);
        // Вужчі колонки (диски/IOPS) переносять довгі заголовки у 3 рядки — типової висоти 26pt
        // не вистачає, і текст обрізається (напр. "Диск Logs/TempDB" → "Диск Logs/Te").
        ws.Row(headerRow).Height = 44;

        int row = headerRow + 1;
        foreach (var n in req.Infrastructure.Where(x => x.NodeCount > 0))
        {
            ws.Cells[row, 1].Value = Xl(n.Name);
            ws.Cells[row, 2].Value = n.Cpu;
            ws.Cells[row, 3].Value = n.Ghz > 0 ? n.Ghz : (object)"";
            ws.Cells[row, 4].Value = n.RamGb;
            ws.Cells[row, 5].Value = n.NodeCount;
            ws.Cells[row, 6].Value = Xl(n.StorageType);
            ws.Cells[row, 7].Value = n.StorageGb > 0 ? n.StorageGb : (object)"";
            if (n.DiskSplitNotApplicable)
            {
                WriteNaCell(ws, row, 8);
                WriteNaCell(ws, row, 9);
                WriteNaCell(ws, row, 10);
            }
            else
            {
                ws.Cells[row, 8].Value = n.StorageGb2 > 0 ? n.StorageGb2 : (object)"";
                ws.Cells[row, 9].Value = n.StorageGb3 > 0 ? n.StorageGb3 : (object)"";
                ws.Cells[row, 10].Value = n.StorageGb4 > 0 ? n.StorageGb4 : (object)"";
            }
            if (n.PageFileNotApplicable)
                WriteNaCell(ws, row, 11);
            else
                ws.Cells[row, 11].Value = n.PageFileGb > 0 ? n.PageFileGb : (object)"";
            ws.Cells[row, 12].Value = n.TotalStorageGb;
            if (n.IopsNotApplicable)
            {
                WriteNaCell(ws, row, 13);
                WriteNaCell(ws, row, 14);
                WriteNaCell(ws, row, 15);
                WriteNaCell(ws, row, 16);
            }
            else
            {
                ws.Cells[row, 13].Value = n.Iops > 0 ? n.Iops : (object)"";
                ws.Cells[row, 14].Value = Xl(n.IopsProfile);
                ws.Cells[row, 15].Value = n.ThroughputMiBs > 0 ? n.ThroughputMiBs : (object)"";
                ws.Cells[row, 16].Value = n.Latency > 0 ? n.Latency : (object)"";
            }
            ws.Cells[row, 17].Value = NodeRole(n.Name);
            ws.Cells[row, 18].Value = Xl(n.Os);
            ws.Cells[row, 19].Value = Xl(n.DbVersion);
            ws.Cells[row, 20].Value = Xl(n.Notes);
            // Числові/кодові стовпці — по центру; власне числа — ще й жирним. Назви/опис — зліва (типово).
            ws.Cells[row, 2, row, 16].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            ws.Cells[row, 2, row, 5].Style.Font.Bold = true;
            ws.Cells[row, 7, row, 13].Style.Font.Bold = true;
            ws.Cells[row, 15, row, 16].Style.Font.Bold = true;
            row++;
        }
        // Підсумковий рядок.
        ws.Cells[row, 1].Value = "Разом";
        ws.Cells[row, 2].Value = Math.Round(req.TotalCpu, 1);
        ws.Cells[row, 4].Value = Math.Round(req.TotalRamGb, 1);
        ws.Cells[row, 5].Value = req.Infrastructure.Sum(n => n.NodeCount);
        ws.Cells[row, 12].Value = req.TotalStorageGb;
        ws.Cells[row, 2, row, 16].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
        ws.Cells[row, 1, row, 20].Style.Font.Bold = true;
        ws.Cells[row, 1, row, 20].Style.Fill.PatternType = ExcelFillStyle.Solid;
        ws.Cells[row, 1, row, 20].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(220, 224, 232));

        // Тонкі рамки лише на таблицю (від шапки до підсумку), щоб порожні рядки-відступ лишались чистими.
        var block = ws.Cells[headerRow, 1, row, 20];
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
            ws.Cells[row, 1].Value = Xl(c.Name);
            ws.Cells[row, 2].Value = Xl(c.Category);
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

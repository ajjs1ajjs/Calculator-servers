using ResourceCalculator.Models;

namespace ResourceCalculator.Services;

// Фасад експорту звітів про пораховані ресурси. Прив'язки до хмарних провайдерів
// немає — лише обчислені вимоги (CPU/RAM/диски/IOPS).
//
// Сама генерація розведена по двох незалежних білдерах (кожен зі своєю бібліотекою
// і своїм оформленням), бо раніше один клас на ~1000 рядків тримав і QuestPDF-, і
// EPPlus-код: правка одного звіту змушувала читати обидва.
//   • PdfReportBuilder   — PDF (QuestPDF)
//   • ExcelReportBuilder — XLSX (EPPlus)
//   • ReportCommon       — спільні підписи/назви/безпечний запис тексту
public class ConfigExportService
{
    private readonly PdfReportBuilder _pdf = new();
    private readonly ExcelReportBuilder _excel = new();

    public byte[] ExportPdf(ResourceRequirement req, ProjectConfig config,
        IReadOnlyList<EnvironmentReport>? environments = null,
        IEnumerable<UserLoadRange>? matrixRanges = null)
        => _pdf.ExportPdf(req, config, environments, matrixRanges);

    public byte[] ExportExcel(ResourceRequirement req, ProjectConfig config,
        IReadOnlyList<EnvironmentReport>? environments = null,
        IEnumerable<UserLoadRange>? matrixRanges = null)
        => _excel.ExportExcel(req, config, environments, matrixRanges);

    // Захист від Excel formula injection — публічний, бо на нього є регресійний тест.
    public static string Xl(string? s) => ReportCommon.Xl(s);

    // Зрозуміле призначення сервера за його назвою (для не-ІТ читачів звіту).
    public static string NodeRole(string name) => ReportCommon.NodeRole(name);
}

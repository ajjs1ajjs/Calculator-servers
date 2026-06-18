using System.Text;
using AIResourceCalculator.Models;

namespace AIResourceCalculator.Services;

// Формує текстовий та HTML-звіт про пораховані ресурси.
// Прив'язки до хмарних провайдерів немає — лише обчислені вимоги (CPU/RAM/диски/IOPS).
public class ConfigExportService
{
    public string ExportTxt(ResourceRequirement req, ProjectConfig config)
    {
        var sb = new StringBuilder();
        sb.AppendLine("========================================");
        sb.AppendLine($"  {config.ProjectName} — Звіт про ресурси");
        sb.AppendLine("========================================");
        sb.AppendLine($"  Користувачів: {config.UserCount}");
        sb.AppendLine($"  Розгортання:  {config.DeploymentType}");
        sb.AppendLine($"  Профіль:      {config.LoadProfile}");
        sb.AppendLine("----------------------------------------");
        sb.AppendLine($"  vCPU:     {req.TotalCpu:F1} cores");
        sb.AppendLine($"  RAM:      {req.TotalRamGb:F1} GB");
        sb.AppendLine($"  Диски:    {req.TotalStorageGb} GB");
        sb.AppendLine($"  IOPS:     {req.TotalIops}");
        sb.AppendLine("----------------------------------------");
        sb.AppendLine("  Інфраструктура:");
        foreach (var i in req.Infrastructure)
            sb.AppendLine($"    {i.Name}: {i.NodeCount}x ({i.Cpu} vCPU, {i.RamGb} GB, диск {i.DiskPerNodeGb} GB)");
        sb.AppendLine("----------------------------------------");
        sb.AppendLine("  Компоненти:");
        foreach (var c in req.Components.Where(c => c.Cpu > 0))
            sb.AppendLine($"    {c.Name}: {c.Cpu:F1} vCPU, {c.RamGb:F1} GB, {c.Replicas} реплік");
        sb.AppendLine("========================================");
        return sb.ToString();
    }

    public string ExportHtml(ResourceRequirement req, ProjectConfig config)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html><head><meta charset='UTF-8'>");
        sb.AppendLine("<style>body{font-family:Arial;margin:40px;background:#eff1f5;color:#4c4f69}h1{color:#1e66f5}table{border-collapse:collapse;width:100%;margin:15px 0}th,td{border:1px solid #acb0be;padding:8px;text-align:left}th{background:#1e66f5;color:white}.kpi{display:flex;gap:15px}.kpi-box{color:white;padding:15px;border-radius:6px;flex:1}</style>");
        sb.AppendLine("</head><body>");
        sb.AppendLine($"<h1>{SanitizeHtml(config.ProjectName)} — Звіт про ресурси</h1>");
        sb.AppendLine($"<p>Користувачів: {config.UserCount} | Розгортання: {config.DeploymentType} | Профіль: {config.LoadProfile}</p>");
        sb.AppendLine("<div class='kpi'>");
        sb.AppendLine($"<div class='kpi-box' style='background:#1e66f5'><h3>vCPU</h3><p>{req.TotalCpu:F1}</p></div>");
        sb.AppendLine($"<div class='kpi-box' style='background:#40a02b'><h3>RAM</h3><p>{req.TotalRamGb:F1} GB</p></div>");
        sb.AppendLine($"<div class='kpi-box' style='background:#fe640b'><h3>Диски</h3><p>{req.TotalStorageGb} GB</p></div>");
        sb.AppendLine($"<div class='kpi-box' style='background:#8839ef'><h3>IOPS</h3><p>{req.TotalIops}</p></div>");
        sb.AppendLine("</div>");
        sb.AppendLine("<h2>Інфраструктура</h2><table><tr><th>Вузол</th><th>vCPU</th><th>RAM</th><th>К-сть</th><th>Диски/вузол</th></tr>");
        foreach (var i in req.Infrastructure)
            sb.AppendLine($"<tr><td>{SanitizeHtml(i.Name)}</td><td>{i.Cpu}</td><td>{i.RamGb}</td><td>{i.NodeCount}</td><td>{i.DiskPerNodeGb} GB</td></tr>");
        sb.AppendLine("</table><h2>Компоненти</h2><table><tr><th>Назва</th><th>vCPU</th><th>RAM</th><th>Репліки</th></tr>");
        foreach (var c in req.Components.Where(c => c.Cpu > 0))
            sb.AppendLine($"<tr><td>{SanitizeHtml(c.Name)}</td><td>{c.Cpu:F1}</td><td>{c.RamGb:F1}</td><td>{c.Replicas}</td></tr>");
        sb.AppendLine("</table></body></html>");
        return sb.ToString();
    }

    public static string SanitizeHtml(string input)
        => string.IsNullOrEmpty(input) ? "" : input.Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}

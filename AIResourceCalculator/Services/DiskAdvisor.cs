using System.Text;
using AIResourceCalculator.Interfaces;
using AIResourceCalculator.Models;

namespace AIResourceCalculator.Services;

// Builds the per-node DISK REQUIREMENTS for the calculated infrastructure (not advice — these
// mirror the sizing model). Rule-based, offline:
//  • database nodes: a split layout (OS / Logs+TempDB / MainData / Content) from the matrix, or a
//    computed one if the matrix has none — NO page file (SQL must not use a swap/page file);
//  • other nodes (app/web): OS disk + page file (when the matrix defines one);
//  • every node shows its IOPS profile and latency target.
public static class DiskAdvisor
{
    public static string Build(ResourceRequirement req, ProjectConfig config, ILocalizationService loc)
    {
        string Line(string roleKey, string type, int gb) => string.Format(loc["disk.line"], loc[roleKey], type, gb);

        var sb = new StringBuilder();

        foreach (var n in req.Infrastructure)
        {
            if (n.NodeCount <= 0) continue;
            bool isSql = n.Name.Contains("SQL", StringComparison.OrdinalIgnoreCase)
                      || n.Name.Contains("PostgreSQL", StringComparison.OrdinalIgnoreCase)
                      || n.Name.Contains("Oracle", StringComparison.OrdinalIgnoreCase);
            bool hasSplit = n.StorageGb2 > 0 || n.StorageGb3 > 0 || n.StorageGb4 > 0;
            // Показуємо будь-який вузол, що має диск (включно з Kubernetes Master/Worker) — так само,
            // як і для БД, а не лише вузли з IOPS/pagefile/розбивкою.
            bool relevant = isSql || hasSplit || n.PageFileGb > 0 || n.Iops > 0 || n.StorageGb > 0;
            if (!relevant) continue;

            var lines = new List<string>();

            if (hasSplit)
            {
                // Honor the layout defined in the matrix / imported Excel.
                if (n.StorageGb > 0) lines.Add(Line("disk.os", Nz(n.StorageType), n.StorageGb));
                if (n.StorageGb2 > 0) lines.Add(Line("disk.logs", Nz(n.StorageType2), n.StorageGb2));
                if (n.StorageGb3 > 0) lines.Add(Line("disk.data", Nz(n.StorageType3), n.StorageGb3));
                if (n.StorageGb4 > 0) lines.Add(Line("disk.content", Nz(n.StorageType4), n.StorageGb4));
            }
            else if (isSql) // database without an explicit split → compute one
            {
                int total = Math.Max(n.StorageGb, 200);
                int os = 100;
                int data = (int)Math.Ceiling(total * 0.55);
                int logs = (int)Math.Ceiling(total * 0.25);
                int content = Math.Max(50, total - data - logs);
                lines.Add(Line("disk.os", "SSD", os));
                lines.Add(Line("disk.data", "SSD", data));
                lines.Add(Line("disk.logs", "SSD", logs));
                lines.Add(Line("disk.content", "SATA", content));
            }
            else if (n.StorageGb > 0) // app/web: OS disk only
            {
                lines.Add(Line("disk.os", Nz(n.StorageType), n.StorageGb));
            }

            // Page file — NOT for SQL; only where the matrix defines one (app/web servers).
            if (!isSql && n.PageFileGb > 0)
                lines.Add(string.Format(loc["disk.pagefileLine"], Nz(n.PageFileType), n.PageFileGb));

            // IOPS profile + throughput (MiB/s) + latency target.
            if (n.Iops > 0 || n.Latency > 0)
            {
                var profile = string.IsNullOrWhiteSpace(n.IopsProfile) ? "" : $" {n.IopsProfile}";
                var mib = n.ThroughputMiBs > 0 ? n.ThroughputMiBs.ToString() : "—";
                lines.Add(string.Format(loc["disk.perfLine"], n.Iops, profile, mib, Trim(n.Latency)));
            }

            if (lines.Count == 0) continue;
            sb.AppendLine(string.Format(loc["disk.nodeHeader"], n.Name));
            foreach (var l in lines) sb.AppendLine(l);
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    private static string Nz(string type) => string.IsNullOrWhiteSpace(type) ? "SSD" : type;
    private static string Trim(double v) => v % 1 == 0 ? ((int)v).ToString() : v.ToString("0.#");
}

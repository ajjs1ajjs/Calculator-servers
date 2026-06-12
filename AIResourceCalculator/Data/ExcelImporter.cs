using System.IO;
using OfficeOpenXml;
using AIResourceCalculator.Models;

namespace AIResourceCalculator.Data;

public class ExcelImporter
{
    public SizingMatrix Import(string filePath)
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        using var package = new ExcelPackage(new FileInfo(filePath));
        var matrix = new SizingMatrix();

        foreach (var ws in package.Workbook.Worksheets)
        {
            var name = ws.Name.Trim();
            var isPerformance = name.Contains("Масштабування") || name.Contains("Продуктивн");
            if (name.Contains("k8s"))
                ParseK8sSheet(ws, matrix, isPerformance);
            else if (name.Equals("MSSQL", StringComparison.OrdinalIgnoreCase))
                ParseMssqlSheet(ws, matrix);
            else if (name.Contains("Windows"))
                ParseWindowsSheet(ws, matrix);
        }

        return matrix;
    }

    private void ParseMssqlSheet(ExcelWorksheet ws, SizingMatrix matrix)
    {
        for (int row = 2; row <= 14; row++)
        {
            var minUser = GetInt(ws, row, 1);
            var maxUser = GetInt(ws, row, 2);
            if (minUser == 0) continue;

            matrix.MsSqlRanges.Add(new UserLoadRange
            {
                MinUsers = minUser, MaxUsers = maxUser,
                Cpu = GetDouble(ws, row, 3),
                RamMin = GetDouble(ws, row, 4),
                RamRec = GetDouble(ws, row, 5),
                Iops = GetInt(ws, row, 6),
                Latency = GetDouble(ws, row, 7)
            });
        }

        for (int row = 18; row <= 29; row++)
        {
            var minUser = GetInt(ws, row, 1);
            var maxUser = GetInt(ws, row, 2);
            if (minUser == 0) continue;

            matrix.MsSqlPerformanceRanges.Add(new UserLoadRange
            {
                MinUsers = minUser, MaxUsers = maxUser,
                Cpu = GetDouble(ws, row, 3),
                RamMin = GetDouble(ws, row, 4),
                RamRec = GetDouble(ws, row, 5),
                Iops = GetInt(ws, row, 6),
                Latency = GetDouble(ws, row, 7)
            });
        }
    }

    private void ParseK8sSheet(ExcelWorksheet ws, SizingMatrix matrix, bool isPerformance)
    {
        var infra = new List<InfrastructureNode>();
        var components = new List<ServiceComponent>();

        for (int row = 6; row <= 9; row++)
        {
            var name = GetString(ws, row, 2);
            if (string.IsNullOrEmpty(name) || name.StartsWith("---") || name.Contains("всього")) continue;

            infra.Add(new InfrastructureNode
            {
                Name = name,
                MinVersion = GetDouble(ws, row, 3),
                Cpu = GetDouble(ws, row, 4),
                RamGb = GetDouble(ws, row, 5),
                NodeCount = GetInt(ws, row, 6),
                Os = GetString(ws, row, 7),
                StorageType = GetString(ws, row, 8),
                StorageGb = GetInt(ws, row, 9)
            });
        }

        for (int row = 14; row <= 45; row++)
        {
            var cat = GetString(ws, row, 1);
            var name = GetString(ws, row, 2);
            if (string.IsNullOrEmpty(name) || name == "Pods:" || name.StartsWith("---")) continue;
            if (name == "CPU" || name == "RAM" || name == "Кількість") continue;
            if (cat.Contains("Total") || cat.Contains("Всього") || cat.Contains("Разом")) continue;

            var cpu = GetDouble(ws, row, 3);
            var ram = GetDouble(ws, row, 4);
            var replicas = GetInt(ws, row, 5);

            if (cpu == 0 && ram == 0) continue;

            components.Add(new ServiceComponent
            {
                Name = name,
                Cpu = cpu,
                RamGb = ram,
                Replicas = replicas,
                Notes = GetString(ws, row, 7),
                Category = cat
            });
        }

        if (isPerformance)
        {
            matrix.K8sPerformanceComponents = components;
            if (infra.Count >= 1) matrix.DefaultK8sSql = infra[0];
            if (infra.Count >= 2) matrix.DefaultK8sMaster = infra[1];
            if (infra.Count >= 3) matrix.DefaultK8sWorker = infra[2];
        }
        else
        {
            matrix.K8sBasicComponents = components;
            if (infra.Count >= 1) matrix.DefaultK8sSql = infra[0];
            if (infra.Count >= 2) matrix.DefaultK8sMaster = infra[1];
            if (infra.Count >= 3) matrix.DefaultK8sWorker = infra[2];
        }
    }

    private void ParseWindowsSheet(ExcelWorksheet ws, SizingMatrix matrix)
    {
        for (int row = 26; row <= 37; row++)
        {
            var minUser = GetInt(ws, row, 1);
            var maxUser = GetInt(ws, row, 2);
            if (minUser == 0) continue;

            matrix.AppServerRanges.Add(new UserLoadRange
            {
                MinUsers = minUser, MaxUsers = maxUser,
                InstanceCount = GetInt(ws, row, 3),
                Ghz = GetDouble(ws, row, 4),
                Cpu = GetInt(ws, row, 5),
                Iops = GetInt(ws, row, 6),
                RamMin = GetDouble(ws, row, 7),
                RamRec = GetDouble(ws, row, 8)
            });
        }

        for (int row = 41; row <= 49; row++)
        {
            var minUser = GetInt(ws, row, 1);
            var maxUser = GetInt(ws, row, 2);
            if (minUser == 0) continue;

            matrix.WebServerRanges.Add(new UserLoadRange
            {
                MinUsers = minUser, MaxUsers = maxUser,
                InstanceCount = GetInt(ws, row, 3),
                Ghz = GetDouble(ws, row, 4),
                Cpu = GetInt(ws, row, 5),
                Iops = GetInt(ws, row, 6),
                RamMin = GetDouble(ws, row, 7),
                RamRec = GetDouble(ws, row, 8)
            });
        }

        for (int row = 52; row <= 63; row++)
        {
            var minUser = GetInt(ws, row, 1);
            var maxUser = GetInt(ws, row, 2);
            if (minUser == 0) continue;

            matrix.AppServerPerformanceRanges.Add(new UserLoadRange
            {
                MinUsers = minUser, MaxUsers = maxUser,
                InstanceCount = GetInt(ws, row, 3),
                Ghz = GetDouble(ws, row, 4),
                Cpu = GetInt(ws, row, 5),
                Iops = GetInt(ws, row, 6),
                RamMin = GetDouble(ws, row, 7),
                RamRec = GetDouble(ws, row, 8)
            });
        }

        for (int row = 64; row <= 73; row++)
        {
            var minUser = GetInt(ws, row, 1);
            var maxUser = GetInt(ws, row, 2);
            if (minUser == 0) continue;

            matrix.WebServerPerformanceRanges.Add(new UserLoadRange
            {
                MinUsers = minUser, MaxUsers = maxUser,
                InstanceCount = GetInt(ws, row, 3),
                Ghz = GetDouble(ws, row, 4),
                Cpu = GetInt(ws, row, 5),
                Iops = GetInt(ws, row, 6),
                RamMin = GetDouble(ws, row, 7),
                RamRec = GetDouble(ws, row, 8)
            });
        }
    }

    private double GetDouble(ExcelWorksheet ws, int row, int col)
    {
        var val = ws.Cells[row, col].Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(val)) return 0;
        if (double.TryParse(val.Replace(",", "."), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var d))
            return d;
        if (double.TryParse(val.Replace(".", ","), out var d2))
            return d2;
        return 0;
    }

    private int GetInt(ExcelWorksheet ws, int row, int col)
    {
        var val = ws.Cells[row, col].Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(val)) return 0;
        if (int.TryParse(val, out var i)) return i;
        var d = GetDouble(ws, row, col);
        return (int)Math.Round(d);
    }

    private string GetString(ExcelWorksheet ws, int row, int col)
    {
        return ws.Cells[row, col].Text?.Trim() ?? "";
    }
}

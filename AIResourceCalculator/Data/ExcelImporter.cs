using System.Globalization;
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

            if (name.Contains("k8s"))
                ParseK8sSheet(ws, matrix);
            else if (name.Equals("MSSQL", StringComparison.OrdinalIgnoreCase))
                ParseMssqlSheet(ws, matrix);
            else if (name.Contains("Windows"))
                ParseWindowsSheet(ws, matrix);
        }

        return matrix;
    }

    private void ParseMssqlSheet(ExcelWorksheet ws, SizingMatrix matrix)
    {
        var dataStart = FindHeaderRow(ws, 1, 1, new[] { "Min", "Users", "користувач" });
        if (dataStart == 0) dataStart = 2;

        for (int row = dataStart + 1; ; row++)
        {
            if (row > ws.Dimension.End.Row) break;
            var minUser = GetInt(ws, row, 1);
            if (minUser == 0) break;
            matrix.MsSqlRanges.Add(new UserLoadRange
            {
                MinUsers = minUser, MaxUsers = GetInt(ws, row, 2),
                Cpu = GetDouble(ws, row, 3),
                RamMin = GetDouble(ws, row, 4),
                RamRec = GetDouble(ws, row, 5),
                Iops = GetInt(ws, row, 6),
                Latency = GetDouble(ws, row, 7)
            });
        }

        var perfHeader = FindHeaderRow(ws, dataStart + 15, 1, new[] { "Min", "Users", "користувач" });
        if (perfHeader == 0) perfHeader = dataStart + 16;
        else perfHeader++;

        for (int row = perfHeader; ; row++)
        {
            if (row > ws.Dimension.End.Row) break;
            var minUser = GetInt(ws, row, 1);
            if (minUser == 0) break;
            matrix.MsSqlPerformanceRanges.Add(new UserLoadRange
            {
                MinUsers = minUser, MaxUsers = GetInt(ws, row, 2),
                Cpu = GetDouble(ws, row, 3),
                RamMin = GetDouble(ws, row, 4),
                RamRec = GetDouble(ws, row, 5),
                Iops = GetInt(ws, row, 6),
                Latency = GetDouble(ws, row, 7)
            });
        }
    }

    private void ParseK8sSheet(ExcelWorksheet ws, SizingMatrix matrix)
    {
        bool isPerf = ws.Name.Contains("Документообіг") || ws.Name.Contains("Продуктивн");
        var maxRow = ws.Dimension?.End.Row ?? 100;

        // Find infrastructure nodes: look for SQL/Master/Worker in column 2
        int infraEnd = 0;
        for (int row = 3; row <= maxRow; row++)
        {
            var name = GetString(ws, row, 2);
            if (string.IsNullOrEmpty(name)) { if (row > 5) { infraEnd = row; break; } continue; }
            if (name.StartsWith("---") || name.Contains("всього") || name.Contains("Pods") || name == "CPU" || name == "RAM" || name == "Кількість")
            {
                if (row > 5) { infraEnd = row; break; }
                continue;
            }

            var node = new InfrastructureNode
            {
                Name = name,
                MinVersion = GetDouble(ws, row, 3),
                Cpu = GetDouble(ws, row, 4),
                RamGb = GetDouble(ws, row, 5),
                NodeCount = GetInt(ws, row, 6),
                Os = GetString(ws, row, 7),
                StorageType = GetString(ws, row, 8),
                StorageGb = GetInt(ws, row, 9),
                StorageType2 = GetString(ws, row, 10),
                StorageGb2 = GetInt(ws, row, 11),
                StorageType3 = GetString(ws, row, 12),
                StorageGb3 = GetInt(ws, row, 13),
                StorageType4 = GetString(ws, row, 14),
                StorageGb4 = GetInt(ws, row, 15),
                Iops = GetInt(ws, row, 16),
                Latency = GetDouble(ws, row, 17)
            };

            if (name.Contains("SQL"))
            {
                if (isPerf && matrix.DefaultK8sSql == null) matrix.DefaultK8sSql = node;
                else if (!isPerf) matrix.DefaultK8sSql = node;
            }
            else if (name.Contains("Master"))
            {
                if (isPerf && matrix.DefaultK8sMaster == null) matrix.DefaultK8sMaster = node;
                else if (!isPerf) matrix.DefaultK8sMaster = node;
            }
            else if (name.Contains("Worker"))
            {
                if (isPerf && matrix.DefaultK8sWorker == null) matrix.DefaultK8sWorker = node;
                else if (!isPerf) matrix.DefaultK8sWorker = node;
            }
        }

        // Parse module sections (rows 13-45)
        var modules = ParseK8sModules(ws, isPerf);
        var targetList = isPerf ? matrix.DocumentFlowModules : matrix.StandardModules;
        foreach (var mod in modules)
        {
            var existing = targetList.FirstOrDefault(m => m.Name == mod.Name);
            if (existing == null)
                targetList.Add(mod);
            else
            {
                foreach (var comp in mod.Components)
                {
                    var existingComp = existing.Components.FirstOrDefault(c => c.Name == comp.Name);
                    if (existingComp == null)
                        existing.Components.Add(comp);
                }
            }
        }

        // Also populate the legacy Modules list for backward compatibility
        foreach (var mod in modules)
        {
            var existing = matrix.Modules.FirstOrDefault(m => m.Name == mod.Name);
            if (existing == null)
                matrix.Modules.Add(mod);
            else if (isPerf)
            {
                foreach (var comp in mod.Components)
                {
                    var existingComp = existing.Components.FirstOrDefault(c => c.Name == comp.Name);
                    if (existingComp == null)
                        existing.Components.Add(comp);
                    else
                    {
                        existingComp.PerfCpu = comp.Cpu;
                        existingComp.PerfRamGb = comp.RamGb;
                    }
                }
            }
        }
    }

    private static readonly HashSet<string> _sectionHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "ForceBPM", "LMS", "HR Portal"
    };

    private List<ProjectModule> ParseK8sModules(ExcelWorksheet ws, bool isPerf)
    {
        var moduleComponents = new Dictionary<string, List<ModuleComponent>>();
        string? currentSection = null;
        var maxRow = ws.Dimension?.End.Row ?? 100;

        for (int row = Math.Max(1, FindHeaderRow(ws, 1, 1, new[] { "Pods", "підав", "подов", "pods" })); row <= maxRow; row++)
        {
            var name = GetString(ws, row, 2);
            if (string.IsNullOrEmpty(name) || name.StartsWith("---")) continue;
            if (name == "Pods:" || name == "CPU" || name == "RAM" || name == "Кількість") continue;

            // Skip summary rows (totals)
            var col1 = GetString(ws, row, 1);
            if (col1.Contains("ліцензій") || col1.Contains("сесій") || col1 == "1")
                continue;

            // Section header -> switch module context
            if (_sectionHeaders.Contains(name))
            {
                currentSection = name;
                continue;
            }

            // Determine target module
            var moduleName = currentSection ?? DetermineModuleByComponent(name);

            var cpu = GetDouble(ws, row, 3);
            var ram = GetDouble(ws, row, 4);
            var qty = GetInt(ws, row, 5);
            var notes = GetString(ws, row, 8);

            if (cpu == 0 && ram == 0) continue;

            var (formula, fixedReplicas) = GetFormula(name, qty, moduleName);

            var comp = new ModuleComponent
            {
                Name = name,
                Cpu = isPerf ? 0 : cpu,
                RamGb = isPerf ? 0 : ram,
                PerfCpu = isPerf ? cpu : 0,
                PerfRamGb = isPerf ? ram : 0,
                Formula = formula,
                FixedReplicas = fixedReplicas,
                Notes = notes,
                HasLocalSql = notes.Contains("SQL", StringComparison.OrdinalIgnoreCase),
                HasRedis = name.Contains("Redis", StringComparison.OrdinalIgnoreCase)
            };

            if (!moduleComponents.TryGetValue(moduleName, out var list))
                moduleComponents[moduleName] = list = new();
            var existing = list.FirstOrDefault(c => c.Name == name);
            if (existing == null)
                list.Add(comp);
            else if (isPerf)
            {
                existing.PerfCpu = cpu;
                existing.PerfRamGb = ram;
            }
        }

        return moduleComponents.Select(kvp => new ProjectModule
        {
            Name = kvp.Key, Description = kvp.Key, IsEnabled = true,
            Components = kvp.Value
        }).ToList();
    }

    private static string DetermineModuleByComponent(string name)
    {
        if (name.StartsWith("AS") || name.Contains("AS-")) return "App Server";
        if (name.Contains("ROBOT")) return "ROBOT";
        if (name.Contains("Webrmd") || name.Contains("SmartID") || name.StartsWith("WS") || name.StartsWith("Web"))
            return "Web";
        return "Uncategorized";
    }

    private (ReplicaFormula formula, int fixedReplicas) GetFormula(string name, int qty, string moduleName)
    {
        if (qty > 0)
        {
            return name switch
            {
                string n when n.Contains("ROBOT") => (ReplicaFormula.Per100Plus1000, 1),
                string n when n.Contains("WS (Веб сервіси)") || n.Contains("WS (WebSocket)") => (ReplicaFormula.Per50Plus500, 1),
                string n when n.Contains("ForceBPM Engine") => (ReplicaFormula.OnePlusPer100, 1),
                _ => (ReplicaFormula.Fixed, qty)
            };
        }

        if (moduleName == "HR Portal" && (name.Contains("SmartID") || name.Contains("GraphQL")))
            return (ReplicaFormula.Per100Users, 0);

        return (ReplicaFormula.Per25Users, 0);
    }

    private void ParseWindowsSheet(ExcelWorksheet ws, SizingMatrix matrix)
    {
        bool isPerf = ws.Name.Contains("Документообіг") || ws.Name.Contains("Продуктивн");
        var maxRow = ws.Dimension?.End.Row ?? 100;

        // Infrastructure: find nodes by name (SQL, App, Web)
        for (int row = 3; row <= maxRow; row++)
        {
            var name = GetString(ws, row, 2);
            if (string.IsNullOrEmpty(name) || name.StartsWith("---") || name.Contains("Pods") || name.Contains("CPU") || name.Contains("RAM")) 
            {
                if (row > 10) break;
                continue;
            }

            // Check if looks like a node name
            bool isSql = name.Contains("SQL");
            bool isApp = name.Contains("додатків") || name.Contains("App");
            bool isWeb = name.Contains("Веб") || name.Contains("Web") || name.Contains("IIS");
            if (!isSql && !isApp && !isWeb) continue;

            var node = new InfrastructureNode
            {
                Name = name,
                MinVersion = GetDouble(ws, row, 3),
                Cpu = GetDouble(ws, row, 4),
                RamGb = GetDouble(ws, row, 5),
                NodeCount = GetInt(ws, row, 6),
                Os = GetString(ws, row, 7),
                StorageType = GetString(ws, row, 8),
                StorageGb = GetInt(ws, row, 9),
                StorageType2 = GetString(ws, row, 10),
                StorageGb2 = GetInt(ws, row, 11),
                StorageType3 = GetString(ws, row, 12),
                StorageGb3 = GetInt(ws, row, 13),
                StorageType4 = GetString(ws, row, 14),
                StorageGb4 = GetInt(ws, row, 15),
                PageFileType = GetString(ws, row, 16),
                PageFileGb = GetInt(ws, row, 17),
                Iops = GetInt(ws, row, 18),
                IopsProfile = GetString(ws, row, 19),
                Latency = GetDouble(ws, row, 20)
            };

            if (name.Contains("SQL"))
            {
                if (isPerf && matrix.DefaultWindowsSql == null) matrix.DefaultWindowsSql = node;
                else if (!isPerf) matrix.DefaultWindowsSql = node;
            }
            else if (name.Contains("додатків") || name.Contains("App"))
            {
                if (isPerf && matrix.DefaultWindowsApp == null) matrix.DefaultWindowsApp = node;
                else if (!isPerf) matrix.DefaultWindowsApp = node;
            }
            else if (name.Contains("Веб") || name.Contains("Web") || name.Contains("IIS"))
            {
                if (isPerf && matrix.DefaultWindowsWeb == null) matrix.DefaultWindowsWeb = node;
                else if (!isPerf) matrix.DefaultWindowsWeb = node;
            }
        }

        // AppServer ranges
        var appHeader = FindHeaderRow(ws, 3, 1, new[] { "Min", "Users", "Кількість", "К-сть" });
        if (appHeader == 0) appHeader = isPerf ? 53 : 26;
        var appList = isPerf ? matrix.AppServerPerformanceRanges : matrix.AppServerRanges;
        for (int row = appHeader + 1; row <= maxRow; row++)
        {
            var minUser = GetInt(ws, row, 1);
            if (minUser == 0) break;
            if (row > appHeader + 15) break;
            appList.Add(new UserLoadRange
            {
                MinUsers = minUser, MaxUsers = GetInt(ws, row, 2),
                InstanceCount = GetInt(ws, row, 3),
                Ghz = GetDouble(ws, row, 4),
                Cpu = GetDouble(ws, row, 5),
                Iops = GetInt(ws, row, 6),
                RamMin = GetDouble(ws, row, 7),
                RamRec = GetDouble(ws, row, 8)
            });
        }

        // WebServer ranges
        var webHeader = FindHeaderRow(ws, appHeader + 14, 1, new[] { "Min", "Users", "Кількість", "К-сть" });
        if (webHeader == 0) webHeader = isPerf ? 68 : 41;
        var webList = isPerf ? matrix.WebServerPerformanceRanges : matrix.WebServerRanges;
        for (int row = webHeader + 1; row <= maxRow; row++)
        {
            var minUser = GetInt(ws, row, 1);
            if (minUser == 0) break;
            if (row > webHeader + 12) break;
            webList.Add(new UserLoadRange
            {
                MinUsers = minUser, MaxUsers = GetInt(ws, row, 2),
                InstanceCount = GetInt(ws, row, 3),
                Ghz = GetDouble(ws, row, 4),
                Cpu = GetDouble(ws, row, 5),
                Iops = GetInt(ws, row, 6),
                RamMin = GetDouble(ws, row, 7),
                RamRec = GetDouble(ws, row, 8)
            });
        }
    }

    private static double GetDouble(ExcelWorksheet ws, int row, int col)
    {
        var cell = ws.Cells[row, col];
        if (cell.Value is double or int or decimal)
            return Convert.ToDouble(cell.Value, CultureInfo.InvariantCulture);
        var text = cell.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(text)) return 0;
        if (double.TryParse(text.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
            return d;
        if (double.TryParse(text.Replace(".", ","), out var d2))
            return d2;
        return 0;
    }

    private static int GetInt(ExcelWorksheet ws, int row, int col)
    {
        var cell = ws.Cells[row, col];
        if (cell.Value is int i) return i;
        return (int)Math.Round(GetDouble(ws, row, col));
    }

    private static string GetString(ExcelWorksheet ws, int row, int col)
    {
        return ws.Cells[row, col].Text?.Trim() ?? "";
    }

    private static int FindHeaderRow(ExcelWorksheet ws, int startRow, int col, string[] keywords)
    {
        for (int row = startRow; row <= (ws.Dimension?.End.Row ?? 100); row++)
        {
            var text = GetString(ws, row, col);
            if (keywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase)))
                return row;
        }
        return 0;
    }
}

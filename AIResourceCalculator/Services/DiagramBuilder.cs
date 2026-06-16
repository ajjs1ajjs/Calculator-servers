using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using AIResourceCalculator.Models;

namespace AIResourceCalculator.Services;

public static class DiagramBuilder
{
    private const double NodeW = 180;
    private const double NodeH = 65;
    private const double GapX = 40;
    private const double GapY = 30;
    private const double OffsetX = 40;
    private const double OffsetY = 30;

    public static Border BuildDiagram(ResourceRequirement req)
    {
        var canvas = new Canvas();
        var nodes = req.Infrastructure.ToList();
        if (nodes.Count == 0)
            return new Border { Child = new TextBlock { Text = "No infrastructure data", Foreground = Brushes.Gray } };

        var sql = nodes.Where(n => n.Name.Contains("SQL", StringComparison.OrdinalIgnoreCase)).ToList();
        var masters = nodes.Where(n => n.Name.Contains("Master", StringComparison.OrdinalIgnoreCase)).ToList();
        var gpu = nodes.Where(n => n.Name.Contains("GPU", StringComparison.OrdinalIgnoreCase)).ToList();
        var workers = nodes.Where(n => n.Name.Contains("Worker", StringComparison.OrdinalIgnoreCase)).ToList();
        var apps = nodes.Where(n => n.Name.Contains("App", StringComparison.OrdinalIgnoreCase) && !n.Name.Contains("Worker", StringComparison.OrdinalIgnoreCase)).ToList();
        var webs = nodes.Where(n => (n.Name.Contains("Web", StringComparison.OrdinalIgnoreCase) || n.Name.Contains("IIS", StringComparison.OrdinalIgnoreCase)) && !n.Name.Contains("Worker", StringComparison.OrdinalIgnoreCase)).ToList();
        var others = nodes.Except(sql).Except(masters).Except(gpu).Except(workers).Except(apps).Except(webs).ToList();

        var rows = new List<List<InfrastructureNode>>();
        if (sql.Count > 0) rows.Add(sql);
        if (masters.Count > 0) rows.Add(masters);
        if (gpu.Count > 0) rows.Add(gpu);
        if (workers.Count > 0) rows.Add(workers);
        if (apps.Count > 0) rows.Add(apps);
        if (webs.Count > 0) rows.Add(webs);
        if (others.Count > 0) rows.Add(others);

        var positions = new Dictionary<string, (double x, double y, double cx, double cy)>();
        double maxY = OffsetY;

        for (int rowIdx = 0; rowIdx < rows.Count; rowIdx++)
        {
            var row = rows[rowIdx];
            var rowWidth = row.Count * NodeW + (row.Count - 1) * GapX;
            var startX = Math.Max(OffsetX, (800 - rowWidth) / 2);

            for (int colIdx = 0; colIdx < row.Count; colIdx++)
            {
                var n = row[colIdx];
                var cx = startX + colIdx * (NodeW + GapX);
                var cy = maxY;
                var centerX = cx + NodeW / 2;
                var centerY = cy + NodeH / 2;
                positions[n.Name] = (cx, cy, centerX, centerY);

                DrawNode(canvas, n, cx, cy);
            }
            maxY += NodeH + GapY;
        }

        // Connection lines: SQL → everyone (data flow), Master → Workers (management)
        foreach (var node in nodes)
        {
            if (node.Name.Contains("SQL", StringComparison.OrdinalIgnoreCase)) continue;

            // SQL connection
            if (sql.Count > 0 && positions.TryGetValue(sql[0].Name, out var sqlPos) && positions.TryGetValue(node.Name, out var nodePos))
            {
                DrawConnection(canvas, sqlPos.cx, sqlPos.cy + NodeH / 2, nodePos.cx, nodePos.cy - NodeH / 2);
            }

            // Master → Worker connection
            if (node.Name.Contains("Worker", StringComparison.OrdinalIgnoreCase) && masters.Count > 0 && positions.TryGetValue(masters[0].Name, out var mPos) && positions.TryGetValue(node.Name, out var workerPos))
            {
                DrawConnection(canvas, mPos.cx + NodeW / 2, mPos.cy, workerPos.cx, workerPos.cy - NodeH / 2);
            }
        }

        // Legend
        var legendX = OffsetX;
        var legendY = maxY + 15;
        var legendItems = new[] { ("SQL Server", "#e74c3c"), ("Master", "#3498db"), ("GPU", "#9b59b6"),
            ("Worker", "#2ecc71"), ("App", "#8e44ad"), ("Web", "#f39c12"), ("Other", "#95a5a6") };
        foreach (var (name, clr) in legendItems)
        {
            var dot = new Rectangle { Width = 12, Height = 12, Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(clr)), RadiusX = 2, RadiusY = 2 };
            Canvas.SetLeft(dot, legendX);
            Canvas.SetTop(dot, legendY);
            canvas.Children.Add(dot);
            var lbl = new TextBlock { Text = name, FontSize = 10, Foreground = Brushes.White, Opacity = 0.7 };
            Canvas.SetLeft(lbl, legendX + 16);
            Canvas.SetTop(lbl, legendY - 1);
            canvas.Children.Add(lbl);
            legendX += 105;
        }

        canvas.Width = Math.Max(800, legendX + GapX);
        canvas.Height = Math.Max(400, legendY + 40);

        return new Border
        {
            Child = canvas,
            Background = new SolidColorBrush(Color.FromRgb(0x1a, 0x1a, 0x2e)),
            MinWidth = 600,
            MinHeight = 300
        };
    }

    private static void DrawNode(Canvas canvas, InfrastructureNode n, double cx, double cy)
    {
        var color = GetNodeColor(n.Name);

        var shadow = new Rectangle { Width = NodeW, Height = NodeH, Fill = new SolidColorBrush(Color.FromArgb(60, 0, 0, 0)), RadiusX = 8, RadiusY = 8 };
        Canvas.SetLeft(shadow, cx + 3);
        Canvas.SetTop(shadow, cy + 3);
        canvas.Children.Add(shadow);

        var rect = new Rectangle { Width = NodeW, Height = NodeH, Fill = new SolidColorBrush(color), RadiusX = 8, RadiusY = 8, Stroke = new SolidColorBrush(Color.FromArgb(100, 255, 255, 255)), StrokeThickness = 1 };
        Canvas.SetLeft(rect, cx);
        Canvas.SetTop(rect, cy);
        canvas.Children.Add(rect);

        var nameText = new TextBlock { Text = n.Name, FontSize = 13, FontWeight = FontWeights.Bold, Foreground = Brushes.White, TextAlignment = TextAlignment.Center, Width = NodeW - 10 };
        Canvas.SetLeft(nameText, cx + 5);
        Canvas.SetTop(nameText, cy + 6);
        canvas.Children.Add(nameText);

        var specText = new TextBlock { Text = $"{n.Cpu} vCPU  |  {n.RamGb} GB  |  {n.NodeCount}x", FontSize = 10, Foreground = Brushes.White, Opacity = 0.85, TextAlignment = TextAlignment.Center, Width = NodeW - 10 };
        Canvas.SetLeft(specText, cx + 5);
        Canvas.SetTop(specText, cy + 26);
        canvas.Children.Add(specText);

        var infoText = new TextBlock { Text = $"{n.StorageGb} GB  |  {ShortOs(n.Os)}", FontSize = 9, Foreground = Brushes.White, Opacity = 0.6, TextAlignment = TextAlignment.Center, Width = NodeW - 10 };
        Canvas.SetLeft(infoText, cx + 5);
        Canvas.SetTop(infoText, cy + 42);
        canvas.Children.Add(infoText);
    }

    private static void DrawConnection(Canvas canvas, double x1, double y1, double x2, double y2)
    {
        var line = new Line { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, Stroke = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)), StrokeThickness = 1.5, StrokeDashArray = new DoubleCollection { 4, 3 } };
        canvas.Children.Add(line);

        var arrowSize = 6;
        var angle = Math.Atan2(y2 - y1, x2 - x1);
        var arrow1 = new Line { X1 = x2, Y1 = y2, X2 = x2 - arrowSize * Math.Cos(angle - 0.4), Y2 = y2 - arrowSize * Math.Sin(angle - 0.4), Stroke = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)), StrokeThickness = 1.5 };
        var arrow2 = new Line { X1 = x2, Y1 = y2, X2 = x2 - arrowSize * Math.Cos(angle + 0.4), Y2 = y2 - arrowSize * Math.Sin(angle + 0.4), Stroke = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)), StrokeThickness = 1.5 };
        canvas.Children.Add(arrow1);
        canvas.Children.Add(arrow2);
    }

    public static string BuildSvg(ResourceRequirement req, ProjectConfig? config = null)
    {
        var svc = new ConfigExportService();
        return svc.ExportSvg(req, config ?? new ProjectConfig { ProjectName = "Project" });
    }

    private static Color GetNodeColor(string name)
    {
        var n = name.ToLower();
        if (n.Contains("sql")) return Color.FromRgb(0xe7, 0x4c, 0x3c);
        if (n.Contains("master")) return Color.FromRgb(0x34, 0x98, 0xdb);
        if (n.Contains("worker")) return Color.FromRgb(0x2e, 0xcc, 0x71);
        if (n.Contains("app")) return Color.FromRgb(0x9b, 0x59, 0xb6);
        if (n.Contains("web") || n.Contains("iis")) return Color.FromRgb(0xf3, 0x9c, 0x12);
        return Color.FromRgb(0x95, 0xa5, 0xa6);
    }

    private static string ShortOs(string os) => os switch
    {
        string s when s.Contains("Ubuntu") => "Ubuntu",
        string s when s.Contains("Windows") => "Win",
        string s when s.Contains("Win") => "Win",
        _ => os?.Length > 8 ? os[..8] : (os ?? "")
    };
}

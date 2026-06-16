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
    private const double GapX = 60;
    private const double GapY = 40;
    private const double OffsetX = 40;
    private const double OffsetY = 30;

    public static Border BuildDiagram(ResourceRequirement req)
    {
        var canvas = new Canvas { Width = 800, Height = 400 };

        var nodes = req.Infrastructure.ToList();
        if (nodes.Count == 0) return new Border { Child = new TextBlock { Text = "No infrastructure data", Foreground = Brushes.Gray } };

        var cols = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(nodes.Count)));

        double maxX = OffsetX + NodeW;
        double maxY = OffsetY + NodeH;

        for (int i = 0; i < nodes.Count; i++)
        {
            var n = nodes[i];
            var cx = OffsetX + (i % cols) * (NodeW + GapX);
            var cy = OffsetY + (i / cols) * (NodeH + GapY);
            maxX = Math.Max(maxX, cx + NodeW + GapX);
            maxY = Math.Max(maxY, cy + NodeH + GapY);

            var color = GetNodeColor(n.Name);

            // Shadow
            var shadow = new Rectangle
            {
                Width = NodeW, Height = NodeH,
                Fill = new SolidColorBrush(Color.FromArgb(60, 0, 0, 0)),
                RadiusX = 8, RadiusY = 8
            };
            Canvas.SetLeft(shadow, cx + 3);
            Canvas.SetTop(shadow, cy + 3);
            canvas.Children.Add(shadow);

            // Main rect
            var rect = new Rectangle
            {
                Width = NodeW, Height = NodeH,
                Fill = new SolidColorBrush(color),
                RadiusX = 8, RadiusY = 8,
                Stroke = new SolidColorBrush(Color.FromArgb(100, 255, 255, 255)),
                StrokeThickness = 1
            };
            Canvas.SetLeft(rect, cx);
            Canvas.SetTop(rect, cy);
            canvas.Children.Add(rect);

            // Node name
            var nameText = new TextBlock
            {
                Text = n.Name,
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                TextAlignment = TextAlignment.Center,
                Width = NodeW - 10
            };
            Canvas.SetLeft(nameText, cx + 5);
            Canvas.SetTop(nameText, cy + 6);
            canvas.Children.Add(nameText);

            // Specs
            var specText = new TextBlock
            {
                Text = $"{n.Cpu} vCPU  |  {n.RamGb} GB  |  {n.NodeCount}x",
                FontSize = 10,
                Foreground = Brushes.White,
                Opacity = 0.85,
                TextAlignment = TextAlignment.Center,
                Width = NodeW - 10
            };
            Canvas.SetLeft(specText, cx + 5);
            Canvas.SetTop(specText, cy + 26);
            canvas.Children.Add(specText);

            // Storage + OS
            var infoText = new TextBlock
            {
                Text = $"{n.StorageGb} GB  |  {ShortOs(n.Os)}",
                FontSize = 9,
                Foreground = Brushes.White,
                Opacity = 0.6,
                TextAlignment = TextAlignment.Center,
                Width = NodeW - 10
            };
            Canvas.SetLeft(infoText, cx + 5);
            Canvas.SetTop(infoText, cy + 42);
            canvas.Children.Add(infoText);

            // Connection lines (except first node)
            if (i > 0)
            {
                var prevCx = OffsetX + ((i - 1) % cols) * (NodeW + GapX) + NodeW / 2;
                var prevCy = OffsetY + ((i - 1) / cols) * (NodeH + GapY) + NodeH;
                var curCx = cx + NodeW / 2;
                var curCy = cy;

                var line = new Line
                {
                    X1 = prevCx, Y1 = prevCy,
                    X2 = curCx, Y2 = curCy,
                    Stroke = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)),
                    StrokeThickness = 2,
                    StrokeDashArray = new DoubleCollection { 4, 3 }
                };
                canvas.Children.Add(line);
            }
        }

        // Legend
        var legendY = maxY + 20;
        var legendItems = new[] { ("SQL Server", "#e74c3c"), ("Master", "#3498db"), ("Worker", "#2ecc71"), ("App", "#9b59b6"), ("Web", "#f39c12"), ("Other", "#95a5a6") };
        var legendX = OffsetX;
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

            legendX += 110;
        }

        canvas.Width = Math.Max(800, maxX + GapX);
        canvas.Height = Math.Max(400, legendY + 40);

        return new Border
        {
            Child = canvas,
            Background = new SolidColorBrush(Color.FromRgb(0x1a, 0x1a, 0x2e)),
            MinWidth = 600,
            MinHeight = 300
        };
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

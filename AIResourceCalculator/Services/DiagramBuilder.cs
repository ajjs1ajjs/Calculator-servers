using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using AIResourceCalculator.Models;

namespace AIResourceCalculator.Services;

public static class DiagramBuilder
{
    private const double NodeW = 200;
    private const double NodeH = 74;
    private const double GapX = 36;
    private const double GapY = 56;
    private const double OffsetY = 64;
    private const double CanvasW = 880;

    public static Border BuildDiagram(ResourceRequirement req)
    {
        var canvas = new Canvas();
        var nodes = req.Infrastructure.Where(n => n.NodeCount > 0).ToList();
        if (nodes.Count == 0)
            return new Border { Child = new TextBlock { Text = "Немає даних інфраструктури", Foreground = Brushes.Gray, Margin = new Thickness(20) } };

        var sql = nodes.Where(n => n.Name.Contains("SQL", StringComparison.OrdinalIgnoreCase) || n.Name.Contains("PostgreSQL", StringComparison.OrdinalIgnoreCase) || n.Name.Contains("Oracle", StringComparison.OrdinalIgnoreCase)).ToList();
        var masters = nodes.Where(n => n.Name.Contains("Master", StringComparison.OrdinalIgnoreCase)).ToList();
        var gpu = nodes.Where(n => n.Name.Contains("GPU", StringComparison.OrdinalIgnoreCase)).ToList();
        var workers = nodes.Where(n => n.Name.Contains("Worker", StringComparison.OrdinalIgnoreCase)).ToList();
        var apps = nodes.Where(n => n.Name.Contains("App", StringComparison.OrdinalIgnoreCase) && !n.Name.Contains("Worker", StringComparison.OrdinalIgnoreCase)).ToList();
        var webs = nodes.Where(n => (n.Name.Contains("Web", StringComparison.OrdinalIgnoreCase) || n.Name.Contains("IIS", StringComparison.OrdinalIgnoreCase)) && !n.Name.Contains("Worker", StringComparison.OrdinalIgnoreCase)).ToList();
        var others = nodes.Except(sql).Except(masters).Except(gpu).Except(workers).Except(apps).Except(webs).ToList();

        // Layered top→bottom: entry → web → master/app → worker/gpu → sql (data tier at bottom)
        var rows = new List<List<InfrastructureNode>>();
        void AddRow(List<InfrastructureNode> r) { if (r.Count > 0) rows.Add(r); }
        AddRow(webs);
        AddRow(masters);
        AddRow(apps);
        AddRow(workers);
        AddRow(gpu);
        AddRow(others);
        AddRow(sql);

        var rowY = new double[rows.Count];
        var positions = new Dictionary<string, (double cx, double cy)>(); // top-center anchor
        double y = OffsetY;

        // Entry node ("Користувачі / Балансувальник")
        double entryX = (CanvasW - NodeW) / 2;
        DrawEntryNode(canvas, entryX, 8);
        double entryCx = entryX + NodeW / 2;

        for (int r = 0; r < rows.Count; r++)
        {
            var row = rows[r];
            double rowWidth = row.Count * NodeW + (row.Count - 1) * GapX;
            double startX = Math.Max(20, (CanvasW - rowWidth) / 2);
            rowY[r] = y;
            for (int c = 0; c < row.Count; c++)
            {
                double cx = startX + c * (NodeW + GapX);
                positions[row[c].Name] = (cx + NodeW / 2, y);
                DrawNode(canvas, row[c], cx, y);
            }
            y += NodeH + GapY;
        }

        // Connections: entry → first row; each row → next row (centered trunk)
        if (rows.Count > 0)
            foreach (var n in rows[0])
                DrawConnection(canvas, entryCx, 8 + NodeH, positions[n.Name].cx, positions[n.Name].cy);

        for (int r = 0; r < rows.Count - 1; r++)
        {
            double parentCx = rows[r].Count > 0 ? positions[rows[r][rows[r].Count / 2].Name].cx : entryCx;
            double parentBottom = rowY[r] + NodeH;
            foreach (var child in rows[r + 1])
                DrawConnection(canvas, parentCx, parentBottom, positions[child.Name].cx, positions[child.Name].cy);
        }

        // Title
        var title = new TextBlock { Text = "Схема мережі", FontSize = 16, FontWeight = FontWeights.Bold, Foreground = Brushes.White, Opacity = 0.9 };
        Canvas.SetLeft(title, 20); Canvas.SetTop(title, 18);
        canvas.Children.Add(title);

        // Legend
        double legendY = y + 6, legendX = 20;
        foreach (var (label, hex) in new[] { ("SQL/БД", "#ef4444"), ("Master", "#3b82f6"), ("Worker", "#22c55e"), ("GPU", "#a855f7"), ("App", "#8b5cf6"), ("Web/IIS", "#f59e0b") })
        {
            var dot = new Ellipse { Width = 12, Height = 12, Fill = Brush(hex) };
            Canvas.SetLeft(dot, legendX); Canvas.SetTop(dot, legendY + 2); canvas.Children.Add(dot);
            var lbl = new TextBlock { Text = label, FontSize = 11, Foreground = Brushes.White, Opacity = 0.75 };
            Canvas.SetLeft(lbl, legendX + 17); Canvas.SetTop(lbl, legendY); canvas.Children.Add(lbl);
            legendX += 110;
        }

        canvas.Width = CanvasW;
        canvas.Height = legendY + 36;

        var bg = new LinearGradientBrush(Color.FromRgb(0x0f, 0x18, 0x2e), Color.FromRgb(0x1a, 0x24, 0x44), 90);
        return new Border { Child = canvas, Background = bg, MinWidth = 600, MinHeight = 320 };
    }

    private static void DrawEntryNode(Canvas canvas, double x, double yy)
    {
        var rect = new Rectangle { Width = NodeW, Height = NodeH - 14, RadiusX = 12, RadiusY = 12, Fill = Brush("#334155"), Stroke = Brush("#64748b"), StrokeThickness = 1 };
        Canvas.SetLeft(rect, x); Canvas.SetTop(rect, yy); canvas.Children.Add(rect);
        var t = new TextBlock { Text = "Користувачі / Балансувальник", FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, TextAlignment = TextAlignment.Center, Width = NodeW - 12 };
        Canvas.SetLeft(t, x + 6); Canvas.SetTop(t, yy + 16); canvas.Children.Add(t);
    }

    private static void DrawNode(Canvas canvas, InfrastructureNode n, double cx, double cy)
    {
        var (light, dark) = GetNodeColors(n.Name);

        // shadow
        var shadow = new Rectangle { Width = NodeW, Height = NodeH, RadiusX = 12, RadiusY = 12, Fill = new SolidColorBrush(Color.FromArgb(70, 0, 0, 0)) };
        Canvas.SetLeft(shadow, cx + 3); Canvas.SetTop(shadow, cy + 4); canvas.Children.Add(shadow);

        // card with vertical gradient
        var card = new Rectangle
        {
            Width = NodeW, Height = NodeH, RadiusX = 12, RadiusY = 12,
            Fill = new LinearGradientBrush(light, dark, 90),
            Stroke = new SolidColorBrush(Color.FromArgb(90, 255, 255, 255)), StrokeThickness = 1
        };
        Canvas.SetLeft(card, cx); Canvas.SetTop(card, cy); canvas.Children.Add(card);

        // left accent stripe
        var stripe = new Rectangle { Width = 6, Height = NodeH - 16, RadiusX = 3, RadiusY = 3, Fill = new SolidColorBrush(Color.FromArgb(220, 255, 255, 255)) };
        Canvas.SetLeft(stripe, cx + 8); Canvas.SetTop(stripe, cy + 8); canvas.Children.Add(stripe);

        var name = new TextBlock { Text = n.Name, FontSize = 13, FontWeight = FontWeights.Bold, Foreground = Brushes.White, Width = NodeW - 28, TextTrimming = TextTrimming.CharacterEllipsis };
        Canvas.SetLeft(name, cx + 20); Canvas.SetTop(name, cy + 9); canvas.Children.Add(name);

        var spec = new TextBlock { Text = $"{Trim(n.Cpu)} vCPU · {Trim(n.RamGb)} GB · {n.NodeCount}×", FontSize = 11, Foreground = Brushes.White, Opacity = 0.92, Width = NodeW - 28 };
        Canvas.SetLeft(spec, cx + 20); Canvas.SetTop(spec, cy + 30); canvas.Children.Add(spec);

        var info = new TextBlock { Text = $"{n.TotalStorageGb} GB · {ShortOs(n.Os)}", FontSize = 10, Foreground = Brushes.White, Opacity = 0.65, Width = NodeW - 28 };
        Canvas.SetLeft(info, cx + 20); Canvas.SetTop(info, cy + 48); canvas.Children.Add(info);
    }

    private static void DrawConnection(Canvas canvas, double x1, double y1, double x2, double y2)
    {
        var stroke = new SolidColorBrush(Color.FromArgb(120, 148, 197, 255));
        // elbow path: down, across, down
        double midY = (y1 + y2) / 2;
        var poly = new Polyline
        {
            Stroke = stroke, StrokeThickness = 1.6,
            Points = new PointCollection { new Point(x1, y1), new Point(x1, midY), new Point(x2, midY), new Point(x2, y2 - 6) }
        };
        canvas.Children.Add(poly);

        // arrowhead at child top
        var arrow = new Polygon
        {
            Fill = stroke,
            Points = new PointCollection { new Point(x2 - 5, y2 - 6), new Point(x2 + 5, y2 - 6), new Point(x2, y2) }
        };
        canvas.Children.Add(arrow);
    }

    public static string BuildSvg(ResourceRequirement req, ProjectConfig? config = null)
        => new ConfigExportService().ExportSvg(req, config ?? new ProjectConfig { ProjectName = "Project" });

    private static (Color light, Color dark) GetNodeColors(string name)
    {
        var n = name.ToLower();
        if (n.Contains("sql") || n.Contains("postgre") || n.Contains("oracle")) return (C("#f87171"), C("#dc2626"));
        if (n.Contains("master")) return (C("#60a5fa"), C("#2563eb"));
        if (n.Contains("worker")) return (C("#4ade80"), C("#16a34a"));
        if (n.Contains("gpu")) return (C("#c084fc"), C("#9333ea"));
        if (n.Contains("app")) return (C("#a78bfa"), C("#7c3aed"));
        if (n.Contains("web") || n.Contains("iis")) return (C("#fbbf24"), C("#d97706"));
        return (C("#94a3b8"), C("#64748b"));
    }

    private static SolidColorBrush Brush(string hex) => new(C(hex));
    private static Color C(string hex) => (Color)ColorConverter.ConvertFromString(hex);
    private static string Trim(double v) => v % 1 == 0 ? ((int)v).ToString() : v.ToString("0.#");

    private static string ShortOs(string os) => os switch
    {
        string s when s.Contains("Ubuntu") => "Ubuntu",
        string s when s.Contains("Windows") || s.Contains("Win") => "Windows",
        string s when s.Contains("PaaS") => "PaaS",
        _ => string.IsNullOrEmpty(os) ? "—" : (os.Length > 10 ? os[..10] : os)
    };
}

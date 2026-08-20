using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace ResourceCalculator.Avalonia.Converters;

public class VisibilityConverter : IValueConverter
{
    // Avalonia: IsVisible — bool, а не WPF Visibility.Visible/Collapsed.
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && b;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && b;
}

// Поле кількості користувачів модуля: 0 (= «загальна кількість») показуємо порожнім,
// порожній/некоректний ввід трактуємо як 0.
public class ZeroEmptyConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is int i && i > 0 ? i.ToString() : "";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string s && int.TryParse(s.Trim(), out var n) && n > 0 ? n : 0;
}

public class SeverityToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        IBrush brush = (value?.ToString() ?? "").ToLower() switch
        {
            "critical" => Brushes.Red,
            "warning" => Brushes.Orange,
            "overprovisioned" => new SolidColorBrush(Color.FromRgb(218, 165, 32)),
            "ok" => Brushes.Green,
            "info" => new SolidColorBrush(Color.FromRgb(52, 152, 219)),
            _ => Brushes.Gray
        };
        return brush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
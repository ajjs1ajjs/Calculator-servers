using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace AIResourceCalculator.Converters;

public class SeverityToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return (value?.ToString() ?? "").ToLower() switch
        {
            "critical" => new SolidColorBrush(Colors.Red),
            "warning" => new SolidColorBrush(Colors.Orange),
            "overprovisioned" => new SolidColorBrush(Colors.Goldenrod),
            "ok" => new SolidColorBrush(Colors.Green),
            "info" => new SolidColorBrush(Color.FromRgb(52, 152, 219)),
            _ => new SolidColorBrush(Colors.Gray)
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

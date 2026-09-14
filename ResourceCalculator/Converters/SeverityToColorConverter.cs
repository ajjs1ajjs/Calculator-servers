using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace ResourceCalculator.Converters;

public class SeverityToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return (value?.ToString() ?? "").ToLower() switch
        {
            "critical" => new SolidColorBrush(Color.FromRgb(0xD2, 0x0F, 0x39)),
            "warning" => new SolidColorBrush(Color.FromRgb(0xFE, 0x64, 0x0B)),
            "overprovisioned" => new SolidColorBrush(Color.FromRgb(0xDA, 0xA0, 0x20)),
            "ok" => new SolidColorBrush(Color.FromRgb(0x40, 0xA0, 0x2B)),
            "info" => new SolidColorBrush(Color.FromRgb(0x34, 0x98, 0xDB)),
            _ => new SolidColorBrush(Color.FromRgb(0x6C, 0x6F, 0x85))
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

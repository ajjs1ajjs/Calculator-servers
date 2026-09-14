using Avalonia.Data.Converters;
using Avalonia.Data;
using System;
using System.Globalization;

namespace ResourceCalculator.Converters;

public class ZeroEmptyConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is int i && i > 0 ? i.ToString() : "";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string s && int.TryParse(s.Trim(), out var n) && n > 0 ? n : 0;
}

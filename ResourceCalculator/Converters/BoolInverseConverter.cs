using Avalonia.Data;
using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace ResourceCalculator.Converters;

public class BoolInverseConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && !b;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

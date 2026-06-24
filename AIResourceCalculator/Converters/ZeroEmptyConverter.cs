using System.Globalization;
using System.Windows.Data;

namespace AIResourceCalculator.Converters;

// Поле кількості користувачів модуля: 0 (= «загальна кількість») показуємо порожнім,
// порожній/некоректний ввід трактуємо як 0.
public class ZeroEmptyConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is int i && i > 0 ? i.ToString() : "";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string s && int.TryParse(s.Trim(), out var n) && n > 0 ? n : 0;
}

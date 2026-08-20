using Avalonia.Data;
using Avalonia.Markup.Xaml;

namespace ResourceCalculator.Avalonia.Localization;

// Локалізований текст: {loc:Loc key} -> Binding на LocalizationService.Instance["key"].
public class LocExtension : MarkupExtension
{
    public string Key { get; set; } = "";

    public LocExtension() { }

    public LocExtension(string key)
    {
        Key = key;
    }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        return new Binding
        {
            Source = ResourceCalculator.Localization.LocalizationService.Instance,
            Path = $"[{Key}]",
            Mode = BindingMode.OneWay,
            FallbackValue = $"[{Key}]"
        };
    }
}
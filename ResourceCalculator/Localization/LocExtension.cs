using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Data;
using Avalonia.Markup.Xaml;

namespace ResourceCalculator.Localization;

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
        return new Binding($"[{Key}]")
        {
            Source = LocalizationService.Instance,
            Mode = BindingMode.OneWay,
            FallbackValue = $"[{Key}]"
        };
    }
}

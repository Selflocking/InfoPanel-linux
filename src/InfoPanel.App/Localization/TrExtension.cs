using Avalonia.Data;
using Avalonia.Markup.Xaml;

namespace InfoPanel.Localization;

public sealed class TrExtension(string key) : MarkupExtension
{
    public override object ProvideValue(IServiceProvider serviceProvider) =>
        new Binding($"[{key}]") { Source = Localizer.Instance, Mode = BindingMode.OneWay };
}

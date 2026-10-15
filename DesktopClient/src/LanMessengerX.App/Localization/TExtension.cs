using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.MarkupExtensions;
using System.Globalization;

namespace LanMessengerX.App.Localization;

/// <summary>XAML: <c>Text="{loc:T 'Some English text'}"</c> - re-evaluated when the language changes.</summary>
public sealed class TExtension : MarkupExtension
{
    public TExtension(string key) => Key = key;

    public string Key { get; set; }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var b = new ReflectionBindingExtension(nameof(Loc.Revision))
        {
            Source = Loc.Instance,
            Mode = BindingMode.OneWay,
            Converter = new KeyConverter(Key),
        };
        return b.ProvideValue(serviceProvider);
    }

    private sealed class KeyConverter : IValueConverter
    {
        private readonly string _key;
        public KeyConverter(string key) => _key = key;
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => Loc.T(_key);
        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
    }
}

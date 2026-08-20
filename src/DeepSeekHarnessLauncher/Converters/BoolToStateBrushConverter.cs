using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace DeepSeekHarnessLauncher.Converters;

/// <summary>布尔 → 状态色（true=绿，false=红）。</summary>
public sealed class BoolToStateBrushConverter : IValueConverter
{
    private static readonly Brush TrueBrush = Freeze(0x4C, 0xAF, 0x50);
    private static readonly Brush FalseBrush = Freeze(0xE5, 0x39, 0x35);

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? TrueBrush : FalseBrush;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static Brush Freeze(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}

using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using DeepSeekHarnessLauncher.Models;

namespace DeepSeekHarnessLauncher.Converters;

/// <summary>服务状态 → 颜色画刷。</summary>
public sealed class ServiceStateToBrushConverter : IValueConverter
{
    private static readonly Brush Stopped = Freeze(0x9E, 0x9E, 0x9E);   // 灰
    private static readonly Brush Transition = Freeze(0xF5, 0xB0, 0x41); // 黄
    private static readonly Brush Running = Freeze(0x4C, 0xAF, 0x50);    // 绿
    private static readonly Brush Faulted = Freeze(0xE5, 0x39, 0x35);    // 红

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is ServiceState s ? s switch
        {
            ServiceState.Stopped => Stopped,
            ServiceState.Starting or ServiceState.Stopping => Transition,
            ServiceState.Running => Running,
            ServiceState.Faulted => Faulted,
            _ => Stopped,
        } : Stopped;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static Brush Freeze(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}

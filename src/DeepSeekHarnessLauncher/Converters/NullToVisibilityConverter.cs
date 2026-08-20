using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DeepSeekHarnessLauncher.Converters;

/// <summary>null → Collapsed，非 null → Visible（用于错误提示）。</summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is null ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

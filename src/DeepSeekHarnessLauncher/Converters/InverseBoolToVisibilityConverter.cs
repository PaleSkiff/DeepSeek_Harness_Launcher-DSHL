using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DeepSeekHarnessLauncher.Converters;

/// <summary>布尔取反 → 可见性（false=Visible，true=Collapsed）。</summary>
public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && !b ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

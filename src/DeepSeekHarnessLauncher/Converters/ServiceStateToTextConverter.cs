using System.Globalization;
using System.Windows;
using System.Windows.Data;
using DeepSeekHarnessLauncher.Models;

namespace DeepSeekHarnessLauncher.Converters;

/// <summary>服务状态 → 文案（跟随当前语言）。</summary>
public sealed class ServiceStateToTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value is ServiceState s ? s switch
        {
            ServiceState.Stopped => "State.Stopped",
            ServiceState.Starting => "State.Starting",
            ServiceState.Running => "State.Running",
            ServiceState.Stopping => "State.Stopping",
            ServiceState.Faulted => "State.Faulted",
            _ => "State.Stopped",
        } : "State.Stopped";

        return Application.Current?.Resources[key] is string text ? text : key;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

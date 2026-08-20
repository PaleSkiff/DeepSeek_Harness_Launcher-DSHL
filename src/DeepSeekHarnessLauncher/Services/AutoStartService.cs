using Microsoft.Win32;

namespace DeepSeekHarnessLauncher.Services;

public interface IAutoStartService
{
    bool IsEnabled { get; }
    void SetEnabled(bool enabled);
}

/// <summary>开机自启：写入 HKCU Run（用户级，无需管理员）。</summary>
public sealed class AutoStartService : IAutoStartService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private readonly string _valueName;

    public AutoStartService(string valueName = "DeepSeekHarnessLauncher")
    {
        _valueName = valueName;
    }

    public bool IsEnabled
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
                return key?.GetValue(_valueName) is not null;
            }
            catch
            {
                return false;
            }
        }
    }

    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
        if (enabled)
        {
            var exePath = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(exePath))
                key.SetValue(_valueName, $"\"{exePath}\"");
        }
        else
        {
            key.DeleteValue(_valueName, throwOnMissingValue: false);
        }
    }
}

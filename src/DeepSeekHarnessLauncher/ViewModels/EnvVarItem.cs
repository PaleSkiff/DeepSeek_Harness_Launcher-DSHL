using CommunityToolkit.Mvvm.ComponentModel;

namespace DeepSeekHarnessLauncher.ViewModels;

/// <summary>配置页环境变量表的一行。</summary>
public partial class EnvVarItem : ObservableObject
{
    [ObservableProperty]
    private string _key = string.Empty;

    [ObservableProperty]
    private string _value = string.Empty;

    /// <summary>值是否明文显示（掩码开关）。</summary>
    [ObservableProperty]
    private bool _isValueVisible;
}

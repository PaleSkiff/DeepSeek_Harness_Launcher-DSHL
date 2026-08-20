using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace DeepSeekHarnessLauncher.Views;

public partial class EnvironmentView : UserControl
{
    public EnvironmentView()
    {
        InitializeComponent();
    }

    /// <summary>安装日志自动滚轮：新日志到达时自动滚动到底部。</summary>
    private void InstallLogBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox box)
            box.ScrollToEnd();
    }

    private void OpenNodeWebsite_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("https://nodejs.org/") { UseShellExecute = true });
        }
        catch
        {
            // 忽略打开失败。
        }
    }
}

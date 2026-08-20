using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DeepSeekHarnessLauncher.ViewModels;

namespace DeepSeekHarnessLauncher.Views;

public partial class AboutView : UserControl
{
    public AboutView()
    {
        InitializeComponent();
    }

    private void OpenBilibili_Click(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is AboutViewModel vm && !string.IsNullOrWhiteSpace(vm.BilibiliUrl))
        {
            try
            {
                Process.Start(new ProcessStartInfo(vm.BilibiliUrl) { UseShellExecute = true });
            }
            catch
            {
                // 忽略打开失败。
            }
        }
    }
}

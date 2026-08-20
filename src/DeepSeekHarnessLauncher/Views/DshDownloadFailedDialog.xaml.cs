using System.Diagnostics;
using System.Windows;

namespace DeepSeekHarnessLauncher.Views;

/// <summary>DeepSeek Harness 下载失败对话框：提供打开终端 / 复制命令。</summary>
public partial class DshDownloadFailedDialog : Window
{
    private const string DshCommand = "npx --verbose @deepseek-ai/dsh web";

    public DshDownloadFailedDialog()
    {
        InitializeComponent();
        TitleText.Text = Application.Current?.Resources["Dlg.DshDownloadFailedTitle"] as string
            ?? "DeepSeek Harness 下载失败";
        MessageText.Text = Application.Current?.Resources["Dlg.DshDownloadFailedMessage"] as string
            ?? "自动下载未能完成，你可以手动在终端执行以下命令：";
        CommandText.Text = DshCommand;
    }

    /// <summary>显示对话框。</summary>
    public static void Show(Window? owner)
    {
        if (Application.Current is null)
            return;

        var dialog = new DshDownloadFailedDialog { Owner = owner };
        dialog.ShowDialog();
    }

    private void OpenTerminal_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("cmd.exe") { UseShellExecute = true });
        }
        catch
        {
            // 打开终端失败忽略。
        }
        DialogResult = true;
    }

    private void CopyCommand_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(DshCommand);
        }
        catch
        {
            // 复制失败忽略。
        }
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}

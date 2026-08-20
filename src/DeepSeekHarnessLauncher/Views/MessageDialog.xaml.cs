using System.Windows;

namespace DeepSeekHarnessLauncher.Views;

/// <summary>统一风格的自定义提示框（深色圆角，支持信息提示与确认）。</summary>
public partial class MessageDialog : Window
{
    private MessageDialog(string title, string message, bool confirm)
    {
        InitializeComponent();
        TitleText.Text = title;
        MessageText.Text = message;
        if (confirm)
        {
            CancelButton.Visibility = Visibility.Visible;
            CancelButton.Focus();
        }
        else
        {
            OkButton.Focus();
        }
    }

    public bool Result { get; private set; }

    /// <summary>显示信息提示框。</summary>
    public static void ShowInfo(Window? owner, string title, string message)
    {
        if (Application.Current is null)
            return; // 无 UI 上下文（如单元测试）时跳过。

        var dialog = new MessageDialog(title, message, confirm: false) { Owner = owner };
        dialog.ShowDialog();
    }

    /// <summary>显示确认框，返回用户是否点击确定。</summary>
    public static bool ShowConfirm(Window? owner, string title, string message)
    {
        if (Application.Current is null)
            return true; // 无 UI 上下文（如单元测试）时默认确认。

        var dialog = new MessageDialog(title, message, confirm: true) { Owner = owner };
        dialog.ShowDialog();
        return dialog.Result;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        Result = true;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Result = false;
        DialogResult = false;
    }
}

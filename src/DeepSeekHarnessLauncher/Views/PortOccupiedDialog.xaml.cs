using System.Windows;

namespace DeepSeekHarnessLauncher.Views;

public enum PortOccupiedAction
{
    ChangePort,
    ViewProcess,
    Cancel
}

public partial class PortOccupiedDialog : Window
{
    public PortOccupiedDialog(int port, int pid)
    {
        InitializeComponent();
        var template = Application.Current?.Resources["Dlg.PortMessage"] as string
            ?? "端口 {0} 已被占用（占用进程 PID {1}）。请选择处理方式：";
        MessageText.Text = string.Format(template, port, pid);
    }

    public PortOccupiedAction Action { get; private set; } = PortOccupiedAction.Cancel;

    private void ChangePort_Click(object sender, RoutedEventArgs e)
    {
        Action = PortOccupiedAction.ChangePort;
        DialogResult = true;
    }

    private void ViewProcess_Click(object sender, RoutedEventArgs e)
    {
        Action = PortOccupiedAction.ViewProcess;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Action = PortOccupiedAction.Cancel;
        DialogResult = true;
    }
}

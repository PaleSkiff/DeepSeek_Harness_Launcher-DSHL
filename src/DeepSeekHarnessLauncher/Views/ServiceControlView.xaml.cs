using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using DeepSeekHarnessLauncher.Models;
using DeepSeekHarnessLauncher.ViewModels;

namespace DeepSeekHarnessLauncher.Views;

public partial class ServiceControlView : UserControl
{
    public ServiceControlView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is ServiceControlViewModel oldVm)
            oldVm.PortOccupied -= OnPortOccupied;
        if (e.NewValue is ServiceControlViewModel newVm)
            newVm.PortOccupied += OnPortOccupied;
    }

    private void OnPortOccupied(object? sender, PortOccupiedEventArgs e)
    {
        var dialog = new PortOccupiedDialog(e.Port, e.Pid)
        {
            Owner = Window.GetWindow(this),
        };

        if (dialog.ShowDialog() != true)
            return;

        switch (dialog.Action)
        {
            case PortOccupiedAction.ChangePort:
                if (Window.GetWindow(this)?.DataContext is MainViewModel mainVm)
                    mainVm.NavigateCommand.Execute("config");
                break;

            case PortOccupiedAction.ViewProcess:
                OpenTaskManager();
                break;
        }
    }

    private static void OpenTaskManager()
    {
        try
        {
            Process.Start(new ProcessStartInfo("taskmgr.exe") { UseShellExecute = true });
        }
        catch
        {
            // 忽略打开失败。
        }
    }
}

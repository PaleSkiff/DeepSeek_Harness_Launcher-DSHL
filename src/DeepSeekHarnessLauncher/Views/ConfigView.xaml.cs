using System.Windows;
using System.Windows.Controls;
using DeepSeekHarnessLauncher.ViewModels;

namespace DeepSeekHarnessLauncher.Views;

public partial class ConfigView : UserControl
{
    public ConfigView()
    {
        InitializeComponent();
    }

    private void BrowseWorkingDirectory_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = Application.Current?.Resources["Dlg.ChooseWorkDir"] as string ?? "选择工作目录",
        };

        if (dialog.ShowDialog() == true && DataContext is ConfigViewModel vm)
        {
            vm.WorkingDirectory = dialog.FolderName;
        }
    }
}

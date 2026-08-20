using System.Windows;
using System.Windows.Controls;
using DeepSeekHarnessLauncher.ViewModels;

namespace DeepSeekHarnessLauncher.Views;

public partial class LogView : UserControl
{
    public LogView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => HookAutoScroll();
    }

    private void HookAutoScroll()
    {
        if (DataContext is not LogViewModel vm)
            return;

        vm.EntriesView.CollectionChanged += (_, _) =>
        {
            if (vm.AutoScroll && LogList.Items.Count > 0)
            {
                LogList.ScrollIntoView(LogList.Items[^1]);
            }
        };
    }
}

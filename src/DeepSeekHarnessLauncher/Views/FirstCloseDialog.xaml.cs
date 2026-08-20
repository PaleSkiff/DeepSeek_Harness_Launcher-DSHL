using System.Windows;

namespace DeepSeekHarnessLauncher.Views;

public enum CloseChoice
{
    MinimizeToTray,
    Exit
}

public partial class FirstCloseDialog : Window
{
    public FirstCloseDialog()
    {
        InitializeComponent();
    }

    public CloseChoice Choice { get; private set; } = CloseChoice.MinimizeToTray;
    public bool Remember { get; private set; }

    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        Choice = CloseChoice.MinimizeToTray;
        Remember = RememberCheckBox.IsChecked == true;
        DialogResult = true;
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        Choice = CloseChoice.Exit;
        Remember = RememberCheckBox.IsChecked == true;
        DialogResult = true;
    }
}

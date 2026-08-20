using System.Windows;
using System.Windows.Threading;

namespace DeepSeekHarnessLauncher.Views;

/// <summary>
/// 最小化到托盘时的提示卡片：屏幕右上角滑入淡入，2 秒后滑出淡出。
/// </summary>
public partial class TrayNotifyWindow : Window
{
    private double _targetTop;

    public TrayNotifyWindow()
    {
        InitializeComponent();

        var workArea = SystemParameters.WorkArea;
        _targetTop = workArea.Top + 16;
        Left = workArea.Right - Width - 16;
        Top = workArea.Top - Height; // 初始在屏幕上方外，准备滑入。

        TitleText.Text = GetText("Tray.NotifyTitle");
        MessageText.Text = GetText("Tray.NotifyMessage");
    }

    public async Task ShowAndDismissAsync()
    {
        if (Application.Current is null)
            return;

        Show();
        Activate();
        Topmost = true;

        await AnimateAsync(Top, _targetTop, 0, 1);        // 滑入 + 淡入
        await Task.Delay(2000);                            // 停留 2 秒
        await AnimateAsync(_targetTop, _targetTop - Height, 1, 0); // 滑出 + 淡出

        Close();
    }

    private async Task AnimateAsync(double fromTop, double toTop, double fromOpacity, double toOpacity)
    {
        var tcs = new TaskCompletionSource<bool>();
        const double durationMs = 260;
        var start = DateTime.Now;

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        timer.Tick += (_, _) =>
        {
            var progress = Math.Min(1.0, (DateTime.Now - start).TotalMilliseconds / durationMs);
            Top = fromTop + (toTop - fromTop) * progress;
            Opacity = fromOpacity + (toOpacity - fromOpacity) * progress;

            if (progress >= 1.0)
            {
                timer.Stop();
                tcs.TrySetResult(true);
            }
        };
        timer.Start();
        await tcs.Task;
    }

    private static string GetText(string key)
        => Application.Current?.Resources[key] as string ?? key;
}

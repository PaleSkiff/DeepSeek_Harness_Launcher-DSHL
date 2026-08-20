using System.Windows;
using DeepSeekHarnessLauncher.Models;

namespace DeepSeekHarnessLauncher.Views;

/// <summary>
/// 首次启动引导对话框：应用介绍 + 三步使用说明 + 当前环境状态。
/// 仅在首次启动（config.json 不存在）时弹出。
/// </summary>
public partial class FirstRunGuideDialog : Window
{
    public FirstRunGuideDialog(EnvironmentCheckResult result)
    {
        InitializeComponent();

        NodeStatusText.Text = BuildNodeStatus(result);
        DshStatusText.Text = BuildDshStatus(result);
    }

    /// <summary>构造节点状态文案。纯函数，便于测试。</summary>
    public static string BuildNodeStatus(EnvironmentCheckResult result)
        => result.NodeInstalled
            ? $"node.js ✓ {result.NodeVersion?.Trim() ?? string.Empty}".Trim()
            : $"node.js ✗ {Get("Lbl.NotInstalled")}";

    /// <summary>构造 DSH 状态文案。纯函数，便于测试。</summary>
    public static string BuildDshStatus(EnvironmentCheckResult result)
        => result.DshAvailable
            ? $"DeepSeek Harness ✓ {result.DshVersion?.Trim() ?? string.Empty}".Trim()
            : $"DeepSeek Harness ✗ {Get("Lbl.Unavailable")}";

    public static void Show(Window? owner, EnvironmentCheckResult result)
    {
        var dialog = new FirstRunGuideDialog(result) { Owner = owner };
        dialog.ShowDialog();
    }

    private void Start_Click(object sender, RoutedEventArgs e) => Close();

    private static string Get(string key)
        => Application.Current?.Resources[key] as string ?? key;
}

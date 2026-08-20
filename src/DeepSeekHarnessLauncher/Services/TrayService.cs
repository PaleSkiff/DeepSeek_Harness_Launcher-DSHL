using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using DeepSeekHarnessLauncher.Helpers;
using DeepSeekHarnessLauncher.Models;

namespace DeepSeekHarnessLauncher.Services;

public sealed record TrayCallbacks(
    Action ShowWindow,
    Func<Task> Start,
    Func<Task> Stop,
    Func<Task> Restart,
    Func<Task> Exit);

public interface ITrayService
{
    void Initialize(TrayCallbacks callbacks);
    void UpdateState(ServiceState state, ServiceStatus status);
    /// <summary>语言切换后按当前语言刷新托盘菜单与提示文字。</summary>
    void RefreshTexts();
    void Dispose();
}

/// <summary>系统托盘图标 + 右键菜单 + 双击恢复。</summary>
public sealed class TrayService : ITrayService
{
    private readonly ILocalizationService _localization;
    private NotifyIcon? _icon;
    private ToolStripMenuItem? _stateItem;
    private ToolStripMenuItem? _startItem;
    private ToolStripMenuItem? _stopItem;
    private ToolStripMenuItem? _restartItem;
    private ToolStripMenuItem? _openItem;
    private ToolStripMenuItem? _exitItem;
    private ServiceState _lastState = ServiceState.Stopped;
    private string _lastAddress = "127.0.0.1:3080";

    public TrayService(ILocalizationService localization)
    {
        _localization = localization;
        // 语言切换后立即刷新托盘菜单/提示文字，避免英文模式下残留中文。
        _localization.LanguageChanged += (_, _) => RefreshTexts();
    }

    public void Initialize(TrayCallbacks callbacks)
    {
        var menu = new ContextMenuStrip();
        _stateItem = new ToolStripMenuItem($"{GetText("Tray.State")}{GetText("Tray.StateSeparator")}{GetText("State.Stopped")}") { Enabled = false };
        _startItem = new ToolStripMenuItem(GetText("Tray.Start"), null, async (_, _) => await callbacks.Start());
        _stopItem = new ToolStripMenuItem(GetText("Tray.Stop"), null, async (_, _) => await callbacks.Stop());
        _restartItem = new ToolStripMenuItem(GetText("Tray.Restart"), null, async (_, _) => await callbacks.Restart());
        _openItem = new ToolStripMenuItem(GetText("Tray.Open"), null, (_, _) => callbacks.ShowWindow());
        _exitItem = new ToolStripMenuItem(GetText("Tray.Exit"), null, async (_, _) => await callbacks.Exit());

        menu.Items.Add(_stateItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_startItem);
        menu.Items.Add(_stopItem);
        menu.Items.Add(_restartItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_openItem);
        menu.Items.Add(_exitItem);

        _icon = new NotifyIcon
        {
            // 托盘图标使用应用 logo；加载失败时回退到彩色圆点。
            Icon = LogoIconHelper.GetTrayIcon() ?? CreateStateIcon(StateToColor(ServiceState.Stopped)),
            Text = $"DeepSeek Harness Launcher — {GetText("State.Stopped")}",
            Visible = true,
            ContextMenuStrip = menu,
        };
        _icon.DoubleClick += (_, _) => callbacks.ShowWindow();
    }

    public void UpdateState(ServiceState state, ServiceStatus status)
    {
        if (_icon is null)
            return;

        _lastState = state;
        _lastAddress = $"127.0.0.1:{status.Port}";
        RefreshTexts();

        if (_startItem is not null)
            _startItem.Enabled = state is ServiceState.Stopped or ServiceState.Faulted;
        if (_stopItem is not null)
            _stopItem.Enabled = state is ServiceState.Running or ServiceState.Starting;
        if (_restartItem is not null)
            _restartItem.Enabled = state is ServiceState.Running or ServiceState.Faulted;
    }

    /// <summary>
    /// 按当前语言重新读取全部本地化文案（菜单项、状态行、托盘提示文字）。
    /// 语言切换后调用，保证英文模式下托盘不再残留中文。
    /// </summary>
    public void RefreshTexts()
    {
        if (_icon is null)
            return;

        var stateText = StateToText(_lastState);
        _icon.Text = $"DeepSeek Harness Launcher — {stateText} ({_lastAddress})";

        if (_stateItem is not null)
            _stateItem.Text = $"{GetText("Tray.State")}{GetText("Tray.StateSeparator")}{stateText} ({_lastAddress})";
        if (_startItem is not null)
            _startItem.Text = GetText("Tray.Start");
        if (_stopItem is not null)
            _stopItem.Text = GetText("Tray.Stop");
        if (_restartItem is not null)
            _restartItem.Text = GetText("Tray.Restart");
        if (_openItem is not null)
            _openItem.Text = GetText("Tray.Open");
        if (_exitItem is not null)
            _exitItem.Text = GetText("Tray.Exit");
    }

    public void Dispose()
    {
        _icon?.Dispose();
        _icon = null;
    }

    private static string StateToText(ServiceState state) => state switch
    {
        ServiceState.Stopped => GetText("State.Stopped"),
        ServiceState.Starting => GetText("State.Starting"),
        ServiceState.Running => GetText("State.Running"),
        ServiceState.Stopping => GetText("State.Stopping"),
        ServiceState.Faulted => GetText("State.Faulted"),
        _ => GetText("State.Stopped"),
    };

    private static string GetText(string key)
        => System.Windows.Application.Current?.Resources[key] as string ?? key;

    private static Color StateToColor(ServiceState state) => state switch
    {
        ServiceState.Running => Color.FromArgb(0x4C, 0xAF, 0x50),
        ServiceState.Starting or ServiceState.Stopping => Color.FromArgb(0xF5, 0xB0, 0x41),
        ServiceState.Faulted => Color.FromArgb(0xE5, 0x39, 0x35),
        _ => Color.FromArgb(0x9E, 0x9E, 0x9E),
    };

    private static Icon CreateStateIcon(Color color)
    {
        using var bitmap = new Bitmap(16, 16);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(Color.Transparent);
            using var brush = new SolidBrush(color);
            graphics.FillEllipse(brush, 1, 1, 14, 14);
        }

        var handle = bitmap.GetHicon();
        try
        {
            return (Icon)Icon.FromHandle(handle).Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);
}

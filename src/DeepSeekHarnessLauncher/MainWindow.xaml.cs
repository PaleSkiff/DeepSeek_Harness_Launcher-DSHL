using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using DeepSeekHarnessLauncher.Helpers;
using DeepSeekHarnessLauncher.Models;
using DeepSeekHarnessLauncher.Services;
using DeepSeekHarnessLauncher.ViewModels;
using DeepSeekHarnessLauncher.Views;

namespace DeepSeekHarnessLauncher;

public partial class MainWindow : Window
{
    private readonly IServiceController _controller;
    private readonly IConfigService _configService;
    private readonly ITrayService _trayService;
    private readonly IEnvironmentService _environmentService;
    private bool _reallyExit;
    private bool _firstRunGuideShown;

    public MainWindow(
        MainViewModel viewModel,
        IServiceController controller,
        IConfigService configService,
        ITrayService trayService,
        IEnvironmentService environmentService)
    {
        InitializeComponent();
        DataContext = viewModel;
        _controller = controller;
        _configService = configService;
        _trayService = trayService;
        _environmentService = environmentService;

        // 窗口图标使用应用 logo。
        var windowIcon = LogoIconHelper.GetWindowIcon();
        if (windowIcon is not null)
            Icon = windowIcon;

        Loaded += OnLoaded;
        Closing += OnClosing;
        StateChanged += OnStateChanged;
    }

    /// <summary>禁止最大化：无边框窗口最大化会破坏布局，任何途径（双击标题栏/快捷键）都恢复为正常窗口。</summary>
    private void OnStateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Maximized)
            WindowState = WindowState.Normal;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        _trayService.Initialize(new TrayCallbacks(
            ShowWindow: ShowFromTray,
            Start: () => _controller.StartAsync(),
            Stop: () => _controller.StopAsync(),
            Restart: () => _controller.RestartAsync(),
            Exit: ExitApplicationAsync));

        _controller.StateChanged += (_, s) => _trayService.UpdateState(s, _controller.Current);
        _controller.StatusUpdated += (_, st) => _trayService.UpdateState(_controller.State, st);

        var config = _configService.Load();
        if (config.Behavior.AutoStartServiceOnLaunch)
        {
            _ = _controller.StartAsync();
        }
        else
        {
            // 未配置自动启动时，自动检测是否已有外部启动的服务在运行。
            _ = _controller.DetectExternalStateAsync();
        }

        await ShowFirstRunGuideIfNeededAsync();
    }

    private async Task ShowFirstRunGuideIfNeededAsync()
    {
        if (_firstRunGuideShown)
            return;
        _firstRunGuideShown = true;

        // 首次启动（尚无 config.json）时弹出完整引导：应用介绍 + 使用步骤 + 当前环境状态。
        if (File.Exists(_configService.ConfigPath))
            return;

        var result = await _environmentService.CheckAsync();
        FirstRunGuideDialog.Show(this, result);
    }

    private static string GetText(string key)
        => System.Windows.Application.Current?.Resources[key] as string ?? key;

    private void ShowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_reallyExit)
            return;

        e.Cancel = true;
        RequestClose();
    }

    private void RequestClose()
    {
        var config = _configService.Load();

        if (config.Behavior.AskOnFirstClose)
        {
            var dialog = new FirstCloseDialog { Owner = this };
            if (dialog.ShowDialog() == true)
            {
                if (dialog.Remember)
                {
                    config.Behavior.AskOnFirstClose = false;
                    config.Behavior.CloseToTray = dialog.Choice == CloseChoice.MinimizeToTray;
                    _configService.Save(config);
                }
                HandleCloseChoice(dialog.Choice);
            }
            return;
        }

        HandleCloseChoice(config.Behavior.CloseToTray
            ? CloseChoice.MinimizeToTray
            : CloseChoice.Exit);
    }

    private void HandleCloseChoice(CloseChoice choice)
    {
        if (choice == CloseChoice.MinimizeToTray)
        {
            HideToTray();
        }
        else
        {
            _ = ExitApplicationAsync();
        }
    }

    private void HideToTray()
    {
        Hide();
        _ = ShowTrayNotifyAsync();
    }

    private static async Task ShowTrayNotifyAsync()
    {
        try
        {
            var notify = new TrayNotifyWindow();
            await notify.ShowAndDismissAsync();
        }
        catch
        {
            // 提示卡片失败不影响主流程。
        }
    }

    private async Task ExitApplicationAsync()
    {
        if (_reallyExit)
            return;

        var config = _configService.Load();
        var state = _controller.State;
        if (config.Behavior.StopServiceOnExit
            && state is ServiceState.Running or ServiceState.Starting or ServiceState.Stopping)
        {
            var continueExit = MessageDialog.ShowConfirm(
                this,
                GetText("Msg.ExitConfirmTitle"),
                GetText("Msg.ExitConfirm"));

            if (!continueExit)
                return;
        }

        _reallyExit = true;
        await _controller.StopAsync();
        _trayService.Dispose();
        Application.Current.Shutdown();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // 仅单击时拖拽窗口；双击标题栏不触发最大化（应用禁止最大化）。
        if (e.ChangedButton != MouseButton.Left || e.ClickCount != 1)
            return;

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            // 拖拽过程中可能出现状态冲突，忽略。
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        HideToTray();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        RequestClose();
    }
}

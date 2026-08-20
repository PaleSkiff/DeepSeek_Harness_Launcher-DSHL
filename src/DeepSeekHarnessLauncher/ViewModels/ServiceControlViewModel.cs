using System.Text.RegularExpressions;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeepSeekHarnessLauncher.Models;
using DeepSeekHarnessLauncher.Services;

namespace DeepSeekHarnessLauncher.ViewModels;

/// <summary>① 服务控制页 ViewModel：状态展示 + 启动/停止/重启。</summary>
public partial class ServiceControlViewModel : ViewModelBase
{
    private readonly IServiceController _controller;
    private readonly IEnvironmentService _environment;
    private readonly IWebBrowserService _browser;
    private bool _environmentReady = true;

    public string DisplayName => "服务控制";

    [ObservableProperty]
    private ServiceState _state = ServiceState.Stopped;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusPortText))]
    [NotifyPropertyChangedFor(nameof(StatusPidText))]
    [NotifyPropertyChangedFor(nameof(StartedAtText))]
    [NotifyPropertyChangedFor(nameof(HealthText))]
    [NotifyPropertyChangedFor(nameof(LastHealthText))]
    private ServiceStatus _status;

    [ObservableProperty]
    private string _environmentSummary = "…";

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _environmentMissingHint;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    private bool _canStart = true;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StopCommand))]
    private bool _canStop;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RestartCommand))]
    private bool _canRestart;

    // 端口占用消息解析：兼容中文与英文（英文模式下仍能弹出端口占用对话框）。
    private static readonly Regex PortOccupiedZhRegex = new(@"端口\s+(\d+)\s+已被占用\s*\(PID\s+(\d+)\)");
    private static readonly Regex PortOccupiedEnRegex = new(@"Port\s+(\d+)\s+is already in use\s*\(PID\s+(\d+)\)");

    public ServiceControlViewModel(IServiceController controller, IEnvironmentService environment, IWebBrowserService browser)
    {
        _controller = controller;
        _environment = environment;
        _browser = browser;
        _status = controller.Current;
        _state = controller.State;
        ApplyStateFlags(_state);

        controller.StateChanged += (_, s) => OnUi(() => HandleControllerStateChanged(s));
        controller.StatusUpdated += (_, st) => OnUi(() => Status = st);
        controller.ErrorOccurred += (_, msg) => OnUi(() => HandleError(msg));

        // 订阅环境检测广播，保证与环境页检测结果同步。
        environment.CheckCompleted += (_, r) => OnUi(() => UpdateEnvironmentStatus(r));

        _ = LoadEnvironmentAsync();
    }

    /// <summary>端口占用时触发（由 View 层弹对话框处理）。</summary>
    public event EventHandler<PortOccupiedEventArgs>? PortOccupied;

    private void HandleError(string message)
    {
        if (TryParsePortOccupied(message, out var port, out var pid))
        {
            PortOccupied?.Invoke(this, new PortOccupiedEventArgs(port, pid));
        }
        else
        {
            ErrorMessage = message;
        }
    }

    /// <summary>解析端口占用错误消息。纯函数，便于测试。</summary>
    public static bool TryParsePortOccupied(string message, out int port, out int pid)
    {
        port = 0;
        pid = 0;
        if (string.IsNullOrWhiteSpace(message))
            return false;

        var match = PortOccupiedZhRegex.Match(message);
        if (!match.Success)
            match = PortOccupiedEnRegex.Match(message);
        if (!match.Success)
            return false;

        return int.TryParse(match.Groups[1].Value, out port)
            && int.TryParse(match.Groups[2].Value, out pid);
    }

    [RelayCommand(CanExecute = nameof(CanStartExecute))]
    private async Task StartAsync()
    {
        ErrorMessage = null;

        // 立即进入"启动中"（先转状态再做环境检测/自动安装），
        // 避免点击启动后的延迟让人误以为应用坏了。
        await _controller.StartPreparedAsync(() => EnsureEnvironmentReadyAsync());

        // 启动成功后自动打开 DeepSeek Harness 网页。
        if (_controller.State == ServiceState.Running)
        {
            _browser.Open($"http://127.0.0.1:{Status.Port}");
        }
    }

    private async Task<bool> EnsureEnvironmentReadyAsync()
    {
        try
        {
            var env = await _environment.CheckAsync();

            if (!env.NodeInstalled)
            {
                ErrorMessage = Get("Msg.AutoInstallNode");
                var nodeOk = await _environment.InstallNodeAsync(
                    new Progress<string>(p => OnUi(() => ErrorMessage = p)));
                if (!nodeOk)
                {
                    ErrorMessage = Get("Msg.NodeInstallFailed");
                    return false;
                }
            }

            if (!env.DshAvailable)
            {
                ErrorMessage = Get("Msg.AutoInstallDsh");
                var dshOk = await _environment.PrefetchDshAsync(
                    new Progress<string>(p => OnUi(() => ErrorMessage = p)));
                if (!dshOk)
                {
                    ErrorMessage = Get("Msg.DshInstallFailed");
                    return false;
                }
            }

            return true;
        }
        catch
        {
            return true; // 环境检测异常时仍尝试启动。
        }
    }

    private static string Get(string key)
        => Application.Current?.Resources[key] as string ?? key;

    [RelayCommand(CanExecute = nameof(CanStopExecute))]
    private async Task StopAsync()
    {
        ErrorMessage = null;
        await _controller.StopAsync();
    }

    [RelayCommand(CanExecute = nameof(CanRestartExecute))]
    private async Task RestartAsync()
    {
        ErrorMessage = null;
        await _controller.RestartAsync();
    }

    private bool CanStartExecute() => CanStart;
    private bool CanStopExecute() => CanStop;
    private bool CanRestartExecute() => CanRestart;

    public string StatusPortText => $"127.0.0.1:{Status.Port}";
    public string StatusPidText => Status.Pid.HasValue ? $"PID {Status.Pid}" : "PID -";
    public string StartedAtText => Status.StartedAt?.ToLocalTime().ToString("HH:mm:ss") ?? "-";
    public string HealthText => Status.HealthMessage ?? "—";
    public string LastHealthText => Status.LastHealthCheckAt?.ToLocalTime().ToString("HH:mm:ss") ?? Get("Lbl.NotCheckedYet");

    private void HandleControllerStateChanged(ServiceState state)
    {
        State = state;
        ApplyStateFlags(state);
    }

    private void ApplyStateFlags(ServiceState state)
    {
        CanStart = (state is ServiceState.Stopped or ServiceState.Faulted) && _environmentReady;
        CanStop = state is ServiceState.Running or ServiceState.Starting;
        CanRestart = state is ServiceState.Running or ServiceState.Faulted;
    }

    private async Task LoadEnvironmentAsync()
    {
        try
        {
            await _environment.CheckAsync();
            // CheckCompleted 事件会广播结果并更新摘要，无需在此重复赋值。
        }
        catch
        {
            OnUi(() => EnvironmentSummary = Get("Lbl.CheckFailed"));
        }
    }

    private void UpdateEnvironmentSummary(EnvironmentCheckResult result)
    {
        var node = result.NodeInstalled
            ? $"node.js ✓ {(result.NodeVersion ?? "")}".Trim()
            : $"node.js ✗ {Get("Lbl.NotInstalled")}";
        var dsh = result.DshAvailable
            ? "DSH ✓"
            : $"DSH ✗ {Get("Lbl.Unavailable")}";
        EnvironmentSummary = $"{node} · {dsh}";
    }

    private void UpdateEnvironmentStatus(EnvironmentCheckResult result)
    {
        UpdateEnvironmentSummary(result);
        _environmentReady = result.IsReady;
        EnvironmentMissingHint = result.IsReady
            ? null
            : EnvironmentService.BuildMissingMessage(result);
        ApplyStateFlags(State);
    }

    private static void OnUi(Action action)
    {
        var app = Application.Current;
        if (app is null || app.Dispatcher.CheckAccess())
        {
            action();
            return;
        }
        app.Dispatcher.BeginInvoke(action);
    }
}

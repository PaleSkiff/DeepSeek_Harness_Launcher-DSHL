using DeepSeekHarnessLauncher.Models;

namespace DeepSeekHarnessLauncher.Services;

/// <summary>
/// 服务状态机（唯一事实源）：启动/停止/重启 + 健康轮询 + 超时 + 被动监控。
/// 线程安全：状态读写由 _gate 保护，事件在锁外触发。
/// </summary>
public sealed class ServiceController : IServiceController
{
    private readonly IProcessService _process;
    private readonly IHealthChecker _health;
    private readonly IConfigService _config;

    private readonly object _gate = new();
    private ServiceState _state = ServiceState.Stopped;
    private ServiceStatus _current;
    private AppConfig _currentConfig;
    private int? _pid;
    private DateTimeOffset? _startedAt;
    private string? _healthMessage;
    private DateTimeOffset? _lastHealthCheckAt;
    private CancellationTokenSource? _monitorCts;

    public ServiceController(IProcessService process, IHealthChecker health, IConfigService config)
    {
        _process = process;
        _health = health;
        _config = config;
        _currentConfig = config.Load();
        _current = ServiceStatus.CreateStopped(_currentConfig.Network.Port);
    }

    public ServiceState State
    {
        get { lock (_gate) return _state; }
    }

    public ServiceStatus Current
    {
        get { lock (_gate) return _current; }
    }

    public event EventHandler<ServiceState>? StateChanged;
    public event EventHandler<ServiceStatus>? StatusUpdated;
    public event EventHandler<string>? ErrorOccurred;
    public event EventHandler<string>? OutputReceived;

    public void UpdateConfig(AppConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        lock (_gate)
        {
            _currentConfig = config;
            _current = BuildStatusLocked();
        }
        StatusUpdated?.Invoke(this, _current);
    }

    public async Task StartAsync()
    {
        lock (_gate)
        {
            if (_state is ServiceState.Running or ServiceState.Starting or ServiceState.Stopping)
                return;
        }

        await StartCoreAsync();
    }

    /// <summary>
    /// 先立即进入"启动中"，再执行准备回调（如环境检测/自动安装），
    /// 避免准备阶段的延迟让人误以为点击启动没有反应；准备失败则回退到异常状态。
    /// </summary>
    public async Task StartPreparedAsync(Func<Task<bool>> prepare)
    {
        lock (_gate)
        {
            if (_state is ServiceState.Running or ServiceState.Starting or ServiceState.Stopping)
                return;
        }

        TransitionTo(ServiceState.Starting, GetText("Msg.Starting"));

        bool ready;
        try
        {
            ready = await prepare();
        }
        catch
        {
            ready = false;
        }

        if (!ready)
        {
            TransitionTo(ServiceState.Faulted, GetText("Msg.EnvNotReady"));
            return;
        }

        await StartCoreAsync();
    }

    private async Task StartCoreAsync()
    {
        _currentConfig = _config.Load();

        // 清理可能的残留进程后进入 Starting。
        int? leftoverPid;
        lock (_gate)
        {
            leftoverPid = _pid;
            _pid = null;
            _startedAt = null;
        }

        if (leftoverPid.HasValue)
        {
            await _process.StopTreeAsync(leftoverPid.Value, TimeSpan.FromSeconds(5));
        }

        TransitionTo(ServiceState.Starting, GetText("Msg.Starting"));

        try
        {
            // 端口预检
            var occupiedPid = _process.GetPidListeningOnPort(_currentConfig.Network.Port);
            if (occupiedPid.HasValue)
            {
                var message = string.Format(GetText("Msg.PortOccupiedFmt"), _currentConfig.Network.Port, occupiedPid.Value);
                TransitionTo(ServiceState.Faulted, message);
                ErrorOccurred?.Invoke(this, message);
                return;
            }

            var workingDirectory = string.IsNullOrWhiteSpace(_currentConfig.Service.WorkingDirectory)
                ? AppContext.BaseDirectory
                : _currentConfig.Service.WorkingDirectory;

            // 提高 node 堆上限（若用户未自定义）：dsh web 默认堆可能 OOM；
            // 用 2048MB 折中——留出余量，又不会像 4096 那样在低配虚拟机耗尽内存蓝屏。
            var env = new Dictionary<string, string>(_currentConfig.Service.EnvironmentVariables);
            if (!env.ContainsKey("NODE_OPTIONS"))
                env["NODE_OPTIONS"] = "--max-old-space-size=2048";

            var result = await _process.StartAsync(
                _currentConfig.Service.Command,
                _currentConfig.Service.Arguments,
                workingDirectory,
                env,
                line => OutputReceived?.Invoke(this, line));

            if (!result.Success)
            {
                var message = result.Error ?? GetText("Msg.StartFailed");
                TransitionTo(ServiceState.Faulted, message);
                ErrorOccurred?.Invoke(this, message);
                return;
            }

            lock (_gate)
            {
                _pid = result.Pid;
                _startedAt = DateTimeOffset.Now;
            }

            await WaitUntilReadyAsync(_currentConfig);
        }
        catch (Exception ex)
        {
            TransitionTo(ServiceState.Faulted, ex.Message);
        }
    }

    public async Task StopAsync()
    {
        int? pid;
        int stopSeconds;
        lock (_gate)
        {
            if (_state is ServiceState.Stopped or ServiceState.Stopping)
                return;
            _state = ServiceState.Stopping;
            pid = _pid;
            stopSeconds = _currentConfig.Timeout.StopSeconds;
            _current = BuildStatusLocked();
        }
        NotifyStatus();

        try
        {
            // 1) 先终止监听端口上的实际服务进程（通常是 node），避免 cmd 包装进程被杀后 node 脱离进程树而残留。
            var portPid = _process.GetPidListeningOnPort(_currentConfig.Network.Port);
            if (portPid.HasValue)
                await _process.StopTreeAsync(portPid.Value, TimeSpan.FromSeconds(stopSeconds));

            // 2) 再终止启动时记录的包装进程树（cmd/npx）。
            if (pid.HasValue && pid.Value != portPid)
                await _process.StopTreeAsync(pid.Value, TimeSpan.FromSeconds(stopSeconds));

            lock (_gate)
            {
                _pid = null;
                _startedAt = null;
                _state = ServiceState.Stopped;
                _healthMessage = GetText("Msg.Stopped");
                _current = BuildStatusLocked();
            }
            NotifyStateAndStatus();
        }
        catch (Exception ex)
        {
            lock (_gate)
            {
                _pid = null;
                _startedAt = null;
                _state = ServiceState.Faulted;
                _healthMessage = string.Format(GetText("Msg.StopFailedFmt"), ex.Message);
                _current = BuildStatusLocked();
            }
            NotifyStateAndStatus();
        }
        finally
        {
            _monitorCts?.Cancel();
        }
    }

    public async Task RestartAsync()
    {
        await StopAsync();
        await StartAsync();
    }

    /// <summary>
    /// 自动检测端口上是否已有外部启动的服务在运行；若有且健康，则进入 Running。
    /// 用于应用启动时识别"用户已手动启动 DSH"的情况。
    /// </summary>
    public async Task DetectExternalStateAsync()
    {
        lock (_gate)
        {
            if (_state != ServiceState.Stopped)
                return;
        }

        var portPid = _process.GetPidListeningOnPort(_currentConfig.Network.Port);
        if (!portPid.HasValue)
            return; // 端口无监听，保持 Stopped。

        var health = await _health.CheckAsync(_currentConfig.GetEffectiveHealthCheckUrl());
        if (!health.IsHealthy)
            return; // 端口有进程但健康检查失败，不视为就绪。

        lock (_gate)
        {
            _pid = portPid.Value;
            _startedAt = null;
            _state = ServiceState.Running;
            _healthMessage = string.Format(GetText("Msg.DetectedRunningFmt"), portPid.Value);
            _lastHealthCheckAt = DateTimeOffset.Now;
            _current = BuildStatusLocked();
        }
        NotifyStateAndStatus();
        StartMonitoring(_currentConfig);
    }

    // ===================== 内部 =====================

    private async Task WaitUntilReadyAsync(AppConfig config)
    {
        var url = config.GetEffectiveHealthCheckUrl();
        var interval = TimeSpan.FromSeconds(Math.Max(1, config.Network.HealthCheckIntervalSeconds));
        var deadline = DateTimeOffset.UtcNow.AddSeconds(config.Timeout.StartSeconds);

        while (DateTimeOffset.UtcNow < deadline)
        {
            var health = await _health.CheckAsync(url);
            UpdateHealth(health);

            if (health.IsHealthy)
            {
                TransitionTo(ServiceState.Running, GetText("Msg.HealthOk"));
                StartMonitoring(config);
                return;
            }

            if (!IsOurProcessAlive())
            {
                TransitionTo(ServiceState.Faulted, GetText("Msg.ProcessExitedBeforeReady"));
                return;
            }

            await Task.Delay(interval);
        }

        TransitionTo(ServiceState.Faulted, string.Format(GetText("Msg.StartTimeoutFmt"), config.Timeout.StartSeconds));
    }

    private void StartMonitoring(AppConfig config)
    {
        _monitorCts?.Cancel();
        _monitorCts = new CancellationTokenSource();
        _ = MonitorLoopAsync(config, _monitorCts.Token);
    }

    private async Task MonitorLoopAsync(AppConfig config, CancellationToken ct)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(1, config.Network.HealthCheckIntervalSeconds));
        var url = config.GetEffectiveHealthCheckUrl();

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            lock (_gate)
            {
                if (_state != ServiceState.Running)
                    return;
            }

            if (!IsOurProcessAlive())
            {
                TransitionTo(ServiceState.Faulted, GetText("Msg.ProcessExited"));
                return;
            }

            var health = await _health.CheckAsync(url, ct);
            UpdateHealth(health);
        }
    }

    private bool IsOurProcessAlive()
    {
        int? pid;
        lock (_gate)
        {
            pid = _pid;
        }
        return pid.HasValue && _process.IsRunning(pid.Value);
    }

    private void UpdateHealth(HealthResult health)
    {
        lock (_gate)
        {
            _lastHealthCheckAt = DateTimeOffset.Now;
            _healthMessage = health.IsHealthy
                ? string.Format(GetText("Msg.HealthCheckOkFmt"), health.StatusCode?.ToString() ?? "OK")
                : string.Format(GetText("Msg.HealthCheckFailedFmt"), health.Error ?? "no response");
            _current = BuildStatusLocked();
        }
        StatusUpdated?.Invoke(this, _current);
    }

    private void TransitionTo(ServiceState newState, string? message)
    {
        ServiceState oldState;
        lock (_gate)
        {
            oldState = _state;
            _state = newState;
            if (message is not null)
                _healthMessage = message;
            _current = BuildStatusLocked();
        }

        if (oldState != newState)
            StateChanged?.Invoke(this, newState);
        StatusUpdated?.Invoke(this, _current);
    }

    private ServiceStatus BuildStatusLocked() => new()
    {
        State = _state,
        Port = _currentConfig.Network.Port,
        Pid = _pid,
        StartedAt = _startedAt,
        HealthMessage = _healthMessage,
        LastHealthCheckAt = _lastHealthCheckAt,
    };

    private void NotifyStatus()
    {
        ServiceStatus snapshot;
        lock (_gate) snapshot = _current;
        StatusUpdated?.Invoke(this, snapshot);
    }

    private void NotifyStateAndStatus()
    {
        ServiceState state;
        ServiceStatus snapshot;
        lock (_gate)
        {
            state = _state;
            snapshot = _current;
        }
        StateChanged?.Invoke(this, state);
        StatusUpdated?.Invoke(this, snapshot);
    }

    private static string GetText(string key)
        => System.Windows.Application.Current?.Resources[key] as string ?? key;
}

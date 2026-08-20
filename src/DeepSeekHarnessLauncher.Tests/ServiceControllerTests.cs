using DeepSeekHarnessLauncher.Models;
using DeepSeekHarnessLauncher.Services;

namespace DeepSeekHarnessLauncher.Tests;

public sealed class ServiceControllerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ConfigService _configService;

    public ServiceControllerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "dshl-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _configService = new ConfigService(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* ignore */ }
    }

    private (ServiceController, FakeProcessService, FakeHealthChecker) CreateController(
        bool healthy = true,
        int startSeconds = 5,
        int intervalSeconds = 1)
    {
        _configService.Save(new AppConfig
        {
            Timeout = new TimeoutConfig { StartSeconds = startSeconds, StopSeconds = 3 },
            Network = new NetworkConfig { Port = 3080, HealthCheckIntervalSeconds = intervalSeconds },
        });

        var process = new FakeProcessService();
        var health = new FakeHealthChecker
        {
            Handler = _ => new HealthResult { IsHealthy = healthy, StatusCode = 200 },
        };
        var controller = new ServiceController(process, health, _configService);
        return (controller, process, health);
    }

    [Fact]
    public void Ctor_StartsInStoppedState()
    {
        var (controller, _, _) = CreateController();

        Assert.Equal(ServiceState.Stopped, controller.State);
        Assert.Equal(3080, controller.Current.Port);
    }

    [Fact]
    public async Task Start_Success_TransitionsToRunning()
    {
        var (controller, _, _) = CreateController(healthy: true);

        await controller.StartAsync();

        Assert.Equal(ServiceState.Running, controller.State);
        Assert.Equal(12345, controller.Current.Pid);
    }

    [Fact]
    public async Task Start_PassesNodeHeapOptions_ToPreventOom()
    {
        // 服务启动必须通过 NODE_OPTIONS 提高堆上限（用户未自定义时）；
        // 用 2048MB 折中——留出余量，又不会像 4096 那样在低配虚拟机耗尽内存蓝屏。
        var (controller, process, _) = CreateController(healthy: true);

        await controller.StartAsync();

        Assert.Equal(ServiceState.Running, controller.State);
        Assert.Contains(process.StartedEnvironmentVariables, env =>
            env.TryGetValue("NODE_OPTIONS", out var v) && v == "--max-old-space-size=2048");
    }

    [Fact]
    public async Task StartPreparedAsync_StateIsStarting_WhilePreparing()
    {
        // 问题 4：点击启动必须立即显示"正在启动"——准备回调执行期间状态应为 Starting。
        var (controller, _, _) = CreateController(healthy: true);
        var sawStarting = false;

        await controller.StartPreparedAsync(async () =>
        {
            sawStarting = controller.State == ServiceState.Starting;
            await Task.Delay(30);
            return true;
        });

        Assert.True(sawStarting, "准备回调执行期间状态应为「启动中」");
        Assert.Equal(ServiceState.Running, controller.State);
    }

    [Fact]
    public async Task StartPreparedAsync_PrepareReturnsFalse_TransitionsToFaulted()
    {
        // 环境准备失败（如自动安装失败）→ 进入异常状态而非停留在"启动中"。
        var (controller, _, _) = CreateController(healthy: true);

        await controller.StartPreparedAsync(() => Task.FromResult(false));

        Assert.Equal(ServiceState.Faulted, controller.State);
    }

    [Fact]
    public async Task StartPreparedAsync_PrepareReturnsTrue_TransitionsToRunning()
    {
        var (controller, _, _) = CreateController(healthy: true);

        await controller.StartPreparedAsync(() => Task.FromResult(true));

        Assert.Equal(ServiceState.Running, controller.State);
        Assert.Equal(12345, controller.Current.Pid);
    }

    [Fact]
    public async Task Start_PortOccupied_TransitionsToFaulted()
    {
        var (controller, process, _) = CreateController(healthy: true);
        process.PortHandler = _ => 9999;

        await controller.StartAsync();

        Assert.Equal(ServiceState.Faulted, controller.State);
        Assert.Contains("Msg.PortOccupiedFmt", controller.Current.HealthMessage);
    }

    [Fact]
    public async Task Start_HealthFails_UntilTimeout_TransitionsToFaulted()
    {
        var (controller, _, _) = CreateController(healthy: false, startSeconds: 1, intervalSeconds: 1);

        await controller.StartAsync();

        Assert.Equal(ServiceState.Faulted, controller.State);
        Assert.Contains("Msg.StartTimeoutFmt", controller.Current.HealthMessage);
    }

    [Fact]
    public async Task Start_ProcessExitsBeforeReady_TransitionsToFaulted()
    {
        var (controller, process, _) = CreateController(healthy: false, startSeconds: 10, intervalSeconds: 1);
        process.IsRunningHandler = _ => false;

        await controller.StartAsync();

        Assert.Equal(ServiceState.Faulted, controller.State);
    }

    [Fact]
    public async Task Start_WhenAlreadyRunning_DoesNotRestart()
    {
        var (controller, process, _) = CreateController(healthy: true);
        await controller.StartAsync();
        int calls = process.StartCallCount;

        await controller.StartAsync();

        Assert.Equal(ServiceState.Running, controller.State);
        Assert.Equal(calls, process.StartCallCount);
    }

    [Fact]
    public async Task Stop_TransitionsToStopped_AndStopsTree()
    {
        var (controller, process, _) = CreateController(healthy: true);
        await controller.StartAsync();

        await controller.StopAsync();

        Assert.Equal(ServiceState.Stopped, controller.State);
        Assert.Contains(12345, process.StoppedPids);
    }

    [Fact]
    public async Task Stop_WhenStopped_IsNoOp()
    {
        var (controller, process, _) = CreateController(healthy: true);

        await controller.StopAsync();

        Assert.Equal(ServiceState.Stopped, controller.State);
        Assert.Empty(process.StoppedPids);
    }

    [Fact]
    public async Task Restart_StopsThenStarts_ReturnsToRunning()
    {
        var (controller, process, _) = CreateController(healthy: true);
        await controller.StartAsync();
        Assert.Equal(ServiceState.Running, controller.State);

        await controller.RestartAsync();

        Assert.Equal(ServiceState.Running, controller.State);
        Assert.Contains(12345, process.StoppedPids);
        Assert.Equal(2, process.StartCallCount);
    }

    [Fact]
    public async Task Running_ThenProcessDies_MonitorTransitionsToFaulted()
    {
        var (controller, process, _) = CreateController(healthy: true, intervalSeconds: 1);
        await controller.StartAsync();
        Assert.Equal(ServiceState.Running, controller.State);

        process.IsRunningHandler = _ => false;
        await Task.Delay(2500);

        Assert.Equal(ServiceState.Faulted, controller.State);
    }

    [Fact]
    public void UpdateConfig_RefreshesCurrentPort()
    {
        var (controller, _, _) = CreateController(healthy: true);

        controller.UpdateConfig(new AppConfig { Network = new NetworkConfig { Port = 9090 } });

        Assert.Equal(9090, controller.Current.Port);
    }

    [Fact]
    public async Task DetectExternalState_PortHasHealthyService_TransitionsToRunning()
    {
        var (controller, process, _) = CreateController(healthy: true);
        process.PortHandler = _ => 8888;

        await controller.DetectExternalStateAsync();

        Assert.Equal(ServiceState.Running, controller.State);
        Assert.Equal(8888, controller.Current.Pid);
    }

    [Fact]
    public async Task DetectExternalState_NoPort_StaysStopped()
    {
        var (controller, process, _) = CreateController(healthy: true);
        process.PortHandler = _ => null;

        await controller.DetectExternalStateAsync();

        Assert.Equal(ServiceState.Stopped, controller.State);
    }

    [Fact]
    public async Task DetectExternalState_PortButUnhealthy_StaysStopped()
    {
        var (controller, process, _) = CreateController(healthy: false);
        process.PortHandler = _ => 8888;

        await controller.DetectExternalStateAsync();

        Assert.Equal(ServiceState.Stopped, controller.State);
    }

    [Fact]
    public async Task DetectExternalState_WhenAlreadyRunning_NoOp()
    {
        var (controller, process, _) = CreateController(healthy: true);
        await controller.StartAsync();
        Assert.Equal(ServiceState.Running, controller.State);
        process.PortHandler = _ => 9999;

        await controller.DetectExternalStateAsync();

        Assert.Equal(ServiceState.Running, controller.State);
        Assert.Equal(12345, controller.Current.Pid);
    }
}

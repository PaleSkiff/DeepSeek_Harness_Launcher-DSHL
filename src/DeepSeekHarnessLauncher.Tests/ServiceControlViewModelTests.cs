using DeepSeekHarnessLauncher.Models;
using DeepSeekHarnessLauncher.Services;
using DeepSeekHarnessLauncher.ViewModels;

namespace DeepSeekHarnessLauncher.Tests;

public sealed class ServiceControlViewModelTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ConfigService _configService;

    public ServiceControlViewModelTests()
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

    private (ServiceControlViewModel Vm, FakeWebBrowserService Browser, ServiceController Controller, FakeProcessService Process) Create(
        bool healthy = true,
        Func<string, string, CommandResult>? runHandler = null)
    {
        _configService.Save(new AppConfig
        {
            Timeout = new TimeoutConfig { StartSeconds = 5, StopSeconds = 3 },
            Network = new NetworkConfig { Port = 3080, HealthCheckIntervalSeconds = 1 },
        });

        var process = new FakeProcessService { RunHandler = runHandler };
        var health = new FakeHealthChecker
        {
            Handler = _ => new HealthResult { IsHealthy = healthy, StatusCode = 200 },
        };
        var controller = new ServiceController(process, health, _configService);
        var environment = new EnvironmentService(process);
        var browser = new FakeWebBrowserService();
        var vm = new ServiceControlViewModel(controller, environment, browser);
        return (vm, browser, controller, process);
    }

    [Fact]
    public async Task Start_Success_OpensWebPage()
    {
        var (vm, browser, _, _) = Create(healthy: true);

        await vm.StartCommand.ExecuteAsync(null);

        Assert.Single(browser.OpenedUrls);
        Assert.Contains("3080", browser.OpenedUrls[0]);
    }

    [Fact]
    public async Task Start_PortOccupied_DoesNotOpenWebPage()
    {
        var (vm, browser, _, process) = Create(healthy: true);
        process.PortHandler = _ => 9999;

        await vm.StartCommand.ExecuteAsync(null);

        Assert.Empty(browser.OpenedUrls);
    }

    [Fact]
    public async Task Start_WhenDepsMissing_CanStartIsFalse()
    {
        // node/npm 检测均失败 → 缺少依赖 → 启动按钮禁用并给出提示。
        var (vm, _, _, _) = Create(
            healthy: true,
            runHandler: (_, _) => new CommandResult { ExitCode = -1, Error = "not found" });

        await Task.Delay(500); // 等待初始环境检测完成

        Assert.False(vm.CanStart);
        Assert.NotNull(vm.EnvironmentMissingHint);
    }

    [Fact]
    public async Task Start_WhenDepsReady_CanStartIsTrue()
    {
        var (vm, _, _, _) = Create(
            healthy: true,
            runHandler: (command, _) => command switch
            {
                "node" => new CommandResult { ExitCode = 0, Output = "v20.11.0\n" },
                "npx" => new CommandResult { ExitCode = 0, Output = "dsh help ok\n" },
                "npm" => new CommandResult { ExitCode = 0, Output = "0.1.0\n" },
                _ => new CommandResult { ExitCode = 0, Output = "ok" },
            });

        await Task.Delay(500); // 等待初始环境检测完成

        Assert.True(vm.CanStart);
        Assert.Null(vm.EnvironmentMissingHint);
    }
}

using DeepSeekHarnessLauncher.Models;
using DeepSeekHarnessLauncher.Services;
using DeepSeekHarnessLauncher.ViewModels;

namespace DeepSeekHarnessLauncher.Tests;

public sealed class MainViewModelTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ConfigService _configService;
    private readonly FakeProcessService _processService;
    private readonly FakeHealthChecker _healthChecker;
    private readonly ServiceController _controller;
    private readonly MainViewModel _vm;

    public MainViewModelTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "dshl-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _configService = new ConfigService(_tempDir);
        _processService = new FakeProcessService();
        _healthChecker = new FakeHealthChecker { Handler = _ => new HealthResult { IsHealthy = true, StatusCode = 200 } };
        _controller = new ServiceController(_processService, _healthChecker, _configService);
        var environment = new EnvironmentService(_processService);
        var logService = new LogService(_tempDir);
        var autoStartService = new FakeAutoStartService();
        var browser = new FakeWebBrowserService();
        var localization = new FakeLocalizationService();

        _vm = new MainViewModel(
            _controller,
            localization,
            new ServiceControlViewModel(_controller, environment, browser),
            new LogViewModel(logService),
            new EnvironmentViewModel(environment),
            new ConfigViewModel(_configService),
            new SettingsViewModel(_configService, autoStartService, localization),
            new AboutViewModel());
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* ignore */ }
    }

    [Fact]
    public void InitialPage_IsServiceControl()
    {
        Assert.IsType<ServiceControlViewModel>(_vm.CurrentViewModel);
        Assert.Equal("服务控制", _vm.CurrentPageTitle);
    }

    [Fact]
    public void Navigate_ToLog_ChangesCurrentViewModel()
    {
        _vm.NavigateCommand.Execute("log");

        Assert.IsType<LogViewModel>(_vm.CurrentViewModel);
        Assert.Equal("日志", _vm.CurrentPageTitle);
    }

    [Fact]
    public void Navigate_ToEachPage_Works()
    {
        _vm.NavigateCommand.Execute("config");
        Assert.IsType<ConfigViewModel>(_vm.CurrentViewModel);

        _vm.NavigateCommand.Execute("environment");
        Assert.IsType<EnvironmentViewModel>(_vm.CurrentViewModel);

        _vm.NavigateCommand.Execute("settings");
        Assert.IsType<SettingsViewModel>(_vm.CurrentViewModel);

        _vm.NavigateCommand.Execute("about");
        Assert.IsType<AboutViewModel>(_vm.CurrentViewModel);
        Assert.Equal("关于", _vm.CurrentPageTitle);

        _vm.NavigateCommand.Execute("service");
        Assert.IsType<ServiceControlViewModel>(_vm.CurrentViewModel);
    }

    [Fact]
    public void NavItems_HaveExpectedOrder()
    {
        var keys = _vm.NavItems.Select(n => n.Key).ToArray();

        Assert.Equal(
            new[] { "service", "log", "environment", "config", "settings", "about" },
            keys);
    }

    [Fact]
    public void NavItems_HaveExpectedTitles()
    {
        var titles = _vm.NavItems.Select(n => n.Title).ToArray();

        Assert.Equal(
            new[] { "服务控制", "日志", "环境 / 安装", "配置", "设置", "关于" },
            titles);
    }

    [Fact]
    public void Navigate_ToUnknown_KeepsCurrent()
    {
        _vm.NavigateCommand.Execute("settings");
        _vm.NavigateCommand.Execute("unknown");

        Assert.IsType<SettingsViewModel>(_vm.CurrentViewModel);
    }

    [Fact]
    public void StatusState_InitiallyStopped()
    {
        Assert.Equal(ServiceState.Stopped, _vm.State);
        Assert.Equal("未运行", _vm.StatusStateText);
        Assert.Equal("PID -", _vm.StatusPidText);
    }

    [Fact]
    public async Task StatusState_SyncsWithController_OnStartAndStop()
    {
        await _controller.StartAsync();

        Assert.Equal(ServiceState.Running, _vm.State);
        Assert.Equal("运行中", _vm.StatusStateText);
        Assert.Equal("PID 12345", _vm.StatusPidText);

        await _controller.StopAsync();

        Assert.Equal(ServiceState.Stopped, _vm.State);
        Assert.Equal("未运行", _vm.StatusStateText);
        Assert.Equal("PID -", _vm.StatusPidText);
    }
}

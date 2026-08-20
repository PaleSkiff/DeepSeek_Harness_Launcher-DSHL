using DeepSeekHarnessLauncher.Models;
using DeepSeekHarnessLauncher.Services;

namespace DeepSeekHarnessLauncher.Tests;

public sealed class FakeProcessService : IProcessService
{
    public Func<ProcessLaunchResult>? StartHandler { get; set; }
    public Func<int, bool>? IsRunningHandler { get; set; }
    public Func<int, int?>? PortHandler { get; set; }
    public Func<string, string, CommandResult>? RunHandler { get; set; }
    public Func<string, string, CommandResult>? RunElevatedHandler { get; set; }

    /// <summary>StartAsync 成功后通过 onOutputLine 回放的输出行（模拟真实进程的实时输出）。</summary>
    public List<string> StartOutputLines { get; } = new();

    public List<int> StoppedPids { get; } = new();
    public List<string> StartedCommands { get; } = new();
    public List<string> StartedArguments { get; } = new();
    public List<IReadOnlyDictionary<string, string>> StartedEnvironmentVariables { get; } = new();
    public List<IReadOnlyDictionary<string, string>> RunEnvironmentVariables { get; } = new();
    public int StartCallCount { get; private set; }

    public Task<ProcessLaunchResult> StartAsync(
        string command,
        string arguments,
        string workingDirectory,
        IReadOnlyDictionary<string, string> environmentVariables,
        Action<string>? onOutputLine = null,
        CancellationToken cancellationToken = default)
    {
        StartCallCount++;
        StartedCommands.Add(command);
        StartedArguments.Add(arguments);
        StartedEnvironmentVariables.Add(new Dictionary<string, string>(environmentVariables));
        var result = StartHandler?.Invoke() ?? ProcessLaunchResult.Started(12345);

        // 同步回放输出行，保证调用方 await StartAsync 后即可观察到就绪标记。
        if (result.Success && onOutputLine is not null)
        {
            foreach (var line in StartOutputLines)
                onOutputLine(line);
        }

        return Task.FromResult(result);
    }

    public Task<CommandResult> RunAsync(
        string command,
        string arguments,
        TimeSpan timeout,
        IReadOnlyDictionary<string, string>? environmentVariables = null)
    {
        RunEnvironmentVariables.Add(new Dictionary<string, string>(environmentVariables ?? new Dictionary<string, string>()));
        return Task.FromResult(RunHandler?.Invoke(command, arguments)
                               ?? new CommandResult { ExitCode = 0, Output = "ok" });
    }

    public Task<CommandResult> RunElevatedAsync(string command, string arguments, TimeSpan timeout)
        => Task.FromResult(RunElevatedHandler?.Invoke(command, arguments)
                           ?? new CommandResult { ExitCode = 0, Output = "ok" });

    public Task StopTreeAsync(int pid, TimeSpan timeout)
    {
        StoppedPids.Add(pid);
        return Task.CompletedTask;
    }

    public int? GetPidListeningOnPort(int port)
        => PortHandler?.Invoke(port) ?? null;

    public bool IsRunning(int pid)
        => IsRunningHandler?.Invoke(pid) ?? true;
}

public sealed class FakeHealthChecker : IHealthChecker
{
    public Func<string, HealthResult>? Handler { get; set; }

    public Task<HealthResult> CheckAsync(string url, CancellationToken cancellationToken = default)
        => Task.FromResult(Handler?.Invoke(url)
                           ?? new HealthResult { IsHealthy = true, StatusCode = 200 });
}

public sealed class FakeAutoStartService : IAutoStartService
{
    public bool IsEnabled { get; private set; }

    public void SetEnabled(bool enabled) => IsEnabled = enabled;
}

public sealed class FakeWebBrowserService : IWebBrowserService
{
    public List<string> OpenedUrls { get; } = new();

    public void Open(string url) => OpenedUrls.Add(url);
}

public sealed class FakeLocalizationService : ILocalizationService
{
    private static readonly Dictionary<string, string> Zh = new()
    {
        ["Nav.Service"] = "服务控制",
        ["Nav.Log"] = "日志",
        ["Nav.Environment"] = "环境 / 安装",
        ["Nav.Config"] = "配置",
        ["Nav.Settings"] = "设置",
        ["Nav.About"] = "关于",
        ["State.Stopped"] = "未运行",
        ["State.Starting"] = "启动中",
        ["State.Running"] = "运行中",
        ["State.Stopping"] = "停止中",
        ["State.Faulted"] = "异常",
    };

    public string CurrentLanguage { get; private set; } = "zh-CN";
    public event EventHandler? LanguageChanged;

    public void Initialize(string language) => CurrentLanguage = language;

    public void SetLanguage(string language)
    {
        CurrentLanguage = language;
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    public string Get(string key) => Zh.TryGetValue(key, out var v) ? v : key;
}

/// <summary>同步 IProgress，Report 立即执行（避免 Progress&lt;T&gt; 的异步回调时序问题）。</summary>
public sealed class SyncProgress<T> : IProgress<T>
{
    private readonly Action<T> _handler;

    public SyncProgress(Action<T> handler) => _handler = handler;

    public void Report(T value) => _handler(value);
}

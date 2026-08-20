using DeepSeekHarnessLauncher.Models;

namespace DeepSeekHarnessLauncher.Services;

public interface IProcessService
{
    /// <summary>启动长驻进程，返回 PID；逐行回调输出。</summary>
    Task<ProcessLaunchResult> StartAsync(
        string command,
        string arguments,
        string workingDirectory,
        IReadOnlyDictionary<string, string> environmentVariables,
        Action<string>? onOutputLine = null,
        CancellationToken cancellationToken = default);

    /// <summary>同步执行短命令并捕获输出；可附带环境变量（如 NODE_OPTIONS）。</summary>
    Task<CommandResult> RunAsync(
        string command,
        string arguments,
        TimeSpan timeout,
        IReadOnlyDictionary<string, string>? environmentVariables = null);

    /// <summary>以管理员权限（UAC 提权）执行命令，用于 msiexec 静默安装等需要提权的操作。</summary>
    Task<CommandResult> RunElevatedAsync(string command, string arguments, TimeSpan timeout);

    /// <summary>终止进程树，超时兜底。</summary>
    Task StopTreeAsync(int pid, TimeSpan timeout);

    /// <summary>返回监听指定端口的 PID（无则 null）。</summary>
    int? GetPidListeningOnPort(int port);

    bool IsRunning(int pid);
}

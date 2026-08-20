using DeepSeekHarnessLauncher.Models;

namespace DeepSeekHarnessLauncher.Services;

public interface IEnvironmentService
{
    /// <summary>环境检测完成后广播最新结果（供多个页面同步）。</summary>
    event EventHandler<EnvironmentCheckResult>? CheckCompleted;

    Task<EnvironmentCheckResult> CheckAsync();
    Task<bool> InstallNodeAsync(IProgress<string>? progress = null);
    Task<bool> PrefetchDshAsync(IProgress<string>? progress = null);
}

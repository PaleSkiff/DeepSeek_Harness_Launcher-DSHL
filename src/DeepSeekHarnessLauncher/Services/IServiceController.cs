using DeepSeekHarnessLauncher.Models;

namespace DeepSeekHarnessLauncher.Services;

public interface IServiceController
{
    ServiceState State { get; }
    ServiceStatus Current { get; }
    event EventHandler<ServiceState>? StateChanged;
    event EventHandler<ServiceStatus>? StatusUpdated;
    event EventHandler<string>? ErrorOccurred;
    event EventHandler<string>? OutputReceived;
    Task StartAsync();
    /// <summary>先立即进入"启动中"，再执行准备回调；准备失败则进入异常状态。</summary>
    Task StartPreparedAsync(Func<Task<bool>> prepare);
    Task StopAsync();
    Task RestartAsync();
    Task DetectExternalStateAsync();
    void UpdateConfig(AppConfig config);
}

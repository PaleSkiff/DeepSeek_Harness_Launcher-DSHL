namespace DeepSeekHarnessLauncher.Models;

/// <summary>服务状态快照，随状态机变更广播给 UI 与托盘。</summary>
public sealed class ServiceStatus
{
    public ServiceState State { get; init; }
    public int Port { get; init; }
    public int? Pid { get; init; }
    public DateTimeOffset? StartedAt { get; init; }
    public string? HealthMessage { get; init; }
    public DateTimeOffset? LastHealthCheckAt { get; init; }

    public static ServiceStatus CreateStopped(int port) => new()
    {
        State = ServiceState.Stopped,
        Port = port,
    };
}

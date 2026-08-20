namespace DeepSeekHarnessLauncher.Models;

/// <summary>启动长驻进程的结果。</summary>
public sealed class ProcessLaunchResult
{
    public bool Success { get; init; }
    public int Pid { get; init; }
    public string? Error { get; init; }

    public static ProcessLaunchResult Failed(string error) => new()
    {
        Success = false,
        Error = error,
    };

    public static ProcessLaunchResult Started(int pid) => new()
    {
        Success = true,
        Pid = pid,
    };
}

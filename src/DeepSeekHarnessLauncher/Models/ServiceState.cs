namespace DeepSeekHarnessLauncher.Models;

/// <summary>服务状态机枚举，全应用唯一事实源。</summary>
public enum ServiceState
{
    /// <summary>未运行</summary>
    Stopped,
    /// <summary>启动中</summary>
    Starting,
    /// <summary>运行中</summary>
    Running,
    /// <summary>停止中</summary>
    Stopping,
    /// <summary>异常</summary>
    Faulted
}

using System.Text.Json.Serialization;

namespace DeepSeekHarnessLauncher.Models;

/// <summary>启动器配置模型，对应程序目录下 config.json。</summary>
public sealed class AppConfig
{
    public ServiceConfig Service { get; set; } = new();
    public NetworkConfig Network { get; set; } = new();
    public TimeoutConfig Timeout { get; set; } = new();
    public BehaviorConfig Behavior { get; set; } = new();
    public StartupConfig Startup { get; set; } = new();
    public LoggingConfig Logging { get; set; } = new();
    /// <summary>界面语言："zh-CN"（默认）或 "en-US"。</summary>
    public string Language { get; set; } = "zh-CN";

    /// <summary>计算有效的健康检查地址：为空时按端口自动拼装。</summary>
    public string GetEffectiveHealthCheckUrl() =>
        string.IsNullOrWhiteSpace(Network.HealthCheckUrl)
            ? $"http://127.0.0.1:{Network.Port}"
            : Network.HealthCheckUrl!;
}

public sealed class ServiceConfig
{
    public string Command { get; set; } = "npx";
    public string Arguments { get; set; } = "--verbose @deepseek-ai/dsh web";
    public string WorkingDirectory { get; set; } = string.Empty;
    public Dictionary<string, string> EnvironmentVariables { get; set; } = new();
}

public sealed class NetworkConfig
{
    public int Port { get; set; } = 3080;
    public string HealthCheckUrl { get; set; } = string.Empty;
    public int HealthCheckIntervalSeconds { get; set; } = 5;
}

public sealed class TimeoutConfig
{
    public int StartSeconds { get; set; } = 60;
    public int StopSeconds { get; set; } = 15;
}

public sealed class BehaviorConfig
{
    public bool AutoStartServiceOnLaunch { get; set; }
    public bool CloseToTray { get; set; } = true;
    public bool AskOnFirstClose { get; set; } = true;
    public bool StopServiceOnExit { get; set; } = true;
}

public sealed class StartupConfig
{
    public bool AutoStartOnBoot { get; set; }
}

public sealed class LoggingConfig
{
    public int RetentionDays { get; set; } = 7;
}

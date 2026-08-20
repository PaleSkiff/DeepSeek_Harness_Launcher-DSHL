namespace DeepSeekHarnessLauncher.Models;

/// <summary>一条日志记录。</summary>
public sealed class LogEntry
{
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.Now;
    public LogLevel Level { get; init; }
    public string Message { get; init; } = string.Empty;
}

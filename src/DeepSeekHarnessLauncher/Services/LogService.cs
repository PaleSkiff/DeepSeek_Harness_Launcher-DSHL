using System.IO;
using System.Text;
using DeepSeekHarnessLauncher.Models;

namespace DeepSeekHarnessLauncher.Services;

public interface ILogService
{
    event EventHandler<LogEntry>? EntryReceived;
    IReadOnlyList<LogEntry> Entries { get; }
    void Append(string message, LogLevel level = LogLevel.Info);
    void ClearView();
    void Export(string filePath);
    void CleanupOldLogs(int retentionDays);
}

/// <summary>
/// 日志服务：内存收集 + 按日落盘（logs\yyyy-MM-dd.log）+ 导出 + 保留天数清理。
/// 线程安全：Append 可能来自子进程输出回调线程。
/// </summary>
public sealed class LogService : ILogService
{
    private readonly object _gate = new();
    private readonly List<LogEntry> _entries = new();
    private readonly string _logDirectory;

    public LogService(string baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(baseDirectory))
            throw new ArgumentException("基础目录不能为空", nameof(baseDirectory));
        _logDirectory = Path.Combine(baseDirectory, "logs");
    }

    public string LogDirectory => _logDirectory;

    public event EventHandler<LogEntry>? EntryReceived;

    public IReadOnlyList<LogEntry> Entries
    {
        get { lock (_gate) return _entries.ToList(); }
    }

    public void Append(string message, LogLevel level = LogLevel.Info)
    {
        var entry = new LogEntry
        {
            Timestamp = DateTimeOffset.Now,
            Level = level,
            Message = message ?? string.Empty,
        };

        lock (_gate)
            _entries.Add(entry);

        WriteToDailyFile(entry);
        EntryReceived?.Invoke(this, entry);
    }

    public void ClearView()
    {
        lock (_gate)
            _entries.Clear();
    }

    public void Export(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var lines = new List<string>();
        lock (_gate)
        {
            foreach (var e in _entries)
                lines.Add(FormatLine(e));
        }

        File.WriteAllLines(filePath, lines, new UTF8Encoding(false));
    }

    public void CleanupOldLogs(int retentionDays)
    {
        if (retentionDays <= 0)
            return; // 0 = 不清理

        if (!Directory.Exists(_logDirectory))
            return;

        var cutoff = DateTime.Today.AddDays(-(retentionDays - 1));
        try
        {
            foreach (var file in Directory.EnumerateFiles(_logDirectory, "*.log"))
            {
                if (File.GetLastWriteTime(file) < cutoff)
                    File.Delete(file);
            }
        }
        catch
        {
            // 清理失败不应影响运行。
        }
    }

    private void WriteToDailyFile(LogEntry entry)
    {
        try
        {
            Directory.CreateDirectory(_logDirectory);
            var path = Path.Combine(_logDirectory, $"{DateTime.Now:yyyy-MM-dd}.log");
            lock (_gate)
            {
                File.AppendAllText(path, FormatLine(entry) + Environment.NewLine, new UTF8Encoding(false));
            }
        }
        catch
        {
            // 落盘失败不影响内存日志。
        }
    }

    private static string FormatLine(LogEntry entry)
        => $"{entry.Timestamp:HH:mm:ss.fff}  {entry.Level.ToString().ToUpperInvariant(),-5}  {entry.Message}";

    /// <summary>根据输出行内容推断日志级别。纯函数，便于测试。</summary>
    public static LogLevel InferLevel(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return LogLevel.Info;

        var lower = line.ToLowerInvariant();
        if (lower.Contains("error") || lower.Contains("exception")
            || lower.Contains("fail") || lower.Contains("err"))
            return LogLevel.Error;
        if (lower.Contains("warn"))
            return LogLevel.Warn;
        if (lower.Contains("debug"))
            return LogLevel.Debug;
        return LogLevel.Info;
    }
}

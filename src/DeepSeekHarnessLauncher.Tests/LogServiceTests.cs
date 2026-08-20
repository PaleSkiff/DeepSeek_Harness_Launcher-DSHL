using DeepSeekHarnessLauncher.Models;
using DeepSeekHarnessLauncher.Services;

namespace DeepSeekHarnessLauncher.Tests;

public sealed class LogServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly LogService _service;

    public LogServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "dshl-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _service = new LogService(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* ignore */ }
    }

    [Fact]
    public void Append_AddsEntry_AndFiresEvent()
    {
        LogEntry? captured = null;
        _service.EntryReceived += (_, e) => captured = e;

        _service.Append("hello", LogLevel.Info);

        Assert.Single(_service.Entries);
        Assert.Equal("hello", captured!.Message);
    }

    [Fact]
    public void Append_WritesDailyLogFile()
    {
        _service.Append("line1");
        _service.Append("line2", LogLevel.Error);

        var logFile = Path.Combine(_service.LogDirectory, $"{DateTime.Now:yyyy-MM-dd}.log");
        Assert.True(File.Exists(logFile));
        var content = File.ReadAllText(logFile);
        Assert.Contains("line1", content);
        Assert.Contains("line2", content);
    }

    [Fact]
    public void ClearView_RemovesEntries()
    {
        _service.Append("a");
        _service.Append("b");

        _service.ClearView();

        Assert.Empty(_service.Entries);
    }

    [Fact]
    public void Export_WritesAllEntriesToFile()
    {
        _service.Append("one");
        _service.Append("two");
        var exportPath = Path.Combine(_tempDir, "export.log");

        _service.Export(exportPath);

        Assert.True(File.Exists(exportPath));
        var content = File.ReadAllText(exportPath);
        Assert.Contains("one", content);
        Assert.Contains("two", content);
    }

    [Fact]
    public void CleanupOldLogs_DeletesExpiredFiles_KeepsRecent()
    {
        Directory.CreateDirectory(_service.LogDirectory);
        var oldFile = Path.Combine(_service.LogDirectory, "2020-01-01.log");
        var recentFile = Path.Combine(_service.LogDirectory, $"{DateTime.Today:yyyy-MM-dd}.log");
        File.WriteAllText(oldFile, "old");
        File.WriteAllText(recentFile, "recent");
        File.SetLastWriteTime(oldFile, DateTime.Today.AddDays(-30));

        _service.CleanupOldLogs(7);

        Assert.False(File.Exists(oldFile));
        Assert.True(File.Exists(recentFile));
    }

    [Fact]
    public void CleanupOldLogs_ZeroDays_DoesNothing()
    {
        Directory.CreateDirectory(_service.LogDirectory);
        var file = Path.Combine(_service.LogDirectory, "2020-01-01.log");
        File.WriteAllText(file, "x");
        File.SetLastWriteTime(file, DateTime.Today.AddDays(-30));

        _service.CleanupOldLogs(0);

        Assert.True(File.Exists(file));
    }

    [Theory]
    [InlineData("some error occurred", LogLevel.Error)]
    [InlineData("Exception in thread", LogLevel.Error)]
    [InlineData("WARN: deprecated", LogLevel.Warn)]
    [InlineData("debug info", LogLevel.Debug)]
    [InlineData("normal output", LogLevel.Info)]
    public void InferLevel_DetectsCorrectLevel(string line, LogLevel expected)
    {
        Assert.Equal(expected, LogService.InferLevel(line));
    }
}

using DeepSeekHarnessLauncher.Models;
using DeepSeekHarnessLauncher.Services;
using DeepSeekHarnessLauncher.ViewModels;

namespace DeepSeekHarnessLauncher.Tests;

public sealed class LogViewModelTests : IDisposable
{
    private readonly string _tempDir;
    private readonly LogService _logService;

    public LogViewModelTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "dshl-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _logService = new LogService(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* ignore */ }
    }

    [Fact]
    public void MatchesFilter_All_MatchesEverything()
    {
        var entry = new LogEntry { Level = LogLevel.Error, Message = "x" };

        Assert.True(LogViewModel.MatchesFilter(entry, LogFilter.All));
    }

    [Fact]
    public void MatchesFilter_ExactLevel_MatchesOnlyThatLevel()
    {
        var error = new LogEntry { Level = LogLevel.Error, Message = "x" };
        var info = new LogEntry { Level = LogLevel.Info, Message = "x" };

        Assert.True(LogViewModel.MatchesFilter(error, LogFilter.Error));
        Assert.False(LogViewModel.MatchesFilter(info, LogFilter.Error));
    }

    [Fact]
    public void FormatEntry_ProducesExpectedLine()
    {
        var entry = new LogEntry
        {
            Timestamp = new DateTimeOffset(2024, 1, 2, 3, 4, 5, 678, TimeSpan.Zero),
            Level = LogLevel.Warn,
            Message = "hello",
        };

        var line = LogViewModel.FormatEntry(entry);

        Assert.Contains("03:04:05.678", line);
        Assert.Contains("WARN", line);
        Assert.Contains("hello", line);
    }

    [Fact]
    public void Ctor_LoadsExistingEntries_IntoView()
    {
        _logService.Append("existing1");
        _logService.Append("existing2");

        var vm = new LogViewModel(_logService);

        Assert.Equal(2, vm.EntriesView.Cast<object>().Count());
    }

    [Fact]
    public void NewEntry_IsAddedToView()
    {
        var vm = new LogViewModel(_logService);

        _logService.Append("new line");

        Assert.Single(vm.EntriesView.Cast<object>());
    }

    [Fact]
    public void ClearView_EmptiesView_AndService()
    {
        _logService.Append("a");
        var vm = new LogViewModel(_logService);

        vm.ClearViewCommand.Execute(null);

        Assert.Empty(vm.EntriesView.Cast<object>());
        Assert.Empty(_logService.Entries);
    }
}

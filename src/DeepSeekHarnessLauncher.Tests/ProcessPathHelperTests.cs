using DeepSeekHarnessLauncher.Services;

namespace DeepSeekHarnessLauncher.Tests;

public sealed class ProcessPathHelperTests
{
    [Fact]
    public void MergePaths_ConcatenatesInOrder_CurrentMachineUser()
    {
        var merged = ProcessPathHelper.MergePaths("C:\\a", "C:\\b", "C:\\c");

        Assert.Equal("C:\\a;C:\\b;C:\\c", merged);
    }

    [Fact]
    public void MergePaths_DeduplicatesCaseInsensitive()
    {
        var merged = ProcessPathHelper.MergePaths("C:\\a", "c:\\A", "C:\\A");

        Assert.Equal("C:\\a", merged);
    }

    [Fact]
    public void MergePaths_SkipsEmptySegments()
    {
        var merged = ProcessPathHelper.MergePaths("C:\\a;;", ";;C:\\b;", ";");

        Assert.Equal("C:\\a;C:\\b", merged);
    }

    [Fact]
    public void MergePaths_AllEmpty_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, ProcessPathHelper.MergePaths("", "", ""));
    }

    [Fact]
    public void RefreshPathFromRegistry_KeepsExistingEntries()
    {
        // 刷新后进程 PATH 必须仍包含刷新前的所有条目（不丢项、不乱序）。
        var before = Environment.GetEnvironmentVariable("Path") ?? string.Empty;

        ProcessPathHelper.RefreshPathFromRegistry();

        var after = Environment.GetEnvironmentVariable("Path") ?? string.Empty;
        var afterParts = after.Split(';', StringSplitOptions.RemoveEmptyEntries);
        foreach (var entry in before.Split(';', StringSplitOptions.RemoveEmptyEntries))
            Assert.Contains(afterParts, a => string.Equals(a, entry, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void RefreshPathFromRegistry_DoesNotThrow_OnMissingTargets()
    {
        // 只读 Machine/User 环境变量不应抛出异常。
        var ex = Record.Exception(() => ProcessPathHelper.RefreshPathFromRegistry());

        Assert.Null(ex);
    }
}

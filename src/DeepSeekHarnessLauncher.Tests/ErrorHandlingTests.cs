using DeepSeekHarnessLauncher.Models;
using DeepSeekHarnessLauncher.Services;
using DeepSeekHarnessLauncher.ViewModels;

namespace DeepSeekHarnessLauncher.Tests;

public sealed class ErrorHandlingTests
{
    [Fact]
    public void TryParsePortOccupied_Valid_ExtractsPortAndPid()
    {
        var ok = ServiceControlViewModel.TryParsePortOccupied(
            "端口 3080 已被占用 (PID 12345)", out var port, out var pid);

        Assert.True(ok);
        Assert.Equal(3080, port);
        Assert.Equal(12345, pid);
    }

    [Fact]
    public void TryParsePortOccupied_EnglishMessage_ExtractsPortAndPid()
    {
        // 问题 5：英文模式下端口占用消息也应能解析，弹出端口占用对话框。
        var ok = ServiceControlViewModel.TryParsePortOccupied(
            "Port 3080 is already in use (PID 12345)", out var port, out var pid);

        Assert.True(ok);
        Assert.Equal(3080, port);
        Assert.Equal(12345, pid);
    }

    [Fact]
    public void TryParsePortOccupied_OtherMessage_ReturnsFalse()
    {
        var ok = ServiceControlViewModel.TryParsePortOccupied(
            "启动超时（60s）", out _, out _);

        Assert.False(ok);
    }

    [Fact]
    public void TryParsePortOccupied_NullOrEmpty_ReturnsFalse()
    {
        Assert.False(ServiceControlViewModel.TryParsePortOccupied(null!, out _, out _));
        Assert.False(ServiceControlViewModel.TryParsePortOccupied(string.Empty, out _, out _));
    }

    [Fact]
    public void BuildMissingMessage_NodeMissing_ContainsNodeKey()
    {
        var result = new EnvironmentCheckResult { NodeInstalled = false, DshAvailable = true };

        var message = EnvironmentService.BuildMissingMessage(result);

        Assert.Contains("Msg.MissingNode", message);
        Assert.DoesNotContain("Msg.MissingDsh", message);
    }

    [Fact]
    public void BuildMissingMessage_BothMissing_ListsBothKeys()
    {
        var result = new EnvironmentCheckResult { NodeInstalled = false, DshAvailable = false };

        var message = EnvironmentService.BuildMissingMessage(result);

        Assert.Contains("Msg.MissingNode", message);
        Assert.Contains("Msg.MissingDsh", message);
    }

    [Fact]
    public void BuildMissingMessage_AllReady_ReturnsEmpty()
    {
        var result = new EnvironmentCheckResult { NodeInstalled = true, DshAvailable = true };

        Assert.Equal(string.Empty, EnvironmentService.BuildMissingMessage(result));
    }

    [Fact]
    public void BuildMissingMessage_DshMissing_HasSpecificPrompt()
    {
        var result = new EnvironmentCheckResult { NodeInstalled = true, DshAvailable = false };

        var message = EnvironmentService.BuildMissingMessage(result);

        // DSH 缺失时应给出特定提示（资源 key 回退到 Msg.MissingDsh，实际运行时为“请先在当前环境中安装 DeepSeek Harness”）。
        Assert.Contains("Msg.MissingDsh", message);
    }
}

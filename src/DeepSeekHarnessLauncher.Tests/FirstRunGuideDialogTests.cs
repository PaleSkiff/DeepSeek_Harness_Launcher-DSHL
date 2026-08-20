using DeepSeekHarnessLauncher.Models;
using DeepSeekHarnessLauncher.Views;

namespace DeepSeekHarnessLauncher.Tests;

public sealed class FirstRunGuideDialogTests
{
    [Fact]
    public void BuildNodeStatus_Installed_IncludesVersion()
    {
        var result = new EnvironmentCheckResult { NodeInstalled = true, NodeVersion = "v24.19.0\n" };

        Assert.Equal("node.js ✓ v24.19.0", FirstRunGuideDialog.BuildNodeStatus(result));
    }

    [Fact]
    public void BuildNodeStatus_NotInstalled_ShowsMissing()
    {
        var result = new EnvironmentCheckResult { NodeInstalled = false };

        Assert.Contains("✗", FirstRunGuideDialog.BuildNodeStatus(result));
    }

    [Fact]
    public void BuildDshStatus_Available_IncludesVersion()
    {
        var result = new EnvironmentCheckResult { DshAvailable = true, DshVersion = "0.1.0\n" };

        Assert.Equal("DeepSeek Harness ✓ 0.1.0", FirstRunGuideDialog.BuildDshStatus(result));
    }

    [Fact]
    public void BuildDshStatus_Unavailable_ShowsMissing()
    {
        var result = new EnvironmentCheckResult { DshAvailable = false };

        Assert.Contains("✗", FirstRunGuideDialog.BuildDshStatus(result));
    }
}

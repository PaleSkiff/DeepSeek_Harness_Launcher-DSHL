using DeepSeekHarnessLauncher.Services;

namespace DeepSeekHarnessLauncher.Tests;

public sealed class AutoStartServiceTests : IDisposable
{
    private readonly string _valueName;
    private readonly AutoStartService _service;

    public AutoStartServiceTests()
    {
        _valueName = "DSHLauncherTest_" + Guid.NewGuid().ToString("N");
        _service = new AutoStartService(_valueName);
    }

    public void Dispose()
    {
        try { _service.SetEnabled(false); }
        catch { /* ignore */ }
    }

    [Fact]
    public void Initially_NotEnabled()
    {
        Assert.False(_service.IsEnabled);
    }

    [Fact]
    public void SetEnabledTrue_WritesRegistry()
    {
        _service.SetEnabled(true);

        Assert.True(_service.IsEnabled);
    }

    [Fact]
    public void SetEnabledFalse_RemovesRegistry()
    {
        _service.SetEnabled(true);
        Assert.True(_service.IsEnabled);

        _service.SetEnabled(false);

        Assert.False(_service.IsEnabled);
    }
}

using DeepSeekHarnessLauncher.Models;
using DeepSeekHarnessLauncher.Services;

namespace DeepSeekHarnessLauncher.Tests;

public sealed class ConfigServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ConfigService _service;

    public ConfigServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "dshl-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _service = new ConfigService(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* 清理失败不影响测试 */ }
    }

    [Fact]
    public void Load_WhenFileMissing_ReturnsDefaults()
    {
        var config = _service.Load();

        Assert.Equal("npx", config.Service.Command);
        Assert.Equal("--verbose @deepseek-ai/dsh web", config.Service.Arguments);
        Assert.Equal(3080, config.Network.Port);
        Assert.Equal(60, config.Timeout.StartSeconds);
        Assert.Equal(15, config.Timeout.StopSeconds);
        Assert.True(config.Behavior.CloseToTray);
        Assert.Equal(7, config.Logging.RetentionDays);
    }

    [Fact]
    public void Save_Then_Load_RoundTrips()
    {
        var config = new AppConfig();
        config.Network.Port = 9090;
        config.Service.Command = "node";
        config.Service.Arguments = "server.js";
        config.Service.EnvironmentVariables["FOO"] = "bar";

        _service.Save(config);
        var loaded = _service.Load();

        Assert.Equal(9090, loaded.Network.Port);
        Assert.Equal("node", loaded.Service.Command);
        Assert.Equal("server.js", loaded.Service.Arguments);
        Assert.Equal("bar", loaded.Service.EnvironmentVariables["FOO"]);
        Assert.True(File.Exists(_service.ConfigPath));
    }

    [Fact]
    public void Load_WhenFileCorrupt_ReturnsDefaults_AndBacksUpBad()
    {
        File.WriteAllText(_service.ConfigPath, "{ not valid json !!!");

        var config = _service.Load();

        Assert.NotNull(config);
        Assert.Equal("npx", config.Service.Command);
        Assert.True(File.Exists(_service.ConfigPath + ".bad"));
    }

    [Fact]
    public void Load_WithMissingFields_FillsDefaults()
    {
        File.WriteAllText(_service.ConfigPath, """{ "network": { "port": 5000 } }""");

        var config = _service.Load();

        Assert.Equal(5000, config.Network.Port);
        Assert.Equal("npx", config.Service.Command);
        Assert.Equal(60, config.Timeout.StartSeconds);
    }

    [Fact]
    public void GetEffectiveHealthCheckUrl_WhenEmpty_UsesPort()
    {
        var config = new AppConfig { Network = new NetworkConfig { Port = 1234, HealthCheckUrl = "" } };

        Assert.Equal("http://127.0.0.1:1234", config.GetEffectiveHealthCheckUrl());
    }

    [Fact]
    public void GetEffectiveHealthCheckUrl_WhenSet_UsesSetValue()
    {
        var config = new AppConfig
        {
            Network = new NetworkConfig { HealthCheckUrl = "http://localhost:9999/health" },
        };

        Assert.Equal("http://localhost:9999/health", config.GetEffectiveHealthCheckUrl());
    }
}

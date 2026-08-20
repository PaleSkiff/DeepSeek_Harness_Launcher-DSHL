using DeepSeekHarnessLauncher.Services;

namespace DeepSeekHarnessLauncher.Tests;

public sealed class ProcessServiceTests
{
    // ============ ResolveCommand ============

    [Fact]
    public void ResolveCommand_Npx_WrapsInCmd()
    {
        var (fileName, args) = ProcessService.ResolveCommand("npx", "--verbose @deepseek-ai/dsh web");

        Assert.Equal("cmd.exe", fileName);
        Assert.Contains("npx --verbose @deepseek-ai/dsh web", args);
    }

    [Fact]
    public void ResolveCommand_CmdExtension_WrapsInCmd()
    {
        var (fileName, _) = ProcessService.ResolveCommand("npm.cmd", "view foo version");

        Assert.Equal("cmd.exe", fileName);
    }

    [Fact]
    public void ResolveCommand_Exe_NoWrap()
    {
        var (fileName, args) = ProcessService.ResolveCommand("C:\\node\\node.exe", "-v");

        Assert.Equal("C:\\node\\node.exe", fileName);
        Assert.Equal("-v", args);
    }

    // ============ ParsePidFromNetstat ============

    [Fact]
    public void ParsePidFromNetstat_Listening_ReturnsPid()
    {
        const string output = """
          TCP    127.0.0.1:3080    0.0.0.0:0    LISTENING    4242
        """;

        var pid = ProcessService.ParsePidFromNetstat(output, 3080);

        Assert.Equal(4242, pid);
    }

    [Fact]
    public void ParsePidFromNetstat_NotListening_ReturnsNull()
    {
        const string output = """
          TCP    127.0.0.1:3080    192.168.1.5:5555    ESTABLISHED    4242
        """;

        var pid = ProcessService.ParsePidFromNetstat(output, 3080);

        Assert.Null(pid);
    }

    [Fact]
    public void ParsePidFromNetstat_PortSubstring_NoFalseMatch()
    {
        // 端口 13080 不应匹配查询端口 3080
        const string output = """
          TCP    127.0.0.1:13080    0.0.0.0:0    LISTENING    7777
        """;

        var pid = ProcessService.ParsePidFromNetstat(output, 3080);

        Assert.Null(pid);
    }

    [Fact]
    public void ParsePidFromNetstat_Ipv6_ReturnsPid()
    {
        const string output = """
          TCP    [::1]:3080    [::]:0    LISTENING    8888
        """;

        var pid = ProcessService.ParsePidFromNetstat(output, 3080);

        Assert.Equal(8888, pid);
    }

    [Fact]
    public void ParsePidFromNetstat_Empty_ReturnsNull()
    {
        Assert.Null(ProcessService.ParsePidFromNetstat(string.Empty, 3080));
    }

    // ============ 集成测试（真实进程） ============

    [Fact]
    public async Task RunAsync_Echo_ReturnsOutput()
    {
        var service = new ProcessService();

        var result = await service.RunAsync("cmd.exe", "/c echo hello", TimeSpan.FromSeconds(5));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("hello", result.Output);
    }

    [Fact]
    public async Task StartAsync_ThenStopTree_KillsProcess()
    {
        var service = new ProcessService();

        var result = await service.StartAsync(
            "cmd.exe",
            "/c ping -n 30 127.0.0.1",
            Environment.CurrentDirectory,
            new Dictionary<string, string>());

        Assert.True(result.Success);
        Assert.True(service.IsRunning(result.Pid));

        await service.StopTreeAsync(result.Pid, TimeSpan.FromSeconds(5));
        await Task.Delay(500);

        Assert.False(service.IsRunning(result.Pid));
    }
}

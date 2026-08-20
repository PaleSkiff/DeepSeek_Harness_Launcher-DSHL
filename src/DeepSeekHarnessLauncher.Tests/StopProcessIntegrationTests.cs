using System.Net;
using System.Net.Sockets;
using DeepSeekHarnessLauncher.Services;

namespace DeepSeekHarnessLauncher.Tests;

/// <summary>问题1：验证能可靠停止 node 服务进程（真实 node 集成测试）。</summary>
public sealed class StopProcessIntegrationTests : IDisposable
{
    private readonly string _tempDir;

    public StopProcessIntegrationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "dshl-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* ignore */ }
    }

    [Fact]
    public async Task StopTree_ByPortPid_KillsNodeHttpServer_LaunchedViaCmd()
    {
        var service = new ProcessService();
        int port = GetFreePort();
        var script = Path.Combine(_tempDir, "server.js");
        File.WriteAllText(script, $"require('http').createServer((q,s)=>s.end('ok')).listen({port});");

        // "node" 无扩展名 → 走 cmd.exe 包装（与 npx 启动方式一致）。
        var result = await service.StartAsync(
            "node",
            script,
            _tempDir,
            new Dictionary<string, string>());

        Assert.True(result.Success);

        // 等待服务监听端口，拿到实际服务进程（node）的 PID。
        var portPid = await WaitForPortPidAsync(service, port);
        Assert.NotNull(portPid);

        // 通过端口 PID 终止 node 服务进程。
        await service.StopTreeAsync(portPid!.Value, TimeSpan.FromSeconds(5));
        await Task.Delay(400);

        Assert.False(service.IsRunning(portPid.Value));
        Assert.Null(service.GetPidListeningOnPort(port));

        // 清理 cmd 包装进程。
        await service.StopTreeAsync(result.Pid, TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task StopTree_ByDirectPid_KillsNodeHttpServer()
    {
        var service = new ProcessService();
        int port = GetFreePort();
        var script = Path.Combine(_tempDir, "server2.js");
        File.WriteAllText(script, $"require('http').createServer((q,s)=>s.end('ok')).listen({port});");

        var nodeExe = FindNodeExe();
        var result = await service.StartAsync(
            nodeExe,
            $"\"{script}\"",
            _tempDir,
            new Dictionary<string, string>());

        Assert.True(result.Success);

        var portPid = await WaitForPortPidAsync(service, port);
        Assert.NotNull(portPid);

        // 直接杀 node PID。
        await service.StopTreeAsync(portPid!.Value, TimeSpan.FromSeconds(5));
        await Task.Delay(400);

        Assert.False(service.IsRunning(portPid.Value));
        Assert.Null(service.GetPidListeningOnPort(port));
    }

    private static async Task<int?> WaitForPortPidAsync(ProcessService service, int port)
    {
        for (var i = 0; i < 40; i++)
        {
            var pid = service.GetPidListeningOnPort(port);
            if (pid.HasValue)
                return pid;
            await Task.Delay(200);
        }
        return null;
    }

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static string FindNodeExe()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "nodejs", "node.exe"),
            @"C:\Program Files\nodejs\node.exe",
        };
        return candidates.FirstOrDefault(File.Exists) ?? "node";
    }
}

using System.Net;
using System.Net.Sockets;
using System.Text;
using DeepSeekHarnessLauncher.Services;

namespace DeepSeekHarnessLauncher.Tests;

/// <summary>问题4：健康检查应对 2xx/3xx/4xx 视为在线，仅 5xx/连接失败视为不健康。</summary>
public sealed class HealthCheckerTests
{
    [Fact]
    public async Task CheckAsync_200_ReturnsHealthy()
        => await AssertHealthyAsync(200, true);

    [Fact]
    public async Task CheckAsync_304_ReturnsHealthy()
        => await AssertHealthyAsync(304, true);

    [Fact]
    public async Task CheckAsync_401_ReturnsHealthy()
        => await AssertHealthyAsync(401, true);

    [Fact]
    public async Task CheckAsync_500_ReturnsUnhealthy()
        => await AssertHealthyAsync(500, false);

    [Fact]
    public async Task CheckAsync_ConnectionRefused_ReturnsUnhealthy()
    {
        var checker = new HealthChecker();
        var port = GetFreePort();

        var result = await checker.CheckAsync($"http://127.0.0.1:{port}");

        Assert.False(result.IsHealthy);
    }

    private static async Task AssertHealthyAsync(int statusCode, bool expected)
    {
        var (port, serverTask) = StartHttpServer(statusCode);
        try
        {
            var checker = new HealthChecker();

            var result = await checker.CheckAsync($"http://127.0.0.1:{port}");

            Assert.Equal(expected, result.IsHealthy);
            Assert.Equal(statusCode, result.StatusCode);
        }
        finally
        {
            await serverTask;
        }
    }

    private static (int Port, Task Server) StartHttpServer(int statusCode)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var serverTask = Task.Run(async () =>
        {
            try
            {
                using var client = await listener.AcceptTcpClientAsync();
                using var stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.ASCII);

                // 读取请求行与请求头，直到空行。
                string? line;
                while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync()))
                {
                }

                var reason = statusCode switch
                {
                    200 => "OK",
                    304 => "Not Modified",
                    401 => "Unauthorized",
                    500 => "Internal Server Error",
                    _ => "OK",
                };
                const string body = "ok";
                var header = $"HTTP/1.1 {statusCode} {reason}\r\n"
                             + "Content-Type: text/plain\r\n"
                             + $"Content-Length: {body.Length}\r\n"
                             + "Connection: close\r\n\r\n";
                var response = Encoding.ASCII.GetBytes(header + body);
                await stream.WriteAsync(response);
                await stream.FlushAsync();
            }
            catch
            {
                // 测试服务器异常不影响断言。
            }
            finally
            {
                listener.Stop();
            }
        });

        return (port, serverTask);
    }

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}

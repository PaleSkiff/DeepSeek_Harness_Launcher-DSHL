using System.Net.Http;
using DeepSeekHarnessLauncher.Models;

namespace DeepSeekHarnessLauncher.Services;

/// <summary>基于 HttpClient 的 HTTP 健康检查。</summary>
public sealed class HealthChecker : IHealthChecker
{
    private static readonly HttpClient Client = new()
    {
        Timeout = TimeSpan.FromSeconds(5),
    };

    public async Task<HealthResult> CheckAsync(string url, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await Client.GetAsync(url, cancellationToken);
            // 服务是否"在线"以能否收到 HTTP 响应为准：2xx/3xx/4xx 都说明服务在运行，
            // 仅 5xx 或连接失败视为不健康（避免 DSH 返回 3xx 重定向/4xx 时误报"未就绪"）。
            return new HealthResult
            {
                IsHealthy = (int)response.StatusCode < 500,
                StatusCode = (int)response.StatusCode,
            };
        }
        catch (Exception ex)
        {
            return new HealthResult
            {
                IsHealthy = false,
                Error = ex.Message,
            };
        }
    }
}

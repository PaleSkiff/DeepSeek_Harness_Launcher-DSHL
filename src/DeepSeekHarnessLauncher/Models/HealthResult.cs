namespace DeepSeekHarnessLauncher.Models;

/// <summary>健康检查结果。</summary>
public sealed class HealthResult
{
    public bool IsHealthy { get; init; }
    public int? StatusCode { get; init; }
    public string? Error { get; init; }
}

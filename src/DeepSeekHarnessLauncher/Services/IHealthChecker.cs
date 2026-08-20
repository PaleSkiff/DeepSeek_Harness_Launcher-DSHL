using DeepSeekHarnessLauncher.Models;

namespace DeepSeekHarnessLauncher.Services;

public interface IHealthChecker
{
    Task<HealthResult> CheckAsync(string url, CancellationToken cancellationToken = default);
}

namespace DeepSeekHarnessLauncher.Models;

/// <summary>环境检测结果（node.js 与 DSH）。</summary>
public sealed class EnvironmentCheckResult
{
    public bool NodeInstalled { get; set; }
    public string? NodeVersion { get; set; }
    public string? NodePath { get; set; }
    public bool DshAvailable { get; set; }
    public string? DshVersion { get; set; }
    public string Output { get; set; } = string.Empty;

    public bool IsReady => NodeInstalled && DshAvailable;
}

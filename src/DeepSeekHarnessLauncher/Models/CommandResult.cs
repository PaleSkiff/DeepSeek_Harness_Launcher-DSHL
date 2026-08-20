namespace DeepSeekHarnessLauncher.Models;

/// <summary>短命令同步执行并捕获输出的结果。</summary>
public sealed class CommandResult
{
    public int ExitCode { get; init; }
    public string Output { get; init; } = string.Empty;
    public string Error { get; init; } = string.Empty;
    public bool Success => ExitCode == 0;
}

namespace DeepSeekHarnessLauncher.Services;

/// <summary>
/// 进程 PATH 刷新：从注册表（Machine + User）读取最新 PATH 并合并进当前进程，
/// 使本进程内新安装的 node.js / npm / npx 立即可用，无需重启应用。
/// 用于修复"安装 node.js 成功后重新检测仍显示未安装"的问题。
/// </summary>
public static class ProcessPathHelper
{
    /// <summary>将注册表中的 Machine + User PATH 合并进当前进程 PATH（去重、保序）。失败时保持现状。</summary>
    public static void RefreshPathFromRegistry()
    {
        try
        {
            var current = Environment.GetEnvironmentVariable("Path") ?? string.Empty;
            var machine = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.Machine) ?? string.Empty;
            var user = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.User) ?? string.Empty;
            var merged = MergePaths(current, machine, user);
            Environment.SetEnvironmentVariable("Path", merged);
        }
        catch
        {
            // 刷新失败不影响主流程（PATH 保持现状）。
        }
    }

    /// <summary>
    /// 合并三段 PATH：按 current → machine → user 顺序拼接，
    /// 去除重复项（大小写不敏感）与空白项。纯函数，便于测试。
    /// </summary>
    public static string MergePaths(string current, string machine, string user)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var parts = new List<string>();

        foreach (var segment in Split(current).Concat(Split(machine)).Concat(Split(user)))
        {
            var trimmed = segment.Trim();
            if (trimmed.Length > 0 && seen.Add(trimmed))
                parts.Add(trimmed);
        }

        return string.Join(';', parts);
    }

    private static IEnumerable<string> Split(string value)
        => string.IsNullOrEmpty(value)
            ? Array.Empty<string>()
            : value.Split(';', StringSplitOptions.RemoveEmptyEntries);
}

using System.IO;
using System.Text;
using DeepSeekHarnessLauncher.Models;

namespace DeepSeekHarnessLauncher.Services;

/// <summary>检测 node.js 与 DSH 依赖；缺失时提供自动安装/预拉取。</summary>
public sealed class EnvironmentService : IEnvironmentService
{
    private readonly IProcessService _process;

    public EnvironmentService(IProcessService process)
    {
        _process = process;
    }

    public event EventHandler<EnvironmentCheckResult>? CheckCompleted;

    public async Task<EnvironmentCheckResult> CheckAsync()
    {
        // 刷新进程 PATH：msiexec 等安装完成后注册表 PATH 已更新，
        // 但本进程 PATH 仍是启动时快照，不刷新会检测不到刚安装的 node/npm/npx。
        ProcessPathHelper.RefreshPathFromRegistry();

        var result = new EnvironmentCheckResult();
        var sb = new StringBuilder();

        // 并行执行三项检测，减少总耗时（慢速机器上从串行 ~40s 降到 ~10s 以内）。
        var nodeTask = _process.RunAsync("node", "-v", TimeSpan.FromSeconds(5));
        var dshTask = _process.RunAsync("npx", "--no-install @deepseek-ai/dsh --help", TimeSpan.FromSeconds(10));
        var versionTask = _process.RunAsync("npm", "view @deepseek-ai/dsh version", TimeSpan.FromSeconds(10));
        await Task.WhenAll(nodeTask, dshTask, versionTask);

        var node = nodeTask.Result;
        if (node.Success && !string.IsNullOrWhiteSpace(node.Output))
        {
            result.NodeInstalled = true;
            result.NodeVersion = node.Output.Trim();
            sb.AppendLine("> node -v");
            sb.AppendLine(node.Output.Trim());
        }
        else
        {
            sb.AppendLine($"> node -v  →  {GetText("Env.NodeMissingOutput")}");
        }

        // DSH 可用性：npx --no-install 只使用本地已缓存/已全局安装的包（不联网下载），
        // 与"下载失败却显示可用"的误判对齐——本地没有该包即为不可用。
        var dsh = dshTask.Result;
        if (dsh.Success)
        {
            result.DshAvailable = true;
            sb.AppendLine("> npx --no-install @deepseek-ai/dsh --help");
            sb.AppendLine(dsh.Output.Trim());

            // 版本号仅作展示（联网查询 registry），不影响可用性判定。
            var version = versionTask.Result;
            if (version.Success && !string.IsNullOrWhiteSpace(version.Output))
            {
                result.DshVersion = version.Output.Trim();
                sb.AppendLine("> npm view @deepseek-ai/dsh version");
                sb.AppendLine(version.Output.Trim());
            }
        }
        else
        {
            sb.AppendLine($"> npx --no-install @deepseek-ai/dsh --help  →  {GetText("Env.DshMissingOutput")}");
        }

        result.Output = sb.ToString();
        CheckCompleted?.Invoke(this, result);
        return result;
    }

    /// <summary>生成缺失依赖的提示文案。纯函数，便于测试。</summary>
    public static string BuildMissingMessage(EnvironmentCheckResult result)
    {
        var parts = new List<string>();
        if (!result.NodeInstalled)
            parts.Add(GetText("Msg.MissingNode"));
        if (!result.DshAvailable)
            parts.Add(GetText("Msg.MissingDsh"));

        if (parts.Count == 0)
            return string.Empty;

        return string.Join("\n", parts) + "\n" + GetText("Msg.InstallHint");
    }

    private static string GetText(string key)
        => System.Windows.Application.Current?.Resources[key] as string ?? key;

    // Node.js LTS 官方 MSI 下载地址（x64）。
    private const string NodeMsiUrl = "https://nodejs.org/dist/v20.18.0/node-v20.18.0-x64.msi";

    /// <summary>npx web 安装验证的兜底就绪判定端口：web 服务已监听该端口即视为安装成功。</summary>
    private const int DshWebReadyPort = 3080;

    /// <summary>日志批量上报间隔（毫秒）：npx --verbose 输出行数极大，逐行上报会在低配机器上拖垮 UI。</summary>
    private const int LogFlushIntervalMs = 500;

    /// <summary>等待循环轮询间隔（毫秒）。</summary>
    private const int PollIntervalMs = 500;

    /// <summary>
    /// 判定一行输出是否为 DSH 就绪标记：仅匹配包含 3080 端口的 URL/地址。
    /// 注意：不能匹配裸 "dsh web" 子串——npx verbose 回显第一行
    /// （"npm verbose title npm exec @deepseek-ai/dsh web"）就含 "dsh web"，
    /// 会在下载开始前就误报成功。纯函数，便于测试。
    /// </summary>
    public static bool IsDshReadyLine(string line)
        => line.Contains($"127.0.0.1:{DshWebReadyPort}", StringComparison.OrdinalIgnoreCase)
           || line.Contains($"localhost:{DshWebReadyPort}", StringComparison.OrdinalIgnoreCase)
           || (line.Contains("://", StringComparison.OrdinalIgnoreCase)
               && line.Contains($":{DshWebReadyPort}", StringComparison.OrdinalIgnoreCase));

    /// <summary>带进度提示的提权执行：先提示 UAC，静默安装期间每 10 秒心跳上报，避免进度区看起来"消失/卡死"。</summary>
    private async Task<CommandResult> RunElevatedWithProgressAsync(
        string command, string arguments, TimeSpan timeout, IProgress<string>? progress)
    {
        progress?.Report(GetText("Log.RequestingElevation"));
        var install = _process.RunElevatedAsync(command, arguments, timeout);

        var elapsed = 0;
        while (true)
        {
            var done = await Task.WhenAny(install, Task.Delay(TimeSpan.FromSeconds(10)));
            if (done == install)
                return await install;

            elapsed += 10;
            progress?.Report(string.Format(GetText("Log.SilentInstallWaitFmt"), elapsed));
        }
    }

    public async Task<bool> InstallNodeAsync(IProgress<string>? progress = null)
    {
        ProcessPathHelper.RefreshPathFromRegistry();
        progress?.Report($"[{GetText("Log.InstallNode")}]");

        // 1) 优先本地离线 MSI 安装包（随程序分发的 nodejs\*.msi）。
        progress?.Report(GetText("Log.SearchLocal"));
        var localMsi = FindLocalNodeMsi();
        if (!string.IsNullOrEmpty(localMsi))
        {
            var msiName = Path.GetFileName(localMsi);
            progress?.Report(GetText("Log.UseLocalMsi") + msiName);
            progress?.Report(GetText("Log.Cmd") + $"msiexec /i \"{localMsi}\" /qn /norestart");
            var localInstall = await RunElevatedWithProgressAsync(
                "msiexec.exe",
                $"/i \"{localMsi}\" /qn /norestart",
                TimeSpan.FromMinutes(10),
                progress);

            if (localInstall.Success)
            {
                progress?.Report($"[{GetText("Log.Success")}] {GetText("Log.NodeDone")}");
                return true;
            }

            progress?.Report($"[{GetText("Log.Failed")}] {GetText("Log.Error")}{localInstall.Error.Trim()}");
            progress?.Report(GetText("Log.TryOnline"));
        }
        else
        {
            progress?.Report(GetText("Log.NoLocalMsi"));
        }

        // 2) 在线方式（winget → 官网下载 MSI）。
        return await InstallNodeOnlineAsync(progress);
    }

    /// <summary>在线安装 node.js：先 winget，失败后 curl 下载官方 MSI 静默安装。</summary>
    public async Task<bool> InstallNodeOnlineAsync(IProgress<string>? progress = null)
    {
        ProcessPathHelper.RefreshPathFromRegistry();
        progress?.Report(GetText("Log.Winget"));
        progress?.Report(GetText("Log.Cmd") + "winget install --id OpenJS.NodeJS.LTS -e --silent --accept-package-agreements --accept-source-agreements");
        var winget = await _process.RunAsync(
            "winget",
            "install --id OpenJS.NodeJS.LTS -e --silent --accept-package-agreements --accept-source-agreements",
            TimeSpan.FromMinutes(10));

        if (winget.Success)
        {
            progress?.Report($"[{GetText("Log.Success")}] {GetText("Log.NodeDone")}");
            return true;
        }

        progress?.Report($"[{GetText("Log.Failed")}] {GetText("Log.Error")}{winget.Error.Trim()}");
        progress?.Report(GetText("Log.DownloadMsi"));

        var msiPath = Path.Combine(Path.GetTempPath(), $"nodejs-setup-{Guid.NewGuid():N}.msi");
        progress?.Report(GetText("Log.Cmd") + $"curl -L --fail -o \"{msiPath}\" {NodeMsiUrl}");
        var download = await _process.RunAsync(
            "curl",
            $"-L --fail --silent --show-error -o \"{msiPath}\" {NodeMsiUrl}",
            TimeSpan.FromMinutes(10));

        if (!download.Success || !File.Exists(msiPath))
        {
            progress?.Report($"[{GetText("Log.Failed")}] {GetText("Log.Error")}{download.Error.Trim()}");
            return false;
        }

        progress?.Report(GetText("Log.Cmd") + $"msiexec /i \"{msiPath}\" /qn /norestart");
        var install = await RunElevatedWithProgressAsync(
            "msiexec.exe",
            $"/i \"{msiPath}\" /qn /norestart",
            TimeSpan.FromMinutes(10),
            progress);

        TryDelete(msiPath);

        if (install.Success)
        {
            progress?.Report($"[{GetText("Log.Success")}] {GetText("Log.NodeDone")}");
            return true;
        }

        progress?.Report($"[{GetText("Log.Failed")}] {GetText("Log.Error")}{install.Error.Trim()}");
        return false;
    }

    /// <summary>查找本地离线的 node.js MSI 安装包（程序目录或项目根目录的 nodejs\*.msi）。</summary>
    public static string? FindLocalNodeMsi(string? baseDirectory = null)
    {
        try
        {
            var start = baseDirectory ?? AppContext.BaseDirectory;
            var searchDirs = new List<string>
            {
                Path.Combine(start, "nodejs"),
            };

            var dir = new DirectoryInfo(start);
            for (var i = 0; i < 6 && dir is not null; i++)
            {
                searchDirs.Add(Path.Combine(dir.FullName, "nodejs"));
                dir = dir.Parent;
            }

            foreach (var d in searchDirs)
            {
                if (!Directory.Exists(d))
                    continue;

                var msi = Directory.GetFiles(d, "*.msi").FirstOrDefault();
                if (!string.IsNullOrEmpty(msi))
                    return msi;
            }
        }
        catch
        {
            // 查找失败返回 null，走在线安装。
        }

        return null;
    }

    public async Task<bool> PrefetchDshAsync(IProgress<string>? progress = null)
    {
        // 刷新进程 PATH：刚安装 node.js 后必须立即可用 npx/npm，无需重启应用。
        ProcessPathHelper.RefreshPathFromRegistry();
        progress?.Report($"[{GetText("Log.InstallDsh")}]");

        // 1) 在隐藏命令提示符中运行 npx --yes --verbose @deepseek-ai/dsh web：
        //    --yes 自动确认安装（隐藏进程无 stdin，缺少它会卡在 "Ok to proceed?" 交互确认上）；
        //    实时捕获其输出作为详细安装日志；当出现 "dsh web: http://127.0.0.1:3080" 视为安装成功。
        progress?.Report(GetText("Log.NpxWeb"));
        progress?.Report(GetText("Log.Cmd") + "npx --yes --verbose @deepseek-ai/dsh web");
        var npx = await InstallDshViaVerboseWebAsync(progress);

        if (npx)
        {
            progress?.Report($"[{GetText("Log.Success")}] {GetText("Log.DshDone")}");
            return true;
        }

        // 2) npx 失败 → npm 全局安装（需写入 node 安装目录，通常是 Program Files，必须提权）。
        progress?.Report(GetText("Log.NpmInstall"));
        progress?.Report(GetText("Log.Cmd") + "npm install -g @deepseek-ai/dsh");
        progress?.Report(GetText("Log.NpmInstallElevated"));
        var npmPath = await ResolveNpmPathAsync();
        if (string.IsNullOrEmpty(npmPath))
        {
            progress?.Report($"[{GetText("Log.Failed")}] {GetText("Log.Error")}npm not found");
            return false;
        }

        var install = await _process.RunElevatedAsync(
            "cmd.exe",
            $"/c \"{npmPath}\" install -g @deepseek-ai/dsh",
            TimeSpan.FromMinutes(10));

        if (install.Success)
        {
            progress?.Report($"[{GetText("Log.Success")}] {GetText("Log.DshDone")}");
            return true;
        }

        progress?.Report($"[{GetText("Log.Failed")}] {GetText("Log.Error")}{install.Error.Trim()}");
        return false;
    }

    /// <summary>解析 npm.cmd 的完整路径（提权执行需要可执行文件的绝对路径）。</summary>
    private async Task<string?> ResolveNpmPathAsync()
    {
        try
        {
            var where = await _process.RunAsync("where", "npm.cmd", TimeSpan.FromSeconds(10));
            if (where.Success && !string.IsNullOrWhiteSpace(where.Output))
            {
                return where.Output
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Select(l => l.Trim())
                    .FirstOrDefault(l => l.Length > 0);
            }
        }
        catch
        {
            // 解析失败返回 null，走失败分支。
        }

        return null;
    }

    /// <summary>
    /// 在隐藏命令提示符中运行 npx --yes --verbose @deepseek-ai/dsh --help：
    /// --yes 自动确认安装（隐藏进程无 stdin，缺少它会卡在交互确认上）；
    /// 输出批量上报到安装日志（详细安装日志，低配机器友好）；
    /// 无等待上限，进程自行退出后以"包可用性验证"为成功判据，绝不在安装中途强杀；
    /// 无论成败都会清理临时进程，避免残留。
    /// 注意：安装阶段不启动 web 服务——完整 dsh web（含 worker 运行时）内存占用极高，
    /// 在低配虚拟机上会耗尽内存导致蓝屏；--help 只下载并轻量运行一次即退出。
    /// </summary>
    public async Task<bool> InstallDshViaVerboseWebAsync(IProgress<string>? progress = null)
    {
        // 日志行缓冲：npx --verbose 输出量极大，先缓冲再每 500ms 批量上报一次。
        var bufferLock = new object();
        var pendingLines = new List<string>();

        var start = await _process.StartAsync(
            "npx",
            "--yes --verbose @deepseek-ai/dsh --help",
            AppContext.BaseDirectory,
            new Dictionary<string, string>(),
            line =>
            {
                lock (bufferLock)
                    pendingLines.Add(line);
            });

        if (!start.Success)
        {
            progress?.Report($"[{GetText("Log.Failed")}] {GetText("Log.Error")}{start.Error?.Trim()}");
            return false;
        }

        // 等待进程自行退出（下载完成并运行 --help 后即退出）；无等待上限。
        var lastFlush = DateTimeOffset.MinValue;
        while (_process.IsRunning(start.Pid))
        {
            var now = DateTimeOffset.UtcNow;

            // 日志批量上报（500ms 一次，避免逐行触发 UI 更新）。
            if ((now - lastFlush).TotalMilliseconds >= LogFlushIntervalMs)
            {
                lastFlush = now;
                var batch = DrainPendingLines(pendingLines, bufferLock);
                if (batch.Length > 0)
                    progress?.Report(batch);
            }

            await Task.Delay(PollIntervalMs);
        }

        // 冲刷剩余日志。
        var remainder = DrainPendingLines(pendingLines, bufferLock);
        if (remainder.Length > 0)
            progress?.Report(remainder);

        // 进程已退出：包已下载并运行过 --help → 用轻量命令确认包可用。
        var verified = await VerifyDshAvailableAsync();

        progress?.Report(GetText("Log.StoppingVerifyProcess"));
        await _process.StopTreeAsync(start.Pid, TimeSpan.FromSeconds(10));

        // 防御性清理：若 3080 端口仍有监听者（如之前残留的验证服务），一并终止。
        try
        {
            var listener = _process.GetPidListeningOnPort(DshWebReadyPort);
            if (listener.HasValue)
                await _process.StopTreeAsync(listener.Value, TimeSpan.FromSeconds(10));
        }
        catch
        {
            // 清理失败不影响安装结果。
        }

        if (verified)
            return true;

        progress?.Report($"[{GetText("Log.Failed")}] {GetText("Log.Error")}{GetText("Log.VerifyFailed")}");
        return false;
    }

    private static string DrainPendingLines(List<string> pending, object lockObj)
    {
        lock (lockObj)
        {
            if (pending.Count == 0)
                return string.Empty;
            var batch = string.Join(Environment.NewLine, pending);
            pending.Clear();
            return batch;
        }
    }

    /// <summary>
    /// 验证 DeepSeek Harness 包实际可用（与检测页同一条命令，轻量 --help）。
    /// </summary>
    private async Task<bool> VerifyDshAvailableAsync()
    {
        try
        {
            var dsh = await _process.RunAsync(
                "npx",
                "--no-install @deepseek-ai/dsh --help",
                TimeSpan.FromSeconds(30));
            return dsh.Success;
        }
        catch
        {
            return false;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // 忽略清理失败。
        }
    }
}

using System.Text.RegularExpressions;
using DeepSeekHarnessLauncher.Models;
using DeepSeekHarnessLauncher.Services;

namespace DeepSeekHarnessLauncher.Tests;

public sealed class EnvironmentServiceTests
{
    private static EnvironmentService Create(FakeProcessService process) => new(process);

    // ==================== CheckAsync ====================

    [Fact]
    public async Task CheckAsync_NodeAndDshAvailable_ReturnsReady()
    {
        var process = new FakeProcessService
        {
            RunHandler = (command, _) => command switch
            {
                "node" => new CommandResult { ExitCode = 0, Output = "v20.11.0\n" },
                "npx" => new CommandResult { ExitCode = 0, Output = "dsh help ok\n" },
                "npm" => new CommandResult { ExitCode = 0, Output = "0.1.0\n" },
                _ => new CommandResult { ExitCode = -1 },
            },
        };
        var service = Create(process);

        var result = await service.CheckAsync();

        Assert.True(result.IsReady);
        Assert.True(result.NodeInstalled);
        Assert.True(result.DshAvailable);
        Assert.Equal("v20.11.0", result.NodeVersion);
        Assert.Equal("0.1.0", result.DshVersion);
    }

    [Fact]
    public async Task CheckAsync_NodeMissing_ReturnsNotReady()
    {
        var process = new FakeProcessService
        {
            RunHandler = (command, _) => command switch
            {
                "node" => new CommandResult { ExitCode = -1, Error = "not found" },
                "npx" => new CommandResult { ExitCode = 0, Output = "dsh help ok\n" },
                "npm" => new CommandResult { ExitCode = 0, Output = "0.1.0\n" },
                _ => new CommandResult { ExitCode = -1 },
            },
        };
        var service = Create(process);

        var result = await service.CheckAsync();

        Assert.False(result.IsReady);
        Assert.False(result.NodeInstalled);
        Assert.True(result.DshAvailable);
    }

    [Fact]
    public async Task CheckAsync_DshUnavailable_ReturnsNotReady()
    {
        // npx --no-install 失败（本地没有该包）→ DSH 不可用。
        var process = new FakeProcessService
        {
            RunHandler = (command, _) => command switch
            {
                "node" => new CommandResult { ExitCode = 0, Output = "v20.11.0\n" },
                "npx" => new CommandResult { ExitCode = -1, Error = "npx: package not found" },
                _ => new CommandResult { ExitCode = -1 },
            },
        };
        var service = Create(process);

        var result = await service.CheckAsync();

        Assert.False(result.IsReady);
        Assert.True(result.NodeInstalled);
        Assert.False(result.DshAvailable);
    }

    [Fact]
    public async Task CheckAsync_DshNotInstalledLocally_ReportsUnavailable_EvenIfRegistryReachable()
    {
        // 回归测试（问题 2）：DSH 下载/安装失败后不得再显示“可用”。
        // npm view（registry 可达）成功 ≠ 本地已安装；必须以 npx --no-install 为准。
        var process = new FakeProcessService
        {
            RunHandler = (command, _) => command switch
            {
                "node" => new CommandResult { ExitCode = 0, Output = "v20.11.0\n" },
                "npx" => new CommandResult { ExitCode = -1, Error = "npx: package @deepseek-ai/dsh not found" },
                "npm" => new CommandResult { ExitCode = 0, Output = "0.1.0\n" }, // registry 可达
                _ => new CommandResult { ExitCode = -1 },
            },
        };
        var service = Create(process);

        var result = await service.CheckAsync();

        Assert.False(result.DshAvailable, "DSH 未本地安装时即使 npm registry 可达也必须显示不可用");
        Assert.False(result.IsReady);
    }

    [Fact]
    public async Task CheckAsync_DshLocallyInstalled_ReportsAvailable()
    {
        // npx --no-install 成功（本地已缓存/已安装）→ 可用；此时 npm view 失败不影响判定。
        var process = new FakeProcessService
        {
            RunHandler = (command, _) => command switch
            {
                "node" => new CommandResult { ExitCode = 0, Output = "v20.11.0\n" },
                "npx" => new CommandResult { ExitCode = 0, Output = "dsh help ok\n" },
                "npm" => new CommandResult { ExitCode = -1, Error = "offline" },
                _ => new CommandResult { ExitCode = -1 },
            },
        };
        var service = Create(process);

        var result = await service.CheckAsync();

        Assert.True(result.DshAvailable);
        Assert.True(result.IsReady);
    }

    [Fact]
    public async Task CheckAsync_RunsAllThreeChecks_EvenIfDshMissing()
    {
        // 问题 2：检测必须并行执行 node/npx/npm 三项，总耗时不再串行累加；
        // 即使 DSH 未安装，版本查询也并发执行（不阻塞结果）。
        var commands = new List<string>();
        var process = new FakeProcessService
        {
            RunHandler = (command, _) =>
            {
                commands.Add(command);
                return command switch
                {
                    "node" => new CommandResult { ExitCode = 0, Output = "v20.11.0\n" },
                    "npx" => new CommandResult { ExitCode = -1, Error = "not installed" },
                    "npm" => new CommandResult { ExitCode = 0, Output = "0.1.0\n" },
                    _ => new CommandResult { ExitCode = -1 },
                };
            },
        };
        var service = Create(process);

        var result = await service.CheckAsync();

        Assert.Contains("node", commands);
        Assert.Contains("npx", commands);
        Assert.Contains("npm", commands);
        Assert.False(result.DshAvailable);
    }

    // ==================== InstallNodeAsync ====================

    [Fact]
    public async Task InstallNode_WingetSuccess_ReturnsTrue()
    {
        var process = new FakeProcessService
        {
            RunHandler = (_, _) => new CommandResult { ExitCode = 0, Output = "installed" },
        };
        var service = Create(process);

        var ok = await service.InstallNodeOnlineAsync();

        Assert.True(ok);
    }

    [Fact]
    public async Task InstallNode_WingetFails_DownloadsAndInstallsMsi_ReturnsTrue()
    {
        var commands = new List<string>();
        var process = new FakeProcessService
        {
            RunHandler = (command, args) =>
            {
                commands.Add(command);
                if (command == "winget")
                    return new CommandResult { ExitCode = -1, Error = "winget not found" };
                if (command == "curl")
                {
                    // 模拟下载成功：创建临时 msi 文件。
                    var m = Regex.Match(args, @"-o ""([^""]+)""");
                    if (m.Success)
                        File.WriteAllText(m.Groups[1].Value, "fake-msi");
                    return new CommandResult { ExitCode = 0, Output = "downloaded" };
                }
                return new CommandResult { ExitCode = -1 };
            },
            RunElevatedHandler = (command, _) =>
            {
                commands.Add(command);
                return new CommandResult { ExitCode = 0, Output = "installed" };
            },
        };
        var service = Create(process);

        var ok = await service.InstallNodeOnlineAsync();

        Assert.True(ok);
        Assert.Contains("winget", commands);
        Assert.Contains("curl", commands);
        Assert.Contains("msiexec.exe", commands);
    }

    [Fact]
    public async Task InstallNode_WingetFails_DownloadFails_ReturnsFalse()
    {
        var process = new FakeProcessService
        {
            RunHandler = (command, _) => command switch
            {
                "winget" => new CommandResult { ExitCode = -1, Error = "winget not found" },
                "curl" => new CommandResult { ExitCode = -1, Error = "network error" },
                _ => new CommandResult { ExitCode = -1 },
            },
        };
        var service = Create(process);

        var ok = await service.InstallNodeOnlineAsync();

        Assert.False(ok);
    }

    // ==================== PrefetchDshAsync ====================

    [Fact]
    public async Task PrefetchDsh_HelpRun_Verifies_Succeeds()
    {
        // 安装验证：npx --yes --verbose @deepseek-ai/dsh --help（轻量，不启动 web 服务）
        // 下载并运行完成后，包可用性验证通过 → 安装成功。
        var isRunningCalls = 0;
        var process = new FakeProcessService
        {
            StartOutputLines = { "npx: install @deepseek-ai/dsh", "Usage: dsh [command] ..." },
            IsRunningHandler = _ => ++isRunningCalls < 5, // 数秒后进程自行退出
        };
        var service = Create(process);

        var ok = await service.PrefetchDshAsync();

        Assert.True(ok);
        Assert.Equal("npx", Assert.Single(process.StartedCommands));
        Assert.Contains(12345, process.StoppedPids);
    }

    [Fact]
    public async Task PrefetchDsh_NpxWebFails_NpmInstallSucceeds_ReturnsTrue()
    {
        var elevatedCommands = new List<string>();
        var process = new FakeProcessService
        {
            // npx --help 运行失败（进程退出且包验证失败）→ 走 npm 全局安装兜底（提权）。
            IsRunningHandler = _ => false,
            RunHandler = (command, _) =>
            {
                if (command == "where")
                    return new CommandResult { ExitCode = 0, Output = "C:\\Program Files\\nodejs\\npm.cmd\n" };
                return new CommandResult { ExitCode = -1 };
            },
            RunElevatedHandler = (command, args) =>
            {
                elevatedCommands.Add($"{command} {args}");
                return new CommandResult { ExitCode = 0, Output = "installed" };
            },
        };
        var service = Create(process);

        var ok = await service.PrefetchDshAsync();

        Assert.True(ok);
        Assert.Contains("npx", process.StartedCommands);
        // npm 全局安装必须经 cmd.exe 提权，且命令中包含解析出的 npm.cmd 完整路径。
        Assert.Contains(elevatedCommands, c => c.StartsWith("cmd.exe") && c.Contains("npm.cmd") && c.Contains("install -g @deepseek-ai/dsh"));
    }

    [Fact]
    public async Task PrefetchDsh_AllFail_ReturnsFalse()
    {
        var process = new FakeProcessService
        {
            IsRunningHandler = _ => false, // npx web 提前退出
            RunHandler = (_, _) => new CommandResult { ExitCode = -1, Error = "network error" },
        };
        var service = Create(process);

        var ok = await service.PrefetchDshAsync();

        Assert.False(ok);
    }

    [Fact]
    public async Task CheckAsync_FiresCheckCompleted()
    {
        var process = new FakeProcessService
        {
            RunHandler = (command, _) => command switch
            {
                "node" => new CommandResult { ExitCode = 0, Output = "v20.11.0\n" },
                "npx" => new CommandResult { ExitCode = 0, Output = "dsh help ok\n" },
                "npm" => new CommandResult { ExitCode = 0, Output = "0.1.0\n" },
                _ => new CommandResult { ExitCode = -1 },
            },
        };
        var service = Create(process);
        EnvironmentCheckResult? captured = null;
        service.CheckCompleted += (_, r) => captured = r;

        var result = await service.CheckAsync();

        Assert.NotNull(captured);
        Assert.Same(result, captured);
        Assert.True(captured!.IsReady);
    }

    // ==================== 离线安装 ====================

    [Fact]
    public void FindLocalNodeMsi_FindsMsiInNodejsFolder()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "dshl-tests", Guid.NewGuid().ToString("N"));
        var nodejsDir = Path.Combine(tempDir, "nodejs");
        Directory.CreateDirectory(nodejsDir);
        var msi = Path.Combine(nodejsDir, "node-test-x64.msi");
        File.WriteAllText(msi, "fake-msi");

        try
        {
            var found = EnvironmentService.FindLocalNodeMsi(tempDir);

            Assert.NotNull(found);
            Assert.EndsWith("node-test-x64.msi", found);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public void FindLocalNodeMsi_NoMsi_ReturnsNull()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "dshl-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var found = EnvironmentService.FindLocalNodeMsi(tempDir);

            Assert.Null(found);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public void FindLocalNodeMsi_RealNodejsFolder_UsesPlainAsciiMsi()
    {
        // 真实 nodejs 目录应使用纯英文文件名的新 MSI（node-v24.19.0-x64.msi），
        // 避免含中文/全角括号的文件名导致 msiexec 离线安装失败。
        var msi = EnvironmentService.FindLocalNodeMsi();
        if (string.IsNullOrEmpty(msi))
            return; // 无 nodejs 目录的环境跳过。

        var name = Path.GetFileName(msi);
        Assert.EndsWith(".msi", name);
        Assert.DoesNotContain("（", name);
        Assert.DoesNotContain("）", name);
    }

    [Fact]
    public async Task InstallNode_LocalMsiPreferred_WhenAvailable()
    {
        // 项目根目录存在 nodejs\*.msi 时，应优先走 msiexec 离线安装，而不是 winget。
        if (string.IsNullOrEmpty(EnvironmentService.FindLocalNodeMsi()))
            return; // 无离线包的环境跳过此断言。

        var commands = new List<string>();
        var process = new FakeProcessService
        {
            RunHandler = (command, _) =>
            {
                commands.Add(command);
                return new CommandResult { ExitCode = 0, Output = "ok" };
            },
            RunElevatedHandler = (command, _) =>
            {
                commands.Add(command);
                return new CommandResult { ExitCode = 0, Output = "installed" };
            },
        };
        var service = Create(process);

        var ok = await service.InstallNodeAsync();

        Assert.True(ok);
        Assert.Contains("msiexec.exe", commands);
        Assert.DoesNotContain("winget", commands);
    }

    [Fact]
    public async Task InstallNodeOnline_LogsCommandsAndResults()
    {
        var logs = new List<string>();
        var progress = new SyncProgress<string>(logs.Add);
        var process = new FakeProcessService
        {
            RunHandler = (_, _) => new CommandResult { ExitCode = 0, Output = "ok" },
        };
        var service = Create(process);

        var ok = await service.InstallNodeOnlineAsync(progress);

        Assert.True(ok);
        Assert.Contains(logs, l => l.Contains("Log.Cmd"));     // 安装命令
        Assert.Contains(logs, l => l.Contains("Log.Success")); // 成功状态
    }

    [Fact]
    public async Task InstallNodeOnline_Failure_LogsError()
    {
        var logs = new List<string>();
        var progress = new SyncProgress<string>(logs.Add);
        var process = new FakeProcessService
        {
            RunHandler = (_, _) => new CommandResult { ExitCode = -1, Error = "boom" },
        };
        var service = Create(process);

        var ok = await service.InstallNodeOnlineAsync(progress);

        Assert.False(ok);
        Assert.Contains(logs, l => l.Contains("Log.Failed")); // 失败状态
        Assert.Contains(logs, l => l.Contains("boom"));       // 错误信息
    }

    [Fact]
    public async Task InstallNode_UsesElevatedMsiexec()
    {
        // msiexec 静默安装需要管理员权限，必须走提权执行（RunElevatedAsync）。
        var elevatedCommands = new List<string>();
        var process = new FakeProcessService
        {
            RunElevatedHandler = (command, _) =>
            {
                elevatedCommands.Add(command);
                return new CommandResult { ExitCode = 0, Output = "installed" };
            },
        };
        var service = Create(process);

        var ok = await service.InstallNodeAsync();

        Assert.True(ok);
        Assert.Contains("msiexec.exe", elevatedCommands);
    }

    [Fact]
    public async Task InstallNode_ReportsElevationRequest_SoProgressNeverSeemsVanished()
    {
        // 问题 1：提权（UAC）前必须上报提示，安装日志不能静默/消失。
        var logs = new List<string>();
        var progress = new SyncProgress<string>(logs.Add);
        var process = new FakeProcessService
        {
            RunHandler = (_, _) => new CommandResult { ExitCode = 0, Output = "ok" },
            RunElevatedHandler = (_, _) => new CommandResult { ExitCode = 0, Output = "installed" },
        };
        var service = Create(process);

        var ok = await service.InstallNodeAsync(progress);

        Assert.True(ok);
        Assert.Contains(logs, l => l.Contains("Log.RequestingElevation"));
    }

    // ==================== InstallDshViaVerboseWebAsync ====================

    [Fact]
    public async Task InstallDshViaVerboseWeb_HelpRun_Verifies_Succeeds()
    {
        // 安装验证：轻量 --help 运行完成 + 包验证通过 → 成功，并清理临时进程。
        var isRunningCalls = 0;
        var process = new FakeProcessService
        {
            StartOutputLines = { "npx: downloading package...", "Usage: dsh [command]..." },
            IsRunningHandler = _ => ++isRunningCalls < 5, // 下载并运行 --help 后进程自行退出
        };
        var service = Create(process);

        var ok = await service.InstallDshViaVerboseWebAsync();

        Assert.True(ok);
        Assert.Equal("npx", Assert.Single(process.StartedCommands));
        Assert.Equal("--yes --verbose @deepseek-ai/dsh --help", Assert.Single(process.StartedArguments));
        Assert.Contains(12345, process.StoppedPids);
    }

    [Fact]
    public async Task InstallDshViaVerboseWeb_CommandIncludesYesFlag()
    {
        // 回归测试：缺少 --yes 时 npx 会在隐藏进程中等待交互确认
        // （"Ok to proceed? (y)"，进程无 stdin 会永久挂起直到超时），
        // 必须使用 --yes 自动确认安装包下载。
        var isRunningCalls = 0;
        var process = new FakeProcessService
        {
            StartOutputLines = { "Usage: dsh [command]..." },
            IsRunningHandler = _ => ++isRunningCalls < 3,
        };
        var service = Create(process);

        var ok = await service.InstallDshViaVerboseWebAsync();

        Assert.True(ok);
        var args = Assert.Single(process.StartedArguments);
        Assert.StartsWith("--yes ", args);
        Assert.Contains("--verbose", args);
        Assert.Contains("@deepseek-ai/dsh --help", args);
    }

    [Fact]
    public async Task InstallDshViaVerboseWeb_Exits_VerificationFails_ReturnsFalse()
    {
        // 进程退出但包验证失败（下载失败/包不可用）→ 安装失败，且清理进程。
        var process = new FakeProcessService
        {
            IsRunningHandler = _ => false, // 进程已退出
            StartOutputLines = { "npx: failed to fetch package" },
            RunHandler = (_, _) => new CommandResult { ExitCode = -1, Error = "not installed" },
        };
        var service = Create(process);

        var ok = await service.InstallDshViaVerboseWebAsync();

        Assert.False(ok);
        Assert.Contains(12345, process.StoppedPids); // 失败也需清理进程
    }

    [Fact]
    public async Task InstallDshViaVerboseWeb_StartFails_ReturnsFalse()
    {
        var process = new FakeProcessService
        {
            StartHandler = () => ProcessLaunchResult.Failed("cannot launch"),
        };
        var service = Create(process);

        var ok = await service.InstallDshViaVerboseWebAsync();

        Assert.False(ok);
        Assert.Empty(process.StoppedPids); // 未启动成功，无需清理
    }

    [Fact]
    public async Task InstallDshViaVerboseWeb_StreamsEveryLineToProgress()
    {
        var logs = new List<string>();
        var progress = new SyncProgress<string>(logs.Add);
        var isRunningCalls = 0;
        var process = new FakeProcessService
        {
            StartOutputLines =
            {
                "verbose line one",
                "verbose line two",
                "Usage: dsh [command]...",
            },
            IsRunningHandler = _ => ++isRunningCalls < 3,
        };
        var service = Create(process);

        var ok = await service.InstallDshViaVerboseWebAsync(progress);

        Assert.True(ok);
        // 命令行输出的每一行都应成为详细安装日志（按批合并上报，不丢行）。
        Assert.Contains(logs, l => l.Contains("verbose line one"));
        Assert.Contains(logs, l => l.Contains("verbose line two"));
        Assert.Contains(logs, l => l.Contains("Usage: dsh"));
    }

    [Fact]
    public async Task InstallDshViaVerboseWeb_UsesLightHelpCommand_NotWebServer()
    {
        // 蓝屏防护：安装验证必须使用轻量 --help（不启动完整 web 服务），
        // 且不再注入大堆上限（低配虚拟机 4096 堆会耗尽内存蓝屏）。
        var isRunningCalls = 0;
        var process = new FakeProcessService
        {
            StartOutputLines = { "Usage: dsh [command]..." },
            IsRunningHandler = _ => ++isRunningCalls < 3,
        };
        var service = Create(process);

        var ok = await service.InstallDshViaVerboseWebAsync();

        Assert.True(ok);
        var args = Assert.Single(process.StartedArguments);
        Assert.Contains("@deepseek-ai/dsh --help", args);
        Assert.DoesNotContain(args.Split(' ', StringSplitOptions.RemoveEmptyEntries), t => t == "web");
        Assert.DoesNotContain(process.StartedEnvironmentVariables, env => env.ContainsKey("NODE_OPTIONS"));
    }

    [Fact]
    public async Task InstallDshViaVerboseWeb_CleansUpPortListener_AfterInstall()
    {
        // 防御性清理：安装结束后若有进程残留占用 3080，一并终止（避免随后点击启动失败）。
        var isRunningCalls = 0;
        var process = new FakeProcessService
        {
            IsRunningHandler = _ => ++isRunningCalls < 3,
            PortHandler = _ => 9999, // 3080 被（残留）进程监听
        };
        var service = Create(process);

        var ok = await service.InstallDshViaVerboseWebAsync();

        Assert.True(ok);
        Assert.Contains(12345, process.StoppedPids); // npx 进程
        Assert.Contains(9999, process.StoppedPids);  // 3080 端口监听者（残留清理）
    }

    [Fact]
    public async Task PrefetchDsh_NpxFails_NpmNotFound_ReturnsFalse()
    {
        // where 解析不到 npm.cmd 时，npm 全局安装无法进行，返回失败。
        var process = new FakeProcessService
        {
            IsRunningHandler = _ => false,
            RunHandler = (_, _) => new CommandResult { ExitCode = -1, Error = "not found" },
        };
        var service = Create(process);

        var ok = await service.PrefetchDshAsync();

        Assert.False(ok);
    }

    [Fact]
    public void IsDshReadyLine_MatchesBannerVariants()
    {
        // 问题：就绪标记必须兼容不同横幅格式（localhost / 0.0.0.0 / 带斜杠等）。
        Assert.True(EnvironmentService.IsDshReadyLine("dsh web: http://127.0.0.1:3080"));
        Assert.True(EnvironmentService.IsDshReadyLine("Web UI: http://localhost:3080/"));
        Assert.True(EnvironmentService.IsDshReadyLine("[INFO] listening on 127.0.0.1:3080"));
        Assert.True(EnvironmentService.IsDshReadyLine("dsh web: http://0.0.0.0:3080"));

        // 回归（"2 秒装好但检测没有 DSH"）：npx verbose 回显含 "dsh web" 字样，
        // 绝不能误判为就绪标记。
        Assert.False(EnvironmentService.IsDshReadyLine("npm verbose title npm exec @deepseek-ai/dsh web"));
        Assert.False(EnvironmentService.IsDshReadyLine("npm verbose argv \"exec\" \"--loglevel\" \"verbose\" \"--\" \"@deepseek-ai/dsh\" \"web\""));
        Assert.False(EnvironmentService.IsDshReadyLine("npm http fetch GET 200 https://registry.npmjs.org/@deepseek-ai%2fdsh"));
        Assert.False(EnvironmentService.IsDshReadyLine("Need to install the following packages:"));
    }
}

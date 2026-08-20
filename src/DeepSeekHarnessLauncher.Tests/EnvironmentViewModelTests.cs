using DeepSeekHarnessLauncher.Models;
using DeepSeekHarnessLauncher.Services;
using DeepSeekHarnessLauncher.ViewModels;

namespace DeepSeekHarnessLauncher.Tests;

public sealed class EnvironmentViewModelTests
{
    [Fact]
    public async Task InstallMissing_InstallsNodeBeforeDsh()
    {
        var commands = new List<string>();
        var nodeInstalled = false;
        var dshInstalled = false;

        var process = new FakeProcessService
        {
            // npx web 验证进程未出现就绪标记即退出 → 走 npm 全局安装兜底。
            IsRunningHandler = _ => false,
            RunHandler = (command, args) =>
            {
                commands.Add(command);

                // 环境检测：node -v；DSH 用 npx --no-install（本地包检测）
                if (command == "node" && args == "-v")
                    return nodeInstalled
                        ? new CommandResult { ExitCode = 0, Output = "v24.19.0\n" }
                        : new CommandResult { ExitCode = -1, Error = "not found" };
                if (command == "npx")
                    return dshInstalled
                        ? new CommandResult { ExitCode = 0, Output = "dsh help ok\n" }
                        : new CommandResult { ExitCode = -1, Error = "not installed" };
                if (command == "npm" && args.Contains("view"))
                    return new CommandResult { ExitCode = 0, Output = "0.1.0\n" };

                // npm 全局安装兜底：先用 where 解析 npm.cmd 路径
                if (command == "where")
                    return new CommandResult { ExitCode = 0, Output = "C:\\Program Files\\nodejs\\npm.cmd\n" };

                return new CommandResult { ExitCode = 0, Output = "ok" };
            },
            // 安装 node（msiexec 离线，需管理员提权）；npm 全局安装（cmd.exe + npm.cmd 路径，需管理员提权）
            RunElevatedHandler = (command, args) =>
            {
                commands.Add(command);
                commands.Add($"{command} {args}");
                if (command == "msiexec.exe")
                {
                    nodeInstalled = true;
                    return new CommandResult { ExitCode = 0, Output = "installed" };
                }
                if (command == "cmd.exe" && args.Contains("npm.cmd"))
                {
                    dshInstalled = true;
                    return new CommandResult { ExitCode = 0, Output = "installed" };
                }
                return new CommandResult { ExitCode = 0, Output = "ok" };
            },
        };
        var environment = new EnvironmentService(process);
        var vm = new EnvironmentViewModel(environment);

        var ok = await vm.InstallMissingAsync();

        Assert.True(ok);
        var msiIndex = commands.IndexOf("msiexec.exe");
        Assert.True(msiIndex >= 0, "应通过 msiexec 离线安装 node.js");
        Assert.Contains(commands, c => c.Contains("npm.cmd")); // DSH 兜底通过提权的 npm 全局安装
    }

    [Fact]
    public async Task InstallMissing_WhenAllPresent_SkipsInstall()
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
        var environment = new EnvironmentService(process);
        var vm = new EnvironmentViewModel(environment);

        var ok = await vm.InstallMissingAsync();

        Assert.True(ok);
    }

    [Fact]
    public async Task InstallMissing_RechecksEnvironmentAfterInstall()
    {
        var nodeChecks = 0;
        var nodeInstalled = false;
        var dshInstalled = false;

        var process = new FakeProcessService
        {
            // npx web 验证进程未出现就绪标记即退出 → 走 npm 全局安装兜底。
            IsRunningHandler = _ => false,
            RunHandler = (command, args) =>
            {
                if (command == "node" && args == "-v")
                {
                    nodeChecks++;
                    return nodeInstalled
                        ? new CommandResult { ExitCode = 0, Output = "v24.19.0\n" }
                        : new CommandResult { ExitCode = -1, Error = "not found" };
                }
                if (command == "npx")
                    return dshInstalled
                        ? new CommandResult { ExitCode = 0, Output = "dsh help ok\n" }
                        : new CommandResult { ExitCode = -1, Error = "not installed" };
                if (command == "npm" && args.Contains("view"))
                    return new CommandResult { ExitCode = 0, Output = "0.1.0\n" };
                if (command == "where")
                    return new CommandResult { ExitCode = 0, Output = "C:\\Program Files\\nodejs\\npm.cmd\n" };
                return new CommandResult { ExitCode = 0, Output = "ok" };
            },
            // 安装 node（msiexec 离线，需管理员提权）；npm 全局安装（cmd.exe + npm.cmd 路径，需管理员提权）
            RunElevatedHandler = (command, args) =>
            {
                if (command == "msiexec.exe")
                {
                    nodeInstalled = true;
                    return new CommandResult { ExitCode = 0, Output = "installed" };
                }
                if (command == "cmd.exe" && args.Contains("npm.cmd"))
                {
                    dshInstalled = true;
                    return new CommandResult { ExitCode = 0, Output = "installed" };
                }
                return new CommandResult { ExitCode = 0, Output = "ok" };
            },
        };
        var environment = new EnvironmentService(process);
        var vm = new EnvironmentViewModel(environment);

        var ok = await vm.InstallMissingAsync();

        Assert.True(ok);
        // 安装后必须重新执行真实环境检测（node -v 至少被调用 2 次：初始 + 安装后）。
        Assert.True(nodeChecks >= 2, $"node -v 实际检测 {nodeChecks} 次，应 ≥ 2 次（安装后需重新检测）");
    }

    [Fact]
    public async Task CheckAsync_PerformsRealDetection()
    {
        var commands = new List<string>();
        var process = new FakeProcessService
        {
            RunHandler = (command, _) =>
            {
                commands.Add(command);
                if (command == "node")
                    return new CommandResult { ExitCode = 0, Output = "v20.11.0\n" };
                if (command == "npx")
                    return new CommandResult { ExitCode = 0, Output = "dsh help ok\n" };
                if (command == "npm")
                    return new CommandResult { ExitCode = 0, Output = "0.1.0\n" };
                return new CommandResult { ExitCode = -1 };
            },
        };
        var environment = new EnvironmentService(process);
        var vm = new EnvironmentViewModel(environment);

        await vm.CheckCommand.ExecuteAsync(null);

        // 真实检测：必须实际执行 node、npx（本地 DSH 检测）与 npm（版本展示）命令，不能跳过/使用缓存。
        Assert.Contains("node", commands);
        Assert.Contains("npx", commands);
        Assert.Contains("npm", commands);
        Assert.NotNull(vm.Result);
        Assert.True(vm.Result!.IsReady);
    }

    [Fact]
    public async Task Constructor_AutoDetectsEnvironment()
    {
        // 问题 3：环境/安装页无需手动点击即可自动检测。
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
        var environment = new EnvironmentService(process);
        var vm = new EnvironmentViewModel(environment);

        // 构造时触发的自动检测为异步任务，等待其完成。
        for (var i = 0; i < 100 && vm.Result is null; i++)
            await Task.Delay(10);

        Assert.NotNull(vm.Result);
        Assert.True(vm.Result!.IsReady);
    }

    [Fact]
    public async Task InstallCommands_Ignored_WhileInstalling()
    {
        // 正在安装时重复点击不应再次启动安装（防止并发安装互相干扰）。
        var process = new FakeProcessService
        {
            RunHandler = (_, _) => new CommandResult { ExitCode = 0, Output = "ok" },
        };
        var vm = new EnvironmentViewModel(new EnvironmentService(process));
        vm.IsInstalling = true;

        await vm.InstallNodeCommand.ExecuteAsync(null);
        await vm.InstallDshCommand.ExecuteAsync(null);

        // 未进入安装流程：不产生安装日志、不切换安装中状态。
        Assert.Equal(string.Empty, vm.InstallLog);
    }

    [Fact]
    public async Task InstallDsh_Success_AutoRechecksEnvironment()
    {
        // 问题：安装成功后必须自动重新检测，状态立即更新（无需手动点击"重新检测"）。
        var dshInstalled = false;
        var process = new FakeProcessService
        {
            IsRunningHandler = _ => false, // npx web 验证快速失败 → 走 npm 全局安装兜底
            RunHandler = (command, args) =>
            {
                if (command == "node" && args == "-v")
                    return new CommandResult { ExitCode = 0, Output = "v20.11.0\n" };
                if (command == "npx")
                    return dshInstalled
                        ? new CommandResult { ExitCode = 0, Output = "dsh help ok\n" }
                        : new CommandResult { ExitCode = -1, Error = "not installed" };
                if (command == "npm" && args.Contains("view"))
                    return new CommandResult { ExitCode = 0, Output = "0.1.0\n" };
                if (command == "where")
                    return new CommandResult { ExitCode = 0, Output = "C:\\Program Files\\nodejs\\npm.cmd\n" };
                return new CommandResult { ExitCode = 0, Output = "ok" };
            },
            RunElevatedHandler = (command, args) =>
            {
                if (command == "cmd.exe" && args.Contains("npm.cmd"))
                {
                    dshInstalled = true;
                    return new CommandResult { ExitCode = 0, Output = "installed" };
                }
                return new CommandResult { ExitCode = 0, Output = "ok" };
            },
        };
        var vm = new EnvironmentViewModel(new EnvironmentService(process));

        await vm.InstallDshCommand.ExecuteAsync(null);

        // 安装命令完成后 Result 必须已是最新检测结果（DSH 可用）。
        Assert.NotNull(vm.Result);
        Assert.True(vm.Result!.DshAvailable, "安装成功后应自动重新检测，显示 DSH 可用");
    }
}

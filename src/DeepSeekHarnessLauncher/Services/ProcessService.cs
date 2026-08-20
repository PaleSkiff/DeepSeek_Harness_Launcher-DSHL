using System.Diagnostics;
using System.IO;
using System.Text;
using DeepSeekHarnessLauncher.Models;

namespace DeepSeekHarnessLauncher.Services;

/// <summary>
/// 基于 System.Diagnostics.Process 的进程控制：启动/执行命令、终止进程树、端口→PID。
/// </summary>
public sealed class ProcessService : IProcessService
{
    public Task<ProcessLaunchResult> StartAsync(
        string command,
        string arguments,
        string workingDirectory,
        IReadOnlyDictionary<string, string> environmentVariables,
        Action<string>? onOutputLine = null,
        CancellationToken cancellationToken = default)
    {
        var (fileName, args) = ResolveCommand(command, arguments);

        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = args,
            WorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory)
                ? Environment.CurrentDirectory
                : workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        foreach (var kv in environmentVariables)
            psi.Environment[kv.Key] = kv.Value;

        var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                onOutputLine?.Invoke(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                onOutputLine?.Invoke(e.Data);
        };

        try
        {
            if (!process.Start())
                return Task.FromResult(ProcessLaunchResult.Failed(GetText("Err.ProcessStartFailed")));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ProcessLaunchResult.Failed(ex.Message));
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        return Task.FromResult(ProcessLaunchResult.Started(process.Id));
    }

    public async Task<CommandResult> RunAsync(
        string command,
        string arguments,
        TimeSpan timeout,
        IReadOnlyDictionary<string, string>? environmentVariables = null)
    {
        var (fileName, args) = ResolveCommand(command, arguments);

        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        if (environmentVariables is not null)
        {
            foreach (var kv in environmentVariables)
                psi.Environment[kv.Key] = kv.Value;
        }

        using var process = new Process { StartInfo = psi };
        var output = new StringBuilder();
        var error = new StringBuilder();
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                output.AppendLine(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                error.AppendLine(e.Data);
        };

        try
        {
            if (!process.Start())
                return new CommandResult { ExitCode = -1, Error = GetText("Err.CommandStartFailed") };
        }
        catch (Exception ex)
        {
            return new CommandResult { ExitCode = -1, Error = ex.Message };
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var cts = new CancellationTokenSource(timeout);
        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { /* ignore */ }
            return new CommandResult { ExitCode = -1, Error = GetText("Err.CommandTimeout") };
        }

        return new CommandResult
        {
            ExitCode = process.ExitCode,
            Output = output.ToString(),
            Error = error.ToString(),
        };
    }

    public async Task<CommandResult> RunElevatedAsync(string command, string arguments, TimeSpan timeout)
    {
        var psi = new ProcessStartInfo
        {
            FileName = command,
            Arguments = arguments,
            UseShellExecute = true,
            Verb = "runas", // 触发 UAC 提权
            WindowStyle = ProcessWindowStyle.Hidden, // 提权启动的控制台（如 cmd.exe）不弹出黑色窗口
        };

        try
        {
            using var process = Process.Start(psi);
            if (process is null)
                return new CommandResult { ExitCode = -1, Error = GetText("Err.CommandStartFailed") };

            using var cts = new CancellationTokenSource(timeout);
            try
            {
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(entireProcessTree: true); } catch { /* ignore */ }
                return new CommandResult { ExitCode = -1, Error = GetText("Err.CommandTimeout") };
            }

            return new CommandResult { ExitCode = process.ExitCode };
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            // 用户取消 UAC 提示或提权失败。
            return new CommandResult { ExitCode = -1, Error = ex.Message };
        }
        catch (Exception ex)
        {
            return new CommandResult { ExitCode = -1, Error = ex.Message };
        }
    }

    public Task StopTreeAsync(int pid, TimeSpan timeout)
    {
        var waitMs = (int)Math.Max(1, timeout.TotalMilliseconds);

        // 优先使用 taskkill /T /F：系统级终止整个进程树（含 npx/node 等孙进程），最可靠。
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "taskkill.exe",
                Arguments = $"/PID {pid} /T /F",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            using var killer = Process.Start(psi);
            killer?.WaitForExit(waitMs);
        }
        catch
        {
            // taskkill 不可用时走下方 .NET 兜底。
        }

        // 兜底：若进程仍存活，用 .NET Kill 强制终止整棵树。
        try
        {
            using var process = Process.GetProcessById(pid);
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(waitMs);
            }
        }
        catch (ArgumentException)
        {
            // 进程不存在，视为已停止。
        }
        catch (InvalidOperationException)
        {
            // 进程已退出。
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // 访问被拒绝或进程已消失，忽略。
        }

        return Task.CompletedTask;
    }

    public int? GetPidListeningOnPort(int port)
    {
        var output = RunNetstat();
        return ParsePidFromNetstat(output, port);
    }

    public bool IsRunning(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>
    /// 无扩展名 / .cmd / .bat 的命令用 cmd.exe 包装（npx/npm 属于此类）。
    /// 纯函数，便于测试。
    /// </summary>
    public static (string FileName, string Arguments) ResolveCommand(string command, string arguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);

        var ext = Path.GetExtension(command.Trim());
        var needsCmdWrapper = string.IsNullOrEmpty(ext)
            || ext.Equals(".cmd", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".bat", StringComparison.OrdinalIgnoreCase);

        if (!needsCmdWrapper)
            return (command, arguments);

        var full = string.IsNullOrWhiteSpace(arguments) ? command : $"{command} {arguments}";
        return ("cmd.exe", $"/d /s /c \"{full}\"");
    }

    /// <summary>从 netstat -ano -p TCP 输出中解析监听指定端口的 PID。纯函数，便于测试。</summary>
    public static int? ParsePidFromNetstat(string netstatOutput, int port)
    {
        var suffix = $":{port}";
        foreach (var rawLine in netstatOutput.Split('\n'))
        {
            var tokens = rawLine.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length < 5)
                continue;
            if (!tokens[0].Equals("TCP", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!tokens[1].EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                continue;
            if (!tokens[3].Equals("LISTENING", StringComparison.OrdinalIgnoreCase))
                continue;
            if (int.TryParse(tokens[^1], out var pid))
                return pid;
        }
        return null;
    }

    private static string RunNetstat()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "netstat.exe",
                Arguments = "-ano -p TCP",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            };

            using var process = Process.Start(psi);
            if (process is null)
                return string.Empty;
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000);
            return output;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string GetText(string key)
        => System.Windows.Application.Current?.Resources[key] as string ?? key;
}

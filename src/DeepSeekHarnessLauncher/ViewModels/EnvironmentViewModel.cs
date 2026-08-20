using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeepSeekHarnessLauncher.Models;
using DeepSeekHarnessLauncher.Services;
using DeepSeekHarnessLauncher.Views;

namespace DeepSeekHarnessLauncher.ViewModels;

/// <summary>④ 环境页 ViewModel：依赖检测与安装（含进度与日志）。</summary>
public partial class EnvironmentViewModel : ViewModelBase
{
    private readonly IEnvironmentService _environment;

    public string DisplayName => "环境";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NodeStatusText))]
    [NotifyPropertyChangedFor(nameof(DshStatusText))]
    [NotifyPropertyChangedFor(nameof(NodeReady))]
    [NotifyPropertyChangedFor(nameof(DshReady))]
    private EnvironmentCheckResult? _result;

    [ObservableProperty]
    private bool _isChecking;

    [ObservableProperty]
    private string? _message;

    [ObservableProperty]
    private bool _isInstalling;

    [ObservableProperty]
    private string _installTitle = string.Empty;

    [ObservableProperty]
    private string _installLog = string.Empty;

    public EnvironmentViewModel(IEnvironmentService environment)
    {
        _environment = environment;
        // 自动检测：进入环境/安装页时无需手动点击，构造后立即检测一次（与结果页共享广播）。
        _ = CheckAsync();
    }

    public string NodeStatusText => Result is null
        ? Get("Lbl.NotDetected")
        : Result.NodeInstalled
            ? $"{Get("Lbl.Installed")} {Result.NodeVersion ?? string.Empty}".Trim()
            : Get("Lbl.NotInstalled");

    public string DshStatusText => Result is null
        ? Get("Lbl.NotDetected")
        : Result.DshAvailable
            ? $"{Get("Lbl.Available")} {Result.DshVersion ?? string.Empty}".Trim()
            : Get("Lbl.Unavailable");

    public bool NodeReady => Result?.NodeInstalled == true;
    public bool DshReady => Result?.DshAvailable == true;

    [RelayCommand]
    private async Task CheckAsync()
    {
        IsChecking = true;
        Message = null;
        try
        {
            Result = await _environment.CheckAsync();
        }
        catch (Exception ex)
        {
            Message = $"{Get("Lbl.CheckFailed")}：{ex.Message}";
        }
        finally
        {
            IsChecking = false;
        }
    }

    [RelayCommand]
    private async Task InstallNodeAsync()
    {
        if (IsInstalling)
            return; // 正在安装时忽略重复点击。
        await RunInstallAsync(
            "Node.js",
            p => _environment.InstallNodeAsync(p));
    }

    [RelayCommand]
    private async Task InstallDshAsync()
    {
        if (IsInstalling)
            return; // 正在安装时忽略重复点击。
        await RunInstallAsync(
            "DeepSeek Harness",
            p => _environment.PrefetchDshAsync(p));
    }

    /// <summary>按顺序安装缺失组件（Node.js → DeepSeek Harness），返回是否全部就绪。</summary>
    public async Task<bool> InstallMissingAsync()
    {
        if (Result is null)
            Result = await _environment.CheckAsync();

        if (!NodeReady)
        {
            var nodeOk = await RunInstallAsync("Node.js", p => _environment.InstallNodeAsync(p));
            if (!nodeOk)
                return false;
            Result = await _environment.CheckAsync();
        }

        if (!DshReady)
        {
            var dshOk = await RunInstallAsync("DeepSeek Harness", p => _environment.PrefetchDshAsync(p));
            if (!dshOk)
                return false;
            Result = await _environment.CheckAsync();
        }

        return NodeReady && DshReady;
    }

    private async Task<bool> RunInstallAsync(string title, Func<IProgress<string>, Task<bool>> install)
    {
        IsInstalling = true;
        InstallTitle = title;
        InstallLog = string.Empty;

        var progress = new Progress<string>(p => InstallLog += p + Environment.NewLine);

        bool ok;
        try
        {
            ok = await install(progress);
        }
        catch (Exception ex)
        {
            ok = false;
            InstallLog += ex.Message + Environment.NewLine;
        }

        IsInstalling = false;
        AppendLog(ok ? Get("Lbl.InstallDone") : Get("Lbl.InstallFailed"));

        // UAC 提权会把主窗口挤到后台，安装结束后重新激活，让用户看到进度/结果。
        Application.Current?.MainWindow?.Activate();

        // 安装成功后自动重新检测，状态立即更新（无需手动点击"重新检测"）。
        if (ok)
        {
            Result = await _environment.CheckAsync();
        }

        ShowCompletion(ok, title);

        // DeepSeek Harness 下载失败时，提示可手动执行命令。
        if (!ok && title.Contains("DeepSeek Harness"))
            DshDownloadFailedDialog.Show(null);

        return ok;
    }

    private void AppendLog(string line) => InstallLog += line + Environment.NewLine;

    private void ShowCompletion(bool success, string title)
    {
        var message = success
            ? string.Format(Get("Msg.InstallSuccess"), title)
            : string.Format(Get("Msg.InstallFailed"), title);
        MessageDialog.ShowInfo(null, "DeepSeek Harness Launcher", message);
    }

    private static string Get(string key)
        => Application.Current?.Resources[key] as string ?? key;
}

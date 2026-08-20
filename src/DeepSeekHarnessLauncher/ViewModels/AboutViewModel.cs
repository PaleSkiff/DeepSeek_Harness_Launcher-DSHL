namespace DeepSeekHarnessLauncher.ViewModels;

/// <summary>关于页 ViewModel：作者信息与交流群。</summary>
public sealed class AboutViewModel : ViewModelBase
{
    public string DisplayName => "关于";

    public string AppName => "DeepSeek Harness Launcher";
    public string Version => "1.0.0";

    public string BilibiliName => "小舟 Superboy";
    public string BilibiliUrl => "https://space.bilibili.com/701206818";

    public string QqGroupName => "QQ 交流群";
    public string QqGroup => "1001747300";
}

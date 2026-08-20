using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeepSeekHarnessLauncher.Models;
using DeepSeekHarnessLauncher.Services;

namespace DeepSeekHarnessLauncher.ViewModels;

public sealed record NavItem(string Key, string Title);

/// <summary>主窗口 ViewModel：左侧导航 + 内容区切换 + 全局状态镜像（底部状态栏）。</summary>
public partial class MainViewModel : ObservableObject
{
    private readonly ILocalizationService _localization;

    public ServiceControlViewModel ServiceControl { get; }
    public LogViewModel Log { get; }
    public EnvironmentViewModel Environment { get; }
    public ConfigViewModel Config { get; }
    public SettingsViewModel Settings { get; }
    public AboutViewModel About { get; }

    [ObservableProperty]
    private IReadOnlyList<NavItem> _navItems = Array.Empty<NavItem>();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentPageTitle))]
    private ViewModelBase _currentViewModel;

    /// <summary>当前选中的导航项键，用于侧边栏指示条。</summary>
    [ObservableProperty]
    private string _selectedNavKey = "service";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusStateText))]
    private ServiceState _state = ServiceState.Stopped;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusPortText))]
    [NotifyPropertyChangedFor(nameof(StatusPidText))]
    private ServiceStatus _status;

    public string CurrentPageTitle => CurrentViewModel switch
    {
        ServiceControlViewModel => _localization.Get("Nav.Service"),
        LogViewModel => _localization.Get("Nav.Log"),
        EnvironmentViewModel => _localization.Get("Nav.Environment"),
        ConfigViewModel => _localization.Get("Nav.Config"),
        SettingsViewModel => _localization.Get("Nav.Settings"),
        AboutViewModel => _localization.Get("Nav.About"),
        _ => "DeepSeek Harness Launcher",
    };

    public string StatusStateText => State switch
    {
        ServiceState.Stopped => _localization.Get("State.Stopped"),
        ServiceState.Starting => _localization.Get("State.Starting"),
        ServiceState.Running => _localization.Get("State.Running"),
        ServiceState.Stopping => _localization.Get("State.Stopping"),
        ServiceState.Faulted => _localization.Get("State.Faulted"),
        _ => _localization.Get("State.Stopped"),
    };

    public string StatusPortText => $"127.0.0.1:{Status.Port}";
    public string StatusPidText => Status.Pid.HasValue ? $"PID {Status.Pid}" : "PID -";

    public MainViewModel(
        IServiceController controller,
        ILocalizationService localization,
        ServiceControlViewModel serviceControl,
        LogViewModel log,
        EnvironmentViewModel environment,
        ConfigViewModel config,
        SettingsViewModel settings,
        AboutViewModel about)
    {
        _localization = localization;
        ServiceControl = serviceControl;
        Log = log;
        Environment = environment;
        Config = config;
        Settings = settings;
        About = about;
        _currentViewModel = ServiceControl;
        _status = controller.Current;
        _state = controller.State;

        RebuildNavItems();

        controller.StateChanged += (_, s) => OnUi(() => State = s);
        controller.StatusUpdated += (_, st) => OnUi(() => Status = st);
        localization.LanguageChanged += (_, _) => OnUi(OnLanguageChanged);
    }

    private void OnLanguageChanged()
    {
        RebuildNavItems();
        OnPropertyChanged(nameof(CurrentPageTitle));
        OnPropertyChanged(nameof(StatusStateText));
    }

    private void RebuildNavItems()
    {
        NavItems = new[]
        {
            new NavItem("service", _localization.Get("Nav.Service")),
            new NavItem("log", _localization.Get("Nav.Log")),
            new NavItem("environment", _localization.Get("Nav.Environment")),
            new NavItem("config", _localization.Get("Nav.Config")),
            new NavItem("settings", _localization.Get("Nav.Settings")),
            new NavItem("about", _localization.Get("Nav.About")),
        };
    }

    partial void OnSelectedNavKeyChanged(string value)
    {
        CurrentViewModel = value switch
        {
            "service" => ServiceControl,
            "log" => Log,
            "environment" => Environment,
            "config" => Config,
            "settings" => Settings,
            "about" => About,
            _ => CurrentViewModel,
        };
    }

    [RelayCommand]
    private void Navigate(string page)
    {
        if (page is "service" or "log" or "environment" or "config" or "settings" or "about")
            SelectedNavKey = page;
    }

    private static void OnUi(Action action)
    {
        var app = Application.Current;
        if (app is null || app.Dispatcher.CheckAccess())
        {
            action();
            return;
        }
        app.Dispatcher.BeginInvoke(action);
    }
}

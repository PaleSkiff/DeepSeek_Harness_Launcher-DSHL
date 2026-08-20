using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeepSeekHarnessLauncher.Services;

namespace DeepSeekHarnessLauncher.ViewModels;

/// <summary>⑤ 设置页 ViewModel：行为、自启与语言设置。</summary>
public partial class SettingsViewModel : ViewModelBase
{
    private readonly IConfigService _configService;
    private readonly IAutoStartService _autoStartService;
    private readonly ILocalizationService _localization;

    public string DisplayName => "设置";

    [ObservableProperty]
    private bool _autoStartOnBoot;

    [ObservableProperty]
    private bool _autoStartServiceOnLaunch;

    [ObservableProperty]
    private bool _closeToTray;

    [ObservableProperty]
    private bool _askOnFirstClose;

    [ObservableProperty]
    private bool _stopServiceOnExit;

    [ObservableProperty]
    private string _retentionDaysText = "7";

    [ObservableProperty]
    private string _language = "zh-CN";

    [ObservableProperty]
    private string? _validationError;

    public event EventHandler? SettingsSaved;

    public SettingsViewModel(
        IConfigService configService,
        IAutoStartService autoStartService,
        ILocalizationService localization)
    {
        _configService = configService;
        _autoStartService = autoStartService;
        _localization = localization;
        Load();
    }

    partial void OnLanguageChanged(string value)
    {
        // 选择语言后立即切换界面语言。
        _localization.SetLanguage(value);
    }

    [RelayCommand]
    private void Save()
    {
        if (!int.TryParse(RetentionDaysText, out var days) || days < 0)
        {
            ValidationError = GetText("Err.RetentionNonNegative");
            return;
        }
        ValidationError = null;

        var config = _configService.Load();
        config.Behavior.AutoStartServiceOnLaunch = AutoStartServiceOnLaunch;
        config.Behavior.CloseToTray = CloseToTray;
        config.Behavior.AskOnFirstClose = AskOnFirstClose;
        config.Behavior.StopServiceOnExit = StopServiceOnExit;
        config.Startup.AutoStartOnBoot = AutoStartOnBoot;
        config.Logging.RetentionDays = days;
        config.Language = Language;

        _configService.Save(config);
        _autoStartService.SetEnabled(AutoStartOnBoot);

        SettingsSaved?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Cancel() => Load();

    private void Load()
    {
        var config = _configService.Load();
        AutoStartOnBoot = config.Startup.AutoStartOnBoot;
        AutoStartServiceOnLaunch = config.Behavior.AutoStartServiceOnLaunch;
        CloseToTray = config.Behavior.CloseToTray;
        AskOnFirstClose = config.Behavior.AskOnFirstClose;
        StopServiceOnExit = config.Behavior.StopServiceOnExit;
        RetentionDaysText = config.Logging.RetentionDays.ToString();
        Language = config.Language;
    }

    private static string GetText(string key)
        => Application.Current?.Resources[key] as string ?? key;
}

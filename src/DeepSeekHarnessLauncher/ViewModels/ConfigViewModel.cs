using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeepSeekHarnessLauncher.Models;
using DeepSeekHarnessLauncher.Services;

namespace DeepSeekHarnessLauncher.ViewModels;

/// <summary>③ 配置页 ViewModel：表单/JSON 双模式编辑 + 校验 + 落盘。</summary>
public partial class ConfigViewModel : ViewModelBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly IConfigService _configService;
    private AppConfig _loadedConfig;

    public string DisplayName => "配置";

    public ConfigViewModel(IConfigService configService)
    {
        _configService = configService;
        _loadedConfig = configService.Load();
        LoadFromConfig(_loadedConfig);
    }

    /// <summary>保存成功后触发，参数为已保存配置。</summary>
    public event EventHandler<AppConfig>? ConfigSaved;

    // ---- 表单字段 ----
    [ObservableProperty]
    private string _command = string.Empty;

    [ObservableProperty]
    private string _arguments = string.Empty;

    [ObservableProperty]
    private string _workingDirectory = string.Empty;

    [ObservableProperty]
    private string _portText = "3080";

    [ObservableProperty]
    private string _startSecondsText = "60";

    [ObservableProperty]
    private string _stopSecondsText = "15";

    [ObservableProperty]
    private string _healthCheckUrl = string.Empty;

    [ObservableProperty]
    private string _healthCheckIntervalText = "5";

    [ObservableProperty]
    private string _retentionDaysText = "7";

    public ObservableCollection<EnvVarItem> EnvironmentVariables { get; } = new();

    // ---- 模式与文本 ----
    [ObservableProperty]
    private bool _isFormMode = true;

    [ObservableProperty]
    private string _jsonText = string.Empty;

    [ObservableProperty]
    private string? _jsonError;

    [ObservableProperty]
    private string? _validationError;

    // ===================== 校验 =====================

    public IReadOnlyList<string> ValidateForm()
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(Command))
            errors.Add(GetText("Err.CommandEmpty"));
        if (!int.TryParse(PortText, out var port) || port is < 1 or > 65535)
            errors.Add(GetText("Err.PortRange"));
        if (!int.TryParse(StartSecondsText, out var start) || start <= 0)
            errors.Add(GetText("Err.StartTimeoutPositive"));
        if (!int.TryParse(StopSecondsText, out var stop) || stop <= 0)
            errors.Add(GetText("Err.StopTimeoutPositive"));
        if (!int.TryParse(HealthCheckIntervalText, out var interval) || interval <= 0)
            errors.Add(GetText("Err.IntervalPositive"));
        if (!int.TryParse(RetentionDaysText, out var days) || days < 0)
            errors.Add(GetText("Err.RetentionNonNegative"));

        foreach (var item in EnvironmentVariables)
            if (string.IsNullOrWhiteSpace(item.Key))
                errors.Add(GetText("Err.EnvVarNameEmpty"));

        return errors;
    }

    public static IReadOnlyList<string> ValidateConfig(AppConfig config)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(config.Service.Command))
            errors.Add(GetText("Err.CommandEmpty"));
        if (config.Network.Port is < 1 or > 65535)
            errors.Add(GetText("Err.PortRange"));
        if (config.Timeout.StartSeconds <= 0)
            errors.Add(GetText("Err.StartTimeoutPositive"));
        if (config.Timeout.StopSeconds <= 0)
            errors.Add(GetText("Err.StopTimeoutPositive"));
        if (config.Network.HealthCheckIntervalSeconds <= 0)
            errors.Add(GetText("Err.IntervalPositive"));
        if (config.Logging.RetentionDays < 0)
            errors.Add(GetText("Err.RetentionNonNegative"));
        foreach (var kv in config.Service.EnvironmentVariables)
            if (string.IsNullOrWhiteSpace(kv.Key))
                errors.Add(GetText("Err.EnvVarNameEmpty"));
        return errors;
    }

    private static string GetText(string key)
        => Application.Current?.Resources[key] as string ?? key;

    // ===================== 命令 =====================

    [RelayCommand]
    private void Save()
    {
        JsonError = null;

        IReadOnlyList<string> errors;
        AppConfig config;

        if (IsFormMode)
        {
            errors = ValidateForm();
            if (errors.Count > 0)
            {
                ValidationError = string.Join(Environment.NewLine, errors);
                return;
            }
            config = BuildConfigFromForm();
        }
        else
        {
            if (!TryParseJson(JsonText, out config, out var jsonError))
            {
                JsonError = jsonError;
                ValidationError = jsonError;
                return;
            }
            errors = ValidateConfig(config);
            if (errors.Count > 0)
            {
                ValidationError = string.Join(Environment.NewLine, errors);
                return;
            }
        }

        ValidationError = null;
        _configService.Save(config);
        _loadedConfig = config;
        ConfigSaved?.Invoke(this, config);
    }

    [RelayCommand]
    private void Cancel()
    {
        JsonError = null;
        ValidationError = null;
        _loadedConfig = _configService.Load();
        LoadFromConfig(_loadedConfig);
        IsFormMode = true;
    }

    [RelayCommand]
    private void ResetToDefaults()
    {
        JsonError = null;
        ValidationError = null;
        LoadFromConfig(_configService.Defaults);
    }

    [RelayCommand]
    private void SwitchToTextMode()
    {
        JsonError = null;
        ValidationError = null;
        JsonText = JsonSerializer.Serialize(BuildConfigFromForm(), JsonOptions);
        IsFormMode = false;
    }

    [RelayCommand]
    private void SwitchToFormMode()
    {
        if (!TryParseJson(JsonText, out var config, out var error))
        {
            JsonError = error;
            return;
        }
        JsonError = null;
        ValidationError = null;
        LoadFromConfig(config);
        IsFormMode = true;
    }

    [RelayCommand]
    private void AddEnvVar() => EnvironmentVariables.Add(new EnvVarItem());

    [RelayCommand]
    private void RemoveEnvVar(EnvVarItem item)
    {
        if (item is not null)
            EnvironmentVariables.Remove(item);
    }

    [RelayCommand]
    private void ToggleValueVisibility(EnvVarItem item)
    {
        if (item is not null)
            item.IsValueVisible = !item.IsValueVisible;
    }

    // ===================== 内部 =====================

    private AppConfig BuildConfigFromForm()
    {
        return new AppConfig
        {
            Service = new ServiceConfig
            {
                Command = Command.Trim(),
                Arguments = Arguments,
                WorkingDirectory = WorkingDirectory.Trim(),
                EnvironmentVariables = EnvironmentVariables
                    .Where(e => !string.IsNullOrWhiteSpace(e.Key))
                    .GroupBy(e => e.Key.Trim())
                    .ToDictionary(g => g.Key, g => g.Last().Value),
            },
            Network = new NetworkConfig
            {
                Port = int.Parse(PortText),
                HealthCheckUrl = HealthCheckUrl.Trim(),
                HealthCheckIntervalSeconds = int.Parse(HealthCheckIntervalText),
            },
            Timeout = new TimeoutConfig
            {
                StartSeconds = int.Parse(StartSecondsText),
                StopSeconds = int.Parse(StopSecondsText),
            },
            Behavior = _loadedConfig.Behavior,
            Startup = _loadedConfig.Startup,
            Logging = new LoggingConfig
            {
                RetentionDays = int.Parse(RetentionDaysText),
            },
        };
    }

    private void LoadFromConfig(AppConfig config)
    {
        _loadedConfig = config;
        Command = config.Service.Command;
        Arguments = config.Service.Arguments;
        WorkingDirectory = config.Service.WorkingDirectory;
        PortText = config.Network.Port.ToString();
        StartSecondsText = config.Timeout.StartSeconds.ToString();
        StopSecondsText = config.Timeout.StopSeconds.ToString();
        HealthCheckUrl = config.Network.HealthCheckUrl;
        HealthCheckIntervalText = config.Network.HealthCheckIntervalSeconds.ToString();
        RetentionDaysText = config.Logging.RetentionDays.ToString();

        EnvironmentVariables.Clear();
        foreach (var kv in config.Service.EnvironmentVariables)
            EnvironmentVariables.Add(new EnvVarItem { Key = kv.Key, Value = kv.Value });
    }

    private static bool TryParseJson(string json, out AppConfig config, out string error)
    {
        config = null!;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(json))
        {
            error = GetText("Err.ConfigEmpty");
            return false;
        }

        try
        {
            config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions)
                     ?? throw new JsonException(GetText("Err.ConfigEmpty"));
            return true;
        }
        catch (JsonException ex)
        {
            error = string.Format(GetText("Err.JsonSyntaxFmt"), ex.Message);
            return false;
        }
    }
}

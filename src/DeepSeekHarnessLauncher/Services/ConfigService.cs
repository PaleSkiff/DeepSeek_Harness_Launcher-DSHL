using System.IO;
using System.Text.Json;
using DeepSeekHarnessLauncher.Models;

namespace DeepSeekHarnessLauncher.Services;

public interface IConfigService
{
    AppConfig Load();
    void Save(AppConfig config);
    AppConfig Defaults { get; }
    string ConfigPath { get; }
}

/// <summary>
/// 读写程序目录下 config.json（便携模式）。缺失用默认值，损坏时备份为 .bad 并用默认值继续。
/// </summary>
public sealed class ConfigService : IConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _configPath;

    public ConfigService(string baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(baseDirectory))
            throw new ArgumentException("基础目录不能为空", nameof(baseDirectory));
        _configPath = Path.Combine(baseDirectory, "config.json");
    }

    public string ConfigPath => _configPath;

    public AppConfig Defaults => new();

    public AppConfig Load()
    {
        if (!File.Exists(_configPath))
            return Defaults;

        string json;
        try
        {
            json = File.ReadAllText(_configPath);
        }
        catch (IOException)
        {
            return Defaults;
        }

        try
        {
            var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
            if (config is null)
                throw new JsonException("配置内容为空。");
            Normalize(config);
            return config;
        }
        catch (Exception)
        {
            TryBackupCorruptFile();
            return Defaults;
        }
    }

    public void Save(AppConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        Normalize(config);

        string json = JsonSerializer.Serialize(config, JsonOptions);
        var directory = Path.GetDirectoryName(_configPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        File.WriteAllText(_configPath, json);
    }

    /// <summary>补齐 null 集合等，保证对象可用；不覆盖空字符串语义（如健康检查地址）。</summary>
    private static void Normalize(AppConfig config)
    {
        config.Service ??= new ServiceConfig();
        config.Service.EnvironmentVariables ??= new Dictionary<string, string>();
        config.Network ??= new NetworkConfig();
        config.Timeout ??= new TimeoutConfig();
        config.Behavior ??= new BehaviorConfig();
        config.Startup ??= new StartupConfig();
        config.Logging ??= new LoggingConfig();
    }

    private void TryBackupCorruptFile()
    {
        try
        {
            var badPath = _configPath + ".bad";
            if (File.Exists(badPath))
                File.Delete(badPath);
            File.Move(_configPath, badPath);
        }
        catch
        {
            // 备份失败不应影响启动。
        }
    }
}

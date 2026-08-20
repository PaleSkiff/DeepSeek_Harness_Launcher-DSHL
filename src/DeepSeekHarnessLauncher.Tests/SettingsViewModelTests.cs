using DeepSeekHarnessLauncher.Models;
using DeepSeekHarnessLauncher.Services;
using DeepSeekHarnessLauncher.ViewModels;

namespace DeepSeekHarnessLauncher.Tests;

public sealed class SettingsViewModelTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ConfigService _configService;
    private readonly FakeAutoStartService _autoStart;
    private readonly FakeLocalizationService _localization;

    public SettingsViewModelTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "dshl-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _configService = new ConfigService(_tempDir);
        _autoStart = new FakeAutoStartService();
        _localization = new FakeLocalizationService();
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* ignore */ }
    }

    [Fact]
    public void Load_ReadsDefaults()
    {
        var vm = new SettingsViewModel(_configService, _autoStart, _localization);

        Assert.True(vm.CloseToTray);
        Assert.Equal("7", vm.RetentionDaysText);
        Assert.False(vm.AutoStartOnBoot);
    }

    [Fact]
    public void Save_Valid_WritesConfig_AndSyncsAutoStart()
    {
        var vm = new SettingsViewModel(_configService, _autoStart, _localization);
        vm.CloseToTray = false;
        vm.RetentionDaysText = "3";
        vm.AutoStartOnBoot = true;
        vm.AutoStartServiceOnLaunch = true;

        vm.SaveCommand.Execute(null);

        var saved = _configService.Load();
        Assert.False(saved.Behavior.CloseToTray);
        Assert.Equal(3, saved.Logging.RetentionDays);
        Assert.True(saved.Startup.AutoStartOnBoot);
        Assert.True(saved.Behavior.AutoStartServiceOnLaunch);
        Assert.True(_autoStart.IsEnabled);
    }

    [Fact]
    public void Save_InvalidRetentionDays_ShowsError_DoesNotWrite()
    {
        _configService.Save(new AppConfig { Logging = new LoggingConfig { RetentionDays = 5 } });
        var vm = new SettingsViewModel(_configService, _autoStart, _localization);
        vm.RetentionDaysText = "-1";

        vm.SaveCommand.Execute(null);

        Assert.NotNull(vm.ValidationError);
        Assert.Equal(5, _configService.Load().Logging.RetentionDays);
    }

    [Fact]
    public void Cancel_RestoresValuesFromDisk()
    {
        _configService.Save(new AppConfig { Logging = new LoggingConfig { RetentionDays = 9 } });
        var vm = new SettingsViewModel(_configService, _autoStart, _localization);
        vm.RetentionDaysText = "1";

        vm.CancelCommand.Execute(null);

        Assert.Equal("9", vm.RetentionDaysText);
    }

    [Fact]
    public void SettingsViewModel_HasNoBackgroundFeature()
    {
        // 背景模式/背景图功能已移除：SettingsViewModel 不应再包含背景相关成员。
        var path = FindSourceFile("ViewModels", "SettingsViewModel.cs");
        var content = File.ReadAllText(path);

        Assert.DoesNotContain("BackgroundMode", content);
        Assert.DoesNotContain("BackgroundEnabled", content);
        Assert.DoesNotContain("BackgroundImagePath", content);
        Assert.DoesNotContain("ChooseBackgroundImage", content);
    }

    [Fact]
    public void Load_Language_DefaultsToChinese()
    {
        var vm = new SettingsViewModel(_configService, _autoStart, _localization);

        Assert.Equal("zh-CN", vm.Language);
    }

    [Fact]
    public void Save_Language_WritesToDisk()
    {
        var vm = new SettingsViewModel(_configService, _autoStart, _localization);
        vm.Language = "en-US";

        vm.SaveCommand.Execute(null);

        Assert.Equal("en-US", _configService.Load().Language);
    }

    [Fact]
    public void ChangeLanguage_CallsLocalizationService()
    {
        var vm = new SettingsViewModel(_configService, _autoStart, _localization);

        vm.Language = "en-US";

        Assert.Equal("en-US", _localization.CurrentLanguage);
    }

    private static string FindSourceFile(params string[] relativeParts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var parts = new[] { dir.FullName, "src", "DeepSeekHarnessLauncher" }
                .Concat(relativeParts).ToArray();
            var candidate = Path.Combine(parts);
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }
        return string.Join("\\", relativeParts);
    }
}

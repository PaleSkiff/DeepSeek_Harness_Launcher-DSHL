using DeepSeekHarnessLauncher.Models;
using DeepSeekHarnessLauncher.Services;
using DeepSeekHarnessLauncher.ViewModels;

namespace DeepSeekHarnessLauncher.Tests;

public sealed class ConfigViewModelTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ConfigService _service;

    public ConfigViewModelTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "dshl-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _service = new ConfigService(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* ignore */ }
    }

    private ConfigViewModel CreateVm() => new(_service);

    [Fact]
    public void Ctor_LoadsValuesFromDisk()
    {
        _service.Save(new AppConfig { Network = new NetworkConfig { Port = 7777 } });

        var vm = CreateVm();

        Assert.Equal("7777", vm.PortText);
        Assert.Equal("npx", vm.Command);
    }

    [Fact]
    public void ValidateForm_EmptyCommand_ReturnsError()
    {
        var vm = CreateVm();
        vm.Command = "  ";

        var errors = vm.ValidateForm();

        Assert.Contains(errors, e => e.Contains("Err.CommandEmpty"));
    }

    [Fact]
    public void ValidateForm_InvalidPort_ReturnsError()
    {
        var vm = CreateVm();
        vm.PortText = "70000";

        var errors = vm.ValidateForm();

        Assert.Contains(errors, e => e.Contains("Err.PortRange"));
    }

    [Fact]
    public void ValidateForm_NonNumericPort_ReturnsError()
    {
        var vm = CreateVm();
        vm.PortText = "abc";

        var errors = vm.ValidateForm();

        Assert.Contains(errors, e => e.Contains("Err.PortRange"));
    }

    [Fact]
    public void ValidateForm_NonPositiveTimeout_ReturnsError()
    {
        var vm = CreateVm();
        vm.StartSecondsText = "0";

        var errors = vm.ValidateForm();

        Assert.Contains(errors, e => e.Contains("Err.StartTimeoutPositive"));
    }

    [Fact]
    public void ValidateForm_Valid_ReturnsNoErrors()
    {
        var vm = CreateVm();

        var errors = vm.ValidateForm();

        Assert.Empty(errors);
    }

    [Fact]
    public void Save_Valid_WritesConfig_AndFiresEvent()
    {
        var vm = CreateVm();
        vm.Command = "node";
        vm.Arguments = "server.js";
        vm.PortText = "9090";
        AppConfig? captured = null;
        vm.ConfigSaved += (_, c) => captured = c;

        vm.SaveCommand.Execute(null);

        Assert.Null(vm.ValidationError);
        var saved = _service.Load();
        Assert.Equal("node", saved.Service.Command);
        Assert.Equal("server.js", saved.Service.Arguments);
        Assert.Equal(9090, saved.Network.Port);
        Assert.NotNull(captured);
        Assert.Equal(9090, captured!.Network.Port);
    }

    [Fact]
    public void Save_Invalid_DoesNotWrite()
    {
        var vm = CreateVm();
        vm.PortText = "99999";

        vm.SaveCommand.Execute(null);

        Assert.NotNull(vm.ValidationError);
        Assert.False(File.Exists(_service.ConfigPath));
    }

    [Fact]
    public void Cancel_RestoresValuesFromDisk()
    {
        _service.Save(new AppConfig { Network = new NetworkConfig { Port = 5555 } });
        var vm = CreateVm();
        vm.PortText = "1234";

        vm.CancelCommand.Execute(null);

        Assert.Equal("5555", vm.PortText);
        Assert.True(vm.IsFormMode);
    }

    [Fact]
    public void ResetToDefaults_SetsDefaultValues()
    {
        _service.Save(new AppConfig { Network = new NetworkConfig { Port = 5555 } });
        var vm = CreateVm();
        vm.PortText = "1234";

        vm.ResetToDefaultsCommand.Execute(null);

        Assert.Equal("3080", vm.PortText);
        Assert.Equal("npx", vm.Command);
    }

    [Fact]
    public void SwitchToTextMode_PopulatesJson()
    {
        var vm = CreateVm();
        vm.PortText = "8080";

        vm.SwitchToTextModeCommand.Execute(null);

        Assert.False(vm.IsFormMode);
        Assert.Contains("\"port\": 8080", vm.JsonText);
    }

    [Fact]
    public void SwitchToFormMode_ValidJson_LoadsValues()
    {
        var vm = CreateVm();
        vm.SwitchToTextModeCommand.Execute(null);
        vm.JsonText = """{ "network": { "port": 6666 }, "service": { "command": "node" } }""";

        vm.SwitchToFormModeCommand.Execute(null);

        Assert.True(vm.IsFormMode);
        Assert.Equal("6666", vm.PortText);
        Assert.Equal("node", vm.Command);
    }

    [Fact]
    public void SwitchToFormMode_InvalidJson_ShowsError_StaysInTextMode()
    {
        var vm = CreateVm();
        vm.SwitchToTextModeCommand.Execute(null);
        vm.JsonText = "{ broken";

        vm.SwitchToFormModeCommand.Execute(null);

        Assert.False(vm.IsFormMode);
        Assert.NotNull(vm.JsonError);
    }

    [Fact]
    public void Save_InTextMode_WithInvalidJson_ShowsError()
    {
        var vm = CreateVm();
        vm.SwitchToTextModeCommand.Execute(null);
        vm.JsonText = "{ broken";

        vm.SaveCommand.Execute(null);

        Assert.NotNull(vm.JsonError);
        Assert.False(File.Exists(_service.ConfigPath));
    }

    [Fact]
    public void AddAndRemoveEnvVar_Works()
    {
        var vm = CreateVm();
        int initial = vm.EnvironmentVariables.Count;

        vm.AddEnvVarCommand.Execute(null);
        Assert.Equal(initial + 1, vm.EnvironmentVariables.Count);

        var item = vm.EnvironmentVariables.Last();
        vm.RemoveEnvVarCommand.Execute(item);
        Assert.Equal(initial, vm.EnvironmentVariables.Count);
    }

    [Fact]
    public void ToggleValueVisibility_FlipsFlag()
    {
        var vm = CreateVm();
        vm.AddEnvVarCommand.Execute(null);
        var item = vm.EnvironmentVariables.Last();
        Assert.False(item.IsValueVisible);

        vm.ToggleValueVisibilityCommand.Execute(item);

        Assert.True(item.IsValueVisible);
    }

    [Fact]
    public void ValidateConfig_Static_ChecksPortRange()
    {
        var config = new AppConfig { Network = new NetworkConfig { Port = 0 } };

        var errors = ConfigViewModel.ValidateConfig(config);

        Assert.Contains(errors, e => e.Contains("Err.PortRange"));
    }
}

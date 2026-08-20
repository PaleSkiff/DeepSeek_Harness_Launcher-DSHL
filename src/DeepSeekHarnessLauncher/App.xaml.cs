using System.Threading;
using System.Windows;
using System.Windows.Threading;
using DeepSeekHarnessLauncher.Models;
using DeepSeekHarnessLauncher.Services;
using DeepSeekHarnessLauncher.ViewModels;
using DeepSeekHarnessLauncher.Views;
using Microsoft.Extensions.DependencyInjection;

namespace DeepSeekHarnessLauncher;

public partial class App : Application
{
    private const string MutexName = "Global\\DeepSeekHarnessLauncher";
    private ServiceProvider? _serviceProvider;
    private Mutex? _mutex;

    public static ServiceProvider Services => ((App)Current)._serviceProvider!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _mutex = new Mutex(true, MutexName, out var createdNew);
        if (!createdNew)
        {
            MessageDialog.ShowInfo(null, "DeepSeek Harness Launcher",
                Application.Current?.Resources["Msg.AlreadyRunning"] as string ?? "DeepSeek Harness Launcher 已在运行。");
            Shutdown();
            return;
        }

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        // 应用保存的语言设置（默认中文）。
        var config = _serviceProvider.GetRequiredService<IConfigService>().Load();
        _serviceProvider.GetRequiredService<ILocalizationService>().Initialize(config.Language);

        WireServiceOutputToLog();
        CleanupOldLogs();

        var window = _serviceProvider.GetRequiredService<MainWindow>();
        MainWindow = window;
        window.Show();
    }

    private void WireServiceOutputToLog()
    {
        var controller = _serviceProvider!.GetRequiredService<IServiceController>();
        var log = _serviceProvider!.GetRequiredService<ILogService>();
        controller.OutputReceived += (_, line) => log.Append(line, LogService.InferLevel(line));
    }

    private void CleanupOldLogs()
    {
        try
        {
            var log = _serviceProvider!.GetRequiredService<ILogService>();
            var config = _serviceProvider!.GetRequiredService<IConfigService>().Load();
            log.CleanupOldLogs(config.Logging.RetentionDays);
        }
        catch
        {
            // 清理失败不影响启动。
        }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        var baseDirectory = AppContext.BaseDirectory;
        services.AddSingleton<IConfigService>(new ConfigService(baseDirectory));

        services.AddSingleton<IProcessService, ProcessService>();
        services.AddSingleton<IHealthChecker, HealthChecker>();
        services.AddSingleton<IServiceController, ServiceController>();
        services.AddSingleton<IEnvironmentService, EnvironmentService>();
        services.AddSingleton<ILogService>(new LogService(baseDirectory));
        services.AddSingleton<IAutoStartService, AutoStartService>();
        services.AddSingleton<ITrayService, TrayService>();
        services.AddSingleton<IWebBrowserService, WebBrowserService>();
        services.AddSingleton<ILocalizationService, LocalizationService>();

        services.AddSingleton<ServiceControlViewModel>();
        services.AddSingleton<LogViewModel>();
        services.AddSingleton<EnvironmentViewModel>();
        services.AddSingleton<ConfigViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<AboutViewModel>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            var controller = _serviceProvider?.GetService<IServiceController>();
            if (controller?.State is ServiceState.Running or ServiceState.Starting or ServiceState.Stopping)
                controller.StopAsync().GetAwaiter().GetResult();
        }
        catch
        {
            // 退出时尽力停止，失败不影响退出。
        }

        _serviceProvider?.Dispose();
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        base.OnExit(e);
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        var prefix = Application.Current?.Resources["Msg.UnexpectedError"] as string ?? "发生未处理异常：";
        MessageDialog.ShowInfo(null, "DeepSeek Harness Launcher", $"{prefix}\n{e.Exception.Message}");
        e.Handled = true;
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        // 进程级未处理异常：仅记录，避免二次崩溃。
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        e.SetObserved();
    }
}

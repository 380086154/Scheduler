using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Scheduler.App.Localization;
using Scheduler.App.Logging;
using Scheduler.App.Settings;
using Scheduler.App.Ui;

namespace Scheduler.App;

public partial class App : System.Windows.Application
{
    private AppInstance? _instance;
    private IHost? _host;
    private TrayController? _tray;
    private SettingsWindow? _settings;
    private ILocalizationService? _localization;

    protected override async void OnStartup(StartupEventArgs e)
    {
        // 设置窗口是普通表单，用 CPU 绘制即可。避免按本机显卡加载厂商驱动（Intel / AMD / NVIDIA 都一样）。
        System.Windows.Media.RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;

        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        base.OnStartup(e);

        System.Windows.Forms.Application.SetHighDpiMode(System.Windows.Forms.HighDpiMode.PerMonitorV2);

        _instance = new AppInstance();
        if (!_instance.TryEnter())
        {
            Shutdown();
            return;
        }

        _instance.ShowSettingsRequested += () => Dispatch(ShowSettings);
        _localization = new LocalizationService();

        try
        {
            _host = AppHost.Build(e.Args);

            // 先挂托盘，再 await Host。否则 async OnStartup 让出线程时既没有窗口也没有托盘。
            _tray = new TrayController(_host.Services, _localization, () => Dispatch(ShowSettings), () => Dispatch(RequestExit));
            _tray.Show();

            await _host.StartAsync();

            var logger = AppHost.CreateStartupLogger(_host);
            logger.LogInformation(
                "进程启动，PID={ProcessId}，日志目录={LogDir}，用户配置={Settings}",
                Environment.ProcessId,
                FileLoggingExtensions.DefaultLogDirectory,
                UserSettingsStore.FilePath);

            if (UserSettingsStore.WasSeededThisRun || e.Args.Contains("--settings", StringComparer.OrdinalIgnoreCase))
                ShowSettings();
            else
                _tray.ShowBalloon(
                    Text(LocalizationKeys.BalloonTitle),
                    Text(LocalizationKeys.BalloonText));
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                Format(LocalizationKeys.StartupFailed, ex.Message),
                Text(LocalizationKeys.AppName),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _settings = null;

        if (_host is not null)
        {
            try
            {
                _host.StopAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
                _host.Dispose();
            }
            catch
            {
                // ignore shutdown errors
            }
        }

        _instance?.Dispose();
        base.OnExit(e);
    }

    internal void ShowSettings()
    {
        if (_host is null) return;

        try
        {
            if (_settings is not null)
            {
                if (_settings.WindowState == WindowState.Minimized)
                    _settings.WindowState = WindowState.Normal;
                _settings.Show();
                _settings.Activate();
                return;
            }

            _settings = new SettingsWindow(_host.Services, _localization!);
            _settings.Closing += (_, args) =>
            {
                args.Cancel = true;
                _settings.Hide();
                if (ReferenceEquals(MainWindow, _settings))
                    MainWindow = null;
            };
            _settings.Show();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                Format(LocalizationKeys.OpenSettingsFailed, ex.Message),
                Text(LocalizationKeys.AppName),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void RequestExit()
    {
        var result = System.Windows.MessageBox.Show(
            Text(LocalizationKeys.ExitConfirm),
            Text(LocalizationKeys.AppName),
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
            Shutdown();
    }

    private static void Dispatch(Action action)
    {
        var app = Current;
        if (app is null) return;
        if (app.Dispatcher.CheckAccess())
            action();
        else
            app.Dispatcher.Invoke(action, DispatcherPriority.Normal);
    }

    private string Text(string key) => _localization?.GetString(key) ?? key;

    private string Format(string key, params object[] args) => _localization?.Format(key, args) ?? key;

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        var app = Current as App;
        System.Windows.MessageBox.Show(
            app?.Format(LocalizationKeys.UnhandledError, e.Exception.Message) ?? e.Exception.Message,
            app?.Text(LocalizationKeys.AppName) ?? "Scheduler",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
        e.Handled = true;
    }
}

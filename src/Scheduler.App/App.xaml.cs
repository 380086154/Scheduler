using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Scheduler.App.Localization;
using Scheduler.App.Ui;
using Scheduler.Common;
using Scheduler.Core.Logging;
using Scheduler.Core.Settings;

namespace Scheduler.App;

public partial class App : System.Windows.Application
{
    private AppInstance? _instance;
    private NotificationDuty? _notificationDuty;
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

        _instance = new AppInstance();
        if (!_instance.TryEnter())
        {
            Shutdown();
            return;
        }

        var runNotifications = NotificationDuty.TryAcquire(out _notificationDuty);
        _instance.ShowSettingsRequested += () => Dispatch(ShowSettings);
        _localization = new LocalizationService();

        try
        {
            _host = AppHost.Build(e.Args, runNotifications);

            // 服务已在发通知时，这个进程只改配置，不再挂托盘。
            if (runNotifications)
            {
                _tray = new TrayController(_host.Services, _localization, () => Dispatch(ShowSettings), () => Dispatch(RequestExit));
                _tray.Show();
            }

            // 在线程池上启动，避免后台循环把延续绑回 WPF 界面线程。
            await Task.Run(() => _host.StartAsync());

            var logger = AppHost.CreateStartupLogger(_host);
            logger.LogInformation($"进程启动，PID={Environment.ProcessId}，日志目录={FileLoggingExtensions.DefaultLogDirectory}，用户配置={UserSettingsStore.FilePath}");

            if (!runNotifications || UserSettingsStore.WasSeededThisRun || e.Args.Contains("--settings", StringComparer.OrdinalIgnoreCase))
                ShowSettings();
            else
                _tray!.ShowBalloon(
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
        try
        {
            _tray?.Dispose();
            _settings = null;
            StopHost();
        }
        finally
        {
            _instance?.Dispose();
            _notificationDuty?.Dispose();
            _instance = null;
            _notificationDuty = null;
        }

        base.OnExit(e);
    }

    /// <summary>
    /// 退出由界面线程发起。若在这条线程上直接等待 Host 停止，后台任务的延续排不回来，进程会停住，锁文件也一直被占着。
    /// </summary>
    private void StopHost()
    {
        var host = _host;
        _host = null;
        if (host is null) return;

        try
        {
            Task.Run(async () =>
            {
                await host.StopAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            }).GetAwaiter().GetResult();
        }
        catch
        {
            // 忽略关闭时的错误
        }

        try
        {
            host.Dispose();
        }
        catch
        {
            // 忽略关闭时的错误
        }
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
            _settings.Closed += (_, _) =>
            {
                if (ReferenceEquals(MainWindow, _settings))
                    MainWindow = null;
                _settings = null;
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

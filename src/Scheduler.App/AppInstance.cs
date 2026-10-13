using Scheduler.Common;

namespace Scheduler.App;

/// <summary>单实例：重复启动时通知已运行进程打开设置。调试构建使用另一把锁，避免和正在运行的正式程序互相挤掉。</summary>
internal sealed class AppInstance : IDisposable
{
    private static readonly string MutexName = DebugHelper.IsDebug
        ? @"Local\Scheduler.App.Debug"
        : @"Local\Scheduler.App";

    private static readonly string ShowEventName = DebugHelper.IsDebug
        ? @"Local\Scheduler.ShowSettings.Debug"
        : @"Local\Scheduler.ShowSettings";

    private Mutex? _mutex;
    private EventWaitHandle? _showSettings;
    private Thread? _waitThread;
    private bool _ownsMutex;

    public event Action? ShowSettingsRequested;

    public bool TryEnter()
    {
        _mutex = new Mutex(initiallyOwned: true, MutexName, out _ownsMutex);
        if (!_ownsMutex)
        {
            try
            {
                if (EventWaitHandle.TryOpenExisting(ShowEventName, out var existing))
                {
                    existing.Set();
                    existing.Dispose();
                }
            }
            catch
            {
                // 忽略
            }

            _mutex.Dispose();
            _mutex = null;
            return false;
        }

        _showSettings = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        _waitThread = new Thread(WaitForShowSettings)
        {
            IsBackground = true,
            Name = "Scheduler.ShowSettings"
        };
        _waitThread.Start();
        return true;
    }

    private void WaitForShowSettings()
    {
        while (_showSettings is not null)
        {
            try
            {
                if (!_showSettings.WaitOne(Timeout.Infinite))
                    continue;
                ShowSettingsRequested?.Invoke();
            }
            catch (ObjectDisposedException)
            {
                break;
            }
        }
    }

    public void Dispose()
    {
        var waiter = _showSettings;
        _showSettings = null;
        waiter?.Set();
        waiter?.Dispose();

        if (_ownsMutex)
            _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        _mutex = null;
    }
}

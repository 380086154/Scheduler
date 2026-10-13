namespace Scheduler.App;

/// <summary>单实例：重复启动时通知已运行进程打开设置。</summary>
internal sealed class AppInstance : IDisposable
{
    private const string MutexName = @"Local\Scheduler.App";
    private const string ShowEventName = @"Local\Scheduler.ShowSettings";

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
                // ignore
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

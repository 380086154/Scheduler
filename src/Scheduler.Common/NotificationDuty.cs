namespace Scheduler.Common;

/// <summary>
/// Machine-wide ownership of scheduled notifications.
/// The tray app and the Windows service both try to take it; only one process sends.
/// The lock is an exclusive file handle, same idea as <see cref="SingleInstanceHelper"/>.
/// The OS drops the handle when the process exits, including after a crash.
/// </summary>
public sealed class NotificationDuty : IDisposable
{
    /// <summary>
    /// Shared by the interactive user and LocalSystem. Per-user temp would not be visible to both.
    /// </summary>
    public static readonly string LockFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments),
        "Scheduler",
        $"notifications{(DebugHelper.IsDebug ? ".debug" : string.Empty)}.lock");

    private readonly FileStream _lockStream;
    private bool _disposed;

    private NotificationDuty(FileStream lockStream) => _lockStream = lockStream;

    public static bool TryAcquire(out NotificationDuty? duty)
    {
        duty = null;
        try
        {
            var directory = Path.GetDirectoryName(LockFilePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            // FileShare.None: a second open throws IOException while this handle is alive.
            var stream = File.Open(LockFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            duty = new NotificationDuty(stream);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _lockStream.Dispose();
        try { File.Delete(LockFilePath); }
        catch
        {
            // The other process may already hold the file.
        }
    }
}

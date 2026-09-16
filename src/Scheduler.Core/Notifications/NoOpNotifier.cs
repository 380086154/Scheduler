namespace Scheduler.Core.Notifications;

/// <summary>Webhook 未配置时的空操作实现，保证进程仍可启动。</summary>
internal sealed class NoOpNotifier : INotifier
{
    public Task NotifyAsync(string title, string message, CancellationToken token = default)
        => Task.CompletedTask;
}

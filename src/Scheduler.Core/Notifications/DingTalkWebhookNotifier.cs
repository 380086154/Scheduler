namespace Scheduler.Core.Notifications;

/// <summary>钉钉群机器人占位：配置位已留，发送逻辑尚未实现。</summary>
public sealed class DingTalkWebhookNotifier : INotifier
{
    public Task NotifyAsync(string title, string message, CancellationToken token = default)
        => throw new NotSupportedException(
            "钉钉通知尚未实现。请将 Notifications:Provider 设为 Teams 或 WeCom。");
}
namespace Scheduler.Core.Notifications;

/// <summary>通知渠道（同一时间只用一个）。</summary>
public enum NotificationProvider
{
    Teams = 0,
    WeCom = 1,
    DingTalk = 2
}

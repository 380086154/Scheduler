namespace Scheduler.Core.Notifications;

public enum NotificationSchedule
{
    /// <summary>每个工作日</summary>
    DailyWorkday = 0,

    /// <summary>本周最后一个工作日</summary>
    LastWorkdayOfWeek = 1,

    /// <summary>本月最后一个工作日</summary>
    LastWorkdayOfMonth = 2
}

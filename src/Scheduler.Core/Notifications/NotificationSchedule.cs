namespace Scheduler.Core.Notifications;

public enum NotificationRuleKind
{
    /// <summary>到点提醒。</summary>
    Scheduled = 0,

    /// <summary>进程连续未运行达到指定分钟后提醒。</summary>
    ProcessAbsent = 1
}

public enum NotificationSchedule
{
    /// <summary>每个工作日</summary>
    DailyWorkday = 0,

    /// <summary>本周最后一个工作日</summary>
    LastWorkdayOfWeek = 1,

    /// <summary>本月最后一个工作日</summary>
    LastWorkdayOfMonth = 2
}

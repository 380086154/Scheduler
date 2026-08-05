namespace Acclaro.Platform.Application.Notifications;

public sealed class NotificationOptions
{
    public const string SectionName = "Notifications";

    public List<NotificationRuleOptions> Rules { get; set; } = [];
}

public sealed class NotificationRuleOptions
{
    public string Id { get; set; } = "";

    /// <summary>任务启用状态，true 启用</summary>
    public bool Enabled { get; set; } = true;

    public string Title { get; set; } = "";

    public string Message { get; set; } = "";

    /// <summary>计划提醒时刻，格式 HH:mm</summary>
    public string Time { get; set; } = "00:00";

    public NotificationSchedule Schedule { get; set; } = NotificationSchedule.DailyWorkday;

    /// <summary>今日在公司累计解锁分钟数要求；0 表示不要求</summary>
    public int RequireAtOfficeMinutes { get; set; }

    /// <summary>相对 Time 的偏差窗口（分钟），用于重启/暂停追发</summary>
    public int SkewMinutes { get; set; } = 10;
}

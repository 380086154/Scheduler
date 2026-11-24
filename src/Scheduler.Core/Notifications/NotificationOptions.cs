namespace Scheduler.Core.Notifications;

public sealed class NotificationOptions
{
    public const string SectionName = "Notifications";

    /// <summary>通知渠道：Teams / WeCom / DingTalk（三选一）。</summary>
    public NotificationProvider Provider { get; set; } = NotificationProvider.Teams;

    public string? TeamsWebhookUrl { get; set; }

    /// <summary>企业微信群机器人完整 Webhook，含 key。</summary>
    public string? WeComWebhookUrl { get; set; }

    /// <summary>钉钉自定义机器人完整 Webhook，含 access_token。加签时再带 secret。</summary>
    public string? DingTalkWebhookUrl { get; set; }

    public List<NotificationRuleOptions> Rules { get; set; } = [];
}

public sealed class NotificationRuleOptions
{
    public string Id { get; set; } = "";

    /// <summary>任务启用状态，true 启用</summary>
    public bool Enabled { get; set; } = true;

    public NotificationRuleKind Kind { get; set; } = NotificationRuleKind.Scheduled;

    public string Title { get; set; } = "";

    public string Message { get; set; } = "";

    /// <summary>计划提醒时刻，格式 HH:mm</summary>
    public string Time { get; set; } = "00:00";

    public NotificationSchedule Schedule { get; set; } = NotificationSchedule.DailyWorkday;

    /// <summary>进程未运行规则要检查的进程名，不带 .exe。</summary>
    public List<string> ProcessNames { get; set; } = [];

    /// <summary>进程连续未运行达到该分钟数后提醒，并按同样间隔重复。</summary>
    public int AbsentMinutes { get; set; } = 30;

    /// <summary>今日在公司累计解锁分钟数要求；0 表示不要求</summary>
    public int RequireAtOfficeMinutes { get; set; }

    /// <summary>相对 Time 的偏差窗口（分钟），用于重启/暂停追发</summary>
    public int SkewMinutes { get; set; } = 10;
}

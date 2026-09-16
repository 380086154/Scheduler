namespace Scheduler.Core.InputActivity;

public sealed class InputActivityOptions
{
    public const string SectionName = "InputActivity";

    /// <summary>是否启用键鼠统计钩子。</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>今日累计落盘间隔（分钟）。</summary>
    public int PersistIntervalMinutes { get; set; } = 10;

    /// <summary>约定规则 Id：命中后发送今日输入统计。</summary>
    public const string DailyStatsRuleId = "daily-input-stats";
}

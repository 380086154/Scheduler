namespace Acclaro.Platform.Application.InputActivity;

public sealed class InputActivityOptions
{
    public const string SectionName = "InputActivity";

    /// <summary>是否启用键鼠统计钩子。</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>是否记录按键文本（默认关闭）。</summary>
    public bool RecordKeyValues { get; set; }

    /// <summary>
    /// 文本行显示宽度（约等于像素/10：80 ≈ 800px）。
    /// ASCII 计 1，宽字符计 2。
    /// </summary>
    public int LineWidthUnits { get; set; } = 80;

    /// <summary>按键文本按小时文件保留个数。</summary>
    public int HourlyLogRetainFiles { get; set; } = 24;

    /// <summary>今日累计落盘间隔（分钟）。</summary>
    public int PersistIntervalMinutes { get; set; } = 10;

    /// <summary>约定规则 Id：命中后发送今日输入统计。</summary>
    public const string DailyStatsRuleId = "daily-input-stats";
}

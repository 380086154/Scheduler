namespace Scheduler.Core.InputActivity;

/// <summary>今日输入统计快照（通知用）。</summary>
public sealed class InputActivitySnapshot
{
    public DateOnly Date { get; init; }
    public long Keyboard { get; init; }
    public long MouseLeft { get; init; }
    public long MouseRight { get; init; }
    public long MouseMiddle { get; init; }
    public long Wheel { get; init; }

    public long MouseClicks => MouseLeft + MouseRight + MouseMiddle;

    public string FormatNotificationBody() =>
        $"键盘：{Keyboard}\n鼠标点击：{MouseClicks}\n滚轮：{Wheel}";
}

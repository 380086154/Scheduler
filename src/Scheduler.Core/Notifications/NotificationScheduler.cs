using Scheduler.Core.Apis.Holiday;
using Scheduler.Core.InputActivity;
using Scheduler.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Scheduler.Core.Notifications;

public sealed class NotificationScheduler(
    IOptions<NotificationOptions> options,
    HolidayService holidayService,
    SessionPresenceService presenceService,
    InputActivityService inputActivity,
    NotificationStateStore stateStore,
    INotifier notifier,
    ILogger<NotificationScheduler> logger)
{
    private readonly NotificationOptions _options = options.Value;

    public async Task TickAsync(DateTime now, CancellationToken cancellationToken = default)
    {
        if (_options.Rules.Count == 0) return;

        var today = DateOnly.FromDateTime(now);

        foreach (var rule in _options.Rules)
        {
            if (!rule.Enabled) continue;
            if (string.IsNullOrWhiteSpace(rule.Id)) continue;

            try
            {
                if (!IsWithinSkewWindow(now, rule)) continue;
                if (stateStore.HasSent(rule.Id, today)) continue;
                if (!await MatchesScheduleAsync(rule.Schedule, now.Date)) continue;
                if (!presenceService.HasAtOfficeMinutes(rule.RequireAtOfficeMinutes)) continue;

                var title = string.IsNullOrWhiteSpace(rule.Title) ? rule.Id : rule.Title;
                var message = ResolveMessage(rule);

                await notifier.NotifyAsync(title, message, cancellationToken);
                stateStore.MarkSent(rule.Id, today);
                logger.LogInformation("已发送通知 {RuleId}: {Title}", rule.Id, title);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "发送通知失败 {RuleId}", rule.Id);
            }
        }
    }

    private string ResolveMessage(NotificationRuleOptions rule)
    {
        if (string.Equals(rule.Id, InputActivityOptions.DailyStatsRuleId, StringComparison.OrdinalIgnoreCase))
            return inputActivity.GetTodaySnapshot().FormatNotificationBody();

        return string.IsNullOrWhiteSpace(rule.Message) ? rule.Title : rule.Message;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="now"></param>
    /// <param name="rule"></param>
    /// <returns></returns>
    private static bool IsWithinSkewWindow(DateTime now, NotificationRuleOptions rule)
    {
        if (!TimeOnly.TryParse(rule.Time, out var time))
            return false;

        var start = now.Date + time.ToTimeSpan();
        var skew = Math.Max(0, rule.SkewMinutes);
        var end = start.AddMinutes(skew);
        return now >= start && now <= end;
    }

    private async Task<bool> MatchesScheduleAsync(NotificationSchedule schedule, DateTime date)
    {
        return schedule switch
        {
            NotificationSchedule.DailyWorkday => await holidayService.IsWorkDayAsync(date),
            NotificationSchedule.LastWorkdayOfWeek => await holidayService.IsLastWorkDayOfWeekAsync(date),
            NotificationSchedule.LastWorkdayOfMonth => await holidayService.IsLastWorkDayOfMonthAsync(date),
            _ => false
        };
    }
}

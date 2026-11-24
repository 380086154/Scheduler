using Scheduler.Core.Apis.Holiday;
using Scheduler.Core.InputActivity;
using Scheduler.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Scheduler.Core.Notifications;

public sealed class NotificationScheduler(
    IOptionsMonitor<NotificationOptions> options,
    HolidayService holidayService,
    SessionPresenceService presenceService,
    InputActivityService inputActivity,
    NotificationStateStore stateStore,
    ProcessAbsenceStateStore processAbsence,
    INotifier notifier,
    ILogger<NotificationScheduler> logger)
{
    public async Task TickAsync(DateTime now, CancellationToken cancellationToken = default)
    {
        var rules = options.CurrentValue.Rules;
        if (rules.Count == 0) return;

        var today = DateOnly.FromDateTime(now);

        foreach (var rule in rules)
        {
            if (!rule.Enabled) continue;
            if (string.IsNullOrWhiteSpace(rule.Id)) continue;

            try
            {
                if (!await IsDueAsync(rule, now, today)) continue;

                await NotifyRuleAsync(rule, cancellationToken);
                RememberDelivery(rule, now, today);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"发送通知失败 {rule.Id}");
            }
        }
    }

    private async Task<bool> IsDueAsync(NotificationRuleOptions rule, DateTime now, DateOnly today)
    {
        var triggered = rule.Kind == NotificationRuleKind.ProcessAbsent
            ? IsProcessAbsentTriggered(rule, now)
            : IsScheduledTriggered(rule, now, today);
        if (!triggered) return false;
        return await MatchesScheduleAsync(rule.Schedule, now.Date);
    }

    private bool IsScheduledTriggered(NotificationRuleOptions rule, DateTime now, DateOnly today)
    {
        if (!IsWithinSkewWindow(now, rule)) return false;
        if (stateStore.HasSent(rule.Id, today)) return false;
        return presenceService.HasAtOfficeMinutes(rule.RequireAtOfficeMinutes);
    }

    /// <summary>
    /// 进程不在时记下缺席起点。未满间隔时返回 false，起点仍保留，下次 tick 接着算。
    /// </summary>
    private bool IsProcessAbsentTriggered(NotificationRuleOptions rule, DateTime now)
    {
        var names = ProcessPresence.Normalize(rule.ProcessNames);
        if (names.Count == 0) return false;

        if (ProcessPresence.IsAnyRunning(names, logger))
        {
            processAbsence.Clear(rule.Id);
            return false;
        }

        var absentSince = processAbsence.GetAbsentSince(rule.Id);
        if (absentSince is null)
        {
            processAbsence.MarkAbsent(rule.Id, now);
            absentSince = now;
            logger.LogInformation($"进程未运行，开始计时 {rule.Id}: {string.Join(", ", names)}");
        }

        var threshold = TimeSpan.FromMinutes(Math.Max(1, rule.AbsentMinutes));
        if (now - absentSince.Value < threshold) return false;
        if (processAbsence.GetLastNotified(rule.Id) is DateTime last && now - last < threshold) return false;
        return presenceService.IsUnlocked;
    }

    private async Task NotifyRuleAsync(NotificationRuleOptions rule, CancellationToken cancellationToken)
    {
        var title = string.IsNullOrWhiteSpace(rule.Title) ? rule.Id : rule.Title;
        await notifier.NotifyAsync(title, ResolveMessage(rule), cancellationToken);
        logger.LogInformation($"已发送通知 {rule.Id}: {title}");
    }

    private void RememberDelivery(NotificationRuleOptions rule, DateTime now, DateOnly today)
    {
        if (rule.Kind == NotificationRuleKind.ProcessAbsent)
            processAbsence.MarkNotified(rule.Id, now);
        else
            stateStore.MarkSent(rule.Id, today);
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

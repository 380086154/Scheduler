using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Scheduler.Core.Apis.Holiday;
using Scheduler.Core.Notifications;
using Scheduler.Core.Services;

namespace Scheduler.Core.Hosting;

/// <summary>
/// 节假日刷新、在岗采样，以及定时通知。
/// 仅由持有 <see cref="Scheduler.Common.NotificationDuty"/> 的进程注册。
/// </summary>
public class SessionBackgroundService(
    HolidayService holidayService,
    SessionPresenceService presenceService,
    NotificationScheduler notificationScheduler,
    OutlookPresenceMonitor outlookPresence,
    ILogger<SessionBackgroundService> logger) : BackgroundService
{
    private static int day;
    private static DateTime _1minute;  // 1分钟任务
    private static DateTime _5minute;  // 5分钟任务
    private static DateTime _10minute; // 10分钟任务
    private static DateTime _20minute; // 20分钟任务
    private static DateTime _30minute; // 30分钟任务
    private static DateTime _60minute; // 60分钟任务

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("SessionBackgroundService 已启动");

        await holidayService.InitializeAsync().ConfigureAwait(false);

        presenceService.Sample();
        LogHeartbeat(DateTime.Now);

        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken).ConfigureAwait(false);

            var date = DateTime.Now;

            if (date > _1minute)
            {
                _1minute = date.AddSeconds(59);
                try
                {
                    presenceService.Sample(date);
                    await notificationScheduler.TickAsync(date, cancellationToken).ConfigureAwait(false);
                    await outlookPresence.TickAsync(date, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "分钟任务执行失败");
                }
            }

            if (date > _5minute)
            {
                _5minute = date.AddMinutes(4.9);
                LogHeartbeat(date);
            }

            if (date > _10minute)
            {
                _10minute = date.AddMinutes(9.9);
            }

            if (date > _20minute)
            {
                _20minute = date.AddMinutes(19.9);
            }

            if (date > _30minute)
            {
                _30minute = date.AddMinutes(29.9);
            }

            if (date > _60minute)
            {
                _60minute = date.AddMinutes(59.9);
            }

            // 跨天执行
            if (day != 0 && day != date.Day)
                await holidayService.InitializeAsync(true).ConfigureAwait(false);

            day = date.Day;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("SessionBackgroundService 正在停止");
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    private void LogHeartbeat(DateTime date)
    {
        var minutes = (int)presenceService.TodayAtOfficeDuration.TotalMinutes;
        logger.LogInformation(
            $"心跳 {date:HH:mm:ss} 解锁={presenceService.IsUnlocked} 远程={presenceService.IsRemote} 在岗={presenceService.IsAtOfficeNow} 今日在岗={minutes}分钟");
    }
}

using Acclaro.Platform.Application.Apis.Holiday;
using Acclaro.Platform.Application.Notifications;
using Acclaro.Platform.Application.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Acclaro.Platform.Scheduler;

/// <summary>
/// 会话后端服务
/// </summary>
public class SessionBackgroundService(
    HolidayService holidayService,
    SessionPresenceService presenceService,
    NotificationScheduler notificationScheduler,
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

        // 启动时确保当年节假日文件就绪
        await holidayService.InitializeAsync();

        // 启动立即采样一次在岗时长
        presenceService.Sample();
        LogHeartbeat(DateTime.Now);

        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);

            var date = DateTime.Now;

            if (date > _1minute)
            {
                _1minute = date.AddSeconds(59);
                try
                {
                    presenceService.Sample(date);
                    await notificationScheduler.TickAsync(date, cancellationToken);
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
            {
                // logger.LogInformation("跨天：尝试获取新的节假日数据");
                await holidayService.InitializeAsync(true);
            }

            day = date.Day;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("SessionBackgroundService 正在停止");
        await base.StopAsync(cancellationToken);
    }

    private void LogHeartbeat(DateTime date)
    {
        var minutes = (int)presenceService.TodayAtOfficeDuration.TotalMinutes;
        logger.LogInformation(
            "心跳 {Time:HH:mm:ss} 解锁={Unlocked} 远程={Remote} 在岗={AtOffice} 今日在岗={Minutes}分钟",
            date,
            presenceService.IsUnlocked,
            presenceService.IsRemote,
            presenceService.IsAtOfficeNow,
            minutes);
    }
}

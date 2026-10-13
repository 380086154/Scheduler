using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Scheduler.Core.Apis;
using Scheduler.Core.Apis.Holiday;
using Scheduler.Core.InputActivity;
using Scheduler.Core.Notifications;
using Scheduler.Core.Services;
using Scheduler.Core.Settings;

namespace Scheduler.Core.Hosting;

public static class SchedulerRuntime
{
    /// <summary>
    /// 托盘程序与 Windows 服务共用的组合根。
    /// 当另一进程已经负责定时发送时，<paramref name="runNotifications"/> 为 false。
    /// </summary>
    public static void AddSchedulerRuntime(this IHostApplicationBuilder builder, bool runNotifications)
    {
        UserSettingsStore.EnsureSeeded(builder.Configuration);
        builder.Configuration.AddJsonFile(UserSettingsStore.FilePath, optional: true, reloadOnChange: true);
        builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

        builder.Services.AddHttpApis(builder.Configuration);
        builder.Services.AddSingleton<HolidayService>();
        builder.Services.AddSingleton<SessionPresenceService>();
        builder.Services.AddInputActivity(builder.Configuration, capture: runNotifications);
        builder.Services.AddNotifications(builder.Configuration);
        if (runNotifications)
            builder.Services.AddHostedService<SessionBackgroundService>();
    }
}

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Scheduler.App.Logging;
using Scheduler.App.Settings;
using Scheduler.Core.Apis;
using Scheduler.Core.Apis.Holiday;
using Scheduler.Core.InputActivity;
using Scheduler.Core.Notifications;
using Scheduler.Core.Services;

namespace Scheduler.App;

internal static class AppHost
{
    public static IHost Build(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        UserSettingsStore.EnsureSeeded(builder.Configuration);
        builder.Configuration.AddJsonFile(UserSettingsStore.FilePath, optional: true, reloadOnChange: true);
        builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

        builder.Logging.AddFile(builder.Configuration);
        builder.Services.AddSingleton<IHostLifetime, WpfHostLifetime>();
        builder.Services.AddWindowsService();
        builder.Services.AddHttpApis(builder.Configuration);
        builder.Services.AddSingleton<HolidayService>();
        builder.Services.AddSingleton<SessionPresenceService>();
        builder.Services.AddInputActivity(builder.Configuration);
        builder.Services.AddNotifications(builder.Configuration);
        builder.Services.AddHostedService<SessionBackgroundService>();

        return builder.Build();
    }

    public static ILogger CreateStartupLogger(IHost host) =>
        host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Program");
}

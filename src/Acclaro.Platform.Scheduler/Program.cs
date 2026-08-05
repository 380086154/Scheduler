using Acclaro.Platform.Application.Apis;
using Acclaro.Platform.Application.Apis.Holiday;
using Acclaro.Platform.Application.Notifications;
using Acclaro.Platform.Application.Services;
using Acclaro.Platform.Common.Common;
using Acclaro.Platform.Scheduler;
using Acclaro.Platform.Scheduler.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using var singleInstance = new SingleInstanceHelper();
if (!singleInstance.CheckAndActivateInstance(false))
{
    TryWriteStartupNote("已有实例在运行，本进程退出");
    return;
}

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddFile(builder.Configuration);
builder.Services.AddWindowsService();
builder.Services.AddHttpApis(builder.Configuration);
builder.Services.AddSingleton<HolidayService>();
builder.Services.AddSingleton<SessionPresenceService>();
builder.Services.AddNotifications(builder.Configuration);
builder.Services.AddHostedService<SessionBackgroundService>();

var host = builder.Build();
var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Program");
logger.LogInformation("进程启动，PID={ProcessId}，日志目录={LogDir}", Environment.ProcessId, FileLoggingExtensions.DefaultLogDirectory);

await host.RunAsync();

logger.LogInformation("进程已退出");

static void TryWriteStartupNote(string message)
{
    try
    {
        var dir = FileLoggingExtensions.DefaultLogDirectory;
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, FileLoggerProvider.ActiveFileName);
        File.AppendAllText(path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [INF] Program {message}{Environment.NewLine}");
    }
    catch
    {
        // ignore
    }
}

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Scheduler.Core.Hosting;
using Scheduler.Core.Logging;

namespace Scheduler.App;

internal static class AppHost
{
    public static IHost Build(string[] args, bool runNotifications)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.AddSchedulerRuntime(runNotifications);
        builder.Logging.AddFile(builder.Configuration);
        builder.Services.AddSingleton<IHostLifetime, WpfHostLifetime>();
        return builder.Build();
    }

    public static ILogger CreateStartupLogger(IHost host) =>
        host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Program");
}

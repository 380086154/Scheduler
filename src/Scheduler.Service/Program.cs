using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Scheduler.Common;
using Scheduler.Core.Hosting;
using Scheduler.Core.Logging;
using Scheduler.Service;

const string ServiceName = "Scheduler Service";

SharedDataRoot.Apply();
var ownsNotifications = NotificationDuty.TryAcquire(out var duty);
try
{
    var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
    {
        Args = args,
        ContentRootPath = AppContext.BaseDirectory
    });
    builder.Services.AddWindowsService(options => options.ServiceName = ServiceName);
    builder.AddSchedulerRuntime(ownsNotifications);
    builder.Logging.AddFile(builder.Configuration);

    using var host = builder.Build();
    var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Scheduler.Service");
    logger.LogInformation(
        ownsNotifications
            ? "{ServiceName} owns scheduled notifications."
            : "{ServiceName} is idle. Another process already owns scheduled notifications.",
        ServiceName);

    await host.RunAsync();
}
finally
{
    duty?.Dispose();
}

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Scheduler.Core.Apis;

namespace Scheduler.Core.Notifications;

public static class NotificationServiceCollectionExtensions
{
    public static IServiceCollection AddNotifications(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<NotificationOptions>(configuration.GetSection(NotificationOptions.SectionName));
        services.AddSingleton<NotificationStateStore>();
        services.AddSingleton<NotificationScheduler>();
        services.AddHttpClient("notifications", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
            client.UseDefaultHeaders();
        });
        services.AddSingleton<INotifier, RoutingNotifier>();
        return services;
    }
}

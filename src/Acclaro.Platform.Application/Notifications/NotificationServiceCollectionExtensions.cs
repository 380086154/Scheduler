using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Acclaro.Platform.Application.Notifications;

public static class NotificationServiceCollectionExtensions
{
    public static IServiceCollection AddNotifications(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<NotificationOptions>(configuration.GetSection(NotificationOptions.SectionName));
        services.AddSingleton<NotificationStateStore>();
        services.AddSingleton<INotifier, TeamsWebhookNotifier>();
        services.AddSingleton<NotificationScheduler>();
        return services;
    }
}

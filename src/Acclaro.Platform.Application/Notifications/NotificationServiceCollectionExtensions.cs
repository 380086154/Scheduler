using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Acclaro.Platform.Application.Notifications;

public static class NotificationServiceCollectionExtensions
{
    public static IServiceCollection AddNotifications(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<NotificationOptions>(configuration.GetSection(NotificationOptions.SectionName));
        services.AddSingleton<NotificationStateStore>();
        services.AddSingleton<NotificationScheduler>();

        var provider = configuration.GetValue("Notifications:Provider", NotificationProvider.Teams);
        switch (provider)
        {
            case NotificationProvider.WeCom:
                EnsureConfigured(configuration["Notifications:WeComWebhookUrl"], "WeComWebhookUrl");
                if (WeComWebhookNotifier.ExtractKey(configuration["Notifications:WeComWebhookUrl"]) is null)
                    throw new InvalidOperationException("Notifications:WeComWebhookUrl 必须包含 key 查询参数。");
                services.AddSingleton<INotifier, WeComWebhookNotifier>();
                break;

            case NotificationProvider.DingTalk:
                services.AddSingleton<INotifier, DingTalkWebhookNotifier>();
                break;

            default:
                EnsureConfigured(configuration["Notifications:TeamsWebhookUrl"], "TeamsWebhookUrl");
                services.AddSingleton<INotifier, TeamsWebhookNotifier>();
                break;
        }

        return services;
    }

    private static void EnsureConfigured(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"Notifications:{name} 未配置。");
    }
}

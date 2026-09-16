using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Scheduler.Core.Notifications;

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
                if (WeComWebhookNotifier.ExtractKey(configuration["Notifications:WeComWebhookUrl"]) is not { Length: > 0 })
                {
                    AddDisabledNotifier(services, "Notifications:WeComWebhookUrl 未配置或缺少 key。");
                    break;
                }

                services.AddSingleton<INotifier, WeComWebhookNotifier>();
                break;

            case NotificationProvider.DingTalk:
                services.AddSingleton<INotifier, DingTalkWebhookNotifier>();
                break;

            default:
                if (string.IsNullOrWhiteSpace(configuration["Notifications:TeamsWebhookUrl"]))
                {
                    AddDisabledNotifier(services, "Notifications:TeamsWebhookUrl 未配置。");
                    break;
                }

                services.AddSingleton<INotifier, TeamsWebhookNotifier>();
                break;
        }

        return services;
    }

    private static void AddDisabledNotifier(IServiceCollection services, string reason)
    {
        services.AddSingleton<INotifier>(sp =>
        {
            var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("Notifications");
            logger.LogWarning("{Reason} 通知功能已禁用，后台统计与在岗采样仍会运行。", reason);
            return new NoOpNotifier();
        });
    }
}

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Scheduler.Core.InputActivity;

public static class InputActivityServiceCollectionExtensions
{
    public static IServiceCollection AddInputActivity(this IServiceCollection services, IConfiguration configuration, bool capture = true)
    {
        services.Configure<InputActivityOptions>(configuration.GetSection(InputActivityOptions.SectionName));
        services.AddSingleton<InputActivityService>();
        if (capture)
            services.AddHostedService(sp => sp.GetRequiredService<InputActivityService>());
        return services;
    }
}

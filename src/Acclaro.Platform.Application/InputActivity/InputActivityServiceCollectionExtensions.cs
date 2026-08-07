using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Acclaro.Platform.Application.InputActivity;

public static class InputActivityServiceCollectionExtensions
{
    public static IServiceCollection AddInputActivity(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<InputActivityOptions>(configuration.GetSection(InputActivityOptions.SectionName));
        services.AddSingleton<InputActivityService>();
        services.AddHostedService(sp => sp.GetRequiredService<InputActivityService>());
        return services;
    }
}

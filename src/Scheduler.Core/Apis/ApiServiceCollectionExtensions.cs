using Scheduler.Core.Apis.Holiday;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WebApiClientCore;

namespace Scheduler.Core.Apis;

public static class ApiServiceCollectionExtensions
{
    public static IServiceCollection AddHttpApis(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddWebApiClient().UseSourceGeneratorHttpApiActivator();
        services.AddPlatformHttpApi<IHolidayApi>("https://timor.tech/");
        services.AddPlatformHttpApi<IApiHubsHolidayApi>("https://api.apihubs.cn/");
        return services;
    }

    private static IHttpClientBuilder AddPlatformHttpApi<T>(this IServiceCollection services, string? url = "") where T : class
    {
        return services
            .AddHttpApi<T>(x =>
            {
                x.DisableLogging();
                x.UseDefaultUserAgent = false;
            })
            .ConfigureHttpApi(x =>
            {
                if (!string.IsNullOrWhiteSpace(url)) x.HttpHost = new Uri(url);
            })
            .ConfigureHttpClient(client =>
            {
                client.UseDefaultHeaders();
            });
    }

    public static HttpClient UseDefaultHeaders(this HttpClient client)
    {
        client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json, text/plain, */*");
        client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "zh-CN,zh;q=0.9,en;q=0.8");
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        return client;
    }

    private static HttpApiOptions DisableLogging(this HttpApiOptions o)
    {
#if !DEBUG
        o.UseLogging = false;
#endif
        return o;
    }
}

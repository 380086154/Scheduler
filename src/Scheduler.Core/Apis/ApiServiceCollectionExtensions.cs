using Acclaro.Platform.Application.Apis.Holiday;
using Acclaro.Platform.Application.Apis.Teams;
using Acclaro.Platform.Application.Apis.WeCom;
using Acclaro.Platform.Application.Notifications;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WebApiClientCore;

namespace Acclaro.Platform.Application.Apis;

public static class ApiServiceCollectionExtensions
{
    /// <summary>
    /// 注册 HTTP 接口
    /// </summary>
    public static IServiceCollection AddHttpApis(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddWebApiClient().UseSourceGeneratorHttpApiActivator();
        services.AddPlatformHttpApi<IHolidayApi>("https://timor.tech/");
        services.AddPlatformHttpApi<IApiHubsHolidayApi>("https://api.apihubs.cn/");

        var provider = configuration.GetValue("Notifications:Provider", NotificationProvider.Teams);
        switch (provider)
        {
            case NotificationProvider.WeCom:
                services.AddPlatformHttpApi<IWeComApi>("https://qyapi.weixin.qq.com/");
                break;
            case NotificationProvider.DingTalk:
                // 发送尚未实现，不注册 API
                break;
            default:
                services.AddPlatformHttpApi<IWorkflowsApi>(configuration["Notifications:TeamsWebhookUrl"]);
                break;
        }

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

    /// <summary>默认浏览器风格请求头（提莫等站点会校验 User-Agent）</summary>
    public static HttpClient UseDefaultHeaders(this HttpClient client)
    {
        client.DefaultRequestHeaders.Add("Accept", "application/json, text/plain, */*");
        client.DefaultRequestHeaders.Add("Accept-Language", "zh-CN,zh;q=0.9,en;q=0.8");
        client.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        return client;
    }

    /// <summary>设置在生产环境不输出日志</summary>
    private static HttpApiOptions DisableLogging(this HttpApiOptions o)
    {
#if !DEBUG
        o.UseLogging = false;
#endif
        return o;
    }
}

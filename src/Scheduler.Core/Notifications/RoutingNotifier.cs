using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Scheduler.Core.Notifications;

/// <summary>按当前配置发送通知；Webhook 未配置时跳过，不阻止进程运行。</summary>
public sealed class RoutingNotifier(
    IHttpClientFactory httpClientFactory,
    IOptionsMonitor<NotificationOptions> options,
    ILogger<RoutingNotifier> logger) : INotifier
{
    public async Task NotifyAsync(string title, string message, CancellationToken token = default)
    {
        var current = options.CurrentValue;
        switch (current.Provider)
        {
            case NotificationProvider.WeCom:
                await SendWeComAsync(current.WeComWebhookUrl, title, message, token).ConfigureAwait(false);
                break;
            case NotificationProvider.DingTalk:
                logger.LogWarning($"钉钉通知尚未实现，已跳过 {title}");
                break;
            default:
                await SendTeamsAsync(current.TeamsWebhookUrl, title, message, token).ConfigureAwait(false);
                break;
        }
    }

    private async Task SendTeamsAsync(string? url, string title, string message, CancellationToken token)
    {
        if (!IsHttpUrl(url))
        {
            logger.LogWarning($"Notifications:TeamsWebhookUrl 未配置，已跳过 {title}");
            return;
        }

        var text = NotificationText.Body(title, message);
        var json = $$"""
            {
              "$schema": "https://adaptivecards.io/schemas/adaptive-card.json",
              "type": "AdaptiveCard",
              "version": "1.4",
              "msteams": { "width": "Full" },
              "body": [
                { "type": "TextBlock", "text": "{{NotificationText.JsonEncode(text)}}", "wrap": true }
              ]
            }
            """;

        await PostAsync(url!, json, token).ConfigureAwait(false);
    }

    private async Task SendWeComAsync(string? url, string title, string message, CancellationToken token)
    {
        if (WeComWebhookNotifier.ExtractKey(url) is not { Length: > 0 } || !IsHttpUrl(url))
        {
            logger.LogWarning($"Notifications:WeComWebhookUrl 未配置或缺少 key，已跳过 {title}");
            return;
        }

        var content = NotificationText.Truncate(NotificationText.Body(title, message), 2000);
        var json = $$"""
            {
              "msgtype": "text",
              "text": {
                "content": "{{NotificationText.JsonEncode(content)}}"
              }
            }
            """;

        await PostAsync(url!, json, token).ConfigureAwait(false);
    }

    private async Task PostAsync(string url, string json, CancellationToken token)
    {
        var client = httpClientFactory.CreateClient("notifications");
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await client.SendAsync(request, token).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
            throw new HttpRequestException($"通知发送失败 {(int)response.StatusCode}：{body}");
        }
    }

    private static bool IsHttpUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
}

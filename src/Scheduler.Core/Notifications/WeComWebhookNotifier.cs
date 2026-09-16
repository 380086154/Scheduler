using Scheduler.Core.Apis.WeCom;
using Microsoft.Extensions.Options;

namespace Scheduler.Core.Notifications;

/// <summary>企业微信群机器人（text）。</summary>
public sealed class WeComWebhookNotifier(IWeComApi weComApi, IOptions<NotificationOptions> options) : INotifier
{
    public async Task NotifyAsync(string title, string message, CancellationToken token = default)
    {
        var key = ExtractKey(options.Value.WeComWebhookUrl)
            ?? throw new InvalidOperationException("Notifications:WeComWebhookUrl 未配置或缺少 key。");

        var content = NotificationText.Truncate(NotificationText.Body(title, message), 2000);
        var json = $$"""
            {
              "msgtype": "text",
              "text": {
                "content": "{{NotificationText.JsonEncode(content)}}"
              }
            }
            """;

        await weComApi.SendAsync(key, json, token);
    }

    internal static string? ExtractKey(string? webhookUrl)
    {
        if (string.IsNullOrWhiteSpace(webhookUrl)) return null;
        if (!Uri.TryCreate(webhookUrl, UriKind.Absolute, out var uri)) return null;

        var query = uri.Query.TrimStart('?');
        foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = part.Split('=', 2);
            if (kv.Length == 2 && kv[0].Equals("key", StringComparison.OrdinalIgnoreCase))
                return Uri.UnescapeDataString(kv[1]);
        }

        return null;
    }
}

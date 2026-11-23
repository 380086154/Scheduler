using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Web;

namespace Scheduler.Core.Notifications;

/// <summary>
/// 钉钉自定义机器人的地址和加签。
/// 发送本身走 <see cref="RoutingNotifier"/>，和 Teams、企业微信同一条路径。
/// </summary>
public static class DingTalkWebhookNotifier
{
    public static string ComputeSign(string secret, long timestamp)
    {
        var stringToSign = timestamp + "\n" + secret;

        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var dataBytes = Encoding.UTF8.GetBytes(stringToSign);

        using var hmac = new HMACSHA256(keyBytes);
        var hashBytes = hmac.ComputeHash(dataBytes);
        var base64Sign = Convert.ToBase64String(hashBytes);

        // 钉钉要求 URL 编码。UrlEncode 会把空格转成 +，需要换成 %20。
        return HttpUtility.UrlEncode(base64Sign, Encoding.UTF8).Replace("+", "%20");
    }

    /// <summary>
    /// 从完整 Webhook 得到实际请求地址。
    /// <c>secret</c> 只参与签名，不会出现在发出去的 URL 里。
    /// </summary>
    public static bool TryBuildRequestUrl(string? webhookUrl, out string requestUrl)
    {
        requestUrl = "";
        if (string.IsNullOrWhiteSpace(webhookUrl)) return false;
        if (!Uri.TryCreate(webhookUrl.Trim(), UriKind.Absolute, out var uri)) return false;
        if (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) return false;

        var pairs = ParseQuery(uri.Query);
        if (!pairs.Any(pair => pair.Key.Equals("access_token", StringComparison.OrdinalIgnoreCase) && pair.Value.Length > 0))
            return false;

        string? secret = null;
        var kept = new List<string>();
        foreach (var pair in pairs)
        {
            if (pair.Key.Equals("secret", StringComparison.OrdinalIgnoreCase))
            {
                secret = pair.Value;
                continue;
            }

            if (pair.Key.Equals("timestamp", StringComparison.OrdinalIgnoreCase)
                || pair.Key.Equals("sign", StringComparison.OrdinalIgnoreCase))
                continue;

            kept.Add($"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}");
        }

        if (!string.IsNullOrEmpty(secret))
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            kept.Add($"timestamp={timestamp}");
            kept.Add($"sign={ComputeSign(secret, timestamp)}");
        }

        requestUrl = uri.GetLeftPart(UriPartial.Path) + "?" + string.Join("&", kept);
        return true;
    }

    public static void EnsureAccepted(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return;

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("errcode", out var code) || code.ValueKind != JsonValueKind.Number)
                return;

            if (code.GetInt32() != 0)
                throw new HttpRequestException($"钉钉通知发送失败：{body}");
        }
        catch (JsonException)
        {
            // 钉钉成功时返回 JSON。不是 JSON 就交给 HTTP 状态码判断。
        }
    }

    private static List<KeyValuePair<string, string>> ParseQuery(string query)
    {
        var result = new List<KeyValuePair<string, string>>();
        var text = query.TrimStart('?');
        if (text.Length == 0) return result;

        foreach (var part in text.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = part.Split('=', 2);
            var key = Uri.UnescapeDataString(kv[0]);
            var value = kv.Length == 2 ? Uri.UnescapeDataString(kv[1]) : "";
            result.Add(new KeyValuePair<string, string>(key, value));
        }

        return result;
    }
}

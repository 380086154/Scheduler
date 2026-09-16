namespace Acclaro.Platform.Application.Notifications;

internal static class NotificationText
{
    /// <summary>
    /// 实际发送正文：只用 message；为空时才回退 title（兼容未填 Message 的旧配置）。
    /// </summary>
    public static string Body(string title, string message)
        => string.IsNullOrWhiteSpace(message) ? title : message;

    public static string JsonEncode(string value) => value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("\"", "\\\"", StringComparison.Ordinal)
        .Replace("\r", "\\r", StringComparison.Ordinal)
        .Replace("\n", "\\n", StringComparison.Ordinal)
        .Replace("\t", "\\t", StringComparison.Ordinal);

    /// <summary>企业微信 text 内容最长约 2048 字节，按字符粗截断即可。</summary>
    public static string Truncate(string value, int maxChars)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxChars) return value;
        return value[..(maxChars - 1)] + "…";
    }
}

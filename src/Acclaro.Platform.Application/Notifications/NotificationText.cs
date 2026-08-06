namespace Acclaro.Platform.Application.Notifications;

internal static class NotificationText
{
    public static string Combine(string title, string message)
    {
        if (string.IsNullOrWhiteSpace(title)) return message;
        if (string.IsNullOrWhiteSpace(message)) return title;
        //return $"{title}\n{message}";
        return message; // 不发送标题
    }

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

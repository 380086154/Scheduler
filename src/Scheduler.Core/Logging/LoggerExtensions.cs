using Microsoft.Extensions.Logging;

namespace Microsoft.Extensions.Logging;

public static class LoggerExtensions1
{
    /// <summary>
    /// 记录已经拼好的文本。单参数重载比带 params 的模板重载更匹配，
    /// 调用处用插值时就不会再提示“消息模板应为常量”。
    /// 必须转调框架方法；在本类里写 logger.LogInformation(message) 会递归到自身。
    /// </summary>
    public static void LogInformation(this ILogger logger, string? message) =>
        LoggerExtensions.LogInformation(logger, "{Message}", message);

    public static void LogWarning(this ILogger logger, string? message) =>
        LoggerExtensions.LogWarning(logger, "{Message}", message);

    public static void LogError(this ILogger logger, Exception? exception, string? message) =>
        LoggerExtensions.LogError(logger, exception, "{Message}", message);
}

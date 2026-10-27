namespace Microsoft.Extensions.Logging;

/// <summary>
/// 记录已经拼好的文本。单参数重载比带 params 的模板重载更匹配，
/// 调用处用插值时就不会再提示“消息模板应为常量”。
/// 必须转调框架方法；在本类里写 logger.LogInformation(message) 会递归到自身。
/// </summary>
public static class LoggerMessageExtensions
{
    /// <summary>Formats and writes an informational log message.</summary>
    /// <param name="logger">The <see cref="ILogger"/> to write to.</param>
    /// <param name="message">Format string of the log message in message template format. Example: <c>"User {User} logged in from {Address}"</c></param>
    /// <example>logger.LogInformation($"Processing request from {address}")</example>
    public static void LogInformation(this ILogger logger, string? message) =>
        LoggerExtensions.LogInformation(logger, "{Message}", message);

    /// <summary>Formats and writes a warning log message.</summary>
    /// <param name="logger">The <see cref="ILogger"/> to write to.</param>
    /// <param name="message">Format string of the log message in message template format. Example: <c>"User {User} logged in from {Address}"</c></param>
    /// <example>logger.LogWarning($"Error while processing request from {address}")</example>
    public static void LogWarning(this ILogger logger, string? message) =>
        LoggerExtensions.LogWarning(logger, "{Message}", message);

    /// <summary>Formats and writes a warning log message.</summary>
    /// <param name="logger">The <see cref="ILogger"/> to write to.</param>
    /// <param name="exception">The exception to log.</param>
    /// <param name="message">Format string of the log message in message template format. Example: <c>"User {User} logged in from {Address}"</c></param>
    /// <example>logger.LogWarning(exception, "Error while processing request from {address}")</example>
    public static void LogWarning(this ILogger logger, Exception? exception, string? message) =>
        LoggerExtensions.LogWarning(logger, exception, "{Message}", message);

    /// <summary>Formats and writes an error log message.</summary>
    /// <param name="logger">The <see cref="ILogger"/> to write to.</param>
    /// <param name="message">Format string of the log message in message template format. Example: <c>"User {User} logged in from {Address}"</c></param>
    /// <example>logger.LogError($"Processing request from {address}")</example>
    public static void LogError(this ILogger logger, string? message) =>
        LoggerExtensions.LogError(logger, "{Message}", message);

    /// <summary>Formats and writes an error log message.</summary>
    /// <param name="logger">The <see cref="ILogger"/> to write to.</param>
    /// <param name="exception">The exception to log.</param>
    /// <param name="message">Format string of the log message in message template format. Example: <c>"User {User} logged in from {Address}"</c></param>
    /// <example>logger.LogError(exception, $"Error while processing request from {address}")</example>
    public static void LogError(this ILogger logger, Exception? exception, string? message) =>
        LoggerExtensions.LogError(logger, exception, "{Message}", message);
}

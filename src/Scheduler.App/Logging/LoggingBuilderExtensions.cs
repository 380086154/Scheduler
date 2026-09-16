using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Acclaro.Platform.Scheduler.Logging;

public static class FileLoggingExtensions
{
    /// <summary>
    /// 日志固定写在程序目录 logs（不走 AppDataPaths 的 DEBUG→doc/data 重定向，避免找不到）。
    /// </summary>
    public static string DefaultLogDirectory =>
        Path.Combine(AppContext.BaseDirectory, "logs");

    public static ILoggingBuilder AddFile(this ILoggingBuilder builder, IConfiguration? configuration = null)
    {
        var dir = configuration?["Logging:File:Directory"];
        if (string.IsNullOrWhiteSpace(dir))
            dir = DefaultLogDirectory;

        var retainDays = 30;
        if (int.TryParse(configuration?["Logging:File:RetainDays"], out var days) && days > 0)
            retainDays = days;

        Directory.CreateDirectory(dir);
        builder.Services.AddSingleton<ILoggerProvider>(_ => new FileLoggerProvider(dir, retainDays));
        return builder;
    }
}

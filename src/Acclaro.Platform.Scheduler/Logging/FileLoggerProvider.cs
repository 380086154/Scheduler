using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Acclaro.Platform.Scheduler.Logging;

/// <summary>
/// 固定写 logs.log；跨天时备份为 logs.yyyyMMdd.log（类似 log4net / ABP），并按天数清理备份。
/// </summary>
public sealed class FileLoggerProvider(string directory, int retainDays = 30) : ILoggerProvider
{
    public const string ActiveFileName = "logs.log";

    private readonly object _sync = new();
    private readonly int _retainDays = Math.Max(1, retainDays);
    private DateOnly _activeDate;
    private bool _initialized;

    public ILogger CreateLogger(string categoryName) => new FileLogger(categoryName, this);

    internal void Write(LogLevel level, string category, string message, Exception? exception)
    {
        var now = DateTime.Now;
        var line = $"{now:yyyy-MM-dd HH:mm:ss.fff} [{Level(level)}] {TrimCategory(category)} {message}";
        if (exception is not null)
            line += Environment.NewLine + exception;

        lock (_sync)
        {
            Directory.CreateDirectory(directory);
            EnsureRollover(DateOnly.FromDateTime(now));
            AppendLine(Path.Combine(directory, ActiveFileName), line);
        }
    }

    private void EnsureRollover(DateOnly today)
    {
        if (!_initialized)
        {
            _initialized = true;
            var activePath = Path.Combine(directory, ActiveFileName);
            if (File.Exists(activePath))
            {
                var lastWrite = DateOnly.FromDateTime(File.GetLastWriteTime(activePath));
                if (lastWrite < today)
                    RollToArchive(lastWrite);
            }

            _activeDate = today;
            CleanupOldArchives(today);
            return;
        }

        if (_activeDate == today) return;

        RollToArchive(_activeDate);
        _activeDate = today;
        CleanupOldArchives(today);
    }

    private void RollToArchive(DateOnly date)
    {
        var activePath = Path.Combine(directory, ActiveFileName);
        if (!File.Exists(activePath)) return;

        var archivePath = Path.Combine(directory, $"logs.{date:yyyyMMdd}.log");
        if (File.Exists(archivePath))
            archivePath = Path.Combine(directory, $"logs.{date:yyyyMMdd}.{DateTime.Now:HHmmss}.log");

        try
        {
            File.Move(activePath, archivePath);
        }
        catch
        {
            // 备份失败时仍继续写当前文件，避免丢日志
        }
    }

    private void CleanupOldArchives(DateOnly today)
    {
        var cutoff = today.AddDays(-_retainDays);
        foreach (var file in Directory.EnumerateFiles(directory, "logs.*.log"))
        {
            var name = Path.GetFileName(file);
            // logs.yyyyMMdd.log 或 logs.yyyyMMdd.HHmmss.log
            if (!name.StartsWith("logs.", StringComparison.Ordinal) || !name.EndsWith(".log", StringComparison.Ordinal))
                continue;

            var middle = name.AsSpan("logs.".Length, name.Length - "logs.".Length - ".log".Length);
            var datePart = middle.Length >= 8 ? middle[..8] : middle;
            if (!DateOnly.TryParseExact(
                    datePart,
                    "yyyyMMdd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var fileDate))
                continue;

            if (fileDate < cutoff)
            {
                try { File.Delete(file); }
                catch { /* ignore */ }
            }
        }
    }

    private static void AppendLine(string path, string line)
    {
        using var stream = new FileStream(
            path,
            FileMode.Append,
            FileAccess.Write,
            FileShare.ReadWrite);
        using var writer = new StreamWriter(stream, Encoding.UTF8);
        writer.WriteLine(line);
    }

    public void Dispose() { }

    private static string Level(LogLevel level) => level switch
    {
        LogLevel.Trace => "TRC",
        LogLevel.Debug => "DBG",
        LogLevel.Information => "INF",
        LogLevel.Warning => "WRN",
        LogLevel.Error => "ERR",
        LogLevel.Critical => "CRT",
        _ => "???"
    };

    private static string TrimCategory(string category)
    {
        var i = category.LastIndexOf('.');
        return i >= 0 ? category[(i + 1)..] : category;
    }

    private sealed class FileLogger(string categoryName, FileLoggerProvider provider) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;
            provider.Write(logLevel, categoryName, formatter(state, exception), exception);
        }
    }
}

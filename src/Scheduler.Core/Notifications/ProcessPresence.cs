using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Scheduler.Core.Notifications;

/// <summary>按进程名判断程序是否仍在运行。新版 Outlook 是 olk，经典版是 OUTLOOK。</summary>
public static class ProcessPresence
{
    public static IReadOnlyList<string> Normalize(IEnumerable<string>? names)
    {
        if (names is null) return [];

        var result = new List<string>();
        foreach (var name in names)
        {
            var processName = NormalizeOne(name);
            if (processName.Length == 0) continue;
            if (result.Contains(processName, StringComparer.OrdinalIgnoreCase)) continue;
            result.Add(processName);
        }

        return result;
    }

    public static string NormalizeOne(string? name)
    {
        var text = name?.Trim() ?? "";
        if (text.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            text = text[..^4].Trim();
        return text;
    }

    /// <summary>查不到进程时视为仍在运行，避免误报。</summary>
    public static bool IsAnyRunning(IReadOnlyList<string> processNames, ILogger logger)
    {
        var checkedAny = false;
        foreach (var processName in processNames)
        {
            try
            {
                var processes = Process.GetProcessesByName(processName);
                try
                {
                    checkedAny = true;
                    if (processes.Length > 0)
                        return true;
                }
                finally
                {
                    foreach (var process in processes)
                        process.Dispose();
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning($"检查进程 {processName} 失败：{ex.Message}");
                return true;
            }
        }

        return !checkedAny;
    }
}

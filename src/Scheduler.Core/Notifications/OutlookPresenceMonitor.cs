using System.Diagnostics;
using System.Text.Json;
using Scheduler.Common;
using Scheduler.Core.Services;
using Microsoft.Extensions.Logging;

namespace Scheduler.Core.Notifications;

/// <summary>
/// Outlook 不在运行时提醒启动。
/// 新版 Outlook 的进程是 olk.exe，经典版是 OUTLOOK.EXE，两者任一存在即视为正在运行。
/// </summary>
public sealed class OutlookPresenceMonitor(
    INotifier notifier,
    SessionPresenceService presence,
    ILogger<OutlookPresenceMonitor> logger)
{
    public static readonly TimeSpan AbsenceThreshold = TimeSpan.FromMinutes(30);

    private static readonly string[] ProcessNames = ["olk", "OUTLOOK"];

    private readonly string _file = AppDataPaths.GetFile("notifications", "outlook.json");
    private readonly JsonSerializerOptions _json = new() { WriteIndented = true };
    private DateTime? _absentSince;
    private DateTime? _lastNotified;
    private bool _loaded;

    public async Task TickAsync(DateTime now, CancellationToken cancellationToken = default)
    {
        EnsureLoaded();

        if (IsOutlookRunning())
        {
            if (_absentSince is null && _lastNotified is null) return;
            _absentSince = null;
            _lastNotified = null;
            Save();
            return;
        }

        if (_absentSince is null)
        {
            _absentSince = now;
            Save();
            return;
        }

        if (now - _absentSince.Value < AbsenceThreshold) return;
        if (_lastNotified is DateTime last && now - last < AbsenceThreshold) return;
        if (!presence.IsUnlocked) return;

        try
        {
            await notifier.NotifyAsync(
                "启动 Outlook",
                "Outlook 已超过 30 分钟没有运行，请启动 Outlook。",
                cancellationToken).ConfigureAwait(false);
            _lastNotified = now;
            Save();
            logger.LogInformation($"已提醒启动 Outlook，未运行超过 {(int)(now - _absentSince.Value).TotalMinutes} 分钟");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "提醒启动 Outlook 失败");
        }
    }

    private bool IsOutlookRunning()
    {
        try
        {
            foreach (var name in ProcessNames)
            {
                var processes = Process.GetProcessesByName(name);
                try
                {
                    if (processes.Length > 0)
                        return true;
                }
                finally
                {
                    foreach (var process in processes)
                        process.Dispose();
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            logger.LogWarning($"检查 Outlook 进程失败：{ex.Message}");
            return true;
        }
    }

    private void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;

        try
        {
            if (!File.Exists(_file)) return;
            var state = JsonSerializer.Deserialize<OutlookMonitorState>(File.ReadAllText(_file), _json);
            if (state is null) return;
            _absentSince = state.AbsentSince;
            _lastNotified = state.LastNotified;
        }
        catch (Exception ex)
        {
            logger.LogWarning($"读取 Outlook 监测状态失败：{ex.Message}");
        }
    }

    private void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(new OutlookMonitorState
            {
                AbsentSince = _absentSince,
                LastNotified = _lastNotified
            }, _json);
            File.WriteAllText(_file, json);
        }
        catch (Exception ex)
        {
            logger.LogWarning($"保存 Outlook 监测状态失败：{ex.Message}");
        }
    }

    private sealed class OutlookMonitorState
    {
        public DateTime? AbsentSince { get; set; }
        public DateTime? LastNotified { get; set; }
    }
}

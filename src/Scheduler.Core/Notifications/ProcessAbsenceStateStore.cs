using System.Text.Json;
using Scheduler.Common;
using Microsoft.Extensions.Logging;

namespace Scheduler.Core.Notifications;

/// <summary>进程未运行规则的缺席起点和上次提醒时间，按规则 Id 分开保存。</summary>
public sealed class ProcessAbsenceStateStore(ILogger<ProcessAbsenceStateStore> logger)
{
    private readonly string _file = AppDataPaths.GetFile("notifications", "process-absence.json");
    private readonly JsonSerializerOptions _json = new() { WriteIndented = true };
    private readonly Dictionary<string, ProcessAbsenceEntry> _entries = new(StringComparer.OrdinalIgnoreCase);
    private bool _loaded;

    public DateTime? GetAbsentSince(string ruleId) => Get(ruleId)?.AbsentSince;

    public DateTime? GetLastNotified(string ruleId) => Get(ruleId)?.LastNotified;

    public void MarkAbsent(string ruleId, DateTime absentSince)
    {
        var entry = GetOrAdd(ruleId);
        if (entry.AbsentSince is not null) return;
        entry.AbsentSince = absentSince;
        Save();
    }

    public void MarkNotified(string ruleId, DateTime notifiedAt)
    {
        var entry = GetOrAdd(ruleId);
        entry.LastNotified = notifiedAt;
        Save();
    }

    public void Clear(string ruleId)
    {
        EnsureLoaded();
        if (!_entries.Remove(ruleId)) return;
        Save();
    }

    private ProcessAbsenceEntry? Get(string ruleId)
    {
        EnsureLoaded();
        return _entries.GetValueOrDefault(ruleId);
    }

    private ProcessAbsenceEntry GetOrAdd(string ruleId)
    {
        EnsureLoaded();
        if (_entries.TryGetValue(ruleId, out var entry))
            return entry;

        entry = new ProcessAbsenceEntry();
        _entries[ruleId] = entry;
        return entry;
    }

    private void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;

        try
        {
            if (!File.Exists(_file)) return;
            var state = JsonSerializer.Deserialize<Dictionary<string, ProcessAbsenceEntry>>(File.ReadAllText(_file), _json);
            if (state is null) return;
            foreach (var pair in state)
            {
                if (!string.IsNullOrWhiteSpace(pair.Key) && pair.Value is not null)
                    _entries[pair.Key] = pair.Value;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning($"读取进程未运行状态失败：{ex.Message}");
        }
    }

    private void Save()
    {
        try
        {
            File.WriteAllText(_file, JsonSerializer.Serialize(_entries, _json));
        }
        catch (Exception ex)
        {
            logger.LogWarning($"保存进程未运行状态失败：{ex.Message}");
        }
    }

}

/// <summary>必须是 public。System.Text.Json 不能反序列化非 public 类型，否则重启后缺席起点会丢。</summary>
public sealed class ProcessAbsenceEntry
{
    public DateTime? AbsentSince { get; set; }
    public DateTime? LastNotified { get; set; }
}

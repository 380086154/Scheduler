using Acclaro.Platform.Common.Common;

namespace Acclaro.Platform.Application.Notifications;

/// <summary>
/// 通知已发送状态落盘（ruleId + 日期），避免重启重复发送。
/// </summary>
public sealed class NotificationStateStore
{
    private readonly string _file;
    private readonly HashSet<string> _sent = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _sync = new();

    public NotificationStateStore()
    {
        _file = AppDataPaths.GetFile("notifications", "sent.json");
        Load();
    }

    public bool HasSent(string ruleId, DateOnly date)
    {
        var key = Key(ruleId, date);
        lock (_sync) return _sent.Contains(key);
    }

    public void MarkSent(string ruleId, DateOnly date)
    {
        var key = Key(ruleId, date);
        lock (_sync)
        {
            if (!_sent.Add(key)) return;
            SaveUnlocked();
        }
    }

    private static string Key(string ruleId, DateOnly date) => $"{ruleId}|{date:yyyy-MM-dd}";

    private void Load()
    {
        try
        {
            if (!File.Exists(_file)) return;
            foreach (var line in File.ReadAllLines(_file))
            {
                var trimmed = line.Trim();
                if (trimmed.Length > 0) _sent.Add(trimmed);
            }
        }
        catch
        {
            // ignore
        }
    }

    private void SaveUnlocked()
    {
        try
        {
            File.WriteAllLines(_file, _sent.OrderBy(x => x));
        }
        catch
        {
            // ignore
        }
    }
}

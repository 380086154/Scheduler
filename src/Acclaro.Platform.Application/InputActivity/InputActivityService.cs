using System.Runtime.Versioning;
using System.Text;
using Acclaro.Platform.Common.Common;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharpHook;
using SharpHook.Data;
using SharpHook.Providers;

namespace Acclaro.Platform.Application.InputActivity;

/// <summary>
/// 使用 SharpHook 统计今日键鼠次数；可选连续文本记录按键。
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class InputActivityService : IHostedService, IDisposable
{
    private const char EnterSymbol = '↵';

    private readonly InputActivityOptions _options;
    private readonly ILogger<InputActivityService> _logger;
    private readonly string _statsFile;
    private readonly string _keysDir;
    private readonly object _sync = new();
    private readonly HashSet<KeyCode> _keysDown = [];
    private readonly HashSet<KeyCode> _emitCharFor = [];
    private readonly StringBuilder _keyBuffer = new();

    private EventLoopGlobalHook? _hook;
    private Task? _hookTask;

    private DateOnly _date;
    private DateTime _hourBucketLocal;
    private long _keyboard;
    private long _mouseLeft;
    private long _mouseRight;
    private long _mouseMiddle;
    private long _wheel;
    private int _keyLineWidth;
    private DateTime _lastPersistUtc = DateTime.MinValue;

    public InputActivityService(
        IOptions<InputActivityOptions> options,
        ILogger<InputActivityService> logger)
    {
        _options = options.Value;
        _logger = logger;
        _statsFile = AppDataPaths.GetFile("input", "stats.txt");
        _keysDir = Path.Combine(AppContext.BaseDirectory, "logs", "input");
        _date = DateOnly.FromDateTime(DateTime.Today);
        _hourBucketLocal = FloorToHour(DateTime.Now);
        Load();
    }

    public InputActivitySnapshot GetTodaySnapshot()
    {
        lock (_sync)
        {
            EnsureDayUnlocked(DateTime.Now);
            return new InputActivitySnapshot
            {
                Date = _date,
                Keyboard = _keyboard,
                MouseLeft = _mouseLeft,
                MouseRight = _mouseRight,
                MouseMiddle = _mouseMiddle,
                Wheel = _wheel
            };
        }
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("InputActivity 已禁用");
            return Task.CompletedTask;
        }

        if (!OperatingSystem.IsWindows())
        {
            _logger.LogWarning("InputActivity 仅支持 Windows，已跳过");
            return Task.CompletedTask;
        }

        try
        {
            UioHookProvider.Instance.KeyTypedEnabled = _options.RecordKeyValues;

            _hook = new EventLoopGlobalHook(runAsyncOnBackgroundThread: true);
            _hook.KeyPressed += OnKeyPressed;
            _hook.KeyReleased += OnKeyReleased;
            _hook.KeyTyped += OnKeyTyped;
            _hook.MousePressed += OnMousePressed;
            _hook.MouseWheel += OnMouseWheel;

            _hookTask = _hook.RunAsync();
            _logger.LogInformation("InputActivity SharpHook 已启动 (RecordKeyValues={RecordKeyValues})", _options.RecordKeyValues);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "启动 SharpHook 失败");
        }

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (_hook is { IsDisposed: false })
                _hook.Dispose();

            if (_hookTask is not null)
                await _hookTask.WaitAsync(TimeSpan.FromSeconds(3), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "停止 SharpHook 时出现异常");
        }
        finally
        {
            _hook = null;
            _hookTask = null;
        }

        lock (_sync)
        {
            FlushKeyTextUnlocked();
            PersistUnlocked(force: true);
        }

        _logger.LogInformation("InputActivity 已停止");
    }

    public void Dispose()
    {
        try { _hook?.Dispose(); }
        catch { /* ignore */ }
    }

    private void OnKeyPressed(object? sender, KeyboardHookEventArgs e)
    {
        lock (_sync)
        {
            EnsureBucketsUnlocked(DateTime.Now);
            if (!_keysDown.Add(e.Data.KeyCode)) return;

            _keyboard++;
            if (_options.RecordKeyValues)
                _emitCharFor.Add(e.Data.KeyCode);
            PersistUnlocked(force: false);
        }
    }

    private void OnKeyReleased(object? sender, KeyboardHookEventArgs e)
    {
        lock (_sync)
        {
            _keysDown.Remove(e.Data.KeyCode);
            _emitCharFor.Remove(e.Data.KeyCode);
        }
    }

    private void OnKeyTyped(object? sender, KeyboardHookEventArgs e)
    {
        if (!_options.RecordKeyValues) return;

        lock (_sync)
        {
            // 仅记录本次按下的第一次 KeyTyped，忽略长按连发
            if (!_emitCharFor.Remove(e.Data.KeyCode)) return;

            EnsureBucketsUnlocked(DateTime.Now);
            AppendKeyCharUnlocked(e.Data.KeyChar);
        }
    }

    private void OnMousePressed(object? sender, MouseHookEventArgs e)
    {
        lock (_sync)
        {
            EnsureBucketsUnlocked(DateTime.Now);
            switch (e.Data.Button)
            {
                case MouseButton.Button1:
                    _mouseLeft++;
                    break;
                case MouseButton.Button2:
                    _mouseRight++;
                    break;
                case MouseButton.Button3:
                    _mouseMiddle++;
                    break;
                default:
                    return;
            }

            PersistUnlocked(force: false);
        }
    }

    private void OnMouseWheel(object? sender, MouseWheelHookEventArgs e)
    {
        lock (_sync)
        {
            EnsureBucketsUnlocked(DateTime.Now);
            _wheel++;
            PersistUnlocked(force: false);
        }
    }

    private void AppendKeyCharUnlocked(char ch)
    {
        if (ch is '\r' or '\n')
        {
            AppendUnitsUnlocked(EnterSymbol.ToString());
            _keyBuffer.AppendLine();
            _keyLineWidth = 0;
            FlushKeyTextUnlocked();
            return;
        }

        if (ch == '\t')
        {
            AppendUnitsUnlocked("\t");
        }
        else if (!char.IsControl(ch))
        {
            AppendUnitsUnlocked(ch.ToString());
        }
        else
        {
            return;
        }

        if (_keyBuffer.Length >= 64)
            FlushKeyTextUnlocked();
    }

    private void AppendUnitsUnlocked(string text)
    {
        var maxWidth = Math.Max(20, _options.LineWidthUnits);
        foreach (var ch in text)
        {
            var w = DisplayWidth(ch);
            if (_keyLineWidth > 0 && _keyLineWidth + w > maxWidth)
            {
                _keyBuffer.AppendLine();
                _keyLineWidth = 0;
            }

            _keyBuffer.Append(ch);
            _keyLineWidth += w;
        }
    }

    private void EnsureBucketsUnlocked(DateTime now)
    {
        EnsureDayUnlocked(now);
        var hour = FloorToHour(now);
        if (hour == _hourBucketLocal) return;

        FlushKeyTextUnlocked();
        _hourBucketLocal = hour;
        _keyLineWidth = 0;
        CleanupKeyFilesUnlocked();
    }

    private void EnsureDayUnlocked(DateTime now)
    {
        var today = DateOnly.FromDateTime(now);
        if (_date == today) return;

        FlushKeyTextUnlocked();
        _date = today;
        _keyboard = 0;
        _mouseLeft = 0;
        _mouseRight = 0;
        _mouseMiddle = 0;
        _wheel = 0;
        _keysDown.Clear();
        _emitCharFor.Clear();
        _keyLineWidth = 0;
        _lastPersistUtc = DateTime.MinValue;
        PersistUnlocked(force: true);
    }

    private void FlushKeyTextUnlocked()
    {
        if (_keyBuffer.Length == 0) return;

        try
        {
            Directory.CreateDirectory(_keysDir);
            var path = Path.Combine(_keysDir, $"keys-{_hourBucketLocal:yyyyMMdd-HH}.txt");
            File.AppendAllText(path, _keyBuffer.ToString(), Encoding.UTF8);
            _keyBuffer.Clear();
            CleanupKeyFilesUnlocked();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "写入按键文本失败");
        }
    }

    private void CleanupKeyFilesUnlocked()
    {
        try
        {
            var retain = Math.Max(1, _options.HourlyLogRetainFiles);
            if (!Directory.Exists(_keysDir)) return;

            foreach (var file in Directory.GetFiles(_keysDir, "keys-*.txt")
                         .OrderByDescending(File.GetLastWriteTimeUtc)
                         .Skip(retain))
            {
                try { File.Delete(file); }
                catch { /* ignore */ }
            }
        }
        catch
        {
            // ignore
        }
    }

    private void PersistUnlocked(bool force)
    {
        var utc = DateTime.UtcNow;
        var interval = TimeSpan.FromMinutes(Math.Max(1, _options.PersistIntervalMinutes));
        if (!force && utc - _lastPersistUtc < interval) return;

        try
        {
            var text =
                $"{_date:yyyy-MM-dd}{Environment.NewLine}" +
                $"{_keyboard}{Environment.NewLine}" +
                $"{_mouseLeft}{Environment.NewLine}" +
                $"{_mouseRight}{Environment.NewLine}" +
                $"{_mouseMiddle}{Environment.NewLine}" +
                $"{_wheel}{Environment.NewLine}";
            File.WriteAllText(_statsFile, text);
            _lastPersistUtc = utc;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "写入 input stats 失败");
        }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_statsFile)) return;
            var lines = File.ReadAllLines(_statsFile);
            if (lines.Length < 6) return;
            if (!DateOnly.TryParse(lines[0], out var date)) return;
            if (!long.TryParse(lines[1], out var keyboard)) return;
            if (!long.TryParse(lines[2], out var left)) return;
            if (!long.TryParse(lines[3], out var right)) return;
            if (!long.TryParse(lines[4], out var middle)) return;
            if (!long.TryParse(lines[5], out var wheel)) return;

            var today = DateOnly.FromDateTime(DateTime.Today);
            if (date != today) return;

            _date = date;
            _keyboard = keyboard;
            _mouseLeft = left;
            _mouseRight = right;
            _mouseMiddle = middle;
            _wheel = wheel;
        }
        catch
        {
            // ignore corrupt state
        }
    }

    private static DateTime FloorToHour(DateTime value) =>
        new(value.Year, value.Month, value.Day, value.Hour, 0, 0, value.Kind);

    private static int DisplayWidth(char c) => c <= 0x7F ? 1 : 2;
}

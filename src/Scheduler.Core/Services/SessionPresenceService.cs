using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Scheduler.Common;
using Microsoft.Win32;

namespace Scheduler.Core.Services;

/// <summary>
/// 会话在岗检测：本地解锁 + 非 ToDesk 远程，并累计今日在公司时长（落盘防重启丢失）。
/// </summary>
public sealed class SessionPresenceService
{
    /// <summary>满此时长后视为今日已确认在公司，可降低落盘频率。</summary>
    private static readonly TimeSpan ConfirmedAtOfficeThreshold = TimeSpan.FromHours(1);

    /// <summary>确认在公司后的落盘间隔时间，单位分钟</summary>
    private static readonly TimeSpan PersistIntervalWhenConfirmed = TimeSpan.FromMinutes(10);

    private readonly string _presenceFile;
    private readonly object _sync = new();

    private DateOnly _date;
    private TimeSpan _todayAtOfficeDuration;
    private DateTime? _lastSampleUtc;
    private bool _lastAtOffice;
    private DateTime? _lastPersistUtc;

    public SessionPresenceService()
    {
        _presenceFile = AppDataPaths.GetFile("notifications", "presence.json");
        Load();
    }

    public TimeSpan TodayAtOfficeDuration
    {
        get { lock (_sync) return _todayAtOfficeDuration; }
    }

    public bool IsUnlocked => GetSessionStatus() == 1;

    /// <summary>是否远程操作，true 是</summary>
    public bool IsRemote => DetectToDeskRemote();

    public bool IsAtOfficeNow => IsUnlocked && !IsRemote;

    /// <summary>
    /// 是否人在办公室的电脑面前解锁的屏幕，如果是通过远程操作，一些通知则不需要发送。
    /// </summary>
    /// <param name="minutes"></param>
    /// <returns></returns>
    public bool HasAtOfficeMinutes(int minutes)
    {
        if (minutes <= 0) return true;
        lock (_sync) return _todayAtOfficeDuration >= TimeSpan.FromMinutes(minutes);
    }

    /// <summary>采样，累计今日在公司解锁时长。</summary>
    public void Sample(DateTime? now = null)
    {
        var localNow = now ?? DateTime.Now;
        var utcNow = localNow.ToUniversalTime();
        var today = DateOnly.FromDateTime(localNow);
        var atOffice = IsAtOfficeNow;

        lock (_sync)
        {
            var forcePersist = false;
            if (_date != today)
            {
                _date = today;
                _todayAtOfficeDuration = TimeSpan.Zero;
                _lastSampleUtc = null;
                _lastAtOffice = false;
                _lastPersistUtc = null;
                forcePersist = true;
            }

            var wasConfirmed = _todayAtOfficeDuration >= ConfirmedAtOfficeThreshold;

            if (_lastSampleUtc is DateTime last && _lastAtOffice)
            {
                var delta = utcNow - last;
                if (delta > TimeSpan.Zero && delta < TimeSpan.FromMinutes(5))
                    _todayAtOfficeDuration += delta;
            }

            _lastSampleUtc = utcNow;
            _lastAtOffice = atOffice;

            // 刚跨过 1 小时阈值时立刻落盘一次，之后放缓
            if (!wasConfirmed && _todayAtOfficeDuration >= ConfirmedAtOfficeThreshold)
                forcePersist = true;

            PersistIfNeeded(utcNow, forcePersist);
        }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_presenceFile)) return;
            var lines = File.ReadAllLines(_presenceFile);
            if (lines.Length < 2) return;
            if (!DateOnly.TryParse(lines[0], out var date)) return;
            if (!double.TryParse(lines[1], out var seconds)) return;

            _date = date;
            _todayAtOfficeDuration = TimeSpan.FromSeconds(seconds);
            if (_date != DateOnly.FromDateTime(DateTime.Today))
            {
                _date = DateOnly.FromDateTime(DateTime.Today);
                _todayAtOfficeDuration = TimeSpan.Zero;
            }
        }
        catch
        {
            // ignore corrupt state
        }
    }

    private void PersistIfNeeded(DateTime utcNow, bool force)
    {
        if (!force)
        {
            if (_todayAtOfficeDuration >= ConfirmedAtOfficeThreshold
                && _lastPersistUtc is DateTime last
                && utcNow - last < PersistIntervalWhenConfirmed)
            {
                return;
            }
        }

        try
        {
            File.WriteAllText(
                _presenceFile,
                $"{_date:yyyy-MM-dd}{Environment.NewLine}{_todayAtOfficeDuration.TotalSeconds:F0}{Environment.NewLine}");
            _lastPersistUtc = utcNow;
        }
        catch
        {
            // ignore IO errors
        }
    }

    #region Unlock detection (from SystemRunningStatusService)

    [DllImport("Wtsapi32.dll", CharSet = CharSet.Unicode)]
    private static extern bool WTSQuerySessionInformationW(
        IntPtr hServer, uint sessionId, WTS_INFO_CLASS wtsInfoClass, ref IntPtr ppBuffer, ref uint pBytesReturned);

    [DllImport("Wtsapi32.dll", CharSet = CharSet.Unicode)]
    private static extern void WTSFreeMemory(IntPtr pMemory);

    [DllImport("Kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern uint WTSGetActiveConsoleSessionId();

    private enum WTS_INFO_CLASS
    {
        WTSSessionInfoEx = 25
    }

    private enum WTS_CONNECTSTATE_CLASS
    {
        WTSActive,
        WTSConnected,
        WTSConnectQuery,
        WTSShadow,
        WTSDisconnected,
        WTSIdle,
        WTSListen,
        WTSReset,
        WTSDown,
        WTSInit
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WTSINFOEXW
    {
        public int Level;
        public WTSINFOEX_LEVEL_W Data;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WTSINFOEX_LEVEL_W
    {
        public WTSINFOEX_LEVEL1_W WTSInfoExLevel1;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WTSINFOEX_LEVEL1_W
    {
        public int SessionId;
        public WTS_CONNECTSTATE_CLASS SessionState;
        public int SessionFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 33)]
        public string WinStationName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 21)]
        public string UserName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 18)]
        public string DomainName;
        public LARGE_INTEGER LogonTime;
        public LARGE_INTEGER ConnectTime;
        public LARGE_INTEGER DisconnectTime;
        public LARGE_INTEGER LastInputTime;
        public LARGE_INTEGER CurrentTime;
        public uint IncomingBytes;
        public uint OutgoingBytes;
        public uint IncomingFrames;
        public uint OutgoingFrames;
        public uint IncomingCompressedBytes;
        public uint OutgoingCompressedBytes;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct LARGE_INTEGER
    {
        [FieldOffset(0)] public uint LowPart;
        [FieldOffset(4)] public int HighPart;
        [FieldOffset(0)] public long QuadPart;
    }

    /// <returns>0 锁屏，1 已解锁，其他未知（视为未解锁）</returns>
    private static int GetSessionStatus()
    {
        uint sessionId = WTSGetActiveConsoleSessionId();
        uint bytesReturned = 0;
        IntPtr pInfo = IntPtr.Zero;
        try
        {
            if (!WTSQuerySessionInformationW(IntPtr.Zero, sessionId, WTS_INFO_CLASS.WTSSessionInfoEx, ref pInfo, ref bytesReturned)
                || pInfo == IntPtr.Zero)
                return 0;

            var info = Marshal.PtrToStructure<WTSINFOEXW>(pInfo);
            return info.Level == 1 ? info.Data.WTSInfoExLevel1.SessionFlags : 0;
        }
        catch
        {
            return 0;
        }
        finally
        {
            if (pInfo != IntPtr.Zero) WTSFreeMemory(pInfo);
        }
    }

    #endregion

    #region ToDesk remote detection

    private static bool DetectToDeskRemote()
    {
        try
        {
            var installPath = GetToDeskInstallPathFromRegistry();
            if (string.IsNullOrWhiteSpace(installPath)) return false;

            var logsPath = Path.Combine(installPath, "Logs");
            if (!Directory.Exists(logsPath)) return false;

            var date = DateTime.Now;
            var di = new DirectoryInfo(logsPath);
            var latestFiles = di.GetFiles($"session*{date:_yyyy_MM_dd}.log")
                .OrderByDescending(x => x.LastWriteTime)
                .Take(10);

            foreach (var item in latestFiles)
            {
                if (item.LastWriteTime >= date.AddMinutes(-5))
                    return true;
            }
        }
        catch
        {
            // ignore
        }

        return false;
    }

    private static string? GetToDeskInstallPathFromRegistry()
    {
        var path = GetInstPath(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\ToDesk");
        if (!string.IsNullOrWhiteSpace(path)) return path;

        path = GetInstPath(Registry.LocalMachine, @"SOFTWARE\ToDesk");
        if (!string.IsNullOrWhiteSpace(path)) return path;

        return GetInstPath(Registry.CurrentUser, @"SOFTWARE\WOW6432Node\ToDesk");
    }

    private static string? GetInstPath(RegistryKey root, string subKey)
    {
        using var key = root.OpenSubKey(subKey);
        return key?.GetValue("InstPath")?.ToString();
    }

    #endregion
}

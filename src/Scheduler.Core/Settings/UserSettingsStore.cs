using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Scheduler.Core.InputActivity;
using Scheduler.Core.Notifications;

namespace Scheduler.Core.Settings;

public sealed class UserSettingsFile
{
    public bool StartWithWindows { get; set; } = true;

    public NotificationOptions? Notifications { get; set; } = new();

    public InputActivityOptions? InputActivity { get; set; } = new();
}

public static class UserSettingsStore
{
    private const string RelativePath = @"Scheduler\settings.json";
    private static readonly Guid RoamingAppData = new("3EB685DB-65F9-4CF6-A03A-E3EF65729F3D");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string FilePath { get; } = ResolveSettingsFile();

    public static bool WasSeededThisRun { get; private set; }

    public static void EnsureSeeded(IConfiguration configuration)
    {
        if (File.Exists(FilePath))
            return;

        var settings = new UserSettingsFile
        {
            StartWithWindows = true,
            Notifications = configuration.GetSection(NotificationOptions.SectionName).Get<NotificationOptions>()
                            ?? new NotificationOptions(),
            InputActivity = configuration.GetSection(InputActivityOptions.SectionName).Get<InputActivityOptions>()
                            ?? new InputActivityOptions()
        };

        Save(settings);
        WasSeededThisRun = true;
    }

    public static UserSettingsFile Load()
    {
        if (!File.Exists(FilePath))
            return new UserSettingsFile();

        try
        {
            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<UserSettingsFile>(json, JsonOptions) ?? new UserSettingsFile();
        }
        catch
        {
            return new UserSettingsFile();
        }
    }

    public static void Save(UserSettingsFile settings)
    {
        var dir = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(FilePath, json);
    }

    /// <summary>
    /// 交互式程序继续使用自己的 AppData 目录。
    /// Session 0 服务从当前控制台用户读取同一文件，因此在程序里改的设置对服务同样生效。
    /// </summary>
    private static string ResolveSettingsFile()
    {
        var own = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), RelativePath);
        if (Environment.UserInteractive)
            return own;

        var roaming = TryConsoleUserRoamingAppData();
        if (!string.IsNullOrWhiteSpace(roaming))
            return Path.Combine(roaming, RelativePath);

        return DiscoverExistingSettings() ?? own;
    }

    private static string? TryConsoleUserRoamingAppData()
    {
        if (!OperatingSystem.IsWindows())
            return null;

        var sessionId = WTSGetActiveConsoleSessionId();
        if (sessionId == 0xFFFFFFFF)
            return null;
        if (!WTSQueryUserToken(sessionId, out var token))
            return null;

        try
        {
            var folder = RoamingAppData;
            if (SHGetKnownFolderPath(ref folder, 0, token, out var path) != 0 || path == IntPtr.Zero)
                return null;

            try
            {
                return Marshal.PtrToStringUni(path);
            }
            finally
            {
                Marshal.FreeCoTaskMem(path);
            }
        }
        finally
        {
            CloseHandle(token);
        }
    }

    private static string? DiscoverExistingSettings()
    {
        var root = Path.Combine(Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\", "Users");
        if (!Directory.Exists(root))
            return null;

        string? best = null;
        var bestTime = DateTime.MinValue;
        foreach (var dir in Directory.EnumerateDirectories(root))
        {
            var file = Path.Combine(dir, "AppData", "Roaming", RelativePath);
            if (!File.Exists(file))
                continue;

            var written = File.GetLastWriteTimeUtc(file);
            if (written < bestTime)
                continue;

            bestTime = written;
            best = file;
        }

        return best;
    }

    [DllImport("kernel32.dll")]
    private static extern uint WTSGetActiveConsoleSessionId();

    [DllImport("wtsapi32.dll", SetLastError = true)]
    private static extern bool WTSQueryUserToken(uint sessionId, out IntPtr token);

    [DllImport("shell32.dll")]
    private static extern int SHGetKnownFolderPath(ref Guid rfid, uint flags, IntPtr token, out IntPtr path);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);
}

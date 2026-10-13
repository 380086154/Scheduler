using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Scheduler.Common;
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
    /// 调试在 doc/data/notifications，发布在程序 data/notifications。
    /// 新位置还没有文件时，从旧路径复制一次。
    /// </summary>
    private static string ResolveSettingsFile()
    {
        var path = AppDataPaths.GetFile("notifications", "settings.json");
        if (File.Exists(path))
            return path;

        var legacy = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Scheduler",
            "settings.json");
        if (TryCopy(legacy, path))
            return path;

        TryCopy(AppDataPaths.GetFile("settings.json"), path);
        return path;
    }

    private static bool TryCopy(string source, string destination)
    {
        if (!File.Exists(source))
            return false;
        if (string.Equals(Path.GetFullPath(source), Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase))
            return true;

        try
        {
            File.Copy(source, destination);
            return true;
        }
        catch
        {
            return false;
        }
    }
}

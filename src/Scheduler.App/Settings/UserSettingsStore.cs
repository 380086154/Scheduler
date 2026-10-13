using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Scheduler.Core.InputActivity;
using Scheduler.Core.Notifications;

namespace Scheduler.App.Settings;

internal sealed class UserSettingsFile
{
    public bool StartWithWindows { get; set; } = true;

    public NotificationOptions? Notifications { get; set; } = new();

    public InputActivityOptions? InputActivity { get; set; } = new();
}

internal static class UserSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Scheduler",
        "settings.json");

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
}

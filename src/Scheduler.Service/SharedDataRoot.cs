using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32;
using Scheduler.Common;

namespace Scheduler.Service;

/// <summary>
/// 让服务使用托盘程序的 data 目录，避免另写一份 settings.json。
/// 托盘自己的路径规则不变。
/// </summary>
internal static class SharedDataRoot
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "Scheduler";

    public static void Apply()
    {
        var root = Resolve();
        if (!string.IsNullOrWhiteSpace(root))
            AppDataPaths.UseRoot(root);
    }

    private static string? Resolve()
    {
        if (DebugHelper.IsDebug)
        {
            var doc = FindDocData(AppContext.BaseDirectory);
            if (doc != null)
                return doc;
        }

        var exe = FindSchedulerExe();
        if (exe == null)
            return null;

        var exeDir = Path.GetDirectoryName(exe);
        if (string.IsNullOrWhiteSpace(exeDir))
            return null;

        if (exeDir.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}Debug{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
        {
            var doc = FindDocData(exeDir);
            if (doc != null)
                return doc;
        }

        return Path.Combine(exeDir, "data");
    }

    private static string? FindSchedulerExe()
    {
        var beside = Path.Combine(AppContext.BaseDirectory, "Scheduler.exe");
        if (File.Exists(beside))
            return beside;

        return NormalizeExe(ReadRunValue(Registry.CurrentUser))
               ?? NormalizeExe(ReadLoggedOnUserRunValue());
    }

    private static string? ReadRunValue(RegistryKey root)
    {
        using var key = root.OpenSubKey(RunKeyPath);
        return key?.GetValue(RunValueName) as string;
    }

    private static string? ReadLoggedOnUserRunValue()
    {
        var sessionId = WTSGetActiveConsoleSessionId();
        if (sessionId == 0xFFFFFFFF || !WTSQueryUserToken(sessionId, out var token))
            return null;

        try
        {
            using var identity = new WindowsIdentity(token);
            var sid = identity.User?.Value;
            if (string.IsNullOrWhiteSpace(sid))
                return null;

            using var user = Registry.Users.OpenSubKey(sid);
            return user == null ? null : ReadRunValue(user);
        }
        catch
        {
            return null;
        }
        finally
        {
            CloseHandle(token);
        }
    }

    private static string? NormalizeExe(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var path = value.Trim().Trim('"');
        return File.Exists(path) ? path : null;
    }

    private static string? FindDocData(string start)
    {
        for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
        {
            var docData = Path.Combine(dir.FullName, "doc", "data");
            if (Directory.Exists(docData))
                return docData;
        }

        return null;
    }

    [DllImport("kernel32.dll")]
    private static extern uint WTSGetActiveConsoleSessionId();

    [DllImport("wtsapi32.dll", SetLastError = true)]
    private static extern bool WTSQueryUserToken(uint sessionId, out IntPtr token);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);
}

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Acclaro.Platform.Common.Common;

public class SingleInstanceHelper : IDisposable
{
    // 锁文件路径，放在当前用户主目录，权限充足
    private readonly string _lockFile = Path.Combine(Path.GetTempPath(), "session_single.lock");

    private FileStream? _lockStream;
    private const int SW_SHOWNOMAL = 1;

    /// <summary>
    /// 检查单实例，重复启动则激活原有窗口
    /// </summary>
    /// <returns>true=正常启动，false=已有实例</returns>
    public bool CheckAndActivateInstance(bool activateRunningWindow = true)
    {
        try
        {
            // FileMode.OpenOrCreate：文件已存在直接抛 IOException，实现独占
            _lockStream = File.Open(_lockFile, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            return true;
        }
        catch (IOException)
        {
            // 锁文件存在 → 已有实例运行，激活窗口
            //ActivateRunningWindow(activateRunningWindow);
            return false;
        }
    }

    #region 跨平台激活窗口

    private static void ActivateRunningWindow(bool activateRunningWindow)
    {
        if (!activateRunningWindow) return;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            ActivateWindowWindows();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            ActivateWindowLinux();
        }
    }

    private static void ActivateWindowLinux()
    {
        string procName = Process.GetCurrentProcess().ProcessName;
        RunCommand("wmctrl -lp", out string output);

        foreach (var line in output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3) continue;

            if (int.TryParse(parts[2], out int pid))
            {
                try
                {
                    var p = Process.GetProcessById(pid);
                    if (p.ProcessName == procName)
                    {
                        string winId = parts[0];
                        RunCommand($"wmctrl -i -a {winId}", out _);
                        RunCommand($"xdotool windowactivate {winId}", out _);
                        break;
                    }
                }
                catch
                {
                }
            }
        }
    }

    private static void RunCommand(string cmd, out string result)
    {
        result = string.Empty;
        var psi = new ProcessStartInfo("bash", $"-c \"{cmd}\"")
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var p = Process.Start(psi);
        result = p.StandardOutput.ReadToEnd();
        p.WaitForExit();
    }

    #endregion

    #region Windows 激活窗口

    private static void ActivateWindowWindows()
    {
        using var current = Process.GetCurrentProcess();
        var processes = Process.GetProcessesByName(current.ProcessName);
        foreach (var p in processes)
        {
            if (p.Id == current.Id) continue;
            try
            {
                Win32Native.ShowWindowAsync(p.MainWindowHandle, SW_SHOWNOMAL);
                Win32Native.SetForegroundWindow(p.MainWindowHandle);
                break;
            }
            catch { }
        }
    }

    #endregion

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposing) return;

        _lockStream?.Close();
        try { File.Delete(_lockFile); } catch { }
    }
}

using System.Runtime.InteropServices;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Scheduler.App.Localization;
using Scheduler.Core.InputActivity;
using Scheduler.Core.Services;

namespace Scheduler.App.Ui;

/// <summary>用 Shell_NotifyIcon 挂托盘，避免再加载 WinForms。</summary>
internal sealed class TrayController : IDisposable
{
    private const int CallbackMessage = 0x8001;
    private const int WmLButtonDblClk = 0x0203;
    private const int WmRButtonUp = 0x0205;
    private const int WmNull = 0x0000;
    private const int NinSelect = 0x0400;
    private const int NinKeySelect = 0x0401;

    private const uint NimAdd = 0x0;
    private const uint NimModify = 0x1;
    private const uint NimDelete = 0x2;
    private const uint NifMessage = 0x1;
    private const uint NifIcon = 0x2;
    private const uint NifTip = 0x4;
    private const uint NifInfo = 0x10;
    private const uint NifShowTip = 0x80;

    private const uint MfString = 0x0;
    private const uint MfSeparator = 0x800;
    private const uint MfByCommand = 0x0;
    private const uint TpmRightButton = 0x2;
    private const uint TpmReturnCmd = 0x100;
    private const uint TpmNoNotify = 0x80;

    private const uint SettingsId = 1;
    private const uint RefreshId = 2;
    private const uint ExitId = 3;

    private readonly IServiceProvider _services;
    private readonly ILocalizationService _localization;
    private readonly Action _openSettings;
    private readonly Action _exit;
    private readonly WndProcDelegate _wndProc;
    private readonly IntPtr _hwnd;
    private readonly IntPtr _menu;
    private readonly DispatcherTimer _tooltipTimer;
    private bool _added;
    private string _tip = "Scheduler";

    public TrayController(IServiceProvider services, ILocalizationService localization, Action openSettings, Action exit)
    {
        _services = services;
        _localization = localization;
        _openSettings = openSettings;
        _exit = exit;
        _wndProc = WindowProc;
        _hwnd = CreateMessageWindow(_wndProc);
        _menu = NativeMethods.CreatePopupMenu();
        NativeMethods.AppendMenu(_menu, MfString, SettingsId, "");
        NativeMethods.AppendMenu(_menu, MfString, RefreshId, "");
        NativeMethods.AppendMenu(_menu, MfSeparator, 0, null);
        NativeMethods.AppendMenu(_menu, MfString, ExitId, "");

        _tooltipTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        _tooltipTimer.Tick += (_, _) => UpdateTooltip();

        ApplyLanguage();
        _localization.LanguageChanged += OnLanguageChanged;
    }

    public void Show()
    {
        UpdateTooltip();
        var data = CreateData(NifMessage | NifIcon | NifTip | NifShowTip);
        if (!NativeMethods.Shell_NotifyIcon(NimAdd, ref data))
            throw new InvalidOperationException("Could not add the tray icon.");

        _added = true;
        _tooltipTimer.Start();
    }

    public void ShowBalloon(string title, string text)
    {
        if (!_added) return;

        var data = CreateData(NifInfo | NifTip | NifShowTip);
        data.szInfoTitle = title;
        data.szInfo = text;
        data.uTimeoutOrVersion = 4000;
        NativeMethods.Shell_NotifyIcon(NimModify, ref data);
    }

    public void UpdateTooltip()
    {
        try
        {
            var presence = _services.GetRequiredService<SessionPresenceService>();
            var input = _services.GetRequiredService<InputActivityService>();
            var minutes = (int)presence.TodayAtOfficeDuration.TotalMinutes;
            var snap = input.GetTodaySnapshot();
            _tip = Truncate(_localization.Format(
                LocalizationKeys.TrayTooltip,
                minutes,
                snap.Keyboard,
                snap.MouseClicks));
        }
        catch
        {
            _tip = _localization.GetString(LocalizationKeys.AppName);
        }

        if (!_added) return;

        // 只带 NIF_TIP 时，Windows 10/11 会继续显示旧提示。带上 NIF_SHOWTIP，并一起提交图标，悬停文案才会换掉。
        var data = CreateData(NifIcon | NifTip | NifShowTip);
        NativeMethods.Shell_NotifyIcon(NimModify, ref data);
    }

    public void Dispose()
    {
        _tooltipTimer.Stop();
        _localization.LanguageChanged -= OnLanguageChanged;
        if (_added)
        {
            var data = CreateData(0);
            NativeMethods.Shell_NotifyIcon(NimDelete, ref data);
            _added = false;
        }

        if (_menu != IntPtr.Zero)
            NativeMethods.DestroyMenu(_menu);
        if (_hwnd != IntPtr.Zero)
            NativeMethods.DestroyWindow(_hwnd);
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => ApplyLanguage();

    private void ApplyLanguage()
    {
        SetMenuText(SettingsId, _localization.GetString(LocalizationKeys.TraySettings));
        SetMenuText(RefreshId, _localization.GetString(LocalizationKeys.TrayRefresh));
        SetMenuText(ExitId, _localization.GetString(LocalizationKeys.TrayExit));
        UpdateTooltip();
    }

    private void SetMenuText(uint id, string text) =>
        NativeMethods.ModifyMenu(_menu, id, MfByCommand | MfString, id, text);

    private NotifyIconData CreateData(uint flags)
    {
        return new NotifyIconData
        {
            cbSize = Marshal.SizeOf<NotifyIconData>(),
            hwnd = _hwnd,
            uID = 1,
            uFlags = flags,
            uCallbackMessage = CallbackMessage,
            hIcon = AppIconFactory.NotifyIcon.Handle,
            szTip = _tip,
            szInfo = "",
            szInfoTitle = ""
        };
    }

    private void ShowMenu()
    {
        if (!NativeMethods.GetCursorPos(out var point))
            return;

        NativeMethods.SetForegroundWindow(_hwnd);
        var command = NativeMethods.TrackPopupMenuEx(
            _menu,
            TpmRightButton | TpmReturnCmd | TpmNoNotify,
            point.X,
            point.Y,
            _hwnd,
            IntPtr.Zero);
        NativeMethods.PostMessage(_hwnd, WmNull, IntPtr.Zero, IntPtr.Zero);

        switch (command)
        {
            case SettingsId:
                _openSettings();
                break;
            case RefreshId:
                UpdateTooltip();
                break;
            case ExitId:
                _exit();
                break;
        }
    }

    private IntPtr WindowProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == CallbackMessage)
        {
            var mouse = lParam.ToInt32();
            if (mouse == WmLButtonDblClk)
                _openSettings();
            else if (mouse is WmRButtonUp or NinSelect or NinKeySelect)
                ShowMenu();
            return IntPtr.Zero;
        }

        return NativeMethods.DefWindowProc(hwnd, msg, wParam, lParam);
    }

    private static IntPtr CreateMessageWindow(WndProcDelegate wndProc)
    {
        var instance = NativeMethods.GetModuleHandle(null);
        var windowClass = new WndClass
        {
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(wndProc),
            hInstance = instance,
            lpszClassName = "Scheduler.Tray"
        };
        NativeMethods.RegisterClass(ref windowClass);
        var hwnd = NativeMethods.CreateWindowEx(
            0,
            windowClass.lpszClassName,
            "",
            0,
            0,
            0,
            0,
            0,
            new IntPtr(-3),
            IntPtr.Zero,
            instance,
            IntPtr.Zero);
        if (hwnd == IntPtr.Zero)
            throw new InvalidOperationException("Could not create the tray message window.");

        return hwnd;
    }

    private static string Truncate(string text) =>
        text.Length <= 127 ? text : text[..127];

    private delegate IntPtr WndProcDelegate(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WndClass
    {
        public uint style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        public int cbSize;
        public IntPtr hwnd;
        public int uID;
        public uint uFlags;
        public int uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public int dwState;
        public int dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public int uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public int dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    private static class NativeMethods
    {
        [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool Shell_NotifyIcon(uint message, ref NotifyIconData data);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern ushort RegisterClass(ref WndClass windowClass);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr CreateWindowEx(
            int exStyle,
            string className,
            string windowName,
            int style,
            int x,
            int y,
            int width,
            int height,
            IntPtr parent,
            IntPtr menu,
            IntPtr instance,
            IntPtr param);

        [DllImport("user32.dll")]
        public static extern IntPtr DefWindowProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool DestroyWindow(IntPtr hwnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr CreatePopupMenu();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern bool AppendMenu(IntPtr menu, uint flags, uint id, string? text);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern bool ModifyMenu(IntPtr menu, uint item, uint flags, uint id, string? text);

        [DllImport("user32.dll")]
        public static extern bool DestroyMenu(IntPtr menu);

        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out Point point);

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hwnd);

        [DllImport("user32.dll")]
        public static extern uint TrackPopupMenuEx(
            IntPtr menu,
            uint flags,
            int x,
            int y,
            IntPtr hwnd,
            IntPtr ignored);

        [DllImport("user32.dll")]
        public static extern bool PostMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr GetModuleHandle(string? module);
    }
}

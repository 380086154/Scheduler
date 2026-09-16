using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Scheduler.Common;

/// <summary>
/// Windows Win32 原生 API 静态封装类，仅 Windows 平台可用
/// </summary>
public static class Win32Native
{
    /// <summary>
    /// 根据窗口类名+窗口标题查找窗口句柄</summary>
    /// <param name="lpClassName">窗口注册类名，传null忽略类名匹配</param>
    /// <param name="lpWindowName">窗口标题文本，传null忽略标题匹配</param>
    /// <returns>匹配成功返回窗口句柄，无匹配返回IntPtr.Zero</returns>
    [DllImport("user32.dll")]
    public static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

    /// <summary>
    /// 同步设置窗口显示状态（最小化/最大化/还原/隐藏/显示）</summary>
    /// <param name="hWnd">目标窗口句柄</param>
    /// <param name="nCmdShow">显示状态常量，如SW_SHOW、SW_MINIMIZE、SW_MAXIMIZE、SW_RESTORE</param>
    /// <returns>窗口原本可见返回true，原本隐藏返回false</returns>
    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);


    /// <summary>
    /// 获取系统标准尺寸指标（屏幕宽高、标题栏高度、窗口边框大小等系统常量）</summary>
    /// <param name="nIndex">指标类型枚举值SM_*，对应不同系统尺寸</param>
    /// <returns>对应尺寸数值</returns>
    [DllImport("user32.dll")]
    public static extern int GetSystemMetrics(int nIndex);

    //private const int SM_XVIRTUALSCREEN = 76;
    //private const int SM_YVIRTUALSCREEN = 77;
    //private const int SM_CXVIRTUALSCREEN = 78;
    //private const int SM_CYVIRTUALSCREEN = 79;

    /// <summary>获取虚拟屏幕数据</summary>
    /// <returns>(Left, Top, Width, Height)</returns>
    public static (int, int, int, int) GetVirtualScreen() =>
        (GetSystemMetrics(76), GetSystemMetrics(77), 
        GetSystemMetrics(78), GetSystemMetrics(79));

    #region 全局钩子

    /// <summary>
    /// 底层键盘钩子回调委托，全局键盘钩子专用回调函数格式</summary>
    /// <param name="nCode">钩子处理码，>=0代表需要处理当前按键消息</param>
    /// <param name="wParam">按键消息类型：WM_KEYDOWN / WM_SYSKEYDOWN / WM_KEYUP / WM_SYSKEYUP</param>
    /// <param name="lParam">按键底层数据指针，可读取虚拟键码</param>
    /// <returns>返回1拦截按键消息，返回CallNextHookEx结果放行按键</returns>
    public delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    /// <summary>
    /// 安装全局底层键盘钩子，捕获系统全部键盘事件</summary>
    /// <param name="idHook">钩子类型，WH_KEYBOARD_LL=13代表底层键盘钩子</param>
    /// <param name="lpfn">键盘消息回调委托</param>
    /// <param name="hMod">当前进程模块句柄，LL钩子传当前程序模块句柄</param>
    /// <param name="dwThreadId">线程ID，全局钩子填0监听全部线程</param>
    /// <returns>钩子句柄，卸载时传入UnhookWindowsHookEx；失败返回0</returns>
    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    /// <summary>
    /// 卸载已安装的全局钩子，释放钩子资源</summary>
    /// <param name="hhk">SetWindowsHookEx返回的钩子句柄</param>
    /// <returns>卸载成功返回true，失败false，可通过GetLastError查看错误</returns>
    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnhookWindowsHookEx(IntPtr hhk);

    /// <summary>
    /// 将当前键盘消息传递给钩子链中下一个钩子过程
    /// 不调用此函数会阻断系统所有按键消息，必须正常放行不需要拦截的按键</summary>
    /// <param name="hhk">当前钩子句柄</param>
    /// <param name="nCode">钩子处理码</param>
    /// <param name="wParam">消息类型</param>
    /// <param name="lParam">按键数据指针</param>
    /// <returns>下一级钩子返回值</returns>
    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    /// <summary>
    /// 获取指定模块名称对应的模块句柄，用于底层钩子初始化</summary>
    /// <param name="lpModuleName">模块文件名（exe/dll名称），null获取当前进程主模块</param>
    /// <returns>模块句柄，失败返回IntPtr.Zero</returns>
    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern IntPtr GetModuleHandle(string? lpModuleName);

    //[DllImport("user32.dll")]
    //static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    /// <summary>
    /// 模拟键盘按键按下/释放，发送原生硬件按键事件
    /// 用于模拟ESC、功能键等系统按键，无法被普通钩子拦截</summary>
    /// <param name="bVk">虚拟键码VK_*常量，如VK_ESCAPE</param>
    /// <param name="bScan">硬件扫描码，填0自动适配</param>
    /// <param name="dwFlags">按键标识：0=按下，2=释放</param>
    /// <param name="dwExtraInfo">附加扩展信息，传0即可</param>
    [DllImport("user32.dll")]
    public static extern void keybd_event(byte bVk, byte bScan, int dwFlags, int dwExtraInfo);

    [DllImport("user32.dll")]
    public static extern short GetAsyncKeyState(int nVirtKey);

    #endregion

    #region SendInput

    [DllImport("user32.dll")]
    public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT
    {
        public uint type;
        public MOUSEKEYBDHARDWAREINPUT union;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct MOUSEKEYBDHARDWAREINPUT
    {
        [FieldOffset(0)]
        public MOUSEINPUT mi;
        [FieldOffset(0)]
        public KEYBDINPUT ki;
        [FieldOffset(0)]
        public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct HARDWAREINPUT
    {
        public uint uMsg;
        public ushort wParamL;
        public ushort wParamH;
    }

    public const uint INPUT_KEYBOARD = 1;
    public const uint KEYEVENTF_KEYUP = 0x0002;
    public const uint KEYEVENTF_SCANCODE = 0x0008;
    private const ushort VK_ESCAPE = 0x1B; // Esc 键

    public static void SendEscKey()
    {
        var inputs = new INPUT[2];

        // 按下 ESC
        inputs[0] = new INPUT
        {
            type = INPUT_KEYBOARD,
            union = new MOUSEKEYBDHARDWAREINPUT
            {
                ki = new KEYBDINPUT
                {
                    wVk = VK_ESCAPE
                }
            }
        };

        // 释放 ESC
        inputs[1] = new INPUT
        {
            type = INPUT_KEYBOARD,
            union = new MOUSEKEYBDHARDWAREINPUT
            {
                ki = new KEYBDINPUT
                {
                    wVk = VK_ESCAPE,
                    dwFlags = KEYEVENTF_KEYUP
                }
            }
        };

        SendInput(2, inputs, Marshal.SizeOf(typeof(INPUT)));

        Debug.WriteLine("按下 Esc 按键");
    }

    #endregion

    #region Windows 窗口激活API

    /// <summary>
    /// 该函数设置由不同线程产生的窗口的显示状态</summary>  
    /// <param name="hWnd">窗口句柄</param>  
    /// <param name="cmdShow">指定窗口如何显示。查看允许值列表</param>  
    /// <returns>如果窗口原来可见，返回值为非零；如果窗口原来被隐藏，返回值为零</returns>      
    [DllImport("User32.dll")]
    public static extern bool ShowWindowAsync(IntPtr hWnd, int cmdShow);

    /// <summary>  
    /// 将指定窗口提升至前台并激活，获取输入焦点
    /// 可把后台考试窗口强制置顶激活，抢占系统输入焦点</summary>  
    /// <param name="hWnd">将被激活并被调入前台的窗口句柄</param>  
    /// <returns>如果窗口设入了前台，返回值为非零；如果窗口未被设入前台，返回值为零</returns>  
    [DllImport("User32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    #endregion

    /// <summary>
    /// 调整进程工作集大小，强制 Windows 把进程当前空闲物理内存全部置换到虚拟内存 (页面文件)
    /// 瞬间把任务管理器「专用内存」数字压下去。仅优化内存显示数值，不释放堆对象</summary>
    [DllImport("kernel32.dll", EntryPoint = "SetProcessWorkingSetSize")]
    public static extern int SetProcessWorkingSetSize(IntPtr process, int minSize, int maxSize);


    //[DllImport("kernel32.dll")]
    //public static extern bool AllocConsole();

    //[DllImport("kernel32.dll")]
    //public static extern bool FreeConsole();
}

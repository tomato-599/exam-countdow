using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace ExamCountdown;

internal static class Native
{
    public const int GWL_EXSTYLE = -20;
    public const int WS_EX_TOOLWINDOW = 0x00000080;
    public const int WS_EX_NOACTIVATE = 0x08000000;

    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_NOZORDER = 0x0004;
    public const uint SWP_NOACTIVATE = 0x0010;
    public const uint SWP_SHOWWINDOW = 0x0040;

    public static readonly IntPtr HWND_TOPMOST = new(-1);
    public static readonly IntPtr HWND_NOTOPMOST = new(-2);

    public const int SW_HIDE = 0;
    public const int SW_SHOWNOACTIVATE = 4;

    public const uint IMAGE_ICON = 1;
    public const uint LR_LOADFROMFILE = 0x0010;
    public const uint LR_DEFAULTSIZE = 0x0040;

    public const uint MF_STRING = 0x0000;
    public const uint MF_GRAYED = 0x0001;
    public const uint MF_CHECKED = 0x0008;
    public const uint MF_SEPARATOR = 0x0800;

    public const uint TPM_RIGHTBUTTON = 0x0002;
    public const uint TPM_RETURNCMD = 0x0100;

    public const uint NIM_ADD = 0;
    public const uint NIM_MODIFY = 1;
    public const uint NIM_DELETE = 2;

    public const uint NIF_MESSAGE = 0x0001;
    public const uint NIF_ICON = 0x0002;
    public const uint NIF_TIP = 0x0004;
    public const uint NIF_INFO = 0x0010;

    public const uint NIIF_INFO = 0x0001;
    public const uint NIIF_WARNING = 0x0002;

    public const int WM_APP = 0x8000;
    public const uint WM_TRAY_CALLBACK = WM_APP + 1;
    public const int WM_LBUTTONUP = 0x0202;
    public const int WM_LBUTTONDBLCLK = 0x0203;
    public const int WM_RBUTTONUP = 0x0205;
    public const int WM_NULL = 0x0000;

    private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    public static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    public static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr value);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
        int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetCursorPos(out POINT point);

    [DllImport("user32.dll")]
    public static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetWindowTextLengthW")]
    public static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetWindowTextW")]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int maxCount);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr OpenProcess(uint access, bool inheritHandle, uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "QueryFullProcessImageNameW",
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool QueryFullProcessImageName(IntPtr process, uint flags,
        StringBuilder exeName, ref uint size);

    [DllImport("user32.dll")]
    public static extern int GetDpiForWindow(IntPtr hWnd);

    [DllImport("gdi32.dll")]
    public static extern IntPtr CreateRoundRectRgn(int left, int top, int right, int bottom,
        int widthEllipse, int heightEllipse);

    [DllImport("user32.dll")]
    public static extern int SetWindowRgn(IntPtr hWnd, IntPtr region, bool redraw);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DeleteObject(IntPtr handle);

    [DllImport("dwmapi.dll")]
    public static extern int DwmSetWindowAttribute(IntPtr hWnd, int attribute,
        ref int value, int size);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "LoadImageW")]
    public static extern IntPtr LoadImage(IntPtr instance, string name, uint type,
        int cx, int cy, uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DestroyIcon(IntPtr icon);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "CreateWindowExW")]
    public static extern IntPtr CreateWindowEx(uint exStyle, string className, string windowName,
        uint style, int x, int y, int width, int height, IntPtr parent, IntPtr menu,
        IntPtr instance, IntPtr param);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "RegisterClassExW")]
    public static extern ushort RegisterClassEx(ref WNDCLASSEX windowClass);

    [DllImport("user32.dll")]
    public static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetModuleHandleW")]
    public static extern IntPtr GetModuleHandle(string? name);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "Shell_NotifyIconW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool ShellNotifyIcon(uint message, ref NOTIFYICONDATAW data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "CreatePopupMenu")]
    public static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "AppendMenuW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool AppendMenu(IntPtr menu, uint flags, UIntPtr id, string? text);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DestroyMenu(IntPtr menu);

    [DllImport("user32.dll")]
    public static extern uint TrackPopupMenu(IntPtr menu, uint flags, int x, int y,
        int reserved, IntPtr hWnd, IntPtr rect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct WNDCLASSEX
    {
        public uint cbSize;
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
        public IntPtr hIconSm;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct NOTIFYICONDATAW
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public uint uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    public static bool IsLeftButtonDown() => (GetAsyncKeyState(0x01) & 0x8000) != 0;

    public static (int X, int Y) CursorPosition()
    {
        GetCursorPos(out var point);
        return (point.X, point.Y);
    }
}

public sealed class TrayMenuEntry
{
    public string Text { get; init; } = "";
    public int Id { get; init; }
    public bool Checked { get; init; }
    public bool Enabled { get; init; } = true;
    public bool Separator { get; init; }

    public static TrayMenuEntry Item(string text, int id, bool enabled = true, bool check = false)
        => new() { Text = text, Id = id, Enabled = enabled, Checked = check };

    public static TrayMenuEntry Line() => new() { Separator = true };
}

public sealed class TrayIcon : IDisposable
{
    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    private static TrayIcon? _current;
    private static readonly WndProcDelegate Proc = WindowProc;

    private readonly Func<List<TrayMenuEntry>> _buildMenu;
    private readonly Action _onClick;
    private readonly Action _onDoubleClick;
    private readonly Action<int> _onCommand;

    private readonly IntPtr _hwnd;
    private readonly IntPtr _icon;
    private readonly uint _id = 1;
    private string _tip = AppInfo.Title;
    private bool _disposed;

    public TrayIcon(Func<List<TrayMenuEntry>> buildMenu, Action onClick,
        Action onDoubleClick, Action<int> onCommand)
    {
        _buildMenu = buildMenu;
        _onClick = onClick;
        _onDoubleClick = onDoubleClick;
        _onCommand = onCommand;
        _current = this;

        var instance = Native.GetModuleHandle(null);
        var windowClass = new Native.WNDCLASSEX
        {
            cbSize = (uint)Marshal.SizeOf<Native.WNDCLASSEX>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(Proc),
            hInstance = instance,
            lpszClassName = "ExamCountdownTrayWindow",
        };
        Native.RegisterClassEx(ref windowClass);
        _hwnd = Native.CreateWindowEx(0, windowClass.lpszClassName, AppInfo.Title, 0,
            0, 0, 0, 0, IntPtr.Zero, IntPtr.Zero, instance, IntPtr.Zero);

        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "icon.ico");
        _icon = File.Exists(iconPath)
            ? Native.LoadImage(IntPtr.Zero, iconPath, Native.IMAGE_ICON, 0, 0,
                Native.LR_LOADFROMFILE | Native.LR_DEFAULTSIZE)
            : IntPtr.Zero;

        var data = CreateData();
        Native.ShellNotifyIcon(Native.NIM_ADD, ref data);
    }

    private Native.NOTIFYICONDATAW CreateData() => new()
    {
        cbSize = (uint)Marshal.SizeOf<Native.NOTIFYICONDATAW>(),
        hWnd = _hwnd,
        uID = _id,
        uFlags = Native.NIF_MESSAGE | Native.NIF_ICON | Native.NIF_TIP,
        uCallbackMessage = Native.WM_TRAY_CALLBACK,
        hIcon = _icon,
        szTip = _tip.Length > 120 ? _tip[..120] : _tip,
        szInfo = "",
        szInfoTitle = "",
    };

    private static IntPtr WindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        var self = _current;
        if (self != null && msg == Native.WM_TRAY_CALLBACK)
        {
            switch (lParam.ToInt32())
            {
                case Native.WM_LBUTTONUP:
                    self._onClick();
                    return IntPtr.Zero;
                case Native.WM_LBUTTONDBLCLK:
                    self._onDoubleClick();
                    return IntPtr.Zero;
                case Native.WM_RBUTTONUP:
                    self.ShowMenu();
                    return IntPtr.Zero;
            }
        }
        return Native.DefWindowProc(hWnd, msg, wParam, lParam);
    }

    private void ShowMenu()
    {
        var menu = Native.CreatePopupMenu();
        try
        {
            foreach (var entry in _buildMenu())
            {
                if (entry.Separator)
                {
                    Native.AppendMenu(menu, Native.MF_SEPARATOR, UIntPtr.Zero, null);
                    continue;
                }
                var flags = Native.MF_STRING;
                if (!entry.Enabled) flags |= Native.MF_GRAYED;
                if (entry.Checked) flags |= Native.MF_CHECKED;
                Native.AppendMenu(menu, flags, (UIntPtr)(uint)entry.Id, entry.Text);
            }

            Native.GetCursorPos(out var point);
            Native.SetForegroundWindow(_hwnd);
            var command = Native.TrackPopupMenu(menu,
                Native.TPM_RETURNCMD | Native.TPM_RIGHTBUTTON,
                point.X, point.Y, 0, _hwnd, IntPtr.Zero);
            Native.PostMessage(_hwnd, Native.WM_NULL, IntPtr.Zero, IntPtr.Zero);
            if (command != 0) _onCommand((int)command);
        }
        finally
        {
            Native.DestroyMenu(menu);
        }
    }

    public void SetToolTip(string text)
    {
        if (_disposed) return;
        _tip = text;
        var data = CreateData();
        Native.ShellNotifyIcon(Native.NIM_MODIFY, ref data);
    }

    public void ShowBalloon(string title, string text, bool warning = false)
    {
        if (_disposed) return;
        var data = CreateData();
        data.uFlags |= Native.NIF_INFO;
        data.szInfoTitle = title.Length > 60 ? title[..60] : title;
        data.szInfo = text.Length > 250 ? text[..250] : text;
        data.dwInfoFlags = warning ? Native.NIIF_WARNING : Native.NIIF_INFO;
        Native.ShellNotifyIcon(Native.NIM_MODIFY, ref data);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        var data = CreateData();
        Native.ShellNotifyIcon(Native.NIM_DELETE, ref data);
        if (_icon != IntPtr.Zero) Native.DestroyIcon(_icon);
        if (_hwnd != IntPtr.Zero) Native.DestroyWindow(_hwnd);
        if (ReferenceEquals(_current, this)) _current = null;
    }
}

public static class SystemIntegration
{
    private static Mutex? _mutex;

    public static bool AlreadyRunning()
    {
        try
        {
            _mutex = new Mutex(false, $@"Global\{AppInfo.Id}_Mutex", out var createdNew);
            return !createdNew;
        }
        catch
        {
            return false;
        }
    }

    public static string LaunchCommand() => $"\"{Environment.ProcessPath}\"";

    public static bool SetAutostart(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(AppInfo.RunKey);
            if (key == null) return false;
            if (enabled) key.SetValue(AppInfo.Id, LaunchCommand());
            else key.DeleteValue(AppInfo.Id, false);
            return true;
        }
        catch (Exception ex)
        {
            ConfigStore.LogError("设置开机自启动失败:\n" + ex);
            return false;
        }
    }

    public static string? AutostartCommand()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(AppInfo.RunKey);
            return key?.GetValue(AppInfo.Id)?.ToString();
        }
        catch
        {
            return null;
        }
    }

    public static void MakeCountdownWindow(IntPtr hwnd)
    {
        var style = Native.GetWindowLongPtr(hwnd, Native.GWL_EXSTYLE).ToInt64();
        style |= Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE;
        Native.SetWindowLongPtr(hwnd, Native.GWL_EXSTYLE, new IntPtr(style));
        Native.SetWindowPos(hwnd, Native.HWND_TOPMOST, 0, 0, 0, 0,
            Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);

        // 去掉系统绘制的窗口描边 / 阴影，形状完全由窗口区域决定（Win10 上不支持则忽略）
        try
        {
            var noBorder = unchecked((int)0xFFFFFFFE);
            Native.DwmSetWindowAttribute(hwnd, 34, ref noBorder, sizeof(int));
            var noRound = 1;
            Native.DwmSetWindowAttribute(hwnd, 33, ref noRound, sizeof(int));
            var noNcRender = 1;
            Native.DwmSetWindowAttribute(hwnd, 2, ref noNcRender, sizeof(int));
        }
        catch
        {
            // 旧系统没有这些 DWM 属性
        }
    }

    public static void RoundWindow(IntPtr hwnd, int width, int height, int radius)
    {
        if (width <= 2 || height <= 2) return;
        // 内缩 1px，裁掉窗口边缘露出的系统背景细线
        var region = Native.CreateRoundRectRgn(1, 1, width, height, radius, radius);
        if (Native.SetWindowRgn(hwnd, region, true) == 0)
            Native.DeleteObject(region);
    }

    public static double DpiScale(IntPtr hwnd)
    {
        var dpi = Native.GetDpiForWindow(hwnd);
        return dpi <= 0 ? 1.0 : dpi / 96.0;
    }

    public static string ForegroundProcessName()
    {
        try
        {
            var hwnd = Native.GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return "";
            Native.GetWindowThreadProcessId(hwnd, out var pid);
            if (pid == 0) return "";
            var handle = Native.OpenProcess(0x1000, false, pid);
            if (handle == IntPtr.Zero) return "";
            try
            {
                var buffer = new StringBuilder(1024);
                uint size = 1024;
                if (Native.QueryFullProcessImageName(handle, 0, buffer, ref size))
                    return Path.GetFileName(buffer.ToString()).ToLowerInvariant();
            }
            finally
            {
                Native.CloseHandle(handle);
            }
        }
        catch (Exception ex)
        {
            ConfigStore.LogError("前台进程检测失败:\n" + ex);
        }
        return "";
    }

    public static string ForegroundWindowTitle()
    {
        try
        {
            var hwnd = Native.GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return "";
            var length = Native.GetWindowTextLength(hwnd);
            if (length <= 0) return "";
            var buffer = new StringBuilder(length + 1);
            Native.GetWindowText(hwnd, buffer, length + 1);
            return buffer.ToString();
        }
        catch
        {
            return "";
        }
    }
}

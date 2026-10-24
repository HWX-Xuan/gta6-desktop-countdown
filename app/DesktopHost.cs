using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using Forms = System.Windows.Forms;

namespace Gta6Countdown;

/// <summary>
/// Places the frameless widget on Explorer's wallpaper layer (WorkerW), below the
/// desktop icons. Falls back to a bottom-most, click-through window if Explorer's
/// wallpaper host cannot be located.
/// </summary>
internal sealed class DesktopHost
{
    private const int GWL_STYLE = -16;
    private const int GWL_EXSTYLE = -20;

    private const long WS_CHILD = 0x40000000L;
    private const long WS_POPUP = unchecked((long)0x80000000);
    private const long WS_EX_TOOLWINDOW = 0x00000080L;
    private const long WS_EX_TRANSPARENT = 0x00000020L;
    private const long WS_EX_NOACTIVATE = 0x08000000L;
    private const long WS_EX_NOREDIRECTIONBITMAP = 0x00200000L;

    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_FRAMECHANGED = 0x0020;
    private const uint SWP_SHOWWINDOW = 0x0040;

    private const uint SMTO_NORMAL = 0x0000;

    private static readonly IntPtr HwndTop = IntPtr.Zero;
    private static readonly IntPtr HwndBottom = new(1);

    private IntPtr _hostWindow = IntPtr.Zero;

    public bool IsAttached
    {
        get
        {
            var hwnd = GetWidgetHandle();
            return hwnd != IntPtr.Zero && _hostWindow != IntPtr.Zero && GetParent(hwnd) == _hostWindow && IsWindow(_hostWindow);
        }
    }

    public IntPtr HostWindow => _hostWindow;

    public static void MakeNonActivating(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return;
        var style = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
        style |= WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TRANSPARENT;
        SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(style));
    }

    public bool Attach(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return false;

        MakeNonActivating(hwnd);
        var progman = FindWindow("Progman", null);

        // Windows 11 24H2+ ("raised desktop") keeps SHELLDLL_DefView as a layered
        // child of a no-redirection Progman. The only supported way in is to become
        // a WS_CHILD of Progman itself, in the slot between the icons and wallpaper.
        if (progman != IntPtr.Zero && IsRaisedDesktop(progman))
        {
            WidgetLog.Write("Raised desktop topology detected.");
            LogProgmanChildren(progman);

            var originalStyle = GetWindowLongPtr(hwnd, GWL_STYLE).ToInt64();
            var childStyle = (originalStyle & ~WS_POPUP) | WS_CHILD | 0x02000000L /* WS_CLIPCHILDREN */;
            SetWindowLongPtr(hwnd, GWL_STYLE, new IntPtr(childStyle));
            WidgetLog.Write($"Raised desktop: style 0x{originalStyle:X} -> 0x{childStyle:X}.");

            SetParent(hwnd, progman);
            if (GetParent(hwnd) == progman)
            {
                _hostWindow = progman;
                MountBelowDesktopIcons(hwnd, progman);
                ShowWindow(hwnd, 4 /* SW_SHOWNOACTIVATE */);
                WidgetLog.Write($"Attached widget hwnd=0x{hwnd.ToInt64():X} as a child of Progman on the raised desktop.");
                return true;
            }

            WidgetLog.Write(
                $"Raised-desktop SetParent to Progman failed: actual=0x{GetParent(hwnd).ToInt64():X}, error={Marshal.GetLastWin32Error()}.");
            SetWindowLongPtr(hwnd, GWL_STYLE, new IntPtr(originalStyle));
        }

        _hostWindow = FindWallpaperHost(progman, out var hostDescription);

        var candidates = new List<(IntPtr Handle, string Name)>();
        if (_hostWindow != IntPtr.Zero)
        {
            candidates.Add((_hostWindow, hostDescription));
        }

        // Progman is the safe second choice on Windows 11 builds where the
        // wallpaper WorkerW will not accept a cross-process child window.
        if (progman != IntPtr.Zero && !candidates.Any(c => c.Handle == progman))
        {
            candidates.Add((progman, "Progman"));
        }

        foreach (var candidate in candidates)
        {
            WidgetLog.Write(
                $"Trying parent=0x{candidate.Handle.ToInt64():X} ({candidate.Name}); " +
                $"widget style=0x{GetWindowLongPtr(hwnd, GWL_STYLE).ToInt64():X} exstyle=0x{GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64():X}.");
            SetParent(hwnd, candidate.Handle);
            var actualParent = GetParent(hwnd);
            if (actualParent == candidate.Handle)
            {
                _hostWindow = candidate.Handle;
                var isProgman = candidate.Handle == progman;
                ResizeToDesktop(hwnd, topOfHost: !isProgman);
                ShowWindow(hwnd, 4 /* SW_SHOWNOACTIVATE */);
                WidgetLog.Write(
                    $"Attached widget hwnd=0x{hwnd.ToInt64():X} to host=0x{candidate.Handle.ToInt64():X} ({candidate.Name}).");
                return true;
            }

            WidgetLog.Write(
                $"SetParent to 0x{candidate.Handle.ToInt64():X} failed: actual=0x{actualParent.ToInt64():X}, error={Marshal.GetLastWin32Error()}.");
        }

        // Fallback: stay at the bottom of the normal window stack, above the wallpaper.
        SetParent(hwnd, IntPtr.Zero);
        _hostWindow = IntPtr.Zero;
        ResizeToDesktop(hwnd, topOfHost: false);
        WidgetLog.Write($"Attached widget hwnd=0x{hwnd.ToInt64():X} as bottom-most desktop window.");
        return false;
    }

    public bool Reattach(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd != IntPtr.Zero)
        {
            SetParent(hwnd, IntPtr.Zero);
        }

        _hostWindow = IntPtr.Zero;
        return Attach(window);
    }

    public static bool SetDesktopIconsVisible(bool visible)
    {
        var progman = FindWindow("Progman", null);
        if (progman == IntPtr.Zero) return false;

        var shellView = FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
        if (shellView == IntPtr.Zero) return false;

        ShowWindow(shellView, visible ? 5 /* SW_SHOW */ : 0 /* SW_HIDE */);
        WidgetLog.Write($"Desktop icons visible={visible}.");
        return true;
    }

    private static IntPtr GetWidgetHandle()
    {
        // This is only used for validation; every call is cheap.
        if (System.Windows.Application.Current?.MainWindow is not { } window) return IntPtr.Zero;
        return new WindowInteropHelper(window).Handle;
    }

    private static bool IsRaisedDesktop(IntPtr progman)
    {
        var exStyle = GetWindowLongPtr(progman, GWL_EXSTYLE).ToInt64();
        return (exStyle & WS_EX_NOREDIRECTIONBITMAP) != 0 ||
               Environment.OSVersion.Version.Build >= 26100;
    }

    private static void MountBelowDesktopIcons(IntPtr hwnd, IntPtr progman)
    {
        var width = GetSystemMetrics(0);
        var height = GetSystemMetrics(1);
        var shellView = FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
        var shellWorker = FindWindowEx(progman, IntPtr.Zero, "WorkerW", null);

        // In the raised topology the icon view is layered above the wallpaper WorkerW.
        // Sitting immediately above that WorkerW puts us below the icons.
        var anchor = shellWorker != IntPtr.Zero ? shellWorker : HwndBottom;
        SetWindowPos(
            hwnd,
            anchor,
            0,
            0,
            width,
            height,
            SWP_NOACTIVATE | SWP_SHOWWINDOW | SWP_FRAMECHANGED);

        if (shellWorker != IntPtr.Zero)
        {
            SetWindowPos(
                shellWorker,
                HwndBottom,
                0,
                0,
                0,
                0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }

        WidgetLog.Write(
            $"Raised desktop mount: size={width}x{height}, shellView=0x{shellView.ToInt64():X}, " +
            $"wallpaperWorker=0x{shellWorker.ToInt64():X}.");
    }

    private static void LogProgmanChildren(IntPtr progman)
    {
        var child = IntPtr.Zero;
        var count = 0;
        while (count < 24 &&
               (child = FindWindowEx(progman, child, null, null)) != IntPtr.Zero)
        {
            WidgetLog.Write(
                $"Progman child: hwnd=0x{child.ToInt64():X} class={GetClassName(child)} " +
                $"style=0x{GetWindowLongPtr(child, GWL_STYLE).ToInt64():X} ex=0x{GetWindowLongPtr(child, GWL_EXSTYLE).ToInt64():X}");
            count++;
        }
    }

    private static void ResizeToDesktop(IntPtr hwnd, bool topOfHost)
    {
        // SetWindowPos uses physical pixels for this PerMonitorV2 process.
        var width = GetSystemMetrics(0 /* SM_CXSCREEN */);
        var height = GetSystemMetrics(1 /* SM_CYSCREEN */);
        if (width <= 0 || height <= 0)
        {
            width = (int)SystemParameters.PrimaryScreenWidth;
            height = (int)SystemParameters.PrimaryScreenHeight;
        }

        var insertAfter = topOfHost ? HwndTop : HwndBottom;
        SetWindowPos(
            hwnd,
            insertAfter,
            0,
            0,
            width,
            height,
            SWP_NOACTIVATE | SWP_SHOWWINDOW | SWP_FRAMECHANGED);
        WidgetLog.Write($"Resized widget to {width}x{height} (physical).");
    }

    private static IntPtr FindWallpaperHost(IntPtr progman, out string description)
    {
        if (progman != IntPtr.Zero)
        {
            // Ask Explorer to spawn its wallpaper WorkerW if it hasn't already.
            SendMessageTimeout(progman, 0x052C, IntPtr.Zero, IntPtr.Zero, SMTO_NORMAL, 1200, out _);
        }

        // Helpful when diagnosing unusual Explorer builds; only log desktop-ish windows.
        EnumWindows((top, _) =>
        {
            var className = GetClassName(top);
            if (className is "Progman" or "WorkerW")
            {
                var shellView = FindWindowEx(top, IntPtr.Zero, "SHELLDLL_DefView", null);
                WidgetLog.Write(
                    $"Desktop window: hwnd=0x{top.ToInt64():X} class={className} shellDefView=0x{shellView.ToInt64():X}");
            }

            return true;
        }, IntPtr.Zero);

        var wallpaperHost = IntPtr.Zero;
        var iconHost = IntPtr.Zero;

        EnumWindows((top, _) =>
        {
            var shellView = FindWindowEx(top, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (shellView == IntPtr.Zero) return true;

            iconHost = top;
            var nextWorker = FindWindowEx(IntPtr.Zero, top, "WorkerW", null);
            if (nextWorker != IntPtr.Zero)
            {
                wallpaperHost = nextWorker;
                return false;
            }

            return true;
        }, IntPtr.Zero);

        if (wallpaperHost != IntPtr.Zero)
        {
            description = $"WorkerW behind icons, icon host=0x{iconHost.ToInt64():X}";
            return wallpaperHost;
        }

        if (progman != IntPtr.Zero)
        {
            // Windows 11 sometimes keeps the wallpaper under Progman itself.
            var progmanWorker = FindWindowEx(progman, IntPtr.Zero, "WorkerW", null);
            if (progmanWorker != IntPtr.Zero)
            {
                description = "WorkerW child of Progman";
                return progmanWorker;
            }

            description = "Progman fallback";
            return progman;
        }

        description = "no Explorer host";
        return IntPtr.Zero;
    }

    private static string GetClassName(IntPtr hwnd)
    {
        var sb = new StringBuilder(256);
        GetClassName(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string? className, string? windowName);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr childAfter, string? className, string? windowName);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetParent(IntPtr hwnd, IntPtr newParent);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetParent(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr hwnd, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr hwnd, int command);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr newLong);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetClassName(IntPtr hwnd, StringBuilder className, int maxCount);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SendMessageTimeout(
        IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam, uint flags, uint timeout, out IntPtr result);
}

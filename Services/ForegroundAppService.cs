using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace CursorMagic.Services;

public sealed class ForegroundAppService
{
    public string GetForegroundProcessName()
    {
        var hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
        {
            return "";
        }

        NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);
        if (pid == 0)
        {
            return "";
        }

        try
        {
            return Process.GetProcessById((int)pid).ProcessName;
        }
        catch
        {
            return "";
        }
    }

    public bool IsForegroundFullscreen()
    {
        var hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }

        var processName = SettingsService.NormalizeProcessName(GetForegroundProcessName());
        if (processName is "explorer")
        {
            return false;
        }

        var className = GetWindowClassName(hwnd);
        if (className is "Progman" or "WorkerW" or "Shell_TrayWnd")
        {
            return false;
        }

        var style = NativeMethods.GetWindowLong(hwnd, NativeMethods.GwlStyle);
        var hasNormalWindowChrome = (style & NativeMethods.WsCaption) == NativeMethods.WsCaption
            || (style & NativeMethods.WsThickFrame) == NativeMethods.WsThickFrame;
        if (hasNormalWindowChrome)
        {
            return false;
        }

        if (!NativeMethods.GetWindowRect(hwnd, out var rect))
        {
            return false;
        }

        var monitor = NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MonitorDefaultToNearest);
        if (monitor == IntPtr.Zero)
        {
            return false;
        }

        var info = new NativeMethods.MonitorInfo();
        info.cbSize = Marshal.SizeOf<NativeMethods.MonitorInfo>();
        if (!NativeMethods.GetMonitorInfo(monitor, ref info))
        {
            return false;
        }

        var windowWidth = rect.Right - rect.Left;
        var windowHeight = rect.Bottom - rect.Top;
        var monitorWidth = info.rcMonitor.Right - info.rcMonitor.Left;
        var monitorHeight = info.rcMonitor.Bottom - info.rcMonitor.Top;

        return windowWidth >= monitorWidth && windowHeight >= monitorHeight
            && rect.Left <= info.rcMonitor.Left
            && rect.Top <= info.rcMonitor.Top;
    }

    private static string GetWindowClassName(IntPtr hwnd)
    {
        var builder = new StringBuilder(256);
        return NativeMethods.GetClassName(hwnd, builder, builder.Capacity) > 0
            ? builder.ToString()
            : "";
    }

    public bool IsBlockedForeground(IReadOnlyCollection<string> blockedProcessNames)
    {
        var foreground = SettingsService.NormalizeProcessName(GetForegroundProcessName());
        return !string.IsNullOrWhiteSpace(foreground)
            && blockedProcessNames.Any(name => SettingsService.NormalizeProcessName(name) == foreground);
    }

    private static class NativeMethods
    {
        public const uint MonitorDefaultToNearest = 2;
        public const int GwlStyle = -16;
        public const int WsCaption = 0x00C00000;
        public const int WsThickFrame = 0x00040000;

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowRect(IntPtr hWnd, out Rect rect);

        [DllImport("user32.dll")]
        public static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo lpmi);

        [StructLayout(LayoutKind.Sequential)]
        public struct Rect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct MonitorInfo
        {
            public int cbSize;
            public Rect rcMonitor;
            public Rect rcWork;
            public uint dwFlags;
        }
    }
}

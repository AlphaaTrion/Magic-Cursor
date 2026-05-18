using System.Runtime.InteropServices;

namespace CursorMagic.Services;

public static class DpiAwarenessService
{
    private static readonly IntPtr DpiAwarenessContextPerMonitorAwareV2 = new(-4);

    public static void EnablePerMonitorAwareness()
    {
        try
        {
            _ = NativeMethods.SetProcessDpiAwarenessContext(DpiAwarenessContextPerMonitorAwareV2);
        }
        catch
        {
            // Best effort only. WPF can still run if Windows has already selected a DPI mode.
        }
    }

    private static class NativeMethods
    {
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetProcessDpiAwarenessContext(IntPtr dpiContext);
    }
}

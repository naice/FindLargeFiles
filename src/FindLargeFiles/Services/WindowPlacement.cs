using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Win32;

namespace FindLargeFiles.Services;

/// <summary>
/// Persists a window's normal bounds and maximized state in HKCU\Software\FindLargeFiles\{name}.
/// Bounds are stored in physical screen pixels and only restored when they still fit completely
/// into the work area of one of the connected monitors (e.g. not after unplugging a screen).
/// </summary>
public static class WindowPlacement
{
    private const string RootKey = @"Software\FindLargeFiles";

    public static void Attach(Window window, string name)
    {
        var tracker = new Tracker(window, $@"{RootKey}\{name}");
        window.SourceInitialized += (_, _) => tracker.Restore();
        window.LocationChanged += (_, _) => tracker.Capture();
        window.SizeChanged += (_, _) => tracker.Capture();
        window.StateChanged += (_, _) => tracker.Capture();
        window.Closing += (_, _) => tracker.Save();
    }

    /// <summary>True when <paramref name="bounds"/> lies completely inside one of the work areas.</summary>
    public static bool Fits(Int32Rect bounds, IEnumerable<Int32Rect> workAreas) =>
        bounds.Width > 0 && bounds.Height > 0 && workAreas.Any(area =>
            bounds.X >= area.X && bounds.Y >= area.Y &&
            bounds.X + bounds.Width <= area.X + area.Width &&
            bounds.Y + bounds.Height <= area.Y + area.Height);

    public static IReadOnlyList<Int32Rect> MonitorWorkAreas()
    {
        var areas = new List<Int32Rect>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (monitor, _, _, _) =>
        {
            var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (GetMonitorInfo(monitor, ref info))
            {
                var w = info.rcWork;
                areas.Add(new Int32Rect(w.Left, w.Top, w.Right - w.Left, w.Bottom - w.Top));
            }

            return true;
        }, IntPtr.Zero);
        return areas;
    }

    private sealed class Tracker(Window window, string keyPath)
    {
        private Int32Rect? _normalBounds;
        private bool _maximized;

        private IntPtr Handle => new WindowInteropHelper(window).Handle;

        public void Restore()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(keyPath);
                if (key?.GetValue("Left") is not int left || key.GetValue("Top") is not int top ||
                    key.GetValue("Width") is not int width || key.GetValue("Height") is not int height)
                    return;

                var bounds = new Int32Rect(left, top, width, height);
                if (!Fits(bounds, MonitorWorkAreas())) return;

                window.WindowStartupLocation = WindowStartupLocation.Manual;
                SetWindowPos(Handle, IntPtr.Zero, left, top, width, height, SWP_NOZORDER | SWP_NOACTIVATE);
                _normalBounds = bounds;
                if (key.GetValue("Maximized") is 1) window.WindowState = WindowState.Maximized;
            }
            catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException)
            {
                // Missing or unreadable settings simply mean the default placement.
            }
        }

        /// <summary>Remembers the last normal (not maximized/minimized) bounds and the last visible state.</summary>
        public void Capture()
        {
            if (Handle == IntPtr.Zero || !window.IsLoaded) return;
            switch (window.WindowState)
            {
                case WindowState.Normal when GetWindowRect(Handle, out var r):
                    _normalBounds = new Int32Rect(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
                    _maximized = false;
                    break;
                case WindowState.Maximized:
                    _maximized = true;
                    break;
            }
        }

        public void Save()
        {
            Capture();
            if (_normalBounds is not { } b) return;
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(keyPath);
                key.SetValue("Left", b.X, RegistryValueKind.DWord);
                key.SetValue("Top", b.Y, RegistryValueKind.DWord);
                key.SetValue("Width", b.Width, RegistryValueKind.DWord);
                key.SetValue("Height", b.Height, RegistryValueKind.DWord);
                key.SetValue("Maximized", _maximized ? 1 : 0, RegistryValueKind.DWord);
            }
            catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException)
            {
                // Not being able to persist the placement must never block closing the app.
            }
        }
    }

    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    private delegate bool MonitorEnumProc(IntPtr monitor, IntPtr hdc, IntPtr rect, IntPtr data);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc callback, IntPtr data);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);
}

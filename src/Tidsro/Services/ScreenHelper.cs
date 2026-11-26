using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Tidsro.Services;

public static class ScreenHelper
{
    [StructLayout(LayoutKind.Sequential)] private struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO { public int cbSize; public RECT rcMonitor; public RECT rcWork; public uint dwFlags; }

    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
    [DllImport("user32.dll")] private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO mi);
    private const uint MONITOR_DEFAULTTONEAREST = 2;

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdc, IntPtr lprcMonitor, IntPtr data);
    [DllImport("user32.dll")] private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc proc, IntPtr data);
    [DllImport("user32.dll")] private static extern uint GetDpiForSystem();

    /// <summary>Working area (DIPs) of the monitor the window is on; primary work area as fallback.</summary>
    public static Rect WorkAreaForWindow(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd != IntPtr.Zero)
        {
            var mon = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (mon != IntPtr.Zero && GetMonitorInfo(mon, ref mi))
            {
                var dpi = VisualTreeHelper.GetDpi(window);
                var w = mi.rcWork;
                return new Rect(w.Left / dpi.DpiScaleX, w.Top / dpi.DpiScaleY,
                                (w.Right - w.Left) / dpi.DpiScaleX, (w.Bottom - w.Top) / dpi.DpiScaleY);
            }
        }
        return SystemParameters.WorkArea;
    }

    /// <summary>Work area (DIPs) of every monitor attached right now. Scaled by the system DPI, the
    /// scale SystemParameters uses, so the result lines up with a saved position the same way the old
    /// virtual-screen check did. If Windows lists no monitors, the virtual screen box stands in.</summary>
    public static IReadOnlyList<Rect> MonitorWorkAreas()
    {
        var scale = GetDpiForSystem() / 96.0;
        var areas = new List<Rect>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (mon, _, _, _) =>
        {
            var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (GetMonitorInfo(mon, ref mi))
            {
                var w = mi.rcWork;
                areas.Add(new Rect(w.Left / scale, w.Top / scale,
                                   (w.Right - w.Left) / scale, (w.Bottom - w.Top) / scale));
            }
            return true;
        }, IntPtr.Zero);
        if (areas.Count == 0)
            areas.Add(new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
                               SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight));
        return areas;
    }

    // How much of the title bar has to land on one monitor for the window to count as on screen:
    // the caption's full height, and enough width to grab it with the mouse.
    private const double TitleBarHeight = 30;
    private const double MinGrabWidth = 40;

    /// <summary>True when the title bar of a window at this position sits on a monitor's work area,
    /// far enough to grab. Each monitor is checked on its own, not the box around them all: next to a
    /// smaller monitor that box holds a strip that is on no screen at all.</summary>
    public static bool IsTitleBarOnScreen(double left, double top, double width, IEnumerable<Rect> workAreas)
    {
        var titleBar = new Rect(left, top, Math.Max(0, width), TitleBarHeight);
        foreach (var work in workAreas)
        {
            var visible = Rect.Intersect(titleBar, work);
            if (!visible.IsEmpty && visible.Width >= MinGrabWidth && visible.Height >= TitleBarHeight) return true;
        }
        return false;
    }

    /// <summary>Bottom-right position, clamped so the card can never land off-screen.</summary>
    public static Point ClampBottomRight(Rect work, Size card, double margin)
    {
        var x = work.Right - card.Width - margin;
        var y = work.Bottom - card.Height - margin;
        x = Math.Max(work.Left, Math.Min(x, work.Right - card.Width));
        y = Math.Max(work.Top, Math.Min(y, work.Bottom - card.Height));
        return new Point(x, y);
    }
}

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

    // The title bar, and the part of it the mouse can drag: everything left of the minimize, maximize
    // and close buttons (three 46-wide buttons at 100%). A window counts as on screen when at least
    // MinGrabWidth of that part, along the title bar's middle line, lies on one monitor.
    private const double TitleBarHeight = 30;
    private const double CaptionButtonsWidth = 138;
    private const double MinGrabWidth = 40;
    private const double Tolerance = 0.5;   // DIPs from a fractional scale never land exactly on a whole number

    /// <summary>True when the draggable part of the title bar of a window at this position sits on a
    /// monitor's work area, far enough to grab. Each monitor is checked on its own, not the box around
    /// them all: next to a smaller monitor that box holds a strip that is on no screen at all. The check
    /// runs along the title bar's middle line, so a title bar that crosses from one stacked monitor to
    /// the next still counts.</summary>
    public static bool IsTitleBarOnScreen(double left, double top, double width, IEnumerable<Rect> workAreas)
    {
        var y = top + TitleBarHeight / 2;
        var grabRight = left + Math.Max(0, width - CaptionButtonsWidth);
        foreach (var work in workAreas)
        {
            if (y < work.Top || y >= work.Bottom) continue;
            var visible = Math.Min(grabRight, work.Right) - Math.Max(left, work.Left);
            if (visible >= MinGrabWidth - Tolerance) return true;
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

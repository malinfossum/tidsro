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

    [DllImport("shcore.dll")] private static extern int GetDpiForMonitor(IntPtr hMonitor, int dpiType, out uint dpiX, out uint dpiY);
    private const int MDT_EFFECTIVE_DPI = 0;
    private const uint MONITORINFOF_PRIMARY = 1;

    /// <summary>Every monitor attached right now, in the order Windows lists them, with its rectangles in
    /// device pixels and its own scale. If Windows lists no monitors, the virtual screen box stands in at
    /// the system scale.</summary>
    public static IReadOnlyList<DisplayMonitor> Monitors()
    {
        var systemScale = GetDpiForSystem() / 96.0;
        var monitors = new List<DisplayMonitor>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (mon, _, _, _) =>
        {
            var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (GetMonitorInfo(mon, ref mi))
            {
                var scale = GetDpiForMonitor(mon, MDT_EFFECTIVE_DPI, out var dpiX, out _) == 0 && dpiX > 0
                    ? dpiX / 96.0
                    : systemScale;
                monitors.Add(new DisplayMonitor(ToRect(mi.rcMonitor), ToRect(mi.rcWork), scale,
                                                (mi.dwFlags & MONITORINFOF_PRIMARY) != 0));
            }
            return true;
        }, IntPtr.Zero);
        if (monitors.Count == 0)
        {
            var box = new Rect(SystemParameters.VirtualScreenLeft * systemScale, SystemParameters.VirtualScreenTop * systemScale,
                               SystemParameters.VirtualScreenWidth * systemScale, SystemParameters.VirtualScreenHeight * systemScale);
            monitors.Add(new DisplayMonitor(box, box, systemScale, IsPrimary: true));
        }
        return monitors;
    }

    private static Rect ToRect(RECT r) => new(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);

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
    /// the next still counts.
    ///
    /// <para>The position is in DIPs and the monitors are in device pixels, so the title bar is turned
    /// into device pixels first. A window about to open is placed at the scale WPF picks for a new
    /// window (see <see cref="PlacementScale"/>). A window already shown converts Left and Top at the
    /// scale of the monitor it is on, so pass that as <paramref name="scale"/>. With mixed scaling no
    /// single scale is right for every monitor.</para></summary>
    public static bool IsTitleBarOnScreen(double left, double top, double width, IReadOnlyList<DisplayMonitor> monitors,
        double? scale = null)
    {
        var s = scale ?? PlacementScale(left, top, monitors);
        var x = left * s;
        var y = (top + TitleBarHeight / 2) * s;
        var grabRight = (left + Math.Max(0, width - CaptionButtonsWidth)) * s;
        foreach (var monitor in monitors)
        {
            var work = monitor.Work;
            if (y < work.Top || y >= work.Bottom) continue;
            var visible = Math.Min(grabRight, work.Right) - Math.Max(x, work.Left);
            if (visible >= (MinGrabWidth - Tolerance) * s) return true;
        }
        return false;
    }

    /// <summary>The scale WPF opens a window at when its saved top-left corner is (left, top) in DIPs.
    /// WPF looks for the first monitor whose whole rectangle, divided by that monitor's own scale, holds
    /// the corner, and uses that monitor's scale (Window.WindowStartupTopLeftPointHelper in the WPF
    /// source). When no monitor holds it, the window starts on the primary monitor's scale.</summary>
    public static double PlacementScale(double left, double top, IReadOnlyList<DisplayMonitor> monitors)
    {
        var corner = new Point(left, top);
        foreach (var monitor in monitors)
            if (monitor.BoundsInDips.Contains(corner)) return monitor.Scale;
        foreach (var monitor in monitors)
            if (monitor.IsPrimary) return monitor.Scale;
        return monitors.Count > 0 ? monitors[0].Scale : 1.0;
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

/// <summary>One monitor as Windows reports it: the whole screen and its work area (the screen less the
/// taskbar) in device pixels, and its own display scale (1.5 at 150 %).</summary>
public readonly record struct DisplayMonitor(Rect Bounds, Rect Work, double Scale, bool IsPrimary = false)
{
    /// <summary>The whole screen divided by this monitor's own scale, the space WPF matches a saved
    /// top-left corner against.</summary>
    public Rect BoundsInDips => Divide(Bounds);

    /// <summary>The work area divided by this monitor's own scale: the DIPs a window on it measures in.</summary>
    public Rect WorkInDips => Divide(Work);

    private Rect Divide(Rect r) => new(r.X / Scale, r.Y / Scale, r.Width / Scale, r.Height / Scale);
}

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

    [StructLayout(LayoutKind.Sequential)] private struct POINT { public int X, Y; }
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT point);

    /// <summary>The mouse position in device pixels, or (0, 0), the primary monitor's corner, if Windows
    /// cannot say.</summary>
    public static Point CursorPosition() => GetCursorPos(out var p) ? new Point(p.X, p.Y) : new Point(0, 0);

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

    /// <summary>Where the main window opens at launch, in DIPs. A saved position whose title bar is on
    /// screen is kept as it is, size and all. Anything else (first run, an unplugged monitor) centers
    /// on the monitor under the mouse, as WPF's CenterScreen would, but shrunk to fit its work area
    /// first: CenterScreen does no clamping, so a saved height taller than the screen used to put the
    /// title bar above the top edge, where it cannot be dragged.
    ///
    /// <para>With mixed scaling WPF cannot open a window at every spot on a higher-scale monitor (see
    /// FitOn). When the window has no spot of its own there, it centers on the primary monitor instead,
    /// so it always opens where it was planned.</para></summary>
    public static Rect PlanLaunchPlacement(double? left, double? top, double width, double height,
        double maxTrackWidth, IReadOnlyList<DisplayMonitor> monitors, Point cursor)
    {
        if (left is double l && top is double t && IsTitleBarOnScreen(l, t, Math.Min(width, maxTrackWidth), monitors))
            return new Rect(l, t, width, height);

        if (monitors.Count == 0) return new Rect(0, 0, width, height);
        var target = MonitorAt(cursor, monitors);
        if (FitOn(target, width, height, monitors) is { } onTarget) return onTarget;
        var primary = Primary(monitors);
        if (FitOn(primary, width, height, monitors) is { } onPrimary) return onPrimary;
        return Centered(target.WorkInDips, width, height);   // nothing WPF can address exactly; still fits the target
    }

    // The window shrunk to fit this monitor's work area and centered on it, at a corner WPF will read at
    // this monitor's scale. Divided by their own scales, the boxes of monitors at different scales
    // overlap: a corner centered on a 150 % monitor to the right of a 100 % one can fall inside the
    // 100 % box, and WPF would open the window there instead. Such a corner moves the least it can to
    // just past the edge of each other monitor's box, while the window stays inside this work area.
    // Null when no such corner exists.
    private static Rect? FitOn(DisplayMonitor monitor, double width, double height, IReadOnlyList<DisplayMonitor> monitors)
    {
        var work = monitor.WorkInDips;
        var centered = Centered(work, width, height);
        var xs = new List<double> { centered.Left };
        var ys = new List<double> { centered.Top };
        foreach (var other in monitors)
        {
            xs.Add(other.BoundsInDips.Right + 1);    // + 1: a box holds its own right and bottom edges
            ys.Add(other.BoundsInDips.Bottom + 1);
        }

        Rect? best = null;
        var bestShift = double.MaxValue;
        foreach (var x in xs)
        {
            foreach (var y in ys)
            {
                if (x < work.Left || y < work.Top || x + centered.Width > work.Right || y + centered.Height > work.Bottom) continue;
                if (PlacementScale(x, y, monitors) != monitor.Scale) continue;
                var shift = Math.Abs(x - centered.Left) + Math.Abs(y - centered.Top);
                if (shift < bestShift) { bestShift = shift; best = new Rect(x, y, centered.Width, centered.Height); }
            }
        }
        return best;
    }

    private static Rect Centered(Rect work, double width, double height)
    {
        var w = Math.Min(width, work.Width);
        var h = Math.Min(height, work.Height);
        return new Rect(work.Left + (work.Width - w) / 2, work.Top + (work.Height - h) / 2, w, h);
    }

    private static DisplayMonitor Primary(IReadOnlyList<DisplayMonitor> monitors)
    {
        foreach (var monitor in monitors)
            if (monitor.IsPrimary) return monitor;
        return monitors[0];
    }

    // The monitor holding a point in device pixels, else the primary, else the first one listed. The
    // right and bottom edges belong to the next monitor, as they do for MonitorFromPoint.
    private static DisplayMonitor MonitorAt(Point devicePoint, IReadOnlyList<DisplayMonitor> monitors)
    {
        foreach (var monitor in monitors)
        {
            var b = monitor.Bounds;
            if (devicePoint.X >= b.Left && devicePoint.X < b.Right && devicePoint.Y >= b.Top && devicePoint.Y < b.Bottom)
                return monitor;
        }
        return Primary(monitors);
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

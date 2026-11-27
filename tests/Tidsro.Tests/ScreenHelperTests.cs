using System.Windows;
using Tidsro.Services;
using Xunit;

namespace Tidsro.Tests;

public class ScreenHelperTests
{
    [Fact]
    public void Clamp_places_card_bottom_right_with_margin()
    {
        var work = new Rect(0, 0, 1920, 1040);
        var p = ScreenHelper.ClampBottomRight(work, new Size(320, 120), 16);
        Assert.Equal(1920 - 320 - 16, p.X);
        Assert.Equal(1040 - 120 - 16, p.Y);
    }

    [Fact]
    public void Clamp_keeps_card_on_a_small_work_area()
    {
        // work area smaller than card + margin must not push the card off the left/top edge
        var work = new Rect(100, 100, 200, 80);
        var p = ScreenHelper.ClampBottomRight(work, new Size(320, 120), 16);
        Assert.True(p.X >= work.Left);
        Assert.True(p.Y >= work.Top);
        Assert.True(p.X + 320 >= work.Left);   // never fully off-screen
    }

    // ── The saved-position guard: the title bar must sit on a real monitor ──
    // A 1440p monitor on the left and a 1080p one on the right, tops aligned, taskbar 40 high on each.
    // The strip under the right monitor (y 1080 to 1440) is inside the virtual screen box but on no monitor.

    private static readonly DisplayMonitor[] UnequalPair =
    [
        new(new Rect(0, 0, 2560, 1440), new Rect(0, 0, 2560, 1400), 1.0, IsPrimary: true),
        new(new Rect(2560, 0, 1920, 1080), new Rect(2560, 0, 1920, 1040), 1.0),
    ];

    [Fact]
    public void A_title_bar_on_a_monitor_counts_as_on_screen()
    {
        Assert.True(ScreenHelper.IsTitleBarOnScreen(100, 100, 440, UnequalPair));
        Assert.True(ScreenHelper.IsTitleBarOnScreen(2700, 200, 440, UnequalPair));
    }

    [Fact]
    public void A_title_bar_in_the_gap_under_the_smaller_monitor_is_off_screen()
    {
        Assert.False(ScreenHelper.IsTitleBarOnScreen(3000, 1200, 440, UnequalPair));
    }

    [Fact]
    public void A_title_bar_under_the_taskbar_is_off_screen()
    {
        Assert.False(ScreenHelper.IsTitleBarOnScreen(3000, 1050, 440, UnequalPair));
    }

    [Fact]
    public void A_title_bar_past_every_edge_is_off_screen()
    {
        Assert.False(ScreenHelper.IsTitleBarOnScreen(5000, 100, 440, UnequalPair));
        Assert.False(ScreenHelper.IsTitleBarOnScreen(100, -200, 440, UnequalPair));
        Assert.False(ScreenHelper.IsTitleBarOnScreen(-1000, 100, 440, UnequalPair));
    }

    [Fact]
    public void A_window_across_two_monitors_is_on_screen()
    {
        // Mostly in the gap, but the left end of its title bar is on the big monitor and can be grabbed.
        Assert.True(ScreenHelper.IsTitleBarOnScreen(2400, 1200, 440, UnequalPair));
    }

    [Fact]
    public void A_window_snapped_to_the_left_edge_is_on_screen()
    {
        // Windows reports a snapped window a few pixels left of the edge (its invisible resize border).
        Assert.True(ScreenHelper.IsTitleBarOnScreen(-7, 0, 440, UnequalPair));
    }

    [Fact]
    public void A_title_bar_with_only_a_sliver_on_screen_is_off_screen()
    {
        Assert.False(ScreenHelper.IsTitleBarOnScreen(4470, 100, 440, UnequalPair));   // 10 wide on screen
    }

    [Fact]
    public void A_fractional_position_from_a_scaled_display_is_on_screen()
    {
        // At 125% a physical top of 11 is saved as 8.8 DIPs, which never adds up to a whole number.
        var scaled = new[] { new DisplayMonitor(new Rect(0, 0, 1920, 1080), new Rect(0, 0, 1920, 1040), 1.25, IsPrimary: true) };
        Assert.True(ScreenHelper.IsTitleBarOnScreen(100.4, 8.8, 440, scaled));
        Assert.True(ScreenHelper.IsTitleBarOnScreen(1495.8 + 0.2, 8.8, 440, scaled));   // just 40 left to grab
    }

    [Fact]
    public void A_window_with_only_its_caption_buttons_on_screen_is_off_screen()
    {
        // Minimize, maximize and close cannot be dragged, so they do not make the window reachable.
        Assert.False(ScreenHelper.IsTitleBarOnScreen(-350, 100, 440, UnequalPair));
    }

    [Fact]
    public void A_title_bar_across_two_stacked_monitors_is_on_screen()
    {
        var stacked = new[]
        {
            new DisplayMonitor(new Rect(0, 0, 1920, 1080), new Rect(0, 0, 1920, 1080), 1.0, IsPrimary: true),
            new DisplayMonitor(new Rect(0, 1080, 1920, 1080), new Rect(0, 1080, 1920, 1040), 1.0),
        };
        Assert.True(ScreenHelper.IsTitleBarOnScreen(100, 1065, 440, stacked));   // 15 on each
    }

    // ── Mixed display scaling: each monitor counts in its own scale ──
    // WPF saves a window's position in the scale of the monitor it is on, and on the next launch puts
    // it back on the first monitor whose rectangle, divided by its own scale, holds the top-left corner.
    // The check has to follow the same rule, in device pixels, or the two disagree.

    // A 1080p primary at 100 % on the left and a 4K monitor at 150 % on its right, tops aligned.
    private static readonly DisplayMonitor[] PrimaryAt100With4KAt150 =
    [
        new(new Rect(0, 0, 1920, 1080), new Rect(0, 0, 1920, 1040), 1.0, IsPrimary: true),
        new(new Rect(1920, 0, 3840, 2160), new Rect(1920, 0, 3840, 2100), 1.5),
    ];

    // A 4K primary at 150 % on the left and a 1080p monitor at 100 % on its right.
    private static readonly DisplayMonitor[] PrimaryAt150With1080pAt100 =
    [
        new(new Rect(0, 0, 3840, 2160), new Rect(0, 0, 3840, 2100), 1.5, IsPrimary: true),
        new(new Rect(3840, 0, 1920, 1080), new Rect(3840, 0, 1920, 1040), 1.0),
    ];

    [Fact]
    public void A_window_low_on_a_scaled_second_monitor_is_on_screen()
    {
        // Saved at 150 %: (1300, 1100) is device pixel (1950, 1650), well inside the 4K work area. Counted
        // in the primary's scale it looked like a spot under the 1080p monitor, so it was thrown away.
        Assert.True(ScreenHelper.IsTitleBarOnScreen(1300, 1100, 440, PrimaryAt100With4KAt150));
    }

    [Fact]
    public void A_window_past_the_right_edge_of_a_scaled_monitor_is_off_screen()
    {
        // Saved at 150 %: left 3830 is device pixel 5745, so only 15 pixels are left before the edge at
        // 5760. Counted in the primary's scale the title bar looked 302 wide and on screen.
        Assert.False(ScreenHelper.IsTitleBarOnScreen(3830, 200, 440, PrimaryAt100With4KAt150));
    }

    [Fact]
    public void A_window_on_an_unscaled_monitor_beside_a_scaled_primary_is_on_screen()
    {
        // Saved at 100 % on the right-hand monitor. Divided by the primary's 150 % that monitor shrank to
        // x 2560 to 3840, so every window on it was sent back to the center.
        Assert.True(ScreenHelper.IsTitleBarOnScreen(4000, 100, 440, PrimaryAt150With1080pAt100));
    }

    [Fact]
    public void A_window_already_on_screen_checks_a_new_corner_at_its_own_scale()
    {
        // A shown window converts Left and Top at the scale of the monitor it is on, not the way a new
        // window starts. On the 4K monitor, (1000, 1000) is device pixel (1500, 1500): below the 1080p
        // monitor, with only a few pixels on the 4K one. As a new window it would open on the 1080p one.
        Assert.False(ScreenHelper.IsTitleBarOnScreen(1000, 1000, 440, PrimaryAt100With4KAt150, scale: 1.5));
        Assert.True(ScreenHelper.IsTitleBarOnScreen(1000, 1000, 440, PrimaryAt100With4KAt150));
    }

    [Fact]
    public void Where_two_scaled_monitors_overlap_the_first_listed_one_sets_the_scale()
    {
        // Divided by their own scales the 1080p monitor covers x 0 to 1920 and the 4K one x 1280 to
        // 3840, so x 1500 is on both. WPF takes the first monitor Windows lists, and so does the check.
        Assert.Equal(1.0, ScreenHelper.PlacementScale(1500, 100, PrimaryAt100With4KAt150));
        Assert.Equal(1.5, ScreenHelper.PlacementScale(2000, 100, PrimaryAt100With4KAt150));
    }

    [Fact]
    public void A_corner_on_no_monitor_takes_the_primary_scale()
    {
        // A window snapped to the left edge reports a corner just left of every monitor.
        Assert.Equal(1.5, ScreenHelper.PlacementScale(-7, 0, PrimaryAt150With1080pAt100));
        Assert.True(ScreenHelper.IsTitleBarOnScreen(-7, 0, 440, PrimaryAt150With1080pAt100));
    }

    // ── Launch placement: a window that is not restored is centered and must fit ──

    private static readonly DisplayMonitor[] One1080p =
        [new(new Rect(0, 0, 1920, 1080), new Rect(0, 0, 1920, 1040), 1.0, IsPrimary: true)];

    private static readonly Point CursorOnPrimary = new(500, 500);

    [Fact]
    public void A_first_run_window_is_centered_on_the_work_area()
    {
        var plan = ScreenHelper.PlanLaunchPlacement(null, null, 440, 600, 4000, One1080p, CursorOnPrimary);
        Assert.Equal(new Rect(740, 220, 440, 600), plan);
    }

    [Fact]
    public void A_tall_window_with_an_off_screen_position_is_shortened_to_fit_with_its_title_bar_on_screen()
    {
        // Saved on a taller monitor that is gone. Centered at full height its top would be at -180.
        var plan = ScreenHelper.PlanLaunchPlacement(5000, 100, 440, 1400, 4000, One1080p, CursorOnPrimary);
        Assert.Equal(new Rect(740, 0, 440, 1040), plan);
    }

    [Fact]
    public void A_window_wider_than_the_work_area_is_narrowed_to_fit()
    {
        var plan = ScreenHelper.PlanLaunchPlacement(null, null, 2400, 600, 4000, One1080p, CursorOnPrimary);
        Assert.Equal(new Rect(0, 220, 1920, 600), plan);
    }

    [Fact]
    public void A_centered_window_fits_the_monitor_under_the_mouse_at_its_own_scale()
    {
        // The 4K monitor at 150 % is 2560 by 1400 DIPs once its taskbar is taken off.
        var cursorOn4K = new Point(3000, 500);
        var plan = ScreenHelper.PlanLaunchPlacement(null, null, 440, 1600, 4000, PrimaryAt100With4KAt150, cursorOn4K);
        Assert.Equal(new Rect(2340, 0, 440, 1400), plan);
    }

    // A 1080p primary at 100 % and a 1440p monitor at 150 % on its right. Divided by its scale the
    // 1440p one covers x 1280 to 2987, and the primary's box (x 0 to 1920) overlaps the left of it.
    private static readonly DisplayMonitor[] PrimaryAt100With1440pAt150 =
    [
        new(new Rect(0, 0, 1920, 1080), new Rect(0, 0, 1920, 1040), 1.0, IsPrimary: true),
        new(new Rect(1920, 0, 2560, 1440), new Rect(1920, 0, 2560, 1400), 1.5),
    ];

    [Fact]
    public void A_centered_corner_that_another_monitor_would_claim_moves_onto_the_target()
    {
        // Centered in the 1440p monitor's DIPs the corner is x 1913, inside the primary's box, so WPF
        // would open the window at device pixel 1913 on the primary's right edge. The corner moves the
        // least it can, to x 1921, which only the 1440p monitor holds: device pixels 2882 to 3542.
        var cursorOn1440p = new Point(3000, 500);
        var plan = ScreenHelper.PlanLaunchPlacement(null, null, 440, 600, 4000, PrimaryAt100With1440pAt150, cursorOn1440p);

        Assert.Equal(1921, plan.Left, 3);
        Assert.Equal((1400 / 1.5 - 600) / 2, plan.Top, 3);
        Assert.Equal(new Size(440, 600), plan.Size);
        Assert.Equal(1.5, ScreenHelper.PlacementScale(plan.Left, plan.Top, PrimaryAt100With1440pAt150));
    }

    [Fact]
    public void A_window_that_has_no_spot_of_its_own_on_the_target_opens_on_the_primary()
    {
        // 1500 wide does not fit between x 1921 and the 1440p monitor's right edge at 2987.
        var cursorOn1440p = new Point(3000, 500);
        var plan = ScreenHelper.PlanLaunchPlacement(null, null, 1500, 600, 4000, PrimaryAt100With1440pAt150, cursorOn1440p);

        Assert.Equal(new Rect(210, 220, 1500, 600), plan);
        Assert.Equal(1.0, ScreenHelper.PlacementScale(plan.Left, plan.Top, PrimaryAt100With1440pAt150));
    }

    [Fact]
    public void A_restored_window_keeps_its_position_and_size()
    {
        // Its title bar is on screen, so nothing moves. A window this tall can be a deliberate choice
        // on stacked monitors, so its height is left alone too.
        var stacked = new[]
        {
            new DisplayMonitor(new Rect(0, 0, 1920, 1080), new Rect(0, 0, 1920, 1080), 1.0, IsPrimary: true),
            new DisplayMonitor(new Rect(0, 1080, 1920, 1080), new Rect(0, 1080, 1920, 1040), 1.0),
        };
        var plan = ScreenHelper.PlanLaunchPlacement(100, 300, 440, 1600, 4000, stacked, CursorOnPrimary);
        Assert.Equal(new Rect(100, 300, 440, 1600), plan);
    }

    [Fact]
    public void No_monitors_means_off_screen()
    {
        Assert.False(ScreenHelper.IsTitleBarOnScreen(100, 100, 440, []));
    }
}

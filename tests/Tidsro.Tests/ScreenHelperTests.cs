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

    private static readonly Rect[] UnequalPair =
    [
        new Rect(0, 0, 2560, 1400),
        new Rect(2560, 0, 1920, 1040),
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
    public void No_monitors_means_off_screen()
    {
        Assert.False(ScreenHelper.IsTitleBarOnScreen(100, 100, 440, []));
    }
}

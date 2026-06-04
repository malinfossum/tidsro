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
}

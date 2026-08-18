using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Tidsro.Models;

namespace Tidsro.Views;

public sealed class NullToCollapsedConverter : IValueConverter
{
    public object Convert(object? v, Type t, object? p, CultureInfo c) =>
        v is null || (v is string s && s.Length == 0) ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}

public sealed class BoolToVisibleConverter : IValueConverter
{
    public object Convert(object? v, Type t, object? p, CultureInfo c) =>
        v is true ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}

/// <summary>The inverse of <see cref="BoolToVisibleConverter"/>: true collapses. Used to drop the
/// big numerals from the one running-timer row whose countdown the hero card is already showing.</summary>
public sealed class BoolToCollapsedConverter : IValueConverter
{
    public object Convert(object? v, Type t, object? p, CultureInfo c) =>
        v is true ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}

public sealed class SoundChoiceToLabelConverter : IValueConverter
{
    public object Convert(object? v, Type t, object? p, CultureInfo c) => v switch
    {
        SoundChoice.None => "Silent",
        SoundChoice.SoftChime => "Soft chime",
        SoundChoice.Marimba => "Marimba",
        SoundChoice.Bell => "Bell",
        SoundChoice.PianoJingle => "Piano jingle",
        SoundChoice.ElectricPianoJingle => "Electric piano jingle",
        SoundChoice.BellJingle => "Bell jingle",
        _ => "",
    };
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}

public sealed class BoolToSoundGlyphConverter : IValueConverter
{
    // Segoe Fluent Icons code points, built from char codes so the source stays plain ASCII
    // (same pattern as TimerItemViewModel): Volume (0xE767) when audible, Mute (0xE74F) when silent.
    private static readonly string Volume = ((char)0xE767).ToString();
    private static readonly string Mute = ((char)0xE74F).ToString();

    public object Convert(object? v, Type t, object? p, CultureInfo c) => v is true ? Volume : Mute;
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}

/// <summary>Show a shell panel when its own index (ConverterParameter) matches the selected tab.
/// Both panels stay loaded so switching tabs cannot re-run their Loaded fade-in storyboards or lose
/// their scroll position — only visibility changes.</summary>
public sealed class IndexToVisibleConverter : IValueConverter
{
    public object Convert(object? v, Type t, object? p, CultureInfo c) =>
        v is int selected
        && p is string s
        && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var own)
        && selected == own
            ? Visibility.Visible
            : Visibility.Collapsed;

    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}

/// <summary>The content measure for a tab panel, from the width available to it.
/// A fixed MaxWidth made a wide window feel like it was ignoring the resize: the column froze at
/// 502 while the page grew around it. Instead the column takes a steady share of the window once
/// there is room to spare - never narrower than Floor (the width the composer form wants plus its
/// card padding), never wider than Ceiling (past which the alarm rows read as stretched again).
/// Below the floor the clamp returns Floor, which is wider than what is available, so the panel
/// simply fills the window exactly as it did before the measure existed.</summary>
public sealed class WidthToMeasureConverter : IValueConverter
{
    public const double Floor = 502;
    public const double Ceiling = 720;
    public const double Share = 0.66;

    public object Convert(object? v, Type t, object? p, CultureInfo c) =>
        v is double available && !double.IsNaN(available)
            ? Math.Clamp(available * Share, Floor, Ceiling)
            : Floor;

    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}

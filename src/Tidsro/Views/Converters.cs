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

public sealed class SoundChoiceToLabelConverter : IValueConverter
{
    public object Convert(object? v, Type t, object? p, CultureInfo c) => v switch
    {
        SoundChoice.None => "Silent",
        SoundChoice.SoftChime => "Soft chime",
        SoundChoice.Marimba => "Marimba",
        SoundChoice.Bell => "Bell",
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

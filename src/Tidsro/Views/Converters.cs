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

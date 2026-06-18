using CommunityToolkit.Mvvm.ComponentModel;
using Tidsro.Models;

namespace Tidsro.ViewModels;

/// <summary>One day toggle in the editor's Custom day picker. Carries its flag and a full name for accessibility.</summary>
public partial class DayToggleViewModel : ObservableObject
{
    public Weekdays Flag { get; }
    public string Short { get; }   // "M", "T", "W", ...
    public string Full { get; }    // "Monday", ...

    [ObservableProperty] private bool _isSelected;

    public DayToggleViewModel(Weekdays flag, string shortLabel, string full)
    {
        Flag = flag;
        Short = shortLabel;
        Full = full;
    }

    public static IReadOnlyList<DayToggleViewModel> Week() => new[]
    {
        new DayToggleViewModel(Weekdays.Mon, "M", "Monday"),
        new DayToggleViewModel(Weekdays.Tue, "T", "Tuesday"),
        new DayToggleViewModel(Weekdays.Wed, "W", "Wednesday"),
        new DayToggleViewModel(Weekdays.Thu, "T", "Thursday"),
        new DayToggleViewModel(Weekdays.Fri, "F", "Friday"),
        new DayToggleViewModel(Weekdays.Sat, "S", "Saturday"),
        new DayToggleViewModel(Weekdays.Sun, "S", "Sunday"),
    };
}

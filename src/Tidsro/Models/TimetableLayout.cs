namespace Tidsro.Models;

/// <summary>One alarm placed in the week. Layout data only — no pixels, no view concerns.</summary>
public sealed record TimetableEntry(
    Guid Id, string? Label, string DayName, int Hour, int Minute, SoundChoice Sound, bool IsEnabled, int SlotIndex)
{
    public string TimeText => $"{Hour:D2}:{Minute:D2}";

    /// <summary>Whether this entry starts exactly when its slot does. It decides whether the cell
    /// prints a time, but only in a row that holds more than one start — see
    /// <see cref="TimetableRow.ShowsCellTimes"/>. A row where every entry is at 12:15 says so in its
    /// gutter and the cells stay bare; a row holding both 12:00 and 12:15 cannot, so there the
    /// off-boundary entries print their own time.</summary>
    public bool IsOnSlotBoundary => Minute % TimetableLayout.SlotMinutes == 0;

    /// <summary>The label as it is drawn and announced. The add form permits an alarm with no label,
    /// so the raw <see cref="Label"/> can be null or blank — which would draw an empty box and
    /// announce with a leading comma (", Monday, 09:00"). "No label" is the same stand-in the
    /// Schedule tab's rows already use, so the two tabs name an unlabelled alarm identically.</summary>
    public string DisplayLabel => string.IsNullOrWhiteSpace(Label) ? "No label" : Label!;

    /// <summary>What a screen reader reads for this row. Carries the weekday, because the grid
    /// rendering is reached by widening the window and its column headers are easy to navigate past;
    /// and carries the off state, which is otherwise encoded only by dimming.</summary>
    public string AccessibleName => IsEnabled
        ? $"{DisplayLabel}, {DayName}, {TimeText}"
        : $"{DisplayLabel}, {DayName}, {TimeText}, off";
}

/// <summary>One row of the vertical axis: a 30-minute band starting at Hour:Minute.</summary>
public sealed record TimetableSlot(int Index, int Hour, int Minute)
{
    public bool IsWholeHour => Minute == 0;
    public string Label => $"{Hour:D2}:{Minute:D2}";
}

/// <summary>One weekday column, Monday first.</summary>
public sealed record TimetableDay(Weekdays Day, string Name, bool IsToday, IReadOnlyList<TimetableEntry> Entries)
{
    /// <summary>Names the column for a screen reader. Without it the wide grid is navigable but
    /// structureless — the rendering is chosen by window width, which is a poor proxy for eyesight,
    /// so the grid has to stand on its own.</summary>
    public string AccessibleName
    {
        get
        {
            var count = Entries.Count switch
            {
                0 => "no alarms",
                1 => "1 alarm",
                var n => $"{n} alarms",
            };
            return IsToday ? $"{Name}, today, {count}" : $"{Name}, {count}";
        }
    }

    /// <summary>Whether the agenda draws this day at all. Days with nothing on them are noise — an
    /// empty Tuesday heading tells you nothing — but today is the exception: a week view that
    /// silently omits the day you are standing in is worse than one empty heading, and the agenda is
    /// the rendering most people actually see (the window opens at 440px).</summary>
    public bool ShowInAgenda => Entries.Count > 0 || IsToday;

    /// <summary>True only for a today with nothing on it, which is the one case that needs a line of
    /// its own rather than a bare heading.</summary>
    public bool IsFreeToday => IsToday && Entries.Count == 0;
}

/// <summary>One day's share of one slot: the entries that fall in this half hour on this weekday.
/// Usually empty, occasionally one, rarely two.</summary>
public sealed record TimetableCell(Weekdays Day, string DayName, IReadOnlyList<TimetableEntry> Entries);

/// <summary>One horizontal band of the wide grid: a slot that has something on it, and one cell per
/// column beside it, Monday first — five of them on a week with a free weekend, seven otherwise.
/// Row-major on purpose — see <see cref="TimetableLayout"/>.</summary>
public sealed record TimetableRow(TimetableSlot Slot, IReadOnlyList<TimetableCell> Cells)
{
    /// <summary>The one time every entry in this row starts at, or null when they differ. Walked
    /// rather than cached: a row holds a handful of entries, and a cached field would be copied
    /// stale by the record's own <c>with</c>.</summary>
    private string? SharedTime
    {
        get
        {
            string? shared = null;
            foreach (var cell in Cells)
                foreach (var entry in cell.Entries)
                {
                    if (shared is null) shared = entry.TimeText;
                    else if (shared != entry.TimeText) return null;
                }
            return shared;
        }
    }

    /// <summary>What the row's gutter states. The slot is a half-hour BAND, so naming the row after
    /// it would read a 12:15 alarm as 12:00. When the whole row starts at one time — which is the
    /// ordinary case for a week of recurring alarms, where a class repeats at the same time on
    /// several days — the gutter states that time instead. Only a row of mixed starts falls back to
    /// the band, and then the cells speak for themselves.</summary>
    public string GutterLabel => SharedTime ?? Slot.Label;

    /// <summary>Whether the cells in this row print their own times. False whenever the gutter has
    /// already said it, which keeps a time out of a hundred-pixel column where it would push the
    /// label into an ellipsis.</summary>
    public bool ShowsCellTimes => SharedTime is null;

    /// <summary>A belt-and-braces guard, not a display value. The grid's row containers carry no
    /// AutomationProperties.Name on purpose — the entries inside them are what is named — but a WPF
    /// item container that does end up with a peer names itself from ToString(), and a record's
    /// generated ToString() would read out the entire row. If that fallback ever fires, it should
    /// say the time the row states, which is at least true.</summary>
    public override string ToString() => GutterLabel;
}

/// <summary>The whole projected week. Immutable; rebuilt rather than mutated.</summary>
public sealed record TimetableWeek(
    bool IsEmpty,
    IReadOnlyList<TimetableSlot> Slots,
    IReadOnlyList<TimetableDay> Days,
    IReadOnlyList<TimetableDay> GridDays,
    IReadOnlyList<TimetableRow> Rows)
{
    /// <summary>Whether the grid left the weekend out, which is what the line under it answers to.
    /// Read off the two collections rather than stored: <see cref="GridDays"/> is
    /// <see cref="Days"/> minus the weekend or nothing at all, so there is no third state to keep
    /// in step.</summary>
    public bool HidesWeekend => GridDays.Count < Days.Count;
}

/// <summary>
/// Projects recurring alarms into a Monday-first week grid. Pure: no state, no I/O, and the current
/// time arrives as a parameter — the same shape as <see cref="RecurrenceRules"/>.
///
/// <para><b>Build is total.</b> data.json is user-writable and importable, so this never throws for any
/// input. An entry that cannot be placed is skipped and the rest of the week still renders. That is a
/// deliberate departure from RecurrenceRules.NextOccurrence, which throws on an empty day set: right
/// for arming an alarm, wrong for drawing one, where one bad row would otherwise reach the global
/// exception handler and read as "Tidsro is broken".</para>
///
/// <para><b>Two shapes of the same week.</b> <c>Days</c> is column-major (a weekday and everything on
/// it) and drives the agenda. <c>Rows</c> is row-major (a slot and its seven cells) and drives the
/// wide grid, where a gutter label and the seven day cells at the same half hour have to sit on one
/// line. Bucketing here rather than in XAML is what makes that alignment structural: the grid draws
/// one element per row and the gutter is part of it, so nothing can drift. It is also what keeps the
/// arithmetic testable — see the rejected "layout maths in XAML converters" in the spec.</para>
/// </summary>
public static class TimetableLayout
{
    public const int SlotMinutes = 30;
    public const int MinimumSpanMinutes = 6 * 60;
    private const int PadMinutes = 60;
    private const int DayMinutes = 24 * 60;

    private static readonly (Weekdays Flag, string Name)[] Week =
    {
        (Weekdays.Mon, "Monday"), (Weekdays.Tue, "Tuesday"), (Weekdays.Wed, "Wednesday"),
        (Weekdays.Thu, "Thursday"), (Weekdays.Fri, "Friday"), (Weekdays.Sat, "Saturday"),
        (Weekdays.Sun, "Sunday"),
    };

    public static TimetableWeek Build(IEnumerable<TimerItem>? alarms, DateTimeOffset now)
    {
        var usable = Collect(alarms);
        if (usable.Count == 0) return Empty(now);

        var (startMinutes, slotCount) = ResolveSpan(usable);
        var slots = BuildSlots(startMinutes, slotCount);
        var today = DayFlag(now.DayOfWeek);

        var days = new List<TimetableDay>(Week.Length);
        foreach (var (flag, name) in Week)
        {
            var entries = usable
                .Where(u => (u.Days & flag) != 0)
                .OrderBy(u => u.Minutes)
                .ThenBy(u => u.Label, StringComparer.Ordinal)
                .ThenBy(u => u.Id)
                .Select(u => new TimetableEntry(
                    u.Id, u.Label, name, u.Minutes / 60, u.Minutes % 60, u.Sound, u.IsEnabled,
                    (FloorToSlot(u.Minutes) - startMinutes) / SlotMinutes))
                .ToList();
            days.Add(new TimetableDay(flag, name, flag == today, entries));
        }

        var gridDays = ResolveGridDays(days);
        return new TimetableWeek(IsEmpty: false, slots, days, gridDays, BuildRows(slots, gridDays));
    }

    /// <summary>Which weekdays the grid gives a column to. Saturday and Sunday cost two sevenths of
    /// the width between them, and on a term timetable they are usually two empty columns — so they
    /// are dropped unless there is something to show. Two exceptions, both the same principle: an
    /// alarm on either weekend day brings BOTH columns back, so a lone Sunday never turns up on its
    /// own at the end of a row; and today always keeps its column, because a week view that omits
    /// the day you are standing in is worse than an empty column (the agenda makes the same
    /// exception in <see cref="TimetableDay.ShowInAgenda"/>). What is dropped is said in words
    /// beneath the grid — see <see cref="TimetableWeek.HidesWeekend"/> — never silently.</summary>
    private static List<TimetableDay> ResolveGridDays(List<TimetableDay> days)
    {
        static bool IsWeekend(TimetableDay d) => d.Day is Weekdays.Sat or Weekdays.Sun;

        return days.Any(d => IsWeekend(d) && (d.Entries.Count > 0 || d.IsToday))
            ? days
            : days.Where(d => !IsWeekend(d)).ToList();
    }

    /// <summary>Turn the seven day columns inside out into rows the wide grid draws directly, so a
    /// row's gutter label and its seven cells are one element and cannot fall out of line with each
    /// other.
    ///
    /// <para><b>Only the slots that have something on them get a row.</b> An empty half hour is not
    /// drawn, thinned, or collapsed into a band that states its own length — it is simply absent, so
    /// the week reads as the list of times it has something on, evenly spaced. The vertical scale is
    /// deliberately not proportional: 07:00 and 15:00 sit next to each other when nothing falls
    /// between them, exactly as a printed timetable lists its periods.</para></summary>
    private static List<TimetableRow> BuildRows(List<TimetableSlot> slots, List<TimetableDay> days)
    {
        // One pass per day, not one scan per cell: 48 slots x 7 days would otherwise re-walk the
        // day's entries 48 times for a week that usually has a handful.
        var bySlot = days.Select(d => d.Entries.ToLookup(e => e.SlotIndex)).ToList();

        var rows = new List<TimetableRow>();
        foreach (var slot in slots)
        {
            var occupied = false;
            for (var i = 0; i < days.Count && !occupied; i++) occupied = bySlot[i].Contains(slot.Index);
            if (!occupied) continue;

            var cells = new List<TimetableCell>(days.Count);
            for (var i = 0; i < days.Count; i++)
                cells.Add(new TimetableCell(days[i].Day, days[i].Name, bySlot[i][slot.Index].ToList()));
            rows.Add(new TimetableRow(slot, cells));
        }

        return rows;
    }

    private readonly record struct Placed(
        Guid Id, string? Label, int Minutes, SoundChoice Sound, bool IsEnabled, Weekdays Days);

    private static List<Placed> Collect(IEnumerable<TimerItem>? alarms)
    {
        var usable = new List<Placed>();
        if (alarms is null) return usable;

        foreach (var a in alarms)
        {
            if (a is null) continue;                              // a null element must not take the tab down
            if (a.RecurringDays is not { } raw) continue;         // one-shots and countdowns are not timetable rows
            var days = raw & RecurrenceRules.AllDays;             // strip unknown bits, as Sanitized does
            if (days == Weekdays.None) continue;
            if (a.EndsAt is not { } next) continue;               // no occurrence -> nothing to place

            usable.Add(new Placed(a.Id, a.Label, next.Hour * 60 + next.Minute, a.Sound, a.IsEnabled, days));
        }

        return usable;
    }

    // Pad an hour each side, grow to the six-hour minimum in whole slots, then clamp to the day —
    // giving the clamped length back at the far end so a 00:30 alarm still gets a full six hours.
    private static (int StartMinutes, int SlotCount) ResolveSpan(List<Placed> usable)
    {
        var start = FloorToSlot(usable.Min(u => u.Minutes)) - PadMinutes;
        var end = CeilToSlot(usable.Max(u => u.Minutes)) + PadMinutes;

        var deficitSlots = (MinimumSpanMinutes - (end - start) + SlotMinutes - 1) / SlotMinutes;
        if (deficitSlots > 0)
        {
            var before = deficitSlots / 2;
            start -= before * SlotMinutes;
            end += (deficitSlots - before) * SlotMinutes;
        }

        if (start < 0) { end = Math.Min(DayMinutes, end - start); start = 0; }
        if (end > DayMinutes) { start = Math.Max(0, start - (end - DayMinutes)); end = DayMinutes; }

        return (start, (end - start) / SlotMinutes);
    }

    private static List<TimetableSlot> BuildSlots(int startMinutes, int slotCount)
    {
        var slots = new List<TimetableSlot>(slotCount);
        for (var i = 0; i < slotCount; i++)
        {
            var minutes = startMinutes + i * SlotMinutes;
            slots.Add(new TimetableSlot(i, minutes / 60, minutes % 60));
        }
        return slots;
    }

    private static TimetableWeek Empty(DateTimeOffset now)
    {
        var today = DayFlag(now.DayOfWeek);
        var days = Week
            .Select(d => new TimetableDay(d.Flag, d.Name, d.Flag == today, Array.Empty<TimetableEntry>()))
            .ToList();
        // GridDays is the full week here rather than the weekday subset: an empty week draws the
        // empty state instead of a grid, so there is no column to drop and nothing to explain.
        return new TimetableWeek(
            IsEmpty: true, Array.Empty<TimetableSlot>(), days, days, Array.Empty<TimetableRow>());
    }

    private static int FloorToSlot(int minutes) => minutes / SlotMinutes * SlotMinutes;

    private static int CeilToSlot(int minutes) => (minutes + SlotMinutes - 1) / SlotMinutes * SlotMinutes;

    private static Weekdays DayFlag(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => Weekdays.Mon,
        DayOfWeek.Tuesday => Weekdays.Tue,
        DayOfWeek.Wednesday => Weekdays.Wed,
        DayOfWeek.Thursday => Weekdays.Thu,
        DayOfWeek.Friday => Weekdays.Fri,
        DayOfWeek.Saturday => Weekdays.Sat,
        _ => Weekdays.Sun,
    };
}

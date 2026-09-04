namespace Tidsro.Models;

/// <summary>Which piece of a block a row is drawing. The wide grid draws one independent element per
/// row, so there is no shared vertical grid for a <c>Grid.RowSpan</c> to span; a block is one segment
/// per row it covers, and this says which. <see cref="Instant"/> is an alarm with no end at all.</summary>
public enum SegmentRole { Instant, Start, Middle, End, Whole }

/// <summary>One alarm placed in the week. Layout data only — no pixels, no view concerns.</summary>
public sealed record TimetableEntry(
    Guid Id, string? Label, string DayName, int Hour, int Minute, SoundChoice Sound, bool IsEnabled,
    int SlotIndex, int? EndMinute, SegmentRole Role, int LaneIndex, int LaneCount)
{
    public string TimeText => $"{Hour:D2}:{Minute:D2}";

    /// <summary>Whether this alarm has an end at all. An instant is drawn as a point in its band; a
    /// block is drawn at its length, as one segment per row it covers.</summary>
    public bool IsBlock => EndMinute is not null;

    /// <summary>"09:00" for an instant, "09:00–10:30" for a block. What the agenda prints.</summary>
    public string RangeText => EndMinute is { } end
        ? $"{TimeText}–{end / 60:D2}:{end % 60:D2}"
        : TimeText;

    /// <summary>Whether this entry starts exactly when its slot does. It decides whether the cell
    /// prints a time, but only in a row that holds more than one start — see
    /// <see cref="TimetableRow.ShowsCellTimes"/>. A row where every entry is at 12:15 says so in its
    /// gutter and the cells stay bare; a row holding both 12:00 and 12:15 cannot, so there the
    /// off-boundary entries print their own time.</summary>
    public bool IsOnSlotBoundary => Minute % TimetableLayout.SlotMinutes == 0;

    /// <summary>Whether the cell prints this entry's own start time beside its label. Stamped by
    /// <see cref="TimetableLayout"/> once the row it lands in is known, because the decision is the
    /// ROW's (see <see cref="TimetableRow.ShowsCellTimes"/>) and the entry is what the template
    /// binds to. It is not walked up the visual tree: the cell sits inside the lane strip, so an
    /// ancestor walk lands on a lane, which is what silently dropped every cell time in v2.4.0.</summary>
    public bool ShowsTime { get; init; }

    /// <summary>Whether the printed time goes ABOVE the label rather than beside it. A lane in a
    /// shared cluster is about <see cref="TimetableLayout.MinimumLaneWidth"/> wide, and a time and a
    /// label cannot both fit across that — side by side, the label trims to an ellipsis and the cell
    /// stops naming the alarm, which is the one thing it exists to do. Stacking spends a line of
    /// height, which the band has, instead of width, which it does not.</summary>
    public bool StacksTime => ShowsTime && LaneCount > 1;

    /// <summary>Whether the printed time sits beside the label on one line. A day drawing a single
    /// lane has the whole column, so the pair fits and a second line would only add height.</summary>
    public bool InlinesTime => ShowsTime && LaneCount == 1;

    /// <summary>The label as it is drawn and announced. The add form permits an alarm with no label,
    /// so the raw <see cref="Label"/> can be null or blank — which would draw an empty box and
    /// announce with a leading comma (", Monday, 09:00"). "No label" is the same stand-in the
    /// Schedule tab's rows already use, so the two tabs name an unlabelled alarm identically.</summary>
    public string DisplayLabel => string.IsNullOrWhiteSpace(Label) ? "No label" : Label!;

    /// <summary>What a screen reader reads for this row. Carries the weekday, because the grid
    /// rendering is reached by widening the window and its column headers are easy to navigate past;
    /// and carries the off state, which is otherwise encoded only by dimming.</summary>
    public string AccessibleName
    {
        get
        {
            // Spoken words, not a dash: a screen reader reads "09:00–10:30" unreliably, and a block's
            // length is the whole point of the field.
            var time = EndMinute is { } end ? $"{TimeText} to {end / 60:D2}:{end % 60:D2}" : TimeText;
            return IsEnabled
                ? $"{DisplayLabel}, {DayName}, {time}"
                : $"{DisplayLabel}, {DayName}, {time}, off";
        }
    }
}

/// <summary>One row of the vertical axis: a 30-minute band starting at Hour:Minute.</summary>
public sealed record TimetableSlot(int Index, int Hour, int Minute)
{
    public bool IsWholeHour => Minute == 0;
    public string Label => $"{Hour:D2}:{Minute:D2}";
}

/// <summary>One weekday column, Monday first.
///
/// <para><see cref="Entries"/> holds one entry per ALARM, not per band: a block that covers three
/// half hours is one thing on your Tuesday, and it is this list the agenda draws and the column's
/// accessible name counts. The wide grid's per-band segments live on the rows instead.</para></summary>
public sealed record TimetableDay(
    Weekdays Day, string Name, bool IsToday, IReadOnlyList<TimetableEntry> Entries, int LaneCount = 1)
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

/// <summary>One lane of one cell: what this band holds in one column of the day. Usually one entry,
/// occasionally two (09:00 and 09:15 share a band without overlapping), often none.</summary>
public sealed record TimetableLane(IReadOnlyList<TimetableEntry> Entries, bool IsToday)
{
    /// <summary>The blocks whose bar this band draws. A continuation band has one of these and
    /// nothing in <see cref="Announced"/>: the bar is painted by the lane's own Border, which has no
    /// automation peer, so a three-hour block is drawn as one unbroken bar and announced once.
    ///
    /// <para>Usually one. Two when a block ends and the next begins inside the same half hour — they
    /// do not overlap, so they share a lane, and the one bar the band draws has to speak for both.
    /// Taking the first of them instead left a block's opening band unlit for up to half an hour
    /// after it had started, which shipped in v2.4.0.</para></summary>
    public IReadOnlyList<TimetableEntry> Bars => Entries.Where(e => e.IsBlock).ToList();

    /// <summary>What this band draws content for, and therefore what a screen reader reaches. A
    /// block's middle and end segments are excluded: they are the same alarm passing through, and
    /// announcing them would read a three-row block out three times over.</summary>
    public IReadOnlyList<TimetableEntry> Announced =>
        Entries.Where(e => e.Role is not (SegmentRole.Middle or SegmentRole.End)).ToList();

    /// <summary>A lane is a layout device, not information — nothing should announce "lane 2 of 2".
    /// This exists for the same reason <see cref="TimetableRow.ToString"/> does: a WPF item container
    /// that ends up with an automation peer names itself from ToString(), and the fallback must say
    /// nothing rather than read out a collection's type name.</summary>
    public override string ToString() => string.Empty;
}

/// <summary>One day's share of one slot: the entries that fall in this half hour on this weekday.
/// Usually empty, occasionally one, rarely two.
///
/// <para><see cref="Lanes"/> is as long as the day's lane count, and each lane holds what this band
/// has in it — usually nothing or one thing. The fixed length is what keeps a block's bar the same
/// width all the way down its run: were the cell to draw only what it holds, a bar would widen in
/// every band its neighbour happens not to occupy. A lane can hold more than one entry, because two
/// alarms can share a half-hour band without overlapping in time (09:00 and 09:15 want one lane and
/// two lines), which is the phase-1 behaviour lanes must not break.</para></summary>
public sealed record TimetableCell(
    Weekdays Day, string DayName, IReadOnlyList<TimetableLane> Lanes, int OverflowCount)
{
    /// <summary>What this band actually holds, in lane order and start order within a lane. Walked
    /// rather than stored, like <see cref="TimetableRow.GutterLabel"/>: a cached copy would be
    /// carried stale by the record's own <c>with</c>.</summary>
    public IReadOnlyList<TimetableEntry> Entries => Lanes.SelectMany(lane => lane.Entries).ToList();

    public bool HasOverflow => OverflowCount > 0;

    /// <summary>What the grid prints in place of the entries it had no lane for. The agenda lists
    /// every one of them, so this summarises rather than hides.</summary>
    public string OverflowText => $"+{OverflowCount} more";
}

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
                    // Starts only. A continuation is a block passing through this band, not something
                    // beginning here, so naming the row after it would claim a time nothing starts at.
                    if (entry.Role is SegmentRole.Middle or SegmentRole.End) continue;
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

    /// <summary>How many columns the wide grid draws. Five on a week with a free weekend, seven
    /// otherwise — the number a lane's readable width has to be bought in, since every column is the
    /// same width.</summary>
    public int GridColumnCount => GridDays.Count;

    /// <summary>The most lanes any drawn column divides itself into, and therefore what decides
    /// whether the grid still reads. Taken from the days the grid actually draws: a lane count on a
    /// weekend column that was dropped would ask the window for width nothing spends.</summary>
    public int MaxLaneCount
    {
        get
        {
            var most = 1;
            foreach (var day in GridDays)
                if (day.LaneCount > most) most = day.LaneCount;
            return most;
        }
    }
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
        var daySegments = new List<List<TimetableEntry>>(Week.Length);
        foreach (var (flag, name) in Week)
        {
            var dayPlaced = usable
                .Where(u => (u.Days & flag) != 0)
                .OrderBy(u => u.Minutes)
                .ThenBy(u => u.Label, StringComparer.Ordinal)
                .ThenBy(u => u.Id)
                .ToList();
            var laneCount = AssignLanes(dayPlaced);

            // A block emits one entry per band it covers, so the row-major grid can draw it as a
            // continuous bar without a row span it has no shared grid for. Written as a loop rather
            // than a LINQ projection because the role depends on where the band sits in the run.
            var segments = new List<TimetableEntry>();
            foreach (var u in dayPlaced)
            {
                var first = (FloorToSlot(u.Minutes) - startMinutes) / SlotMinutes;
                var last = u.EndMinute is { } e
                    ? (CeilToSlot(e) - SlotMinutes - startMinutes) / SlotMinutes
                    : first;
                if (last < first) last = first;

                for (var s = first; s <= last; s++)
                {
                    var role =
                        u.EndMinute is null ? SegmentRole.Instant :
                        first == last ? SegmentRole.Whole :
                        s == first ? SegmentRole.Start :
                        s == last ? SegmentRole.End :
                        SegmentRole.Middle;

                    segments.Add(new TimetableEntry(
                        u.Id, u.Label, name, u.Minutes / 60, u.Minutes % 60, u.Sound, u.IsEnabled,
                        s, u.EndMinute, role, u.LaneIndex, u.LaneCount));
                }
            }
            // The day lists each alarm once; the grid's rows get every segment.
            daySegments.Add(segments);
            days.Add(new TimetableDay(flag, name, flag == today,
                segments.Where(e => e.Role is not (SegmentRole.Middle or SegmentRole.End)).ToList(),
                laneCount));
        }

        var gridDays = ResolveGridDays(days);

        // The rows are built from the SEGMENTS of the days the grid draws, not from the days' own
        // one-per-alarm lists: a block has to appear in every band it covers to be drawn as a bar.
        var gridSegments = gridDays.Select(d => daySegments[days.IndexOf(d)]).ToList();
        return new TimetableWeek(
            IsEmpty: false, slots, days, gridDays, BuildRows(slots, gridDays, gridSegments));
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
    private static List<TimetableRow> BuildRows(
        List<TimetableSlot> slots, List<TimetableDay> days, List<List<TimetableEntry>> segments)
    {
        // One pass per day, not one scan per cell: 48 slots x 7 days would otherwise re-walk the
        // day's entries 48 times for a week that usually has a handful.
        var bySlot = segments.Select(day => day.ToLookup(e => e.SlotIndex)).ToList();

        var rows = new List<TimetableRow>();
        foreach (var slot in slots)
        {
            var occupied = false;
            for (var i = 0; i < days.Count && !occupied; i++) occupied = bySlot[i].Contains(slot.Index);
            if (!occupied) continue;

            var cells = new List<TimetableCell>(days.Count);
            for (var i = 0; i < days.Count; i++)
            {
                // The lane array is the day's width, not this band's occupancy, so a bar keeps the
                // same width down its whole run. Anything past the cap is counted, never drawn.
                // The band's own width: the widest cluster reaching into it, not the day's widest.
                var width = 1;
                foreach (var entry in bySlot[i][slot.Index])
                    if (entry.LaneCount > width) width = entry.LaneCount;

                var buckets = new List<TimetableEntry>[Math.Min(width, MaxLanes)];
                for (var l = 0; l < buckets.Length; l++) buckets[l] = new List<TimetableEntry>();

                var overflow = 0;
                var hasContent = false;
                foreach (var entry in bySlot[i][slot.Index])
                {
                    hasContent = true;
                    if (entry.LaneIndex < buckets.Length) buckets[entry.LaneIndex].Add(entry);
                    else overflow++;
                }

                // An empty cell gets NO lanes, not a row of empty ones. The view collapses an items
                // control with nothing in it, and that is what keeps an empty cell — most of them —
                // out of the automation tree entirely. Handing it empty lanes would put every one of
                // them back as a nameless list.
                var lanes = hasContent
                    ? buckets.Select(b => new TimetableLane(b, days[i].IsToday)).ToArray()
                    : Array.Empty<TimetableLane>();

                cells.Add(new TimetableCell(days[i].Day, days[i].Name, lanes, overflow));
            }
            rows.Add(StampCellTimes(new TimetableRow(slot, cells)));
        }

        return rows;
    }

    /// <summary>Hands every entry in a mixed row the row's answer about printing its own time, so
    /// the cell template binds to its own DataContext instead of hunting for the row above it.
    ///
    /// <para>Only the ROWS are stamped. <see cref="TimetableDay.Entries"/> keeps the unstamped
    /// segments, which is correct: the agenda prints <see cref="TimetableEntry.RangeText"/> on every
    /// line and has no gutter to defer to, so the question this answers does not arise there.</para></summary>
    private static TimetableRow StampCellTimes(TimetableRow row)
    {
        if (!row.ShowsCellTimes) return row;

        // A continuation is a block passing through the band, not something starting in it - printing
        // its start time here would name a minute nothing begins at.
        static bool Starts(TimetableEntry e) => e.Role is not (SegmentRole.Middle or SegmentRole.End);

        var cells = row.Cells
            .Select(cell => cell with
            {
                Lanes = cell.Lanes
                    .Select(lane => lane with
                    {
                        Entries = lane.Entries
                            .Select(e => e.IsOnSlotBoundary || !Starts(e) ? e : e with { ShowsTime = true })
                            .ToList(),
                    })
                    .ToList(),
            })
            .ToList();

        return row with { Cells = cells };
    }

    private readonly record struct Placed(
        Guid Id, string? Label, int Minutes, SoundChoice Sound, bool IsEnabled, Weekdays Days,
        int? EndMinute, int LaneIndex = 0, int LaneCount = 1);

    /// <summary>How many lanes a day column will draw side by side. Lanes are the one axis this view
    /// leaves unbounded — rows stop at 48 because the span clamps to a day, but a cluster is as wide
    /// as the number of alarms overlapping, and an import is capped at 8 MB rather than at a count.
    /// Three keeps a lane readable and keeps a hostile file from becoming a grid with thousands of
    /// columns in every cell. What does not fit is counted, and the agenda still lists all of it.</summary>
    public const int MaxLanes = 3;

    /// <summary>The width the wide grid needs before it is worth drawing at all, with one lane per
    /// column. Below it the agenda draws instead.</summary>
    public const double MinimumGridWidth = 760;

    /// <summary>What one lane needs to still show a label rather than an ellipsis. Phase 1 already
    /// had to keep a time out of a hundred-pixel column for the same reason; splitting that column
    /// three ways leaves about 33px, which is a bar with no words on it — unreadable first for low
    /// vision and then for everyone.</summary>
    public const double MinimumLaneWidth = 90;

    /// <summary>What the grid spends on itself before any column gets a pixel: the time gutter and
    /// the scroll bar. Measured off the shipped rendering rather than guessed — at a 900px window
    /// the panel is 822 wide (the window's 32px padding each side), the gutter takes 46 and the
    /// scroll bar 16, leaving 760 for five columns. The gutter is Auto-width but every label it can
    /// hold is "HH:MM", so it does not move.</summary>
    public const double GridChromeWidth = 64;

    /// <summary>How wide the panel has to be before the grid is worth drawing at this many lanes.
    /// Every column is the same width, so the widest day sets what all of them need: the columns
    /// share what is left after the chrome, and each has to fit its lanes at
    /// <see cref="MinimumLaneWidth"/> apiece. Below that the agenda takes over — it lists every
    /// alarm and reads at any size, which is why it is the right thing to fall back to.
    ///
    /// <para>The lane count is clamped to <see cref="MaxLanes"/> because that is all the grid ever
    /// draws: a hand-edited file with fifty overlapping alarms must not be able to ask for a window
    /// no monitor has. Never below <see cref="MinimumGridWidth"/>, so a week without a single
    /// overlap flips exactly where it always did. Pure, and here rather than in the converter for
    /// the reason all the layout arithmetic is here — this is where tests reach it.</para></summary>
    public static double RequiredGridWidth(int laneCount, int columnCount) =>
        Math.Max(
            MinimumGridWidth,
            GridChromeWidth
            + Math.Max(columnCount, 0) * Math.Clamp(laneCount, 1, MaxLanes) * MinimumLaneWidth);

    /// <summary>Whether this entry is happening now. Start inclusive, end exclusive, so a block that
    /// ends at 10:30 stops being current the moment 10:30 arrives and the next one can take over
    /// cleanly. An instant has no duration and is never current; a disabled block is never lit.
    ///
    /// <para>Every segment of a block answers the same way, which is what makes a three-row bar light
    /// as one thing rather than a third of one. Pure, so the view can ask it per entry from a
    /// converter without putting the rule anywhere a test cannot reach.</para></summary>
    public static bool IsCurrent(TimetableEntry entry, bool isToday, int nowMinuteOfDay)
    {
        if (!isToday || !entry.IsEnabled || entry.EndMinute is not { } end) return false;

        var start = entry.Hour * 60 + entry.Minute;
        return nowMinuteOfDay >= start && nowMinuteOfDay < end;
    }

    /// <summary>Whether any of a band's blocks is happening now — what a lane's single bar answers,
    /// since it draws for every block in the band rather than for one of them.</summary>
    public static bool IsCurrent(IEnumerable<TimetableEntry>? entries, bool isToday, int nowMinuteOfDay) =>
        entries is not null && entries.Any(e => IsCurrent(e, isToday, nowMinuteOfDay));

    /// <summary>Give each entry the lowest lane free at its start, walking in start order — so lane
    /// order is time order, which is also the order a screen reader announces them in. Returns how
    /// many lanes the grid will draw, which is the number used or <see cref="MaxLanes"/>, whichever
    /// is smaller. Entries past the cap keep their real lane index: the grid filters them out and
    /// counts them, and the agenda shows them.</summary>
    private static int AssignLanes(List<Placed> dayPlaced)
    {
        var laneFreeFrom = new List<int>();
        var clusterStart = 0;      // index of the first member of the cluster being built
        var clusterEnd = int.MinValue;
        var dayMax = 1;

        // Close off a run of mutually overlapping entries by giving every member the SAME width, so a
        // block's bar keeps one width for its whole length. The width is the cluster's, never the
        // day's: one overlap at 11:00 must not narrow an unrelated 07:30 alarm's label to an ellipsis
        // in the same column, which is exactly what a per-day width did (seen on 2026-09-03).
        void CloseCluster(int endExclusive)
        {
            var width = Math.Min(Math.Max(laneFreeFrom.Count, 1), MaxLanes);
            for (var j = clusterStart; j < endExclusive; j++)
                dayPlaced[j] = dayPlaced[j] with { LaneCount = width };
            if (width > dayMax) dayMax = width;
            laneFreeFrom.Clear();
        }

        for (var i = 0; i < dayPlaced.Count; i++)
        {
            var p = dayPlaced[i];
            var end = p.EndMinute ?? p.Minutes + 1;   // an instant occupies a moment, not a span

            // A new cluster begins the moment an entry starts at or after everything before it ended.
            if (p.Minutes >= clusterEnd && laneFreeFrom.Count > 0)
            {
                CloseCluster(i);
                clusterStart = i;
            }

            var lane = laneFreeFrom.FindIndex(free => free <= p.Minutes);
            if (lane < 0) { lane = laneFreeFrom.Count; laneFreeFrom.Add(end); }
            else laneFreeFrom[lane] = end;

            dayPlaced[i] = p with { LaneIndex = lane };
            if (end > clusterEnd) clusterEnd = end;
        }

        if (laneFreeFrom.Count > 0) CloseCluster(dayPlaced.Count);
        return dayMax;
    }

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

            // Build is total. The start comes from EndsAt and the end from EndMinute — two different
            // sources, and Sanitized compares the end against Hour/Minute, not against EndsAt. An end
            // that does not sit after the start is drawn as an instant rather than reaching the
            // covered-slot walk as a negative span.
            var start = next.Hour * 60 + next.Minute;
            var end = a.EndMinute is { } e && e > start && e <= DayMinutes ? e : (int?)null;

            usable.Add(new Placed(a.Id, a.Label, start, a.Sound, a.IsEnabled, days, end));
        }

        return usable;
    }

    // Pad an hour each side, grow to the six-hour minimum in whole slots, then clamp to the day —
    // giving the clamped length back at the far end so a 00:30 alarm still gets a full six hours.
    private static (int StartMinutes, int SlotCount) ResolveSpan(List<Placed> usable)
    {
        var start = FloorToSlot(usable.Min(u => u.Minutes)) - PadMinutes;
        // A block's end is what the span has to reach, not its start.
        var end = CeilToSlot(usable.Max(u => u.EndMinute ?? u.Minutes)) + PadMinutes;

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

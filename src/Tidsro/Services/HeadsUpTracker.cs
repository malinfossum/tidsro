namespace Tidsro.Services;

/// <summary>
/// Which heads-up cards are open, and for which occurrence. A card closes when its alarm fires, or
/// early once that occurrence will no longer ring (skipped, switched off, deleted, moved by an edit).
/// A card closed early is kept in mind until its fire time and comes back if the occurrence does (an
/// undo, switching the alarm back on), so an opted-in heads-up is never lost to a step the user took
/// back. A card the user closes stays closed. Generic over the card so tests need no window.
/// </summary>
public sealed class HeadsUpTracker<TCard> where TCard : notnull
{
    private readonly Dictionary<TCard, (Guid AlarmId, DateTimeOffset FireAt)> _open = new();
    private readonly HashSet<(Guid AlarmId, DateTimeOffset FireAt)> _parked = new();

    /// <summary>What a tick changes: cards to close because their alarm fired, cards to close because
    /// their occurrence went away, and alarms whose parked heads-up should show again.</summary>
    public sealed record Changes(IReadOnlyList<TCard> Fired, IReadOnlyList<TCard> Gone, IReadOnlyList<Guid> Back);

    public bool IsShown(Guid alarmId, DateTimeOffset fireAt) => _open.ContainsValue((alarmId, fireAt));

    public void Opened(TCard card, Guid alarmId, DateTimeOffset fireAt)
    {
        _open[card] = (alarmId, fireAt);
        _parked.Remove((alarmId, fireAt));
    }

    public void Closed(TCard card) => _open.Remove(card);

    /// <summary>Keep a card's occurrence in mind so it can come back. Call it only for a card from
    /// <see cref="Changes.Gone"/> that is actually closed early: one the user closed first stays closed.</summary>
    public void Park(TCard card)
    {
        if (_open.TryGetValue(card, out var key)) _parked.Add(key);
    }

    /// <summary>Drop every parked card. Clear all and Import close every card and replace the alarms;
    /// nothing from before may come back.</summary>
    public void Forget() => _parked.Clear();

    public Changes Reconcile(DateTimeOffset now, Func<Guid, DateTimeOffset, bool> willRingAt)
    {
        var fired = new List<TCard>();
        var gone = new List<TCard>();
        foreach (var (card, key) in _open)
        {
            if (now >= key.FireAt) fired.Add(card);
            else if (!willRingAt(key.AlarmId, key.FireAt)) gone.Add(card);
        }

        var back = new List<Guid>();
        foreach (var key in _parked.ToList())
        {
            if (now >= key.FireAt) _parked.Remove(key);   // its time is gone: nothing left to warn about
            else if (willRingAt(key.AlarmId, key.FireAt)) { _parked.Remove(key); back.Add(key.AlarmId); }
        }
        return new Changes(fired, gone, back);
    }
}

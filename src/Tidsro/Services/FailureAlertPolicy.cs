namespace Tidsro.Services;

// Decides whether a critical failure earns a modal dialog, so a repeating failure cannot storm the
// user with stacked dialogs. Pure decision logic — no I/O, no WPF types. Called from the UI thread
// only (App's DispatcherTimer tick and its exception handlers); not thread-safe by design, so it
// does not lock. Every successful Try* claim must be paired with a ReleaseDialog() call in a
// finally, or _dialogOpen wedges true and silences every failure dialog for the rest of the run.
public sealed class FailureAlertPolicy
{
    private bool _dialogOpen;
    private bool _saveFailureAnnounced;
    private bool _crashAnnounced;

    // A dialog is already up, or this outage was already announced -> stay quiet. Otherwise claim it.
    public bool TryClaimSaveFailure()
    {
        if (_dialogOpen || _saveFailureAnnounced) return false;
        _saveFailureAnnounced = true;
        _dialogOpen = true;
        return true;
    }

    // A final save happens at most once per run, so — unlike TryClaimSaveFailure — it ignores
    // _saveFailureAnnounced: in a sustained outage the mid-session dialog claims first and no save
    // ever succeeds to clear that flag, which would otherwise defeat the strictly more urgent
    // quit-time warning every time it matters. Still honours the open-dialog guard.
    public bool TryClaimFinalSaveFailure()
    {
        if (_dialogOpen) return false;
        _saveFailureAnnounced = true;
        _dialogOpen = true;
        return true;
    }

    // A successful save ends the outage: the next failure is a new one and earns its own dialog.
    public void NoteSaveSucceeded() => _saveFailureAnnounced = false;

    // Once per instance, never re-arms — even across NoteSaveSucceeded, which only concerns saves.
    public bool TryClaimCrash()
    {
        if (_dialogOpen || _crashAnnounced) return false;
        _crashAnnounced = true;
        _dialogOpen = true;
        return true;
    }

    // Only the dialog-open flag clears; the announced flags persist so the same outage stays quiet.
    public void ReleaseDialog() => _dialogOpen = false;
}

using Tidsro.Services;
using Xunit;

namespace Tidsro.Tests;

public class FailureAlertPolicyTests
{
    [Fact]
    public void TryClaimSaveFailure_announces_the_first_failure()
    {
        var policy = new FailureAlertPolicy();

        Assert.True(policy.TryClaimSaveFailure());
    }

    [Fact]
    public void TryClaimSaveFailure_suppresses_a_second_failure_while_the_first_is_unreleased()
    {
        var policy = new FailureAlertPolicy();

        Assert.True(policy.TryClaimSaveFailure());
        Assert.False(policy.TryClaimSaveFailure());
    }

    [Fact]
    public void TryClaimSaveFailure_stays_suppressed_after_ReleaseDialog_for_the_same_outage()
    {
        var policy = new FailureAlertPolicy();

        Assert.True(policy.TryClaimSaveFailure());
        policy.ReleaseDialog();

        Assert.False(policy.TryClaimSaveFailure());
    }

    [Fact]
    public void TryClaimSaveFailure_announces_again_after_NoteSaveSucceeded()
    {
        var policy = new FailureAlertPolicy();

        Assert.True(policy.TryClaimSaveFailure());
        policy.ReleaseDialog();
        policy.NoteSaveSucceeded();

        Assert.True(policy.TryClaimSaveFailure());
    }

    [Fact]
    public void TryClaimCrash_announces_the_first_crash_only_and_never_rearms()
    {
        var policy = new FailureAlertPolicy();

        Assert.True(policy.TryClaimCrash());
        policy.ReleaseDialog();
        policy.NoteSaveSucceeded();

        Assert.False(policy.TryClaimCrash());
    }

    [Fact]
    public void While_a_dialog_is_open_neither_a_save_failure_nor_a_crash_claims()
    {
        var policy = new FailureAlertPolicy();

        Assert.True(policy.TryClaimSaveFailure());   // opens the dialog, claims the outage

        Assert.False(policy.TryClaimSaveFailure());
        Assert.False(policy.TryClaimCrash());
    }

    [Fact]
    public void TryClaimFinalSaveFailure_is_announced_even_after_a_mid_session_failure_already_was()
    {
        var policy = new FailureAlertPolicy();

        Assert.True(policy.TryClaimSaveFailure());   // the mid-session dialog claims first, as it will in nearly every sustained outage
        policy.ReleaseDialog();

        Assert.True(policy.TryClaimFinalSaveFailure());   // the strictly more urgent quit-time warning must still get through
    }

    [Fact]
    public void TryClaimFinalSaveFailure_is_still_refused_while_a_dialog_is_open()
    {
        var policy = new FailureAlertPolicy();

        Assert.True(policy.TryClaimSaveFailure());   // opens the dialog, unreleased

        Assert.False(policy.TryClaimFinalSaveFailure());
    }

    [Fact]
    public void A_crash_claim_refused_by_an_open_dialog_does_not_burn_the_crash_announcement()
    {
        var policy = new FailureAlertPolicy();

        Assert.True(policy.TryClaimSaveFailure());   // opens the dialog
        Assert.False(policy.TryClaimCrash());         // refused only because the dialog is open
        policy.ReleaseDialog();

        Assert.True(policy.TryClaimCrash());           // the crash was never announced, so it still claims
    }

    [Fact]
    public void A_save_failure_claim_refused_by_an_open_dialog_does_not_burn_the_save_announcement()
    {
        var policy = new FailureAlertPolicy();

        Assert.True(policy.TryClaimCrash());              // opens the dialog
        Assert.False(policy.TryClaimSaveFailure());        // refused only because the dialog is open
        policy.ReleaseDialog();

        Assert.True(policy.TryClaimSaveFailure());          // the save failure was never announced, so it still claims
    }
}

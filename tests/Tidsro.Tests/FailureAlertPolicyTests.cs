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
}

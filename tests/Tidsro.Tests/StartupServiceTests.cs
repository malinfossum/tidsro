using Tidsro.Services;
using Xunit;

namespace Tidsro.Tests;

public class StartupServiceTests
{
    private const string Installed = @"C:\Users\Nugget\AppData\Local\Programs\Tidsro\Tidsro.exe";
    private const string CustomInstall = @"D:\Apps\Tidsro\Tidsro.exe";
    private const string DebugBuild =
        @"C:\Users\Nugget\Documents\Development\GitHub\repos\tidsro\src\Tidsro\bin\Debug\net10.0-windows\Tidsro.exe";
    private const string ReleaseBuild =
        @"C:\Users\Nugget\Documents\Development\GitHub\repos\tidsro\src\Tidsro\bin\Release\net10.0-windows\Tidsro.exe";
    private const string Portable = @"C:\Users\Nugget\Downloads\Tidsro.exe";

    private static readonly Func<string, bool> TargetAlive = _ => true;
    private static readonly Func<string, bool> TargetDead = _ => false;

    [Fact]
    public void Run_value_quotes_the_path_and_passes_the_startup_flag()
    {
        var v = StartupService.RunValueFor(@"C:\Program Files\Tidsro\Tidsro.exe");
        Assert.Equal("\"C:\\Program Files\\Tidsro\\Tidsro.exe\" --startup", v);
    }

    // --- Reading the exe back out of a Run value ---

    [Fact]
    public void The_target_is_read_out_of_a_quoted_run_value()
    {
        Assert.Equal(Installed, StartupService.TargetOf(StartupService.RunValueFor(Installed)));
    }

    [Fact]
    public void The_target_is_read_out_of_an_unquoted_run_value()
    {
        Assert.Equal(@"C:\Tidsro\Tidsro.exe",
            StartupService.TargetOf(@"C:\Tidsro\Tidsro.exe --startup"));
    }

    [Fact]
    public void A_garbled_run_value_has_no_target()
    {
        Assert.Null(StartupService.TargetOf("   "));
    }

    // --- Rule 1: a build output may never claim the Run key, however broken it is ---

    [Fact]
    public void A_debug_build_never_repairs_even_when_the_target_is_dead()
    {
        Assert.False(StartupService.ShouldRepair(
            DebugBuild, StartupService.RunValueFor(Installed), null, TargetDead));
    }

    [Fact]
    public void A_local_release_build_never_repairs_even_when_the_target_is_dead()
    {
        Assert.False(StartupService.ShouldRepair(
            ReleaseBuild, StartupService.RunValueFor(Installed), null, TargetDead));
    }

    // --- Rule 2: repair only when the registered target is actually gone ---

    [Fact]
    public void A_move_is_repaired_because_the_old_target_is_gone()
    {
        Assert.True(StartupService.ShouldRepair(
            CustomInstall, StartupService.RunValueFor(Installed), null, TargetDead));
    }

    [Fact]
    public void A_live_target_is_left_alone()
    {
        Assert.False(StartupService.ShouldRepair(
            Portable, StartupService.RunValueFor(Installed), null, TargetAlive));
    }

    [Fact]
    public void A_garbled_value_is_repaired()
    {
        Assert.True(StartupService.ShouldRepair(Installed, "notapath", null, TargetAlive));
    }

    [Fact]
    public void Autostart_that_is_switched_off_stays_off()
    {
        Assert.False(StartupService.ShouldRepair(Installed, null, null, TargetDead));
    }

    [Fact]
    public void A_value_that_already_points_at_us_is_not_rewritten()
    {
        Assert.False(StartupService.ShouldRepair(
            Installed, StartupService.RunValueFor(Installed), null, TargetDead));
    }

    // --- Rule 3: once the installer records where it put us, only that copy repairs ---

    [Fact]
    public void A_portable_copy_does_not_repair_on_a_machine_with_an_install()
    {
        Assert.False(StartupService.ShouldRepair(
            Portable, StartupService.RunValueFor(Installed), @"D:\Apps\Tidsro", TargetDead));
    }

    [Fact]
    public void The_copy_in_the_recorded_install_folder_repairs()
    {
        Assert.True(StartupService.ShouldRepair(
            CustomInstall, StartupService.RunValueFor(Installed), @"D:\Apps\Tidsro", TargetDead));
    }

    [Fact]
    public void The_recorded_install_folder_is_matched_regardless_of_casing_and_trailing_slash()
    {
        Assert.True(StartupService.ShouldRepair(
            CustomInstall, StartupService.RunValueFor(Installed), @"d:\apps\tidsro\", TargetDead));
    }
}

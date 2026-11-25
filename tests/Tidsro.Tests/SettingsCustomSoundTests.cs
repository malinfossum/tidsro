using System.IO;
using System.Linq;
using Tidsro.Models;
using Tidsro.Services;
using Tidsro.ViewModels;

namespace Tidsro.Tests;

/// <summary>Choosing, previewing and clearing the custom sound from Settings. These act at once
/// rather than waiting for Save, because they copy or delete a real file.</summary>
public class SettingsCustomSoundTests : IDisposable
{
    private readonly string _dir;
    private readonly CustomSoundStore _store;
    private readonly FakeFileDialogService _dialogs = new();
    private readonly FakeSoundService _sound = new();
    private readonly List<(string Title, string Message)> _messages = new();
    private readonly AppSettings _settings = new();
    private int _saves;
    private int _pickersRefreshed;
    private readonly SettingsViewModel _vm;

    public SettingsCustomSoundTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "TidsroTests", Guid.NewGuid().ToString("N"));
        _store = new CustomSoundStore(Path.Combine(_dir, "sounds"));

        var ports = new SoundPorts(
            Store: _store,
            Dialogs: _dialogs,
            Sound: _sound,
            ShowMessage: (t, m) => _messages.Add((t, m)),
            Changed: () => _pickersRefreshed++);

        _vm = new SettingsViewModel(_settings, new FakeStartupService(),
            save: () => _saves++, _ => { }, clearAllAlarms: () => { }, alarmCount: () => 0,
            hasAnythingToClear: () => true, resetWindowPlacement: () => { },
            confirm: (_, _) => true, dataPorts: null, soundPorts: ports);
    }

    public void Dispose() { try { Directory.Delete(_dir, true); } catch { } }

    /// <summary>A real, playable WAV: one of the app's own chimes, read back out of the assembly.</summary>
    private string RealWav(string name)
    {
        var asm = typeof(SoundService).Assembly;
        var res = asm.GetManifestResourceNames().First(n => n.EndsWith(".bell.wav", StringComparison.OrdinalIgnoreCase));
        using var stream = asm.GetManifestResourceStream(res)!;
        Directory.CreateDirectory(_dir);
        var path = Path.Combine(_dir, name);
        using var file = File.Create(path);
        stream.CopyTo(file);
        return path;
    }

    [Fact]
    public void Nothing_chosen_yet_says_so_and_offers_no_custom_entry()
    {
        Assert.False(_vm.HasCustomSound);
        Assert.Equal("No sound chosen", _vm.CustomSoundLabel);
        Assert.DoesNotContain(SoundChoice.Custom, _vm.SoundOptions);
        Assert.False(_vm.PreviewCustomSoundCommand.CanExecute(null));
        Assert.False(_vm.RemoveCustomSoundCommand.CanExecute(null));
    }

    [Fact]
    public void Choosing_a_wav_installs_it_names_it_and_offers_it_everywhere()
    {
        _dialogs.WavPath = RealWav("Gong.wav");

        _vm.ChooseCustomSoundCommand.Execute(null);

        Assert.True(_vm.HasCustomSound);
        Assert.Equal("Gong.wav", _vm.CustomSoundLabel);
        Assert.Equal("Gong.wav", _settings.CustomSoundName);
        Assert.Contains(SoundChoice.Custom, _vm.SoundOptions);
        Assert.Equal(1, _saves);                 // the name is settings — persisted, not held in the draft
        Assert.Equal(1, _pickersRefreshed);      // and the pickers elsewhere re-read their lists
        Assert.Empty(_messages);
    }

    [Fact]
    public void Canceling_the_file_dialog_changes_nothing()
    {
        _dialogs.WavPath = null;

        _vm.ChooseCustomSoundCommand.Execute(null);

        Assert.False(_vm.HasCustomSound);
        Assert.Equal(0, _saves);
        Assert.Empty(_messages);
    }

    // The failure has to reach her on screen: error balloons are switched off on her machine, and a
    // silent refusal looks like the file was accepted right up until an alarm goes quiet.
    [Fact]
    public void A_file_Tidsro_cannot_play_is_reported_and_says_what_to_do()
    {
        Directory.CreateDirectory(_dir);
        var fake = Path.Combine(_dir, "song.wav");
        File.WriteAllBytes(fake, "ID3 this is really an mp3"u8.ToArray());
        _dialogs.WavPath = fake;

        _vm.ChooseCustomSoundCommand.Execute(null);

        Assert.False(_vm.HasCustomSound);
        Assert.Null(_settings.CustomSoundName);
        var (title, message) = Assert.Single(_messages);
        Assert.Equal("That file won't do", title);
        Assert.Contains(".wav", message);
        Assert.Contains("mp3", message);         // names the mistake it is actually there to catch
    }

    [Fact]
    public void Previewing_plays_the_custom_sound()
    {
        _dialogs.WavPath = RealWav("Gong.wav");
        _vm.ChooseCustomSoundCommand.Execute(null);

        Assert.True(_vm.PreviewCustomSoundCommand.CanExecute(null));
        _vm.PreviewCustomSoundCommand.Execute(null);

        Assert.Equal(SoundChoice.Custom, _sound.LastPlayed);
    }

    [Fact]
    public void Removing_clears_the_file_the_name_and_the_picker_entry()
    {
        _dialogs.WavPath = RealWav("Gong.wav");
        _vm.ChooseCustomSoundCommand.Execute(null);

        _vm.RemoveCustomSoundCommand.Execute(null);

        Assert.False(_vm.HasCustomSound);
        Assert.False(_store.HasCustom);
        Assert.Null(_settings.CustomSoundName);
        Assert.Equal("No sound chosen", _vm.CustomSoundLabel);
        Assert.DoesNotContain(SoundChoice.Custom, _vm.SoundOptions);
        Assert.Equal(2, _pickersRefreshed);   // once for the choice, once for the removal
    }

    // Otherwise the dropdown keeps a selection that is no longer in its list and renders blank.
    [Fact]
    public void Removing_a_sound_that_was_the_default_falls_back_to_silent()
    {
        _dialogs.WavPath = RealWav("Gong.wav");
        _vm.ChooseCustomSoundCommand.Execute(null);
        _vm.DefaultSound = SoundChoice.Custom;

        _vm.RemoveCustomSoundCommand.Execute(null);

        Assert.Equal(SoundChoice.None, _vm.DefaultSound);
    }

    [Fact]
    public void Removing_leaves_a_built_in_default_alone()
    {
        _dialogs.WavPath = RealWav("Gong.wav");
        _vm.ChooseCustomSoundCommand.Execute(null);
        _vm.DefaultSound = SoundChoice.Bell;

        _vm.RemoveCustomSoundCommand.Execute(null);

        Assert.Equal(SoundChoice.Bell, _vm.DefaultSound);
    }

    // A backup restored on another machine carries the name but not the audio.
    [Fact]
    public void A_name_with_no_file_behind_it_reads_as_nothing_chosen()
    {
        _settings.CustomSoundName = "gong.wav";

        Assert.False(_vm.HasCustomSound);
        Assert.Equal("No sound chosen", _vm.CustomSoundLabel);
    }
}

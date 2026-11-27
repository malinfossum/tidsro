using System.IO;
using System.Linq;
using System.Reflection;
using Tidsro.Services;

namespace Tidsro.Tests;

/// <summary>The one custom-sound slot: installing a chosen .wav, resolving it back, and removing it.
/// The store owns its folder and always writes the same file name, so nothing a user (or an imported
/// backup) supplies can steer a path.</summary>
public class CustomSoundStoreTests : IDisposable
{
    private readonly string _dir;
    private readonly CustomSoundStore _store;

    public CustomSoundStoreTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "TidsroTests", Guid.NewGuid().ToString("N"));
        _store = new CustomSoundStore(Path.Combine(_dir, "sounds"));
    }

    public void Dispose() { try { Directory.Delete(_dir, true); } catch { } }

    /// <summary>A real, playable WAV: one of the app's own chimes, read back out of the assembly.</summary>
    private string RealWav(string name = "gong.wav")
    {
        var asm = typeof(SoundService).Assembly;
        var res = asm.GetManifestResourceNames().First(n => n.EndsWith(".soft-chime.wav", StringComparison.OrdinalIgnoreCase));
        using var stream = asm.GetManifestResourceStream(res)!;
        Directory.CreateDirectory(_dir);
        var path = Path.Combine(_dir, name);
        using var file = File.Create(path);
        stream.CopyTo(file);
        return path;
    }

    private string Write(string name, byte[] bytes)
    {
        Directory.CreateDirectory(_dir);
        var path = Path.Combine(_dir, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    [Fact]
    public void No_custom_sound_before_one_is_installed()
    {
        Assert.False(_store.HasCustom);
        Assert.Null(_store.FilePath);
    }

    [Fact]
    public void Installing_a_wav_copies_it_in_and_reports_the_chosen_name()
    {
        var source = RealWav("My Gong.wav");

        var name = _store.Install(source);

        Assert.Equal("My Gong.wav", name);          // the display name is what the user picked
        Assert.True(_store.HasCustom);
        Assert.True(File.Exists(_store.FilePath!));
    }

    // The name the user picked is a label; it never becomes part of a path. That is what keeps a
    // hostile name in an imported backup from steering a file read.
    [Fact]
    public void The_installed_copy_lives_in_the_store_folder_under_a_fixed_name()
    {
        _store.Install(RealWav("Anything At All.wav"));

        Assert.Equal(Path.GetFullPath(Path.Combine(_dir, "sounds")), Path.GetDirectoryName(_store.FilePath));
        Assert.Equal("custom.wav", Path.GetFileName(_store.FilePath));
    }

    [Fact]
    public void The_original_file_is_left_alone()
    {
        var source = RealWav();

        _store.Install(source);

        Assert.True(File.Exists(source));   // a copy, never a move
    }

    [Fact]
    public void Installing_a_second_sound_replaces_the_first()
    {
        _store.Install(RealWav("first.wav"));
        var name = _store.Install(RealWav("second.wav"));

        Assert.Equal("second.wav", name);
        Assert.Single(Directory.GetFiles(Path.Combine(_dir, "sounds")));   // one slot, not two
    }

    [Fact]
    public void A_file_that_is_not_a_wav_is_refused_and_nothing_is_written()
    {
        var source = Write("notes.wav", "this is text, not audio"u8.ToArray());

        var name = _store.Install(source);

        Assert.Null(name);
        Assert.False(_store.HasCustom);
    }

    [Fact]
    public void A_refused_file_leaves_an_existing_custom_sound_in_place()
    {
        _store.Install(RealWav("good.wav"));

        var name = _store.Install(Write("bad.wav", "not audio"u8.ToArray()));

        Assert.Null(name);                 // the attempt failed...
        Assert.True(_store.HasCustom);     // ...and did not take the working sound with it
    }

    [Fact]
    public void A_file_over_the_size_cap_is_refused()
    {
        // A valid RIFF/WAVE header on a file far larger than any chime needs to be.
        var big = new byte[CustomSoundStore.MaxBytes + 1];
        "RIFF"u8.CopyTo(big);
        "WAVE"u8.CopyTo(big.AsSpan(8));
        var source = Write("huge.wav", big);

        Assert.Null(_store.Install(source));
        Assert.False(_store.HasCustom);
    }

    [Fact]
    public void A_missing_source_file_is_refused_without_throwing()
    {
        Assert.Null(_store.Install(Path.Combine(_dir, "nothing-here.wav")));
        Assert.False(_store.HasCustom);
    }

    // --- MP3 -------------------------------------------------------------------------------------

    private string Mp3(string name = "tone.mp3") => Write(name, Mp3FramesTests.Tone());

    private static byte[] Installed(string path) => File.ReadAllBytes(path);

    [Fact]
    public void Installing_an_mp3_stores_a_playable_wav_and_reports_the_mp3s_name()
    {
        var name = _store.Install(Mp3("Morning Birds.mp3"));

        Assert.Equal("Morning Birds.mp3", name);
        Assert.True(_store.HasCustom);
        var bytes = Installed(_store.FilePath!);
        Assert.Equal("RIFF"u8.ToArray(), bytes[..4]);
        using var player = new System.Media.SoundPlayer(_store.FilePath!);
        player.Load();   // the same check every alarm sound passes
    }

    // One slot, one fixed name, whatever the user picked: the decoded copy replaces the file the
    // .wav slot always used, so playback and a backup restored elsewhere behave exactly as before.
    [Fact]
    public void An_installed_mp3_lives_under_the_same_fixed_name()
    {
        _store.Install(RealWav("first.wav"));
        _store.Install(Mp3("second.mp3"));

        Assert.Equal("custom.wav", Path.GetFileName(_store.FilePath));
        Assert.Single(Directory.GetFiles(Path.Combine(_dir, "sounds")));
    }

    [Fact]
    public void The_original_mp3_is_left_alone()
    {
        var source = Mp3();
        var before = File.ReadAllBytes(source);

        _store.Install(source);

        Assert.Equal(before, File.ReadAllBytes(source));
    }

    // The bytes decide, not the extension: a mislabeled file is used for what it is.
    [Fact]
    public void An_mp3_renamed_to_wav_is_used_as_the_mp3_it_is()
    {
        Assert.Equal("song.wav", _store.Install(Write("song.wav", Mp3FramesTests.Tone())));
        Assert.True(_store.HasCustom);
    }

    [Fact]
    public void A_wav_renamed_to_mp3_is_copied_as_it_is()
    {
        var wav = RealWav("chime.mp3");

        Assert.Equal("chime.mp3", _store.Install(wav));
        Assert.Equal(File.ReadAllBytes(wav), Installed(_store.FilePath!));
    }

    [Fact]
    public void A_text_file_named_mp3_is_refused()
    {
        Assert.Null(_store.Install(Write("song.mp3", "ID3 this is not audio"u8.ToArray())));
        Assert.False(_store.HasCustom);
    }

    // A long recording at a low bitrate fits under 5 MB but decodes to a wave file many times
    // that size. The length cap is what keeps the copy in AppData small.
    [Fact]
    public void An_mp3_longer_than_the_limit_is_refused_and_the_existing_sound_stays()
    {
        _store.Install(RealWav("good.wav"));
        var before = Installed(_store.FilePath!);

        var tone = Mp3FramesTests.Tone();
        var frames = Mp3Frames.Find(tone)!;
        var start = frames.Frames[0].Offset;
        var end = frames.Frames[^1].Offset + frames.Frames[^1].Length;
        var copies = (int)Math.Ceiling(CustomSoundStore.MaxLength / frames.Duration) + 1;
        var audio = tone[start..end];
        var longMp3 = Enumerable.Repeat(audio, copies).SelectMany(b => b).ToArray();
        Assert.True(longMp3.Length < CustomSoundStore.MaxBytes);   // under the size cap, so length is the reason

        Assert.Null(_store.Install(Write("long.mp3", longMp3)));
        Assert.Equal(before, Installed(_store.FilePath!));
    }

    [Fact]
    public void Removing_the_custom_sound_clears_the_slot()
    {
        _store.Install(RealWav());

        _store.Remove();

        Assert.False(_store.HasCustom);
        Assert.Null(_store.FilePath);
    }

    [Fact]
    public void Removing_when_there_is_nothing_installed_is_harmless()
    {
        _store.Remove();
        Assert.False(_store.HasCustom);
    }
}

using System.IO;
using System.Media;

namespace Tidsro.Services;

/// <summary>Owns the one custom-sound slot: the .wav the user chose, copied into a folder Tidsro
/// controls.
///
/// It is a <em>copy</em> on purpose. Remembering the path the user browsed to would mean that moving,
/// renaming or deleting that file — or picking it off a memory stick — leaves every alarm using it
/// silently mute, with nothing on screen to say why. Owning the file means the sound keeps working.
///
/// The copy always lands on one fixed name, so nothing a user picks or an imported backup carries can
/// steer a file path. The name the user recognizes is kept separately, as a label, in
/// <see cref="Models.AppSettings.CustomSoundName"/>.</summary>
public sealed class CustomSoundStore
{
    /// <summary>Well past any chime — the largest built-in is a little over 1 MB — and small enough
    /// that a wrongly picked film soundtrack is refused rather than copied into AppData.</summary>
    public const int MaxBytes = 5 * 1024 * 1024;

    private const string FileName = "custom.wav";

    private readonly string _folder;

    public CustomSoundStore(string folder) => _folder = folder;

    public static string DefaultFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Tidsro", "sounds");

    private string Target => Path.Combine(_folder, FileName);

    /// <summary>True when a usable custom sound is installed. The file on disk is the truth — a name
    /// restored from a backup taken on another machine has no audio behind it.</summary>
    public bool HasCustom => File.Exists(Target);

    /// <summary>Full path of the installed sound, or null when there is none.</summary>
    public string? FilePath => HasCustom ? Target : null;

    /// <summary>Copy <paramref name="sourcePath"/> into the slot, replacing whatever was there.
    /// Returns the file's own name to show the user, or null when the file was refused — too big,
    /// empty, missing, or not a .wav this app can actually play. A refusal leaves any previously
    /// installed sound untouched.</summary>
    public string? Install(string sourcePath)
    {
        try
        {
            var info = new FileInfo(sourcePath);
            if (!info.Exists || info.Length == 0 || info.Length > MaxBytes) return null;
            if (!LooksLikeWav(sourcePath)) return null;
            if (!IsPlayable(sourcePath)) return null;

            Directory.CreateDirectory(_folder);

            // Stage beside the target and swap, so a copy that fails half way cannot leave a
            // truncated file where a working sound used to be.
            var tmp = Target + ".tmp";
            try
            {
                File.Copy(sourcePath, tmp, overwrite: true);
                if (File.Exists(Target)) File.Replace(tmp, Target, null);
                else File.Move(tmp, Target);
            }
            catch
            {
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { /* best effort */ }
                throw;
            }

            return Path.GetFileName(sourcePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                                   or ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;   // choosing a sound must never fail louder than "that file won't do"
        }
    }

    /// <summary>Empty the slot. Harmless when there is nothing installed.</summary>
    public void Remove()
    {
        try { if (File.Exists(Target)) File.Delete(Target); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* best effort */ }
    }

    // "RIFF" .... "WAVE" — the twelve-byte container header. Cheap, deterministic, and it rejects the
    // common mistake of an .mp3 renamed to .wav before anything touches an audio API.
    private static bool LooksLikeWav(string path)
    {
        using var stream = File.OpenRead(path);
        Span<byte> head = stackalloc byte[12];
        if (stream.ReadAtLeast(head, 12, throwOnEndOfStream: false) < 12) return false;
        return head[..4].SequenceEqual("RIFF"u8) && head[8..12].SequenceEqual("WAVE"u8);
    }

    // The header only proves the container. SoundPlayer needs PCM inside it, and an embed-style check
    // has fooled this app before — so load it for real and let the failure speak.
    private static bool IsPlayable(string path)
    {
        try
        {
            using var player = new SoundPlayer(path);
            player.Load();
            return true;
        }
        catch { return false; }
    }
}

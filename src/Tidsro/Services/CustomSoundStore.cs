using System.IO;

namespace Tidsro.Services;

/// <summary>Owns the one custom-sound slot: the .wav or .mp3 the user chose, copied into a folder
/// Tidsro controls. An .mp3 is decoded to a .wav on the way in (see <see cref="Mp3Decoder"/>), so
/// the slot always holds wave audio and plays through the same SoundPlayer as the built-in chimes.
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

    /// <summary>The longest .mp3 accepted. A low-bitrate recording fits under <see cref="MaxBytes"/>
    /// yet decodes to a wave file many times that size; five minutes keeps the decoded copy to about
    /// 58 MB at most (48 kHz stereo).</summary>
    public static readonly TimeSpan MaxLength = TimeSpan.FromMinutes(5);

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
    /// Returns the file's own name to show the user, or null when the file was refused: too big,
    /// empty, missing, or not a .wav or .mp3 this app can actually play. The bytes decide which of
    /// the two it is, not the extension. A refusal leaves any previously installed sound untouched.</summary>
    public string? Install(string sourcePath)
    {
        LastRefusalWasWaveFormat = false;
        try
        {
            var info = new FileInfo(sourcePath);
            if (!info.Exists || info.Length == 0 || info.Length > MaxBytes) return null;

            // Read once and work from these bytes, so the file that was checked is the file that
            // is stored, even if the original changes underneath.
            var bytes = File.ReadAllBytes(sourcePath);
            if (bytes.Length == 0 || bytes.Length > MaxBytes) return null;

            // A .wav SoundPlayer plays is copied as it is. Anything else, including a RIFF file
            // with MP3 audio inside, is tried as an .mp3 and refused when it is not one either.
            ReadOnlyMemory<byte>? decoded = WaveFile.IsPlayable(bytes) ? bytes : Mp3Decoder.ToWav(bytes, MaxLength);
            if (decoded is not { } wav || !WaveFile.IsPlayable(wav.Span))
            {
                LastRefusalWasWaveFormat = WaveFile.IsWave(bytes);
                return null;
            }

            Directory.CreateDirectory(_folder);

            // Stage beside the target and swap, so a copy that fails half way cannot leave a
            // truncated file where a working sound used to be.
            var tmp = Target + ".tmp";
            try
            {
                File.WriteAllBytes(tmp, wav.Span);
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

    /// <summary>True when the last <see cref="Install"/> refused a real .wav container whose audio
    /// format SoundPlayer cannot play (24-bit or "extensible" exports, for example), so the message can
    /// say what to do about it.</summary>
    public bool LastRefusalWasWaveFormat { get; private set; }

    /// <summary>Empty the slot. Harmless when there is nothing installed.</summary>
    public void Remove()
    {
        try { if (File.Exists(Target)) File.Delete(Target); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* best effort */ }
    }
}

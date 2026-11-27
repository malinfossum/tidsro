using System.Buffers.Binary;

namespace Tidsro.Services;

/// <summary>Whether SoundPlayer will actually play a .wav, decided from its bytes.
///
/// <c>SoundPlayer.Load()</c> is no such check: on a path it only confirms the file exists, and on a
/// stream it only copies the bytes. The format is validated inside <c>Play()</c>, and an alarm
/// swallows that exception, so a file that fails there rings silently. These are the same rules
/// .NET applies before it calls PlaySound, from both of its validators: <c>ValidateSoundFile</c> (a
/// file, as My sound plays) wants a RIFF/WAVE container whose chunks stay inside it and a <c>fmt </c>
/// chunk in PCM, ADPCM or IEEE float; <c>ValidateSoundData</c> (a stream, as the chimes play) also
/// wants a <c>fmt </c> chunk of 16 bytes, or of 18 plus its own cbSize, so that rule applies only when
/// <c>asStream</c> is set. A <c>data</c> chunk with
/// something in it is required too, since without one there is nothing to hear.</summary>
internal static class WaveFile
{
    private const short FormatPcm = 1;
    private const short FormatAdpcm = 2;
    private const short FormatIeeeFloat = 3;

    /// <summary>Has the RIFF/WAVE header, whatever is inside.</summary>
    public static bool IsWave(ReadOnlySpan<byte> bytes)
        => bytes.Length >= 12 && bytes.StartsWith("RIFF"u8) && bytes[8..].StartsWith("WAVE"u8);

    /// <summary>The format tag of the first <c>fmt </c> chunk, or null when there is none to read.</summary>
    public static short? FormatTag(ReadOnlySpan<byte> wav)
    {
        if (!IsWave(wav)) return null;
        long pos = 12;
        while (pos + 8 + 2 <= wav.Length)
        {
            var size = BinaryPrimitives.ReadUInt32LittleEndian(wav[(int)(pos + 4)..]);
            if (wav[(int)pos..].StartsWith("fmt "u8)) return BinaryPrimitives.ReadInt16LittleEndian(wav[(int)(pos + 8)..]);
            pos += 8 + size + (size & 1);
        }
        return null;
    }

    /// <summary>A format tag SoundPlayer refuses whatever else is right with the file.</summary>
    public static bool IsPlayableFormat(short tag) => tag is FormatPcm or FormatAdpcm or FormatIeeeFloat;

    /// <summary>Whether SoundPlayer plays <paramref name="wav"/>: from a file by default, as My sound
    /// plays, or from a stream with <paramref name="asStream"/>, as the built-in chimes play.</summary>
    public static bool IsPlayable(ReadOnlySpan<byte> wav, bool asStream = false)
    {
        if (!IsWave(wav)) return false;

        var riffSize = BinaryPrimitives.ReadUInt32LittleEndian(wav[4..]);
        if (riffSize < 4) return false;
        var riffEnd = 8 + (long)riffSize;

        short? format = null;
        var hasAudio = false;
        long pos = 12;
        while (pos + 8 <= Math.Min(riffEnd, wav.Length))
        {
            var chunk = wav.Slice((int)pos, 8);
            var size = BinaryPrimitives.ReadUInt32LittleEndian(chunk[4..]);
            var dataStart = pos + 8;
            if (dataStart + size > riffEnd) return false;   // a chunk that runs past its container

            if (chunk.StartsWith("fmt "u8) && format is null)
            {
                if (size < 16 || dataStart + 16 > wav.Length) return false;
                if (asStream && size != 16)
                {
                    if (size < 18 || dataStart + 18 > wav.Length) return false;
                    var cbSize = BinaryPrimitives.ReadUInt16LittleEndian(wav[(int)(dataStart + 16)..]);
                    if (cbSize + 18 != size) return false;
                }
                format = BinaryPrimitives.ReadInt16LittleEndian(wav[(int)dataStart..]);
            }
            else if (chunk.StartsWith("data"u8) && size > 0)
            {
                hasAudio = true;
            }

            pos = dataStart + size + (size & 1);   // chunks are padded to an even length
        }

        return format is { } tag && IsPlayableFormat(tag) && hasAudio;
    }
}

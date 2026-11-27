using System.Buffers.Binary;

namespace Tidsro.Services;

/// <summary>Whether SoundPlayer will actually play a .wav, decided from its bytes.
///
/// <c>SoundPlayer.Load()</c> is no such check: on a path it only confirms the file exists, and on a
/// stream it only copies the bytes. The format is validated inside <c>Play()</c>, and an alarm
/// swallows that exception, so a file that fails there rings silently. These are the same rules
/// .NET applies in <c>SoundPlayer.ValidateSoundFile</c> before it calls PlaySound: a RIFF/WAVE
/// container whose chunks stay inside it, and a <c>fmt </c> chunk in PCM, ADPCM or IEEE float. A
/// <c>data</c> chunk with something in it is required too, since without one there is nothing to hear.</summary>
internal static class WaveFile
{
    private const short FormatPcm = 1;
    private const short FormatAdpcm = 2;
    private const short FormatIeeeFloat = 3;

    public static bool IsPlayable(ReadOnlySpan<byte> wav)
    {
        if (wav.Length < 12 || !wav.StartsWith("RIFF"u8) || !wav[8..].StartsWith("WAVE"u8)) return false;

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
                format = BinaryPrimitives.ReadInt16LittleEndian(wav[(int)dataStart..]);
            }
            else if (chunk.StartsWith("data"u8) && size > 0)
            {
                hasAudio = true;
            }

            pos = dataStart + size + (size & 1);   // chunks are padded to an even length
        }

        return format is FormatPcm or FormatAdpcm or FormatIeeeFloat && hasAudio;
    }
}

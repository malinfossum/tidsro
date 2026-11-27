namespace Tidsro.Services;

/// <summary>One MPEG audio frame inside a file: where it starts and how many bytes it takes.</summary>
internal readonly record struct Mp3Frame(int Offset, int Length);

/// <summary>The audio frames found in a file, and the format they share.</summary>
internal sealed record Mp3Stream(int SampleRate, int Channels, int SamplesPerFrame, int BitRate,
    IReadOnlyList<Mp3Frame> Frames)
{
    public TimeSpan Duration => TimeSpan.FromSeconds((double)Frames.Count * SamplesPerFrame / SampleRate);
}

/// <summary>Recognizes an MP3 by walking its frames, the way <c>CustomSoundStore</c> recognizes a WAV
/// by its RIFF header: from the bytes, never the file name.
///
/// One plausible frame header turns up by chance in almost any binary file, so a stream only counts
/// once three headers in a row each sit exactly where the one before said the next would start. Only
/// MPEG audio Layer III is accepted, because that is what an .mp3 is.</summary>
internal static class Mp3Frames
{
    private const int HeaderSize = 4;
    private const int FramesToLock = 3;

    // Layer III bitrates in kbit/s by index; index 0 is "free format" and 15 is invalid.
    private static readonly int[] BitRatesV1 = [0, 32, 40, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320, 0];
    private static readonly int[] BitRatesV2 = [0, 8, 16, 24, 32, 40, 48, 56, 64, 80, 96, 112, 128, 144, 160, 0];

    // Sample rates by version (index 0 = MPEG 2.5, 2 = MPEG 2, 3 = MPEG 1) and rate index.
    private static readonly int[][] SampleRates =
    [
        [11025, 12000, 8000],
        [],
        [22050, 24000, 16000],
        [44100, 48000, 32000],
    ];

    /// <summary>The audio frames in <paramref name="data"/>, or null when it is not an MP3.</summary>
    public static Mp3Stream? Find(ReadOnlySpan<byte> data)
    {
        var pos = SkipId3Tags(data);

        var start = NextLock(data, pos, reference: -1, FramesToLock);
        if (start < 0) return null;

        var frames = new List<Mp3Frame>();
        var p = start;
        while (true)
        {
            var length = FrameLength(data[p..]);
            if (length > 0 && p + length <= data.Length && SameStream(data, start, p))
            {
                frames.Add(new Mp3Frame(p, length));
                p += length;
                continue;
            }

            // A damaged frame or a trailing tag: look further on for the stream to pick up again,
            // so one bad frame costs that frame rather than the rest of the file.
            p = NextLock(data, p + 1, reference: start, framesNeeded: 2);
            if (p < 0) break;
        }

        if (IsInfoFrame(data, frames[0])) frames.RemoveAt(0);
        if (frames.Count == 0) return null;

        var first = data[frames[0].Offset..];
        var version = (first[1] >> 3) & 3;
        return new Mp3Stream(
            SampleRate: SampleRates[version][(first[2] >> 2) & 3],
            Channels: (first[3] >> 6) == 3 ? 1 : 2,
            SamplesPerFrame: version == 3 ? 1152 : 576,
            BitRate: (version == 3 ? BitRatesV1 : BitRatesV2)[first[2] >> 4] * 1000,
            Frames: frames);
    }

    /// <summary>The length in bytes of the Layer III frame whose header starts
    /// <paramref name="header"/>, or 0 when those bytes are not one.</summary>
    public static int FrameLength(ReadOnlySpan<byte> header)
    {
        if (header.Length < HeaderSize) return 0;
        if (header[0] != 0xFF || (header[1] & 0xE0) != 0xE0) return 0;   // 11-bit sync word

        var version = (header[1] >> 3) & 3;   // 0 = MPEG 2.5, 1 = reserved, 2 = MPEG 2, 3 = MPEG 1
        var layer = (header[1] >> 1) & 3;     // 1 = Layer III
        var bitRateIndex = header[2] >> 4;
        var rateIndex = (header[2] >> 2) & 3;
        var padding = (header[2] >> 1) & 1;
        var emphasis = header[3] & 3;

        if (version == 1 || layer != 1 || rateIndex == 3 || emphasis == 2) return 0;

        var bitRate = (version == 3 ? BitRatesV1 : BitRatesV2)[bitRateIndex];
        if (bitRate == 0) return 0;   // free format or invalid: no way to know where the frame ends

        var sampleRate = SampleRates[version][rateIndex];
        var coefficient = version == 3 ? 144000 : 72000;
        return coefficient * bitRate / sampleRate + padding;
    }

    // ID3v2 tags sit in front of the audio and can be large (cover art), so they are stepped over by
    // their declared size rather than searched through. Several can follow one another.
    private static int SkipId3Tags(ReadOnlySpan<byte> data)
    {
        var pos = 0;
        while (pos + 10 <= data.Length && data[pos..].StartsWith("ID3"u8))
        {
            var t = data.Slice(pos, 10);
            if (t[3] is < 2 or > 4 || t[4] == 0xFF) break;
            if ((t[6] | t[7] | t[8] | t[9]) >= 0x80) break;   // sizes are "syncsafe": 7 bits a byte

            var size = (t[6] << 21) | (t[7] << 14) | (t[8] << 7) | t[9];
            var footer = (t[5] & 0x10) != 0 ? 10 : 0;
            pos += 10 + size + footer;
        }
        return Math.Min(pos, data.Length);
    }

    // The first offset at or after `from` where `framesNeeded` frames chain, or -1. With a reference
    // frame, every frame in the chain must also match its format.
    private static int NextLock(ReadOnlySpan<byte> data, int from, int reference, int framesNeeded)
    {
        for (var p = from; p + HeaderSize <= data.Length; p++)
        {
            if (data[p] != 0xFF) continue;
            if (Chains(data, p, reference < 0 ? p : reference, framesNeeded)) return p;
        }
        return -1;
    }

    private static bool Chains(ReadOnlySpan<byte> data, int p, int reference, int framesNeeded)
    {
        for (var i = 0; i < framesNeeded; i++)
        {
            var length = FrameLength(data[p..]);
            if (length == 0 || p + length > data.Length || !SameStream(data, reference, p)) return false;
            p += length;
        }
        return true;
    }

    // Same MPEG version and layer, same sample rate, and both mono or both not.
    private static bool SameStream(ReadOnlySpan<byte> data, int a, int b)
        => (data[a + 1] & 0xFE) == (data[b + 1] & 0xFE)
           && (data[a + 2] & 0x0C) == (data[b + 2] & 0x0C)
           && ((data[a + 3] >> 6) == 3) == ((data[b + 3] >> 6) == 3);

    // Encoders put a Xing, Info or VBRI block in a first frame that holds no audio. It sits after
    // the side information, whose size depends on the version and the channel count.
    private static bool IsInfoFrame(ReadOnlySpan<byte> data, Mp3Frame frame)
    {
        var f = data.Slice(frame.Offset, frame.Length);
        var mono = (f[3] >> 6) == 3;
        var mpeg1 = ((f[1] >> 3) & 3) == 3;
        var crc = (f[1] & 1) == 0 ? 2 : 0;
        var sideInfo = mpeg1 ? (mono ? 17 : 32) : (mono ? 9 : 17);
        var at = HeaderSize + crc + sideInfo;

        return HasTag(f, at, "Xing"u8) || HasTag(f, at, "Info"u8) || HasTag(f, HeaderSize + 32, "VBRI"u8);
    }

    private static bool HasTag(ReadOnlySpan<byte> frame, int at, ReadOnlySpan<byte> tag)
        => at + tag.Length <= frame.Length && frame.Slice(at, tag.Length).SequenceEqual(tag);
}

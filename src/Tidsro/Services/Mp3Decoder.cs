using System.Buffers.Binary;
using System.IO;
using System.Runtime.InteropServices;

namespace Tidsro.Services;

/// <summary>Turns an MP3 into a 16-bit PCM WAV, the one format <c>SoundPlayer</c> plays, using the MP3
/// codec that ships with Windows (the Audio Compression Manager's <c>l3codeca.acm</c>).
///
/// Why decode once, up front, instead of playing the MP3: WPF's <c>MediaPlayer</c> is built on the
/// Windows Media Player control and fails outright when that optional feature is not installed, with
/// "Windows Media Player version 10 or later is required". An alarm that is silent on some machines is
/// the failure this app exists to avoid. Decoding at choose time means a file Windows cannot decode
/// is refused on screen, and every alarm still plays through the same SoundPlayer the built-in chimes
/// use. No audio device is needed: this is format conversion, not playback.</summary>
internal static class Mp3Decoder
{
    private const int FramesPerChunk = 32;

    /// <summary>The decoded WAV file, or null when <paramref name="mp3"/> is not an MP3, is longer
    /// than <paramref name="maxLength"/>, or cannot be decoded on this machine. Returned as a slice
    /// of the buffer it was decoded into, so a long file is not copied once more on the way out.
    /// <paramref name="outputChunk"/> shrinks the codec's output buffer; internal for tests.</summary>
    public static ReadOnlyMemory<byte>? ToWav(ReadOnlySpan<byte> mp3, TimeSpan maxLength, int? outputChunk = null)
    {
        var stream = Mp3Frames.Find(mp3);
        if (stream is null || stream.Duration > maxLength) return null;
        return Decode(mp3, stream, outputChunk);
    }

    private static ReadOnlyMemory<byte>? Decode(ReadOnlySpan<byte> mp3, Mp3Stream stream, int? outputChunk)
    {
        var source = new MpegLayer3WaveFormat
        {
            wFormatTag = WaveFormatMpegLayer3,
            nChannels = (short)stream.Channels,
            nSamplesPerSec = stream.SampleRate,
            nAvgBytesPerSec = stream.BitRate / 8,
            nBlockAlign = 1,
            wBitsPerSample = 0,
            cbSize = MpegLayer3ExtraBytes,
            wID = MpegLayer3IdMpeg,
            fdwFlags = MpegLayer3FlagPaddingIso,
            nBlockSize = (short)stream.Frames[0].Length,
            nFramesPerBlock = 1,
            nCodecDelay = 0,
        };
        var target = new WaveFormatEx
        {
            wFormatTag = WaveFormatPcm,
            nChannels = (short)stream.Channels,
            nSamplesPerSec = stream.SampleRate,
            nAvgBytesPerSec = stream.SampleRate * stream.Channels * 2,
            nBlockAlign = (short)(stream.Channels * 2),
            wBitsPerSample = 16,
            cbSize = 0,
        };

        var blockAlign = stream.Channels * 2;
        var largestFrame = stream.Frames.Max(f => f.Length);
        var srcSize = largestFrame * FramesPerChunk;
        var acm = IntPtr.Zero;
        var src = IntPtr.Zero;
        var dst = IntPtr.Zero;
        var header = IntPtr.Zero;
        var prepared = false;
        try
        {
            // Inside the try: on a Windows without msacm32 this first call is the one that throws.
            // The codec now and then refuses an open with ACMERR_NOTPOSSIBLE while other processes
            // open streams at the same time, so try a few times before calling the file undecodable.
            // A machine with no MP3 codec gives the same answer every time and costs ~100 ms here.
            var opened = -1;
            for (var attempt = 0; attempt < OpenAttempts && opened != 0; attempt++)
            {
                if (attempt > 0) Thread.Sleep(OpenRetryDelay);
                opened = acmStreamOpen(out acm, IntPtr.Zero, ref source, ref target, IntPtr.Zero,
                    IntPtr.Zero, IntPtr.Zero, AcmStreamOpenNonRealtime);
                // A failed open can leave garbage in the handle, and closing that in the finally
                // block crashes the process with an access violation.
                if (opened != 0) acm = IntPtr.Zero;
            }
            if (opened != 0) return null;   // no MP3 codec on this machine

            if (acmStreamSize(acm, srcSize, out var dstSize, AcmStreamSizeSource) != 0 || dstSize <= 0)
                return null;
            if (outputChunk is { } chunk) dstSize = Math.Min(dstSize, chunk);

            src = Marshal.AllocHGlobal(srcSize);
            dst = Marshal.AllocHGlobal(dstSize);
            header = Marshal.AllocHGlobal(Marshal.SizeOf<AcmStreamHeader>());
            Marshal.StructureToPtr(new AcmStreamHeader
            {
                cbStruct = Marshal.SizeOf<AcmStreamHeader>(),
                pbSrc = src,
                cbSrcLength = srcSize,
                pbDst = dst,
                cbDstLength = dstSize,
                dwReservedDriver = new int[ReservedDriverInts],
            }, header, fDeleteOld: false);

            if (acmStreamPrepareHeader(acm, header, 0) != 0) return null;
            prepared = true;

            // Sized up front from the frame count, so a five-minute file is not regrown and copied
            // on its way to 58 MB. The first 44 bytes are left for the header.
            var estimate = Math.Min(int.MaxValue / 2,
                HeaderSize + (long)stream.Frames.Count * stream.SamplesPerFrame * blockAlign + dstSize);
            using var output = new MemoryStream((int)estimate);
            output.Write(new byte[HeaderSize]);
            var staging = new byte[Math.Max(srcSize, dstSize)];

            var frame = 0;
            var pending = 0;   // bytes the codec left unread at the front of src
            var flags = AcmConvertStart | AcmConvertBlockAlign;
            var rounds = 0;
            var maxRounds = 2 * stream.Frames.Count + 64;
            while (true)
            {
                if (++rounds > maxRounds) return null;   // a codec that never drains

                // Top up src with whole frames, after whatever the codec did not use last time. At
                // most FramesPerChunk at a time, so one round's audio fits the output buffer even
                // when a variable-bitrate file packs many small frames into the source buffer.
                var filled = pending;
                for (var added = 0; added < FramesPerChunk && frame < stream.Frames.Count
                                    && filled + stream.Frames[frame].Length <= srcSize; added++)
                {
                    var f = stream.Frames[frame++];
                    mp3.Slice(f.Offset, f.Length).CopyTo(staging);
                    Marshal.Copy(staging, 0, src + filled, f.Length);
                    filled += f.Length;
                }

                var last = frame == stream.Frames.Count;
                if (last) flags = (flags & ~AcmConvertBlockAlign) | AcmConvertEnd;

                SetInt(header, nameof(AcmStreamHeader.cbSrcLength), filled);
                if (acmStreamConvert(acm, header, flags) != 0)
                {
                    if (last && filled == 0) break;   // a codec that will not be asked to flush twice
                    return null;
                }
                flags &= ~AcmConvertStart;

                var used = GetInt(header, nameof(AcmStreamHeader.cbSrcLengthUsed));
                var produced = GetInt(header, nameof(AcmStreamHeader.cbDstLengthUsed));
                if (produced > 0)
                {
                    Marshal.Copy(dst, staging, 0, produced);
                    output.Write(staging, 0, produced);
                }

                pending = filled - used;
                if (pending > 0 && used > 0)
                {
                    Marshal.Copy(src + used, staging, 0, pending);
                    Marshal.Copy(staging, 0, src, pending);
                }

                if (last)
                {
                    // A full output buffer can mean the codec still holds decoded audio it had no
                    // room for, so keep asking until a call comes back short. Without this the last
                    // half second of a long file went missing.
                    if (produced < dstSize && pending == 0) break;
                    if (used == 0 && produced == 0) return null;   // never a shortened sound: refuse it
                    continue;
                }
                if (used == 0 && produced == 0) return null;   // stuck: a stream the codec will not read
            }

            // A truncated last frame can leave half a sample; WAV data must be whole blocks.
            var pcmLength = ((int)output.Length - HeaderSize) / blockAlign * blockAlign;
            if (pcmLength <= 0) return null;

            var wav = output.GetBuffer();
            WriteHeader(wav, pcmLength, stream.SampleRate, stream.Channels);
            return new ReadOnlyMemory<byte>(wav, 0, HeaderSize + pcmLength);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return null;
        }
        finally
        {
            if (prepared)
            {
                SetInt(header, nameof(AcmStreamHeader.cbSrcLength), srcSize);   // unprepare wants the prepared sizes back
                acmStreamUnprepareHeader(acm, header, 0);
            }
            if (header != IntPtr.Zero) Marshal.FreeHGlobal(header);
            if (dst != IntPtr.Zero) Marshal.FreeHGlobal(dst);
            if (src != IntPtr.Zero) Marshal.FreeHGlobal(src);
            if (acm != IntPtr.Zero) acmStreamClose(acm, 0);
        }
    }

    private const int HeaderSize = 44;
    private const int OpenAttempts = 4;
    private static readonly TimeSpan OpenRetryDelay = TimeSpan.FromMilliseconds(30);

    /// <summary>A canonical 44-byte RIFF/WAVE header for <paramref name="pcmLength"/> bytes of
    /// 16-bit PCM, written over the space left for it at the front of <paramref name="wav"/>.</summary>
    private static void WriteHeader(byte[] wav, int pcmLength, int sampleRate, int channels)
    {
        var s = wav.AsSpan();
        "RIFF"u8.CopyTo(s);
        BinaryPrimitives.WriteInt32LittleEndian(s[4..], 36 + pcmLength);
        "WAVE"u8.CopyTo(s[8..]);
        "fmt "u8.CopyTo(s[12..]);
        BinaryPrimitives.WriteInt32LittleEndian(s[16..], 16);
        BinaryPrimitives.WriteInt16LittleEndian(s[20..], WaveFormatPcm);
        BinaryPrimitives.WriteInt16LittleEndian(s[22..], (short)channels);
        BinaryPrimitives.WriteInt32LittleEndian(s[24..], sampleRate);
        BinaryPrimitives.WriteInt32LittleEndian(s[28..], sampleRate * channels * 2);
        BinaryPrimitives.WriteInt16LittleEndian(s[32..], (short)(channels * 2));
        BinaryPrimitives.WriteInt16LittleEndian(s[34..], 16);
        "data"u8.CopyTo(s[36..]);
        BinaryPrimitives.WriteInt32LittleEndian(s[40..], pcmLength);
    }

    private static int GetInt(IntPtr header, string field)
        => Marshal.ReadInt32(header, (int)Marshal.OffsetOf<AcmStreamHeader>(field));

    private static void SetInt(IntPtr header, string field, int value)
        => Marshal.WriteInt32(header, (int)Marshal.OffsetOf<AcmStreamHeader>(field), value);

    // --- msacm32 ---------------------------------------------------------------------------------

    private const short WaveFormatPcm = 1;
    private const short WaveFormatMpegLayer3 = 0x55;
    private const short MpegLayer3ExtraBytes = 12;
    private const short MpegLayer3IdMpeg = 1;
    private const int MpegLayer3FlagPaddingIso = 0;
    private const int AcmStreamOpenNonRealtime = 0x4;
    private const int AcmStreamSizeSource = 0;
    private const int AcmConvertBlockAlign = 0x4;
    private const int AcmConvertStart = 0x10;
    private const int AcmConvertEnd = 0x20;

    // msacm.h reserves 15 DWORDs for the driver on 64-bit Windows and 10 on 32-bit. 15 is right for
    // the shipped x64 build, and only oversizes the header on x86, which ACM accepts.
    private const int ReservedDriverInts = 15;

    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    private struct WaveFormatEx
    {
        public short wFormatTag;
        public short nChannels;
        public int nSamplesPerSec;
        public int nAvgBytesPerSec;
        public short nBlockAlign;
        public short wBitsPerSample;
        public short cbSize;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    private struct MpegLayer3WaveFormat
    {
        public short wFormatTag;
        public short nChannels;
        public int nSamplesPerSec;
        public int nAvgBytesPerSec;
        public short nBlockAlign;
        public short wBitsPerSample;
        public short cbSize;
        public short wID;
        public int fdwFlags;
        public short nBlockSize;
        public short nFramesPerBlock;
        public short nCodecDelay;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AcmStreamHeader
    {
        public int cbStruct;
        public int fdwStatus;
        public IntPtr dwUser;
        public IntPtr pbSrc;
        public int cbSrcLength;
        public int cbSrcLengthUsed;
        public IntPtr dwSrcUser;
        public IntPtr pbDst;
        public int cbDstLength;
        public int cbDstLengthUsed;
        public IntPtr dwDstUser;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = ReservedDriverInts)]
        public int[] dwReservedDriver;
    }

    [DllImport("msacm32.dll")]
    private static extern int acmStreamOpen(out IntPtr phas, IntPtr had, ref MpegLayer3WaveFormat pwfxSrc,
        ref WaveFormatEx pwfxDst, IntPtr pwfltr, IntPtr dwCallback, IntPtr dwInstance, int fdwOpen);

    [DllImport("msacm32.dll")]
    private static extern int acmStreamSize(IntPtr has, int cbInput, out int pdwOutputBytes, int fdwSize);

    [DllImport("msacm32.dll")]
    private static extern int acmStreamPrepareHeader(IntPtr has, IntPtr pash, int fdwPrepare);

    [DllImport("msacm32.dll")]
    private static extern int acmStreamConvert(IntPtr has, IntPtr pash, int fdwConvert);

    [DllImport("msacm32.dll")]
    private static extern int acmStreamUnprepareHeader(IntPtr has, IntPtr pash, int fdwUnprepare);

    [DllImport("msacm32.dll")]
    private static extern int acmStreamClose(IntPtr has, int fdwClose);
}

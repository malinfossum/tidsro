using System.Buffers.Binary;
using Tidsro.Services;

namespace Tidsro.Tests;

/// <summary>The rules SoundPlayer applies before it plays a .wav, checked from the bytes. Each refused
/// case here is a file <c>SoundPlayer.Load()</c> accepts and <c>Play()</c> then rejects, which used to
/// install as My sound and ring silently.</summary>
public class WaveFileTests
{
    /// <summary>A RIFF/WAVE file with one fmt chunk and one data chunk.</summary>
    internal static byte[] Wave(short formatTag = 1, int fmtSize = 16, int dataSize = 8, int? riffSize = null)
    {
        var bytes = new byte[12 + 8 + fmtSize + 8 + dataSize];
        var s = bytes.AsSpan();
        "RIFF"u8.CopyTo(s);
        BinaryPrimitives.WriteInt32LittleEndian(s[4..], riffSize ?? bytes.Length - 8);
        "WAVE"u8.CopyTo(s[8..]);
        "fmt "u8.CopyTo(s[12..]);
        BinaryPrimitives.WriteInt32LittleEndian(s[16..], fmtSize);
        BinaryPrimitives.WriteInt16LittleEndian(s[20..], formatTag);
        BinaryPrimitives.WriteInt16LittleEndian(s[22..], 1);        // mono
        BinaryPrimitives.WriteInt32LittleEndian(s[24..], 8000);
        BinaryPrimitives.WriteInt32LittleEndian(s[28..], 16000);
        BinaryPrimitives.WriteInt16LittleEndian(s[32..], 2);
        BinaryPrimitives.WriteInt16LittleEndian(s[34..], 16);
        var data = 20 + fmtSize;
        "data"u8.CopyTo(s[data..]);
        BinaryPrimitives.WriteInt32LittleEndian(s[(data + 4)..], dataSize);
        return bytes;
    }

    [Theory]
    [InlineData((short)1)]   // PCM
    [InlineData((short)2)]   // MS ADPCM
    [InlineData((short)3)]   // IEEE float
    public void The_formats_SoundPlayer_plays_pass(short tag)
        => Assert.True(WaveFile.IsPlayable(Wave(tag)));

    [Theory]
    [InlineData(unchecked((short)0xFFFE))]   // WAVE_FORMAT_EXTENSIBLE, which a 24-bit export uses
    [InlineData((short)0x55)]                // MP3 inside a RIFF container
    [InlineData((short)0x11)]                // IMA ADPCM
    public void Other_formats_are_refused(short tag)
        => Assert.False(WaveFile.IsPlayable(Wave(tag)));

    [Fact]
    public void A_riff_size_of_zero_is_refused()
        => Assert.False(WaveFile.IsPlayable(Wave(riffSize: 0)));

    [Fact]
    public void A_chunk_running_past_the_container_is_refused()
        => Assert.False(WaveFile.IsPlayable(Wave(riffSize: 40)));   // ends inside the data chunk

    [Fact]
    public void A_file_with_no_audio_in_it_is_refused()
        => Assert.False(WaveFile.IsPlayable(Wave(dataSize: 0)));

    [Fact]
    public void A_file_with_no_format_chunk_is_refused()
    {
        var bytes = Wave();
        "junk"u8.CopyTo(bytes.AsSpan(12));   // rename the fmt chunk to one nobody reads

        Assert.False(WaveFile.IsPlayable(bytes));
    }

    [Fact]
    public void Chunks_before_the_format_chunk_are_stepped_over_with_their_padding()
    {
        var wave = Wave();
        var list = new byte[] { (byte)'L', (byte)'I', (byte)'S', (byte)'T', 3, 0, 0, 0, 1, 2, 3, 0 };   // odd size, one pad byte
        var bytes = wave[..12].Concat(list).Concat(wave[12..]).ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), bytes.Length - 8);

        Assert.True(WaveFile.IsPlayable(bytes));
    }

    [Theory]
    [InlineData("")]
    [InlineData("RIFF")]
    [InlineData("ID3 this is not audio")]
    public void Text_is_refused(string text)
        => Assert.False(WaveFile.IsPlayable(System.Text.Encoding.UTF8.GetBytes(text)));
}

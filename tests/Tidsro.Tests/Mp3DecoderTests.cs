using System.Buffers.Binary;
using System.IO;
using System.Media;
using Tidsro.Services;

namespace Tidsro.Tests;

/// <summary>Turning an MP3 into the PCM wave audio SoundPlayer plays, using the MP3 codec that ships
/// with Windows. Nothing here needs a speaker: decoding is format work, not playback.</summary>
public class Mp3DecoderTests
{
    private static readonly TimeSpan Plenty = TimeSpan.FromMinutes(5);

    [Fact]
    public void The_tone_decodes_to_a_pcm_wav_of_the_same_rate_channels_and_length()
    {
        var wav = Mp3Decoder.ToWav(Mp3FramesTests.Tone(), Plenty);

        Assert.NotNull(wav);
        Assert.Equal("RIFF"u8.ToArray(), wav![..4]);
        Assert.Equal("WAVE"u8.ToArray(), wav[8..12]);
        Assert.Equal(1, BinaryPrimitives.ReadInt16LittleEndian(wav.AsSpan(20)));       // PCM
        Assert.Equal(1, BinaryPrimitives.ReadInt16LittleEndian(wav.AsSpan(22)));       // mono
        Assert.Equal(44100, BinaryPrimitives.ReadInt32LittleEndian(wav.AsSpan(24)));
        Assert.Equal(16, BinaryPrimitives.ReadInt16LittleEndian(wav.AsSpan(34)));      // bits per sample

        var dataBytes = BinaryPrimitives.ReadInt32LittleEndian(wav.AsSpan(40));
        var seconds = dataBytes / (44100.0 * 2);
        Assert.InRange(seconds, 1.4, 1.65);   // 1.5 s of tone, give or take the codec's frame padding
        Assert.Equal(44 + dataBytes, wav.Length);
    }

    // The decoded audio is the tone itself, not silence or noise the right length: the same peak
    // ffmpeg measures on the fixture (max_volume -24.5 dB, about 1953 of 32767), and a 660 Hz pitch
    // read off the zero crossings.
    [Fact]
    public void The_decoded_audio_is_the_tone()
    {
        var wav = Mp3Decoder.ToWav(Mp3FramesTests.Tone(), Plenty)!;
        short Sample(int i) => BinaryPrimitives.ReadInt16LittleEndian(wav.AsSpan(44 + i * 2));
        var count = (wav.Length - 44) / 2;

        var peak = 0;
        for (var i = 0; i < count; i++) peak = Math.Max(peak, Math.Abs((int)Sample(i)));
        Assert.InRange(peak, 1800, 2100);

        // One second of steady tone: 660 cycles cross zero 1320 times.
        var crossings = 0;
        for (var i = 11025; i < 11025 + 44100; i++)
            if ((Sample(i) >= 0) != (Sample(i + 1) >= 0)) crossings++;
        Assert.InRange(crossings, 1300, 1340);
    }

    [Fact]
    public void SoundPlayer_accepts_the_result()
    {
        var wav = Mp3Decoder.ToWav(Mp3FramesTests.Tone(), Plenty)!;

        using var player = new SoundPlayer(new MemoryStream(wav));
        player.Load();
    }

    // Most real files are stereo; the second fixture is a 1 s, 440 Hz stereo tone at 48 kHz, 128 kbps.
    [Fact]
    public void A_stereo_mp3_decodes_to_stereo_at_its_own_rate()
    {
        var mp3 = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "test-tone-stereo.mp3"));

        var wav = Mp3Decoder.ToWav(mp3, Plenty)!;

        Assert.Equal(2, BinaryPrimitives.ReadInt16LittleEndian(wav.AsSpan(22)));
        Assert.Equal(48000, BinaryPrimitives.ReadInt32LittleEndian(wav.AsSpan(24)));
        Assert.Equal(4, BinaryPrimitives.ReadInt16LittleEndian(wav.AsSpan(32)));   // block align
        var seconds = BinaryPrimitives.ReadInt32LittleEndian(wav.AsSpan(40)) / (48000.0 * 4);
        Assert.InRange(seconds, 0.95, 1.1);
    }

    [Fact]
    public void Something_that_is_not_an_mp3_gives_nothing()
        => Assert.Null(Mp3Decoder.ToWav("ID3 this is not audio"u8.ToArray(), Plenty));

    [Fact]
    public void An_mp3_longer_than_the_limit_gives_nothing()
        => Assert.Null(Mp3Decoder.ToWav(Mp3FramesTests.Tone(), TimeSpan.FromSeconds(1)));
}

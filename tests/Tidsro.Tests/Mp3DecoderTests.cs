using System.Buffers.Binary;
using System.IO;
using Tidsro.Services;

namespace Tidsro.Tests;

/// <summary>Turning an MP3 into the PCM wave audio SoundPlayer plays, using the MP3 codec that ships
/// with Windows. Nothing here needs a speaker: decoding is format work, not playback.</summary>
public class Mp3DecoderTests
{
    private static readonly TimeSpan Plenty = TimeSpan.FromMinutes(5);

    private static byte[]? Decode(byte[] mp3, int? outputChunk = null)
        => Mp3Decoder.ToWav(mp3, Plenty, outputChunk)?.ToArray();

    [Fact]
    public void The_tone_decodes_to_a_pcm_wav_of_the_same_rate_channels_and_length()
    {
        var wav = Decode(Mp3FramesTests.Tone());

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
        var wav = Decode(Mp3FramesTests.Tone())!;
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
    public void The_result_passes_the_checks_SoundPlayer_makes_before_playing()
        => Assert.True(WaveFile.IsPlayable(Decode(Mp3FramesTests.Tone())));

    // Most real files are stereo; the second fixture is a 1 s, 440 Hz stereo tone at 48 kHz, 128 kbps.
    [Fact]
    public void A_stereo_mp3_decodes_to_stereo_at_its_own_rate()
    {
        var mp3 = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "test-tone-stereo.mp3"));

        var wav = Decode(mp3)!;

        Assert.Equal(2, BinaryPrimitives.ReadInt16LittleEndian(wav.AsSpan(22)));
        Assert.Equal(48000, BinaryPrimitives.ReadInt32LittleEndian(wav.AsSpan(24)));
        Assert.Equal(4, BinaryPrimitives.ReadInt16LittleEndian(wav.AsSpan(32)));   // block align
        var seconds = BinaryPrimitives.ReadInt32LittleEndian(wav.AsSpan(40)) / (48000.0 * 4);
        Assert.InRange(seconds, 0.95, 1.1);
    }

    // A variable-bitrate file can pack more frames into one input buffer than one output buffer
    // holds, and the codec keeps the rest back until asked again. Squeezing the output buffer to a
    // few frames forces that on the fixture: the tail must still come out, sample for sample.
    [Theory]
    [InlineData(4608)]   // two frames of mono audio
    [InlineData(6912)]   // three
    public void A_small_output_buffer_still_yields_every_sample(int outputChunk)
    {
        var whole = Decode(Mp3FramesTests.Tone())!;

        var squeezed = Decode(Mp3FramesTests.Tone(), outputChunk);

        Assert.NotNull(squeezed);
        Assert.Equal(whole.Length, squeezed!.Length);
        Assert.Equal(whole, squeezed);
    }

    // Never a shortened sound: an output buffer too small for one frame is refused outright.
    [Fact]
    public void An_output_buffer_smaller_than_a_frame_gives_nothing_rather_than_part()
        => Assert.Null(Decode(Mp3FramesTests.Tone(), outputChunk: 1000));

    [Fact]
    public void Something_that_is_not_an_mp3_gives_nothing()
        => Assert.Null(Mp3Decoder.ToWav("ID3 this is not audio"u8.ToArray(), Plenty));

    [Fact]
    public void An_mp3_longer_than_the_limit_gives_nothing()
        => Assert.Null(Mp3Decoder.ToWav(Mp3FramesTests.Tone(), TimeSpan.FromSeconds(1)));
}

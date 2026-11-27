using System.IO;
using Tidsro.Services;

namespace Tidsro.Tests;

/// <summary>Recognizing an MP3 by its frames rather than its extension. The fixture is a 1.5 s,
/// 660 Hz mono tone at 64 kbps, made with ffmpeg, with an ID3v2 tag in front.</summary>
public class Mp3FramesTests
{
    internal static byte[] Tone() =>
        File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "test-tone.mp3"));

    [Fact]
    public void A_real_mp3_is_recognized_with_its_sample_rate_and_channels()
    {
        var stream = Mp3Frames.Find(Tone());

        Assert.NotNull(stream);
        Assert.Equal(44100, stream!.SampleRate);
        Assert.Equal(1, stream.Channels);
        Assert.True(stream.Frames.Count > 50);   // 1.5 s at 1152 samples a frame is about 57
    }

    [Fact]
    public void The_id3_tag_in_front_is_skipped()
    {
        var bytes = Tone();
        var stream = Mp3Frames.Find(bytes)!;

        Assert.Equal("ID3"u8.ToArray(), bytes[..3]);
        Assert.True(stream.Frames[0].Offset >= 10);
        Assert.Equal(0xFF, bytes[stream.Frames[0].Offset]);
    }

    [Fact]
    public void An_mp3_without_a_tag_is_recognized_too()
    {
        var bytes = Tone();
        var first = Mp3Frames.Find(bytes)!.Frames[0].Offset;

        Assert.NotNull(Mp3Frames.Find(bytes[first..]));
    }

    // The encoder's Info frame carries no audio, so it is not handed to the decoder.
    [Fact]
    public void The_encoder_info_frame_is_not_counted_as_audio()
    {
        var bytes = Tone();
        var stream = Mp3Frames.Find(bytes)!;
        var firstFrame = bytes.AsSpan(stream.Frames[0].Offset, stream.Frames[0].Length);

        Assert.Equal(-1, firstFrame.IndexOf("Info"u8));
        Assert.Equal(-1, firstFrame.IndexOf("Xing"u8));
    }

    [Theory]
    [InlineData("")]
    [InlineData("ID3")]
    [InlineData("ID3 this is not audio")]
    [InlineData("RIFF....WAVE")]
    public void Text_is_not_an_mp3(string text)
        => Assert.Null(Mp3Frames.Find(System.Text.Encoding.UTF8.GetBytes(text)));

    // One plausible header by chance is easy; a chain of headers each landing where the last one
    // said the next would start is not.
    [Fact]
    public void A_single_frame_header_followed_by_noise_is_not_an_mp3()
    {
        var bytes = new byte[4096];
        new Random(7).NextBytes(bytes);
        bytes[0] = 0xFF; bytes[1] = 0xFB; bytes[2] = 0x90; bytes[3] = 0x64;   // MPEG-1 Layer III, 128 kbps, 44.1 kHz

        Assert.Null(Mp3Frames.Find(bytes));
    }

    [Fact]
    public void Random_bytes_are_not_an_mp3()
    {
        var bytes = new byte[64 * 1024];
        new Random(42).NextBytes(bytes);

        Assert.Null(Mp3Frames.Find(bytes));
    }

    // A damaged frame in the middle should cost that frame, not the rest of the song.
    [Fact]
    public void A_damaged_frame_mid_file_is_stepped_over()
    {
        var bytes = Tone();
        var whole = Mp3Frames.Find(bytes)!;
        var hit = whole.Frames[whole.Frames.Count / 2];
        bytes[hit.Offset] = 0x00;   // break that frame's sync word

        var damaged = Mp3Frames.Find(bytes)!;

        Assert.Equal(whole.Frames.Count - 1, damaged.Frames.Count);
    }

    [Theory]
    [InlineData(new byte[] { 0xFF, 0xFB, 0x90, 0x64 }, 417)]   // MPEG-1, 128 kbps, 44.1 kHz: 144000 * 128 / 44100
    [InlineData(new byte[] { 0xFF, 0xFB, 0x92, 0x64 }, 418)]   // the same with the padding bit set
    [InlineData(new byte[] { 0xFF, 0xF3, 0x88, 0xC4 }, 288)]   // MPEG-2, 64 kbps, 16 kHz, mono: 72000 * 64 / 16000
    public void Frame_length_follows_the_header(byte[] header, int expected)
        => Assert.Equal(expected, Mp3Frames.FrameLength(header));

    [Theory]
    [InlineData(new byte[] { 0xFF, 0xFD, 0x90, 0x64 })]   // Layer II, not an MP3
    [InlineData(new byte[] { 0xFF, 0xEB, 0x90, 0x64 })]   // reserved MPEG version
    [InlineData(new byte[] { 0xFF, 0xFB, 0xF0, 0x64 })]   // bad bitrate index
    [InlineData(new byte[] { 0xFF, 0xFB, 0x00, 0x64 })]   // free-format bitrate, length unknown
    [InlineData(new byte[] { 0xFF, 0xFB, 0x9C, 0x64 })]   // reserved sample rate
    [InlineData(new byte[] { 0xFF, 0xFB, 0x90, 0x66 })]   // reserved emphasis
    public void Headers_that_are_not_mp3_frames_have_no_length(byte[] header)
        => Assert.Equal(0, Mp3Frames.FrameLength(header));
}

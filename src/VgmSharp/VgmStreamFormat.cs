using System.Text;
using VgmSharp.Native;

namespace VgmSharp;

public enum VgmSampleFormat
{
    Pcm16 = 1,
    Pcm24 = 2,
    Pcm32 = 3,
    Float = 4,
}

/// <summary>
/// Snapshot of a loaded stream's format and metadata. Values are copied out of native
/// memory at the time of the call, so it's safe to hold on to after further decode calls.
/// </summary>
public readonly struct VgmStreamFormat
{
    public int Channels { get; }
    public int InputChannels { get; }
    public int SampleRate { get; }
    public VgmSampleFormat SampleFormat { get; }
    public int SampleSize { get; }

    public int SubsongIndex { get; }
    public int SubsongCount { get; }

    public long StreamSamples { get; }
    public long LoopStart { get; }
    public long LoopEnd { get; }
    public bool LoopFlag { get; }
    public bool PlayForever { get; }
    public long PlaySamples { get; }

    public int StreamBitrate { get; }

    public string CodecName { get; }
    public string LayoutName { get; }
    public string MetaName { get; }
    public string StreamName { get; }

    internal unsafe VgmStreamFormat(in LibvgmstreamFormatT f)
    {
        this.Channels = f.channels;
        this.InputChannels = f.input_channels;
        this.SampleRate = f.sample_rate;
        this.SampleFormat = (VgmSampleFormat)f.sample_format;
        this.SampleSize = f.sample_size;

        this.SubsongIndex = f.subsong_index;
        this.SubsongCount = f.subsong_count;

        this.StreamSamples = f.stream_samples;
        this.LoopStart = f.loop_start;
        this.LoopEnd = f.loop_end;
        this.LoopFlag = f.loop_flag;
        this.PlayForever = f.play_forever;
        this.PlaySamples = f.play_samples;

        this.StreamBitrate = f.stream_bitrate;

        fixed (byte* p = f.codec_name)
        {
            this.CodecName = ReadFixedUtf8(p, 128);
        }

        fixed (byte* p = f.layout_name)
        {
            this.LayoutName = ReadFixedUtf8(p, 128);
        }

        fixed (byte* p = f.meta_name)
        {
            this.MetaName = ReadFixedUtf8(p, 128);
        }

        fixed (byte* p = f.stream_name)
        {
            this.StreamName = ReadFixedUtf8(p, 256);
        }
    }

    private static unsafe string ReadFixedUtf8(byte* p, int maxLen)
    {
        var len = 0;
        while (len < maxLen && p[len] != 0)
        {
            len++;
        }

        return Encoding.UTF8.GetString(p, len);
    }

    public override string ToString()
        => $"{this.MetaName} [{this.CodecName}/{this.LayoutName}] {this.SampleRate}Hz {this.Channels}ch"
           + (this.SubsongCount > 1 ? $" (subsong {this.SubsongIndex}/{this.SubsongCount})" : "")
           + (this.LoopFlag ? $" loop[{this.LoopStart}-{this.LoopEnd}]" : "");
}

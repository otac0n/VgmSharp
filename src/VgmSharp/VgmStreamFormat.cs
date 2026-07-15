using System;
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
        Channels = f.channels;
        InputChannels = f.input_channels;
        SampleRate = f.sample_rate;
        SampleFormat = (VgmSampleFormat)f.sample_format;
        SampleSize = f.sample_size;

        SubsongIndex = f.subsong_index;
        SubsongCount = f.subsong_count;

        StreamSamples = f.stream_samples;
        LoopStart = f.loop_start;
        LoopEnd = f.loop_end;
        LoopFlag = f.loop_flag;
        PlayForever = f.play_forever;
        PlaySamples = f.play_samples;

        StreamBitrate = f.stream_bitrate;

        fixed (byte* p = f.codec_name) CodecName = ReadFixedUtf8(p, 128);
        fixed (byte* p = f.layout_name) LayoutName = ReadFixedUtf8(p, 128);
        fixed (byte* p = f.meta_name) MetaName = ReadFixedUtf8(p, 128);
        fixed (byte* p = f.stream_name) StreamName = ReadFixedUtf8(p, 256);
    }

    private static unsafe string ReadFixedUtf8(byte* p, int maxLen)
    {
        int len = 0;
        while (len < maxLen && p[len] != 0) len++;
        return Encoding.UTF8.GetString(p, len);
    }

    public override string ToString()
        => $"{MetaName} [{CodecName}/{LayoutName}] {SampleRate}Hz {Channels}ch"
           + (SubsongCount > 1 ? $" (subsong {SubsongIndex}/{SubsongCount})" : "")
           + (LoopFlag ? $" loop[{LoopStart}-{LoopEnd}]" : "");
}

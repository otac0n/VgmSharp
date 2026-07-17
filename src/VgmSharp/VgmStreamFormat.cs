// Copyright © John Gietzen. All Rights Reserved. This source is subject to the ISC license. Please see license.md for more information.

namespace VgmSharp
{
    using System.Text;
    using VgmSharp.Native;

    /// <summary>
    /// Snapshot of a loaded stream's format and metadata. Values are copied out of native
    /// memory at the time of the call, so it's safe to hold on to after further decode calls.
    /// </summary>
    public readonly struct VgmStreamFormat
    {
        internal unsafe VgmStreamFormat(in LibvgmstreamFormatT f)
        {
            this.Channels = f.Channels;
            this.InputChannels = f.InputChannels;
            this.SampleRate = f.SampleRate;
            this.SampleFormat = (VgmSampleFormat)f.SampleFormat;
            this.SampleSize = f.SampleSize;

            this.SubsongIndex = f.SubsongIndex;
            this.SubsongCount = f.SubsongCount;

            this.StreamSamples = f.StreamSamples;
            this.LoopStart = f.LoopStart;
            this.LoopEnd = f.LoopEnd;
            this.LoopFlag = f.LoopFlag;
            this.PlayForever = f.PlayForever;
            this.PlaySamples = f.PlaySamples;

            this.StreamBitrate = f.StreamBitrate;

            fixed (byte* p = f.CodecName)
            {
                this.CodecName = ReadFixedUtf8(p, 128);
            }

            fixed (byte* p = f.LayoutName)
            {
                this.LayoutName = ReadFixedUtf8(p, 128);
            }

            fixed (byte* p = f.MetaName)
            {
                this.MetaName = ReadFixedUtf8(p, 128);
            }

            fixed (byte* p = f.StreamName)
            {
                this.StreamName = ReadFixedUtf8(p, 256);
            }
        }

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

        public override string ToString() =>
            $"{this.MetaName} [{this.CodecName}/{this.LayoutName}] {this.SampleRate}Hz {this.Channels}ch" +
            (this.SubsongCount > 1 ? $" (subsong {this.SubsongIndex}/{this.SubsongCount})" : string.Empty) +
            (this.LoopFlag ? $" loop[{this.LoopStart}-{this.LoopEnd}]" : string.Empty);

        private static unsafe string ReadFixedUtf8(byte* p, int maxLen)
        {
            var len = 0;
            while (len < maxLen && p[len] != 0)
            {
                len++;
            }

            return Encoding.UTF8.GetString(p, len);
        }
    }
}

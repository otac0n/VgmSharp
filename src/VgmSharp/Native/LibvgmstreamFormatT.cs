// Copyright © John Gietzen. All Rights Reserved. This source is subject to the ISC license. Please see license.md for more information.

namespace VgmSharp.Native
{
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct LibvgmstreamFormatT
    {
        public int Channels;

        public int SampleRate;

        public LibvgmstreamSfmt SampleFormat;

        public int SampleSize;

        public uint ChannelLayout;

        public int SubsongIndex;

        public int SubsongCount;

        public int InputChannels;

        public long StreamSamples;

        public long LoopStart;

        public long LoopEnd;

        [MarshalAs(UnmanagedType.I1)]
        public bool LoopFlag;

        [MarshalAs(UnmanagedType.I1)]
        public bool PlayForever;

        public long PlaySamples;

        public int StreamBitrate;

        public fixed byte CodecName[128];

        public fixed byte LayoutName[128];

        public fixed byte MetaName[128];

        public fixed byte StreamName[256];

        public int FormatId;
    }
}

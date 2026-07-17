// Copyright © John Gietzen. All Rights Reserved. This source is subject to the ISC license. Please see license.md for more information.

namespace VgmSharp.Native
{
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Sequential)]
    public struct LibvgmstreamConfigT
    {
        [MarshalAs(UnmanagedType.I1)]
        public bool DisableConfigOverride;

        [MarshalAs(UnmanagedType.I1)]
        public bool AllowPlayForever;

        [MarshalAs(UnmanagedType.I1)]
        public bool PlayForever;

        [MarshalAs(UnmanagedType.I1)]
        public bool IgnoreLoop;

        [MarshalAs(UnmanagedType.I1)]
        public bool ForceLoop;

        [MarshalAs(UnmanagedType.I1)]
        public bool ReallyForceLoop;

        [MarshalAs(UnmanagedType.I1)]
        public bool IgnoreFade;

        public double LoopCount;

        public double FadeTime;

        public double FadeDelay;

        public int StereoTrack;

        public int AutoDownmixChannels;

        public LibvgmstreamSfmt ForceSfmt;
    }
}

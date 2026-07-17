// Copyright © John Gietzen. All Rights Reserved. This source is subject to the ISC license. Please see license.md for more information.

namespace VgmSharp.Native
{
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Sequential)]
    internal struct LibvgmstreamDecoderT
    {
        public IntPtr Buf;

        public int BufSamples;

        public int BufBytes;

        [MarshalAs(UnmanagedType.I1)]
        public bool Done;
    }
}

// Copyright © John Gietzen. All Rights Reserved. This source is subject to the ISC license. Please see license.md for more information.

namespace VgmSharp.Native
{
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Sequential)]
    internal struct LibvgmstreamT
    {
        public IntPtr Priv;

        /// <summary>Unmanaged <c>const libvgmstream_format_t*</c> pointer.</summary>
        public IntPtr Format;

        /// <summary>Unmanaged <c>libvgmstream_decoder_t*</c> pointer.</summary>
        public IntPtr Decoder;
    }
}

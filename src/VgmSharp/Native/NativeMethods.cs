// Copyright © John Gietzen. All Rights Reserved. This source is subject to the ISC license. Please see license.md for more information.

namespace VgmSharp.Native
{
    using System.Runtime.InteropServices;

    internal static class NativeMethods
    {
        /// <summary>
        /// The logical name of the library.
        /// </summary>
        /// <remarks>
        /// The OS-specific loader resolves this to "libvgmstream.so" on Linux and "vgmstream.dll" on Windows.
        /// </remarks>
        private const string Lib = "vgmstream";

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern uint libvgmstream_get_version();

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr libvgmstream_init();

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern void libvgmstream_free(IntPtr lib);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern void libvgmstream_setup(IntPtr lib, ref LibvgmstreamConfigT cfg);

        /// <remarks>
        /// Pass <see cref="IntPtr.Zero"/> to clear config.
        /// </remarks>
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern void libvgmstream_setup(IntPtr lib, IntPtr cfg);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern int libvgmstream_open_stream(IntPtr lib, IntPtr libsf, int subsong);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern void libvgmstream_close_stream(IntPtr lib);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern int libvgmstream_render(IntPtr lib);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern int libvgmstream_fill(IntPtr lib, IntPtr buf, int buf_samples);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern long libvgmstream_get_play_position(IntPtr lib);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern void libvgmstream_seek(IntPtr lib, long sample);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern void libvgmstream_reset(IntPtr lib);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr libvgmstream_create(IntPtr libsf, int subsong, ref LibvgmstreamConfigT cfg);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr libvgmstream_create(IntPtr libsf, int subsong, IntPtr cfg);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int libvgmstream_format_describe(IntPtr lib, IntPtr dst, int dst_size);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern bool libvgmstream_is_valid(string filename, IntPtr cfg);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr libvgmstream_get_extensions(out int size);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr libvgmstream_get_common_extensions(out int size);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern IntPtr libstreamfile_open_from_stdio(string filename);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern void libstreamfile_close(IntPtr libsf);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr libstreamfile_open_buffered(IntPtr ext_libsf);
    }
}

using System.Runtime.InteropServices;

namespace VgmSharp.Native;

public enum LibvgmstreamSfmt : int
{
    Pcm16 = 1,
    Pcm24 = 2,
    Pcm32 = 3,
    Float = 4,
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct LibvgmstreamFormatT
{
    public int channels;

    public int sample_rate;

    public LibvgmstreamSfmt sample_format;

    public int sample_size;

    public uint channel_layout;

    public int subsong_index;

    public int subsong_count;

    public int input_channels;

    public long stream_samples;

    public long loop_start;

    public long loop_end;

    [MarshalAs(UnmanagedType.I1)]
    public bool loop_flag;

    [MarshalAs(UnmanagedType.I1)]
    public bool play_forever;

    public long play_samples;

    public int stream_bitrate;

    public fixed byte codec_name[128];

    public fixed byte layout_name[128];

    public fixed byte meta_name[128];

    public fixed byte stream_name[256];

    public int format_id;
}

[StructLayout(LayoutKind.Sequential)]
internal struct LibvgmstreamDecoderT
{
    public IntPtr buf;

    public int buf_samples;

    public int buf_bytes;

    [MarshalAs(UnmanagedType.I1)]
    public bool done;
}

[StructLayout(LayoutKind.Sequential)]
internal struct LibvgmstreamT
{
    public IntPtr priv;

    /// <remarks><c>const libvgmstream_format_t*</c></remarks>
    public IntPtr format;

    /// <remarks><c>libvgmstream_decoder_t*</c></remarks>
    public IntPtr decoder;
}

[StructLayout(LayoutKind.Sequential)]
public struct LibvgmstreamConfigT
{
    [MarshalAs(UnmanagedType.I1)]
    public bool disable_config_override;

    [MarshalAs(UnmanagedType.I1)]
    public bool allow_play_forever;

    [MarshalAs(UnmanagedType.I1)]
    public bool play_forever;

    [MarshalAs(UnmanagedType.I1)]
    public bool ignore_loop;

    [MarshalAs(UnmanagedType.I1)]
    public bool force_loop;

    [MarshalAs(UnmanagedType.I1)]
    public bool really_force_loop;

    [MarshalAs(UnmanagedType.I1)]
    public bool ignore_fade;

    public double loop_count;

    public double fade_time;

    public double fade_delay;

    public int stereo_track;

    public int auto_downmix_channels;

    public LibvgmstreamSfmt force_sfmt;
}

[StructLayout(LayoutKind.Sequential)]
internal struct LibstreamfileT
{
    public IntPtr user_data;

    /// <remarks><see cref="ReadDelegate"/></remarks>
    public IntPtr read;

    /// <remarks><see cref="GetSizeDelegate"/></remarks>
    public IntPtr get_size;

    /// <remarks><see cref="GetNameDelegate"/></remarks>
    public IntPtr get_name;

    /// <remarks><see cref="OpenDelegate"/></remarks>
    public IntPtr open;

    /// <remarks><see cref="CloseDelegate"/></remarks>
    public IntPtr close;
}

/// <remarks><c>int (*)(void* user_data, uint8_t* dst, int64_t offset, int length)</c></remarks>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal unsafe delegate int ReadDelegate(IntPtr userData, byte* dst, long offset, int length);

/// <remarks><c>int64_t (*)(void* user_data)</c></remarks>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate long GetSizeDelegate(IntPtr userData);

/// <remarks><c>const char* (*)(void* user_data)</c></remarks>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate IntPtr GetNameDelegate(IntPtr userData);

/// <remarks><c>libstreamfile_t* (*)(void* user_data, const char* filename)</c></remarks>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate IntPtr OpenDelegate(IntPtr userData, IntPtr filename);

/// <remarks><c>void (*)(libstreamfile_t*)</c></remarks>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void CloseDelegate(IntPtr libsf);

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

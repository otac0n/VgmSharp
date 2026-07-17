using System.Runtime.InteropServices;
using VgmSharp.Native;

namespace VgmSharp;

/// <summary>
/// Provides file extension information for vgmstream.
/// </summary>
public static class FileExtensions
{
    private static IReadOnlyList<string>? allExtensions;
    private static IReadOnlyList<string>? commonExtensions;

    /// <summary>
    /// Every file extension (no leading dot, e.g. <c>"vag"</c>) that vgmstream's format parsers recognize.
    /// </summary>
    public static IReadOnlyList<string> SupportedExtensions
        => allExtensions ??= ReadExtensionList(NativeMethods.libvgmstream_get_extensions);

    /// <summary>
    /// The subset of <see cref="SupportedExtensions"/> that vgmstream considers common formats (e.g. <c>"wav"</c>, <c>"ogg"</c>).
    /// </summary>
    public static IReadOnlyList<string> CommonExtensions
        => commonExtensions ??= ReadExtensionList(NativeMethods.libvgmstream_get_common_extensions);

    private delegate IntPtr GetExtensionsNative(out int size);

    private static IReadOnlyList<string> ReadExtensionList(GetExtensionsNative native)
    {
        var arrayPtr = native(out var count);
        if (arrayPtr == IntPtr.Zero || count <= 0)
        {
            return [];
        }

        var result = new List<string>(count);
        for (var i = 0; i < count; i++)
        {
            var strPtr = Marshal.ReadIntPtr(arrayPtr, i * IntPtr.Size);
            result.Add(Marshal.PtrToStringAnsi(strPtr) ?? string.Empty);
        }

        return result.AsReadOnly();
    }
}

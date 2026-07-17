// Copyright © John Gietzen. All Rights Reserved. This source is subject to the ISC license. Please see license.md for more information.

namespace VgmSharp
{
    using System.Diagnostics.CodeAnalysis;
    using System.Runtime.InteropServices;
    using VgmSharp.Native;

    /// <summary>
    /// Provides file extension information for vgmstream.
    /// </summary>
    public static class FileExtensions
    {
        private delegate IntPtr GetExtensionsNative(out int size);

        /// <summary>
        /// Gets the file extensions that vgmstream's format parsers recognize.
        /// </summary>
        /// <remarks>
        /// The extensions do not include a leading dot, e.g. <c>"vag"</c>.
        /// </remarks>
        public static IReadOnlyList<string> SupportedExtensions
            => field ??= ReadExtensionList(NativeMethods.libvgmstream_get_extensions);

        /// <summary>
        /// Gets the subset of <see cref="SupportedExtensions"/> that vgmstream considers common formats.
        /// </summary>
        public static IReadOnlyList<string> CommonExtensions
            => field ??= ReadExtensionList(NativeMethods.libvgmstream_get_common_extensions);

        [SuppressMessage("Performance", "CA1859:Use concrete types when possible for improved performance", Justification = "The empty array is not concretely a ReadOnlyCollection.")]
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
}

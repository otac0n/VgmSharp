// Copyright © John Gietzen. All Rights Reserved. This source is subject to the ISC license. Please see license.md for more information.

namespace VgmSharp.Native
{
    using System.Runtime.InteropServices;

    /// <summary>Unmananged <c>int (*)(void* user_data, uint8_t* dst, int64_t offset, int length)</c> function pointer.</summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal unsafe delegate int ReadDelegate(IntPtr userData, byte* dst, long offset, int length);

    /// <summary>Unmananged <c>int64_t (*)(void* user_data)</c> function pointer.</summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate long GetSizeDelegate(IntPtr userData);

    /// <summary>Unmananged <c>const char* (*)(void* user_data)</c> function pointer.</summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate IntPtr GetNameDelegate(IntPtr userData);

    /// <summary>Unmananged <c>libstreamfile_t* (*)(void* user_data, const char* filename)</c> function pointer.</summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate IntPtr OpenDelegate(IntPtr userData, IntPtr filename);

    /// <summary>Unmananged <c>void (*)(libstreamfile_t*)</c> function pointer.</summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void CloseDelegate(IntPtr libsf);

    [StructLayout(LayoutKind.Sequential)]
    internal struct LibstreamfileT
    {
        public IntPtr UserData;

        /// <summary>See <see cref="ReadDelegate"/>.</summary>
        public IntPtr Read;

        /// <summary>See <see cref="GetSizeDelegate"/>.</summary>
        public IntPtr GetSize;

        /// <summary>See <see cref="GetNameDelegate"/>.</summary>
        public IntPtr GetName;

        /// <summary>See <see cref="OpenDelegate"/>.</summary>
        public IntPtr Open;

        /// <summary>See <see cref="CloseDelegate"/>.</summary>
        public IntPtr Close;
    }
}

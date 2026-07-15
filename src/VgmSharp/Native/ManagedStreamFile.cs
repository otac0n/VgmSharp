using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace VgmSharp.Native;

/// <summary>
/// Wraps a .NET <see cref="Stream"/> as a native libstreamfile_t so vgmstream can read from
/// arbitrary managed sources (archives, embedded resources, network, etc.) instead of a real file.
///
/// Caveats vs. a real file path:
/// - Only *self*-reopen is supported (vgmstream re-opening the same virtual filename to get an
///   independent handle for the main decode stream -- this is required, not optional, since
///   vgmstream_open_stream() needs it internally). Opening genuinely different companion files
///   (e.g. .dsp+.dsh pairs, TXTH/TXTP references) is NOT supported and always fails; only formats
///   that are fully self-contained in one stream work.
/// - The underlying Stream must support Seek + Read; it is NOT closed/disposed by this wrapper
///   (you own its lifetime). Every reopened "handle" shares the same Stream + a common lock, since
///   reads always seek to an absolute offset first, so concurrent handles stay correct as long as
///   each individual read is atomic (which the shared lock guarantees) -- just note that truly
///   parallel decoding across handles will serialize on that lock rather than run concurrently.
/// </summary>
internal sealed unsafe class ManagedStreamFile : IDisposable
{
    private readonly Stream _stream;
    private readonly object _lock;
    private readonly bool _isRoot;

    private GCHandle _selfHandle;      // pins 'this' so native user_data can find it back
    private byte[] _nameUtf8 = Array.Empty<byte>();
    private GCHandle _namePin;
    private readonly string _virtualFilename;

    private readonly ReadDelegate _read;
    private readonly GetSizeDelegate _getSize;
    private readonly GetNameDelegate _getName;
    private readonly OpenDelegate _open;
    private readonly CloseDelegate _close;

    private IntPtr _nativeStruct; // unmanaged LibstreamfileT*
    private bool _disposed;

    public IntPtr NativePointer => _nativeStruct;

    public ManagedStreamFile(Stream stream, string virtualFilename)
        : this(stream, virtualFilename, new object(), isRoot: true)
    {
        if (!stream.CanRead || !stream.CanSeek)
            throw new ArgumentException("Stream must support both reading and seeking.", nameof(stream));
    }

    private ManagedStreamFile(Stream stream, string virtualFilename, object sharedLock, bool isRoot)
    {
        _stream = stream;
        _lock = sharedLock;
        _isRoot = isRoot;
        _virtualFilename = virtualFilename;

        _read = ReadImpl;
        _getSize = GetSizeImpl;
        _getName = GetNameImpl;
        _open = OpenImpl;
        _close = CloseImpl;

        SetName(virtualFilename);

        _selfHandle = GCHandle.Alloc(this, GCHandleType.Normal);

        var native = new LibstreamfileT
        {
            user_data = GCHandle.ToIntPtr(_selfHandle),
            read = Marshal.GetFunctionPointerForDelegate(_read),
            get_size = Marshal.GetFunctionPointerForDelegate(_getSize),
            get_name = Marshal.GetFunctionPointerForDelegate(_getName),
            open = Marshal.GetFunctionPointerForDelegate(_open),
            close = Marshal.GetFunctionPointerForDelegate(_close),
        };

        _nativeStruct = Marshal.AllocHGlobal(Marshal.SizeOf<LibstreamfileT>());
        Marshal.StructureToPtr(native, _nativeStruct, false);
    }

    private void SetName(string virtualFilename)
    {
        var bytes = Encoding.UTF8.GetBytes(virtualFilename);
        _nameUtf8 = new byte[bytes.Length + 1];
        Array.Copy(bytes, _nameUtf8, bytes.Length); // trailing 0 already present
        _namePin = GCHandle.Alloc(_nameUtf8, GCHandleType.Pinned);
    }

    private static string? PtrToStringUtf8(IntPtr ptr)
    {
        if (ptr == IntPtr.Zero) return null;
        int len = 0;
        while (Marshal.ReadByte(ptr, len) != 0) len++;
        if (len == 0) return string.Empty;
        byte[] bytes = new byte[len];
        Marshal.Copy(ptr, bytes, 0, len);
        return Encoding.UTF8.GetString(bytes);
    }

    private static ManagedStreamFile? FromUserData(IntPtr userData)
    {
        if (userData == IntPtr.Zero) return null;
        var handle = GCHandle.FromIntPtr(userData);
        return handle.Target as ManagedStreamFile;
    }

    private int ReadImpl(IntPtr userData, byte* dst, long offset, int length)
    {
        var self = FromUserData(userData);
        if (self == null) return 0;
        lock (self._lock)
        {
            try
            {
                if (self._stream.Position != offset)
                    self._stream.Seek(offset, SeekOrigin.Begin);

                int total = 0;
                var buffer = new byte[Math.Min(length, 1 << 16)];
                while (total < length)
                {
                    int chunk = Math.Min(buffer.Length, length - total);
                    int n = self._stream.Read(buffer, 0, chunk);
                    if (n <= 0) break;
                    Marshal.Copy(buffer, 0, (IntPtr)(dst + total), n);
                    total += n;
                }
                return total;
            }
            catch
            {
                return 0;
            }
        }
    }

    private long GetSizeImpl(IntPtr userData)
    {
        var self = FromUserData(userData);
        if (self == null) return 0;
        lock (self._lock)
        {
            try { return self._stream.Length; }
            catch { return 0; }
        }
    }

    private IntPtr GetNameImpl(IntPtr userData)
    {
        var self = FromUserData(userData);
        if (self == null) return IntPtr.Zero;
        return self._namePin.AddrOfPinnedObject();
    }

    private IntPtr OpenImpl(IntPtr userData, IntPtr filename)
    {
        var self = FromUserData(userData);
        if (self == null || filename == IntPtr.Zero) return IntPtr.Zero;

        string? requested = PtrToStringUtf8(filename);
        if (requested == null) return IntPtr.Zero;

        // vgmstream needs to reopen the *same* stream (by name) to get an independent handle
        // for the main decode path -- this is required for basically every format, not optional.
        // We can only satisfy that self-reopen case (sharing our Stream + lock); genuinely
        // different companion filenames aren't backed by anything we can open here.
        if (!string.Equals(requested, self._virtualFilename, StringComparison.OrdinalIgnoreCase))
            return IntPtr.Zero;

        var child = new ManagedStreamFile(self._stream, self._virtualFilename, self._lock, isRoot: false);
        return child.NativePointer;
    }

    private void CloseImpl(IntPtr libsf)
    {
        // Called by vgmstream (via libstreamfile_close) when it's done with this handle.
        // We free native-side resources here but deliberately do NOT dispose the
        // underlying managed Stream -- the caller (or, for the root instance, VgmStream.OpenFromStream)
        // owns its lifetime; child handles created via OpenImpl never own the Stream either.
        FreeNativeResources();
    }

    private void FreeNativeResources()
    {
        if (_nativeStruct != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_nativeStruct);
            _nativeStruct = IntPtr.Zero;
        }
        if (_namePin.IsAllocated) _namePin.Free();
        if (_selfHandle.IsAllocated) _selfHandle.Free();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        FreeNativeResources();
    }
}


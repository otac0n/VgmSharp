using System.Runtime.InteropServices;
using System.Text;

namespace VgmSharp.Native;

internal sealed unsafe class ManagedStreamFile : IDisposable
{
    private readonly Stream stream;
    private readonly object @lock;
    private readonly bool ownsStream;
    private readonly string virtualFilename;
    private readonly Func<string, Stream?>? relatedFileOpener;

    /// <summary>
    /// Pins 'this' so native user_data can find it.
    /// </summary>
    private GCHandle selfHandle;
    private byte[] nameUtf8 = [];
    private GCHandle namePin;

    private readonly ReadDelegate read;
    private readonly GetSizeDelegate getSize;
    private readonly GetNameDelegate getName;
    private readonly OpenDelegate open;
    private readonly CloseDelegate close;

    /// <remarks>unmanaged <c>LibstreamfileT*</c></remarks>
    private IntPtr nativeStruct;
    private bool disposed;

    public IntPtr NativePointer => this.nativeStruct;


    /// <summary>
    /// Wraps a .NET <see cref="Stream"/> as a native libstreamfile_t so vgmstream can read from
    /// arbitrary managed sources (archives, embedded resources, network, etc.) instead of a real file.
    /// </summary>
    /// <remarks>
    /// Caveats vs. a real file path:
    /// - Self-reopen (vgmstream re-opening the same virtual filename to get an independent handle for
    ///   the main decode stream) is always supported and required -- vgmstream_open_stream() needs it
    ///   internally for basically every format.
    /// - Opening genuinely different companion files (e.g. .dsp+.dsh pairs, TXTH/TXTP sidecars, or
    ///   just "the next file vgmstream feels like peeking at") is supported IF a
    ///   <c>relatedFileOpener</c> delegate is supplied (see <see cref="VgmStream.OpenFromStream"/>).
    ///   Without one, those requests fail cleanly (same as before) and only fully self-contained
    ///   formats work.
    /// - The root Stream must support Seek + Read; it is NOT closed/disposed by this wrapper (the
    ///   caller owns its lifetime). Streams obtained from <c>relatedFileOpener</c>, by contrast, ARE
    ///   owned and disposed by this wrapper once vgmstream is done with them -- the delegate is
    ///   expected to hand over a fresh Stream each call, the same way <see cref="Stream.Read"/> callers
    ///   don't expect to get the same Stream instance back twice.
    /// - Every *self*-reopened handle shares the same Stream + a common lock, since reads always seek
    ///   to an absolute offset first, so concurrent handles stay correct as long as each individual
    ///   read is atomic (which the shared lock guarantees) -- just note that truly parallel decoding
    ///   across handles will serialize on that lock rather than run concurrently. Related files opened
    ///   via the delegate get their own independent Stream and lock, since they're genuinely separate
    ///   underlying data.
    /// </remarks>
    /// <param name="relatedFileOpener">
    /// Called when vgmstream wants to open a file other than <paramref name="virtualFilename"/>
    /// itself (companion/sidecar files -- TXTH, dual-file formats, etc). Receives the path
    /// vgmstream constructed (typically <paramref name="virtualFilename"/>'s directory + a new
    /// name/extension) and should return an open, seekable Stream for it, or null if it doesn't
    /// exist. Exceptions are treated the same as returning null (the related file just won't be
    /// found; most formats treat missing companions as "not applicable" rather than fatal).
    /// The returned Stream is owned by VgmSharp from that point on and will be disposed once
    /// vgmstream is done with it.
    /// </param>
    public ManagedStreamFile(Stream stream, string virtualFilename, Func<string, Stream?>? relatedFileOpener = null)
        : this(stream, virtualFilename, new object(), ownsStream: false, relatedFileOpener)
    {
        if (!stream.CanRead || !stream.CanSeek)
        {
            throw new ArgumentException("Stream must support both reading and seeking.", nameof(stream));
        }
    }

    private ManagedStreamFile(Stream stream, string virtualFilename, object sharedLock, bool ownsStream, Func<string, Stream?>? relatedFileOpener)
    {
        this.stream = stream;
        this.@lock = sharedLock;
        this.ownsStream = ownsStream;
        this.virtualFilename = virtualFilename;
        this.relatedFileOpener = relatedFileOpener;

        this.read = this.ReadImpl;
        this.getSize = this.GetSizeImpl;
        this.getName = this.GetNameImpl;
        this.open = this.OpenImpl;
        this.close = this.CloseImpl;

        this.SetName(virtualFilename);

        this.selfHandle = GCHandle.Alloc(this, GCHandleType.Normal);

        var native = new LibstreamfileT
        {
            user_data = GCHandle.ToIntPtr(this.selfHandle),
            read = Marshal.GetFunctionPointerForDelegate(this.read),
            get_size = Marshal.GetFunctionPointerForDelegate(this.getSize),
            get_name = Marshal.GetFunctionPointerForDelegate(this.getName),
            open = Marshal.GetFunctionPointerForDelegate(this.open),
            close = Marshal.GetFunctionPointerForDelegate(this.close),
        };

        this.nativeStruct = Marshal.AllocHGlobal(Marshal.SizeOf<LibstreamfileT>());
        Marshal.StructureToPtr(native, this.nativeStruct, false);
    }

    private void SetName(string virtualFilename)
    {
        var bytes = Encoding.UTF8.GetBytes(virtualFilename);
        this.nameUtf8 = new byte[bytes.Length + 1];
        Array.Copy(bytes, this.nameUtf8, bytes.Length);
        this.namePin = GCHandle.Alloc(this.nameUtf8, GCHandleType.Pinned);
    }

    private static string? PtrToStringUtf8(IntPtr ptr)
    {
        if (ptr == IntPtr.Zero)
        {
            return null;
        }

        var len = 0;
        while (Marshal.ReadByte(ptr, len) != 0)
        {
            len++;
        }

        if (len == 0)
        {
            return string.Empty;
        }

        var bytes = new byte[len];
        Marshal.Copy(ptr, bytes, 0, len);
        return Encoding.UTF8.GetString(bytes);
    }

    private static ManagedStreamFile? FromUserData(IntPtr userData)
    {
        if (userData == IntPtr.Zero)
        {
            return null;
        }

        var handle = GCHandle.FromIntPtr(userData);
        return handle.Target as ManagedStreamFile;
    }

    private int ReadImpl(IntPtr userData, byte* dst, long offset, int length)
    {
        var self = FromUserData(userData);
        if (self == null)
        {
            return 0;
        }

        lock (self.@lock)
        {
            try
            {
                if (self.stream.Position != offset)
                {
                    self.stream.Seek(offset, SeekOrigin.Begin);
                }

                var total = 0;
                var buffer = new byte[Math.Min(length, 1 << 16)];
                while (total < length)
                {
                    var chunk = Math.Min(buffer.Length, length - total);
                    var n = self.stream.Read(buffer, 0, chunk);
                    if (n <= 0)
                    {
                        break;
                    }

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
        if (self == null)
        {
            return 0;
        }

        lock (self.@lock)
        {
            try { return self.stream.Length; }
            catch { return 0; }
        }
    }

    private IntPtr GetNameImpl(IntPtr userData)
    {
        var self = FromUserData(userData);
        if (self == null)
        {
            return IntPtr.Zero;
        }

        return self.namePin.AddrOfPinnedObject();
    }

    private IntPtr OpenImpl(IntPtr userData, IntPtr filename)
    {
        var self = FromUserData(userData);
        if (self == null || filename == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        var requested = PtrToStringUtf8(filename);
        if (requested == null)
        {
            return IntPtr.Zero;
        }

        // TODO: Determine case sensitivity based on the underlying filesystem. However, for same filename, the case shouldn't change here.
        if (string.Equals(requested, self.virtualFilename, StringComparison.OrdinalIgnoreCase))
        {
            var child = new ManagedStreamFile(self.stream, self.virtualFilename, self.@lock, ownsStream: false, self.relatedFileOpener);
            return child.NativePointer;
        }

        if (self.relatedFileOpener != null)
        {
            Stream? related = null;
            try
            {
                related = self.relatedFileOpener(requested);
                if (related != null && related.CanRead && related.CanSeek)
                {
                    var relatedSf = new ManagedStreamFile(related, requested, new object(), ownsStream: true, self.relatedFileOpener);
                    related = null;
                    return relatedSf.NativePointer;
                }
            }
            catch
            {
                try
                {
                    related?.Dispose();
                }
                catch
                {
                }
            }
        }

        return IntPtr.Zero;
    }

    private void CloseImpl(IntPtr libsf) =>
        this.Dispose();

    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        this.FreeNativeResources();
        if (this.ownsStream)
        {
            try 
            {
                this.stream.Dispose();
            }
            catch
            {
            }
        }

        this.disposed = true;
        GC.SuppressFinalize(this);
    }

    private void FreeNativeResources()
    {
        if (this.nativeStruct != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(this.nativeStruct);
            this.nativeStruct = IntPtr.Zero;
        }

        if (this.namePin.IsAllocated)
        {
            this.namePin.Free();
        }

        if (this.selfHandle.IsAllocated)
        {
            this.selfHandle.Free();
        }
    }

    ~ManagedStreamFile()
    {
        this.FreeNativeResources();
    }
}

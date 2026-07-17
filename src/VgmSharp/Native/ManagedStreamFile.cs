// Copyright © John Gietzen. All Rights Reserved. This source is subject to the ISC license. Please see license.md for more information.

namespace VgmSharp.Native
{
    using System.Runtime.InteropServices;
    using System.Text;

    internal sealed unsafe class ManagedStreamFile : IDisposable
    {
        private readonly Stream stream;
        private readonly object @lock;
        private readonly bool ownsStream;
        private readonly string virtualFilename;
        private readonly Func<string, Stream?>? relatedFileOpener;

        private readonly ReadDelegate read;
        private readonly GetSizeDelegate getSize;
        private readonly GetNameDelegate getName;
        private readonly OpenDelegate open;
        private readonly CloseDelegate close;

        /// <summary>
        /// Pins 'this' so native user_data can find it.
        /// </summary>
        private GCHandle selfHandle;
        private byte[] nameUtf8 = [];
        private GCHandle namePin;
        private bool disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="ManagedStreamFile"/> class.
        /// </summary>
        /// <remarks>
        /// Wraps a .NET <see cref="Stream"/> as a native <c>libstreamfile_t</c> so vgmstream can read from
        /// arbitrary managed sources (archives, embedded resources, network, etc.) instead of a real file.
        /// </remarks>
        /// <param name="stream">The stream to wrap.</param>
        /// <param name="virtualFilename">The filename associated with the stream. Used to determine a parser.</param>
        /// <param name="relatedFileOpener">
        /// Called when vgmstream wants to open a file other than <paramref name="virtualFilename"/> itself
        /// (companion/sidecar files, dual-file formats, etc).
        /// Receives the path vgmstream constructed and should return a Stream or null if it doesn't exist.
        /// The returned Stream is owned by VgmSharp and will be disposed once vgmstream is done with it.
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
                UserData = GCHandle.ToIntPtr(this.selfHandle),
                Read = Marshal.GetFunctionPointerForDelegate(this.read),
                GetSize = Marshal.GetFunctionPointerForDelegate(this.getSize),
                GetName = Marshal.GetFunctionPointerForDelegate(this.getName),
                Open = Marshal.GetFunctionPointerForDelegate(this.open),
                Close = Marshal.GetFunctionPointerForDelegate(this.close),
            };

            this.NativePointer = Marshal.AllocHGlobal(Marshal.SizeOf<LibstreamfileT>());
            Marshal.StructureToPtr(native, this.NativePointer, false);
        }

        ~ManagedStreamFile()
        {
            this.FreeNativeResources();
        }

        /// <summary>Gets the unmanaged <c>LibstreamfileT*</c> pointer.</summary>
        public IntPtr NativePointer { get; private set; }

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

        private void SetName(string virtualFilename)
        {
            var bytes = Encoding.UTF8.GetBytes(virtualFilename);
            this.nameUtf8 = new byte[bytes.Length + 1];
            Array.Copy(bytes, this.nameUtf8, bytes.Length);
            this.namePin = GCHandle.Alloc(this.nameUtf8, GCHandleType.Pinned);
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
                try
                {
                    return self.stream.Length;
                }
                catch
                {
                    return 0;
                }
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

        private void FreeNativeResources()
        {
            if (this.NativePointer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(this.NativePointer);
                this.NativePointer = IntPtr.Zero;
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
    }
}

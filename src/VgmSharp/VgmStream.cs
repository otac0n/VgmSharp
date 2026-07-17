using System.Runtime.InteropServices;
using VgmSharp.Native;

namespace VgmSharp;

/// <summary>
/// A single opened audio stream / subsong, backed by vgmstream's native libvgmstream.
/// </summary>
/// <remarks>
/// Not thread-safe: use one instance per thread, or synchronize externally.
/// </remarks>
public sealed class VgmStream : IDisposable
{
    private IntPtr lib;
    private bool disposed;

    public VgmStreamFormat Format { get; private set; }

    public bool Done { get; private set; }

    /// <summary>API version reported by the loaded native library (major.minor.patch).</summary>
    public static Version NativeApiVersion
    {
        get
        {
            var v = NativeMethods.libvgmstream_get_version();
            return new Version((int)((v >> 24) & 0xFF), (int)((v >> 16) & 0xFF), (int)(v & 0xFFFF));
        }
    }

    private VgmStream(IntPtr lib)
    {
        this.lib = lib;
        this.RefreshFormat();
    }

    /// <summary>Opens a subsong from a file on disk.</summary>
    /// <param name="filePath">The path of the file to open.</param>
    /// <param name="subsong"><c>1..N</c>, or <c>0</c> for the default subsong.</param>
    /// <param name="config">Optional playback/decode configuration.</param>
    public static VgmStream Open(string filePath, int subsong = 0, VgmStreamConfig? config = null)
    {
        ArgumentNullException.ThrowIfNull(filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("File not found.", filePath);
        }

        var libsf = NativeMethods.libstreamfile_open_from_stdio(filePath);
        if (libsf == IntPtr.Zero)
        {
            throw new VgmStreamException($"Could not open '{filePath}' for reading.");
        }

        return OpenCore(libsf, subsong, config, disposeAfter: null);
    }

    /// <summary>
    /// Opens a subsong from an arbitrary managed <see cref="Stream"/> (must support Seek + Read).
    /// See <see cref="ManagedStreamFile"/> for details on how self-reopen and companion files work.
    /// </summary>
    /// <param name="stream">The input stream containing the audio data.</param>
    /// <param name="virtualFilename">A required filename vgmstream uses to pick the right format parser.</param>
    /// <param name="subsong"><c>1..N</c>, or <c>0</c> for the default subsong.</param>
    /// <param name="config">Optional playback/decode configuration.</param>
    /// <param name="openRelatedFile">
    /// <para>Optional. Called when vgmstream wants to open a file other than the stream itself.</para>
    /// <para>Use this to plug in your own filesystem abstraction (embedded resources, an IFileSystem wrapper,
    /// a virtual/packed filesystem, etc.) instead of real disk I/O.</para>
    /// <para>Paths will be rooted based on <paramref name="virtualFilename"/>.</para>
    /// </param>
    public static VgmStream OpenFromStream(
        Stream stream,
        string virtualFilename,
        int subsong = 0,
        VgmStreamConfig? config = null,
        Func<string, Stream?>? openRelatedFile = null)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (string.IsNullOrEmpty(virtualFilename))
        {
            throw new ArgumentException("A virtual filename with extension is required.", nameof(virtualFilename));
        }

        var msf = new ManagedStreamFile(stream, virtualFilename, openRelatedFile);
        try
        {
            return OpenCore(msf.NativePointer, subsong, config, disposeAfter: msf);
        }
        catch
        {
            msf.Dispose();
            throw;
        }
    }

    private static VgmStream OpenCore(IntPtr libsf, int subsong, VgmStreamConfig? config, IDisposable? disposeAfter)
    {
        var lib = NativeMethods.libvgmstream_init();
        if (lib == IntPtr.Zero)
        {
            NativeMethods.libstreamfile_close(libsf);
            disposeAfter?.Dispose();
            throw new VgmStreamException("libvgmstream_init() failed (out of memory?).");
        }

        if (config != null)
        {
            var native = config.ToNative();
            NativeMethods.libvgmstream_setup(lib, ref native);
        }

        var result = NativeMethods.libvgmstream_open_stream(lib, libsf, subsong);

        // Per libvgmstream.h: the streamfile isn't needed after _open_stream and should be
        // closed here; vgmstream re-opens internally (e.g. for companion files) as needed.
        NativeMethods.libstreamfile_close(libsf);
        disposeAfter?.Dispose();

        if (result < 0)
        {
            NativeMethods.libvgmstream_free(lib);
            throw new VgmStreamException(
                $"vgmstream could not open subsong {subsong} (error {result}). " +
                "The format may be unsupported, the subsong index invalid, or the file corrupt/truncated.");
        }

        return new VgmStream(lib);
    }

    private void RefreshFormat()
    {
        var libStruct = Marshal.PtrToStructure<LibvgmstreamT>(this.lib);
        var fmt = Marshal.PtrToStructure<LibvgmstreamFormatT>(libStruct.format);
        this.Format = new VgmStreamFormat(fmt);
    }

    /// <summary>
    /// Decodes the next internal chunk of audio.
    /// </summary>
    /// <remarks>
    /// The returned span points into native memory that stays valid only until the next call to <see cref="Render"/> or disposal.
    /// </remarks>
    public unsafe ReadOnlySpan<byte> Render()
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);

        var result = NativeMethods.libvgmstream_render(this.lib);
        if (result < 0)
        {
            throw new VgmStreamException($"libvgmstream_render() failed ({result}).");
        }

        var libStruct = Marshal.PtrToStructure<LibvgmstreamT>(this.lib);
        var dec = Marshal.PtrToStructure<LibvgmstreamDecoderT>(libStruct.decoder);
        this.Done = dec.done;

        if (dec.buf == IntPtr.Zero || dec.buf_bytes <= 0)
        {
            return [];
        }

        return new ReadOnlySpan<byte>((void*)dec.buf, dec.buf_bytes);
    }

    /// <summary>
    /// Fills a specified buffer with up to <paramref name="sampleCountPerChannel"/> samples.
    /// </summary>
    /// <returns>
    /// The number of samples-per-channel actually decoded (may be less at end of stream,
    /// with the remainder of the buffer zeroed by vgmstream).
    /// </returns>
    public unsafe int Fill(Span<byte> buffer, int sampleCountPerChannel)
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);

        var bytesNeeded = sampleCountPerChannel * this.Format.Channels * this.Format.SampleSize;
        if (buffer.Length < bytesNeeded)
        {
            throw new ArgumentException(
                $"Buffer too small: need at least {bytesNeeded} bytes for {sampleCountPerChannel} samples/channel.",
                nameof(buffer));
        }

        fixed (byte* p = buffer)
        {
            var result = NativeMethods.libvgmstream_fill(this.lib, (IntPtr)p, sampleCountPerChannel);
            if (result < 0)
            {
                throw new VgmStreamException($"libvgmstream_fill() failed ({result}).");
            }

            var libStruct = Marshal.PtrToStructure<LibvgmstreamT>(this.lib);
            var dec = Marshal.PtrToStructure<LibvgmstreamDecoderT>(libStruct.decoder);
            this.Done = dec.done;
            return dec.buf_samples;
        }
    }

    /// <summary>Streams decoded blocks until the stream is done. Each block is a fresh managed copy.</summary>
    public IEnumerable<byte[]> RenderBlocks()
    {
        while (!this.Done)
        {
            var span = this.Render();
            if (span.Length == 0)
            {
                if (this.Done)
                {
                    yield break;
                }

                continue;
            }
            yield return span.ToArray();
        }
    }

    /// <summary>Decodes the whole stream (respecting loop/config settings) into one buffer.</summary>
    public byte[] DecodeAll()
    {
        using var ms = new MemoryStream();
        foreach (var block in this.RenderBlocks())
        {
            ms.Write(block, 0, block.Length);
        }

        return ms.ToArray();
    }

    /// <summary>Decodes the whole stream and writes it out as a standard PCM/float .wav file.</summary>
    public void DecodeToWavFile(string outputPath)
    {
        using var fs = File.Create(outputPath);
        WaveWriter.Write(fs, this);
    }

    public long Position =>
        NativeMethods.libvgmstream_get_play_position(this.lib);

    public void Seek(long sample)
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);
        NativeMethods.libvgmstream_seek(this.lib, sample);
        this.RefreshDoneFlag();
    }

    public void Reset()
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);
        NativeMethods.libvgmstream_reset(this.lib);
        this.Done = false;
        this.RefreshFormat();
    }

    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        this.FreeNativeResources();
        this.disposed = true;
        GC.SuppressFinalize(this);
    }

    private void RefreshDoneFlag()
    {
        var libStruct = Marshal.PtrToStructure<LibvgmstreamT>(this.lib);
        var dec = Marshal.PtrToStructure<LibvgmstreamDecoderT>(libStruct.decoder);
        this.Done = dec.done;
    }

    private void FreeNativeResources()
    {
        if (this.lib != IntPtr.Zero)
        {
            NativeMethods.libvgmstream_free(this.lib);
            this.lib = IntPtr.Zero;
        }
    }

    ~VgmStream()
    {
        this.FreeNativeResources();
    }
}

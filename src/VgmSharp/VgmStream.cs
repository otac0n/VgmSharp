using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using VgmSharp.Native;

namespace VgmSharp;

/// <summary>
/// A single opened game-audio stream/subsong, backed by vgmstream's native libvgmstream.
/// Not thread-safe: use one instance per thread, or synchronize externally.
/// </summary>
public sealed class VgmStream : IDisposable
{
    private IntPtr _lib;
    private bool _disposed;

    public VgmStreamFormat Format { get; private set; }
    public bool Done { get; private set; }

    private VgmStream(IntPtr lib)
    {
        _lib = lib;
        RefreshFormat();
    }

    /// <summary>API version reported by the loaded native library (major.minor.patch).</summary>
    public static Version NativeApiVersion
    {
        get
        {
            uint v = NativeMethods.libvgmstream_get_version();
            return new Version((int)((v >> 24) & 0xFF), (int)((v >> 16) & 0xFF), (int)(v & 0xFFFF));
        }
    }

    /// <summary>Opens a subsong from a real file on disk.</summary>
    /// <param name="subsong">1..N, or 0 for the default/first subsong.</param>
    public static VgmStream Open(string filePath, int subsong = 0, VgmStreamConfig? config = null)
    {
        if (filePath is null) throw new ArgumentNullException(nameof(filePath));
        if (!File.Exists(filePath)) throw new FileNotFoundException("File not found.", filePath);

        IntPtr libsf = NativeMethods.libstreamfile_open_from_stdio(filePath);
        if (libsf == IntPtr.Zero)
            throw new VgmStreamException($"Could not open '{filePath}' for reading.");

        return OpenCore(libsf, subsong, config, disposeAfter: null);
    }

    /// <summary>
    /// Opens a subsong from an arbitrary managed <see cref="Stream"/> (must support Seek + Read).
    /// See <see cref="ManagedStreamFile"/> for the caveats vs. real file paths
    /// (companion-file lookups aren't supported for stream-backed input).
    /// </summary>
    /// <param name="virtualFilename">
    /// A filename (extension matters!) vgmstream uses to pick the right format parser,
    /// even though no such file needs to exist on disk.
    /// </param>
    public static VgmStream OpenFromStream(Stream stream, string virtualFilename, int subsong = 0, VgmStreamConfig? config = null)
    {
        if (stream is null) throw new ArgumentNullException(nameof(stream));
        if (string.IsNullOrEmpty(virtualFilename)) throw new ArgumentException("A virtual filename with extension is required.", nameof(virtualFilename));

        var msf = new ManagedStreamFile(stream, virtualFilename);
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
        IntPtr lib = NativeMethods.libvgmstream_init();
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

        int result = NativeMethods.libvgmstream_open_stream(lib, libsf, subsong);

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
        var libStruct = Marshal.PtrToStructure<LibvgmstreamT>(_lib);
        var fmt = Marshal.PtrToStructure<LibvgmstreamFormatT>(libStruct.format);
        Format = new VgmStreamFormat(fmt);
    }

    /// <summary>
    /// Decodes the next internal chunk of audio. The returned span points into native memory
    /// that stays valid only until the next call to <see cref="Render"/> or disposal --
    /// copy it out (e.g. via <c>ToArray()</c>) before calling Render again if you need to keep it.
    /// </summary>
    public unsafe ReadOnlySpan<byte> Render()
    {
        ThrowIfDisposed();

        int result = NativeMethods.libvgmstream_render(_lib);
        if (result < 0)
            throw new VgmStreamException($"libvgmstream_render() failed ({result}).");

        var libStruct = Marshal.PtrToStructure<LibvgmstreamT>(_lib);
        var dec = Marshal.PtrToStructure<LibvgmstreamDecoderT>(libStruct.decoder);
        Done = dec.done;

        if (dec.buf == IntPtr.Zero || dec.buf_bytes <= 0)
            return ReadOnlySpan<byte>.Empty;

        return new ReadOnlySpan<byte>((void*)dec.buf, dec.buf_bytes);
    }

    /// <summary>
    /// Fills a caller-provided buffer with up to <paramref name="sampleCountPerChannel"/> samples.
    /// Returns the number of samples-per-channel actually decoded (may be less at end of stream,
    /// with the remainder of the buffer zeroed by vgmstream).
    /// </summary>
    public unsafe int Fill(Span<byte> buffer, int sampleCountPerChannel)
    {
        ThrowIfDisposed();

        int bytesNeeded = sampleCountPerChannel * Format.Channels * Format.SampleSize;
        if (buffer.Length < bytesNeeded)
            throw new ArgumentException(
                $"Buffer too small: need at least {bytesNeeded} bytes for {sampleCountPerChannel} samples/channel.",
                nameof(buffer));

        fixed (byte* p = buffer)
        {
            int result = NativeMethods.libvgmstream_fill(_lib, (IntPtr)p, sampleCountPerChannel);
            if (result < 0)
                throw new VgmStreamException($"libvgmstream_fill() failed ({result}).");

            // libvgmstream_fill's return value is just a >=0/<0 result code, NOT the sample
            // count (unlike _render). The actual number of samples decoded into our buffer is
            // reported via decoder->buf_samples.
            var libStruct = Marshal.PtrToStructure<LibvgmstreamT>(_lib);
            var dec = Marshal.PtrToStructure<LibvgmstreamDecoderT>(libStruct.decoder);
            Done = dec.done;
            return dec.buf_samples;
        }
    }

    private void RefreshDoneFlag()
    {
        var libStruct = Marshal.PtrToStructure<LibvgmstreamT>(_lib);
        var dec = Marshal.PtrToStructure<LibvgmstreamDecoderT>(libStruct.decoder);
        Done = dec.done;
    }

    /// <summary>Streams decoded blocks until the stream is done. Each block is a fresh managed copy.</summary>
    public IEnumerable<byte[]> RenderBlocks()
    {
        while (!Done)
        {
            var span = Render();
            if (span.Length == 0)
            {
                if (Done) yield break;
                continue;
            }
            yield return span.ToArray();
        }
    }

    /// <summary>Decodes the whole stream (respecting loop/config settings) into one buffer.</summary>
    public byte[] DecodeAll()
    {
        using var ms = new MemoryStream();
        foreach (var block in RenderBlocks())
            ms.Write(block, 0, block.Length);
        return ms.ToArray();
    }

    /// <summary>Decodes the whole stream and writes it out as a standard PCM/float .wav file.</summary>
    public void DecodeToWavFile(string outputPath)
    {
        using var fs = File.Create(outputPath);
        WaveWriter.Write(fs, this);
    }

    public long Position => NativeMethods.libvgmstream_get_play_position(_lib);

    public void Seek(long sample)
    {
        ThrowIfDisposed();
        NativeMethods.libvgmstream_seek(_lib, sample);
        RefreshDoneFlag();
    }

    public void Reset()
    {
        ThrowIfDisposed();
        NativeMethods.libvgmstream_reset(_lib);
        Done = false;
        RefreshFormat();
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(VgmStream));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_lib != IntPtr.Zero)
        {
            NativeMethods.libvgmstream_free(_lib);
            _lib = IntPtr.Zero;
        }
        GC.SuppressFinalize(this);
    }

    ~VgmStream()
    {
        if (_lib != IntPtr.Zero)
        {
            NativeMethods.libvgmstream_free(_lib);
            _lib = IntPtr.Zero;
        }
    }
}

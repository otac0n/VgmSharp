namespace VgmSharp;

public static class WaveWriter
{
    /// <summary>
    /// Decodes the full stream and writes a standard RIFF/WAVE file (PCM16/24/32 or IEEE float,
    /// matching the stream's native output format). Requires a seekable output stream so the
    /// header's size fields can be patched in after decoding.
    /// </summary>
    public static void Write(Stream output, VgmStream stream)
    {
        if (!output.CanSeek)
        {
            throw new ArgumentException("Output stream must be seekable so header sizes can be patched.", nameof(output));
        }

        var fmt = stream.Format;
        var channels = (ushort)fmt.Channels;
        var sampleRate = (uint)fmt.SampleRate;
        var bitsPerSample = (ushort)(fmt.SampleSize * 8);
        var blockAlign = (ushort)(channels * fmt.SampleSize);
        var byteRate = sampleRate * blockAlign;
        var audioFormat = fmt.SampleFormat == VgmSampleFormat.Float ? (ushort)3 /* WAVE_FORMAT_IEEE_FLOAT */ : (ushort)1 /* PCM */;

        long riffSizePos, dataSizePos, dataStartPos;

        using (var w = new BinaryWriter(output, System.Text.Encoding.ASCII, leaveOpen: true))
        {
            w.Write(new[] { 'R', 'I', 'F', 'F' });
            riffSizePos = output.Position;
            w.Write((uint)0); // patched below
            w.Write(new[] { 'W', 'A', 'V', 'E' });

            w.Write(new[] { 'f', 'm', 't', ' ' });
            w.Write((uint)16);
            w.Write(audioFormat);
            w.Write(channels);
            w.Write(sampleRate);
            w.Write(byteRate);
            w.Write(blockAlign);
            w.Write(bitsPerSample);

            w.Write(new[] { 'd', 'a', 't', 'a' });
            dataSizePos = output.Position;
            w.Write((uint)0); // patched below
            dataStartPos = output.Position;

            foreach (var block in stream.RenderBlocks())
            {
                w.Write(block);
            }

            var dataEndPos = output.Position;
            var dataSize = (uint)(dataEndPos - dataStartPos);
            var riffSize = (uint)(dataEndPos - riffSizePos - 4 + 4); // RIFF size excludes 'RIFF'+size itself

            output.Position = riffSizePos;
            w.Write(riffSize);
            output.Position = dataSizePos;
            w.Write(dataSize);
            output.Position = dataEndPos;
        }
    }
}

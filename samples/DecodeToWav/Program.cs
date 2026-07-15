using System;
using System.IO;
using VgmSharp;

if (args.Length < 1)
{
    Console.WriteLine("Usage: DecodeToWav <input file> [output.wav]");
    return 1;
}

Console.WriteLine($"vgmstream native API version: {VgmStream.NativeApiVersion}");

string input = args[0];
string output = args.Length > 1 ? args[1] : Path.ChangeExtension(input, ".out.wav");

using (var stream = VgmStream.Open(input, config: VgmStreamConfig.PlayOnceNoLoop()))
{
    Console.WriteLine($"Opened: {stream.Format}");

    long totalBytes = 0;
    int blocks = 0;
    foreach (var block in stream.RenderBlocks())
    {
        totalBytes += block.Length;
        blocks++;
    }
    Console.WriteLine($"Render(): decoded {totalBytes} bytes across {blocks} blocks.");

    stream.Reset();
    stream.DecodeToWavFile(output);
    Console.WriteLine($"Wrote {output} ({new FileInfo(output).Length} bytes).");

    stream.Reset();
    Span<byte> fillBuf = new byte[4096];
    int totalFillSamples = 0;
    while (!stream.Done)
        totalFillSamples += stream.Fill(fillBuf, fillBuf.Length / (stream.Format.Channels * stream.Format.SampleSize));
    Console.WriteLine($"Fill(): decoded {totalFillSamples} total samples/channel.");
}

byte[] fileBytes = File.ReadAllBytes(input);
using (var ms = new MemoryStream(fileBytes))
using (var streamBacked = VgmStream.OpenFromStream(ms, Path.GetFileName(input), config: VgmStreamConfig.PlayOnceNoLoop()))
{
    byte[] fromStream = streamBacked.DecodeAll();
    Console.WriteLine($"OpenFromStream(): decoded {fromStream.Length} bytes from a MemoryStream (no real file path involved).");
}

return 0;

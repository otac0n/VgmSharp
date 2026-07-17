# VgmSharp

A cross-platform .NET wrapper around [vgmstream](https://github.com/vgmstream/vgmstream)'s `libvgmstream` C API, for decoding hundreds of video game audio formats (ADX, HCA, DSP, BRSTM, XWMA, and many more — see `VgmStream.NativeApiVersion` / `libvgmstream_get_extensions()`).

## Building

```bash
# requires: git, cmake, build-essential, libmpg123-dev, libvorbis-dev, libspeex-dev
./native/build-linux.sh
```

```powershell
# requires: git, cmake, Visual Studio (Desktop C++ workload) or VS Build Tools
.\native\build-windows.ps1
```

```powershell
dotnet pack src\VgmSharp.slnx -c Release
```

## Usage

```csharp
using VgmSharp;

// Decode a file straight to a .wav
using var stream = VgmStream.Open("bgm01.adx", config: VgmStreamConfig.PlayOnceNoLoop());
Console.WriteLine(stream.Format); // channels, sample rate, codec, loop points, etc.
stream.DecodeToWavFile("bgm01.wav");

// Or stream it
using var stream2 = VgmStream.Open("bgm01.adx");
foreach (var pcmBlock in stream2.RenderBlocks())
{
    // ...
}

// Or decode from an arbitrary Stream (archive entry, embedded resource, network...)
using var stream3 = VgmStream.OpenFromStream(myStream, "bgm01.adx");
byte[] pcm = stream3.DecodeAll();
```

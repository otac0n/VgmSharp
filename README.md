VgmSharp
========

[![ISC Licensed](https://img.shields.io/badge/license-ISC-blue.svg?style=flat-square)](license.md)
[![Get it on NuGet](https://img.shields.io/nuget/v/VgmSharp.svg?style=flat-square)](http://nuget.org/packages/VgmSharp)

A cross-platform .NET wrapper around [vgmstream](https://github.com/vgmstream/vgmstream)'s `libvgmstream` C API, for decoding hundreds of video game audio formats (ADX, HCA, DSP, BRSTM, XWMA, and many more — see `VgmStream.NativeApiVersion` / `libvgmstream_get_extensions()`).

Getting Started
---------------

```powershell
PM> Install-Package VgmSharp
```

```csharp
using VgmSharp;

// Decode a file straight to a .wav
using var input = VgmStreamReader.Open("bgm01.adx", config: VgmStreamConfig.PlayOnceNoLoop());
using var output = File.Create("bgm01.wav");
input.DecodeTo(output);

// Or stream it
using var reader = VgmStreamReader.Open("bgm01.adx");
Console.WriteLine(stream.Format); // channels, sample rate, codec, loop points, etc.
foreach (var pcmBlock in reader.RenderBlocks())
{
    // ...
}

// Or decode from an arbitrary Stream (archive entry, embedded resource, network...)
using var reader2 = VgmStreamReader.Open(myStream, "stream.vag");
byte[] pcm = reader2.DecodeAll();
```

Building
--------

```bash
sudo apt-get install -y git cmake build-essential libmpg123-dev libvorbis-dev libspeex-dev
./native/build-linux.sh
```

```powershell
# requires: git, cmake, Visual Studio (Desktop C++ workload) or VS Build Tools
.\native\build-windows.ps1
```

```powershell
dotnet pack src\VgmSharp.slnx -c Release
```


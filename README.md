# VgmSharp

A cross-platform .NET wrapper around [vgmstream](https://github.com/vgmstream/vgmstream)'s
`libvgmstream` C API, for decoding hundreds of video game audio formats (ADX, HCA, DSP, BRSTM,
XWMA, and many more — see `VgmStream.NativeApiVersion` / `libvgmstream_get_extensions()`).

No actively-maintained cross-platform C# wrapper existed for this (there's a stale, Windows-leaning
`vgmstream` NuGet package from ~2021 P/Invoking an old, undocumented build). This wraps vgmstream's
current, proper public C API (`src/libvgmstream.h`, added upstream specifically to support bindings
like this one) instead.

## Status

- **Linux (x64): built and verified end-to-end.** `libvgmstream.so` was built from real vgmstream
  source in the environment this was developed in, and every managed API path — `Render()`/
  `RenderBlocks()`, `Fill()`, `DecodeToWavFile()`, and `OpenFromStream()` (decoding from an arbitrary
  `Stream`, not a file) — was run against it and the decoded PCM was byte-diffed against a known-good
  reference. All identical.
- **Windows (x64): written, not yet run.** `native/build-windows.ps1` targets the same CMake
  `libvgmstream_shared` target (which already wires up `__declspec(dllexport)` correctly), but there
  was no Windows machine available to actually run it. Please run it once and let me know if the
  DLL name/output path needs adjusting — I'd rather flag that than claim it's verified when it isn't.

## Layout

```
src/VgmSharp/              the managed library (P/Invoke + friendly wrapper API)
native/build-linux.sh      builds libvgmstream.so from vgmstream source (tested)
native/build-windows.ps1   builds vgmstream.dll from vgmstream source (untested, see above)
runtimes/{rid}/native/     native binaries land here, packaged into the .nupkg
samples/DecodeToWav/       a small console app exercising every code path
.github/workflows/build.yml  CI: builds native libs on both OSes, then packs the NuGet package
```

## Building the native libraries

```bash
# Linux
./native/build-linux.sh
# requires: git, cmake, build-essential, libmpg123-dev, libvorbis-dev, libspeex-dev
```

```powershell
# Windows (from a Developer PowerShell for VS prompt)
.\native\build-windows.ps1
# requires: git, cmake, Visual Studio (Desktop C++ workload) or VS Build Tools
```

Both scripts pin to a specific vgmstream commit (`VGMSTREAM_REF` env var / `-VgmstreamRef` param) for
reproducible builds, and drop the result into `runtimes/<rid>/native/`.

FFmpeg support (`USE_FFMPEG`) is off by default — it unlocks extra codecs but adds a much heavier
build and more runtime `.so`/`.dll` dependencies to ship. `USE_CELT`/`USE_G719`/`USE_ATRAC9`/
`USE_G7221` are on by default (matching upstream) but pull their source via CMake `FetchContent` at
configure time, so the build machine needs normal internet access.

## Building the managed package

```bash
dotnet build src/VgmSharp/VgmSharp.csproj -c Release
dotnet pack src/VgmSharp/VgmSharp.csproj -c Release -o ./artifacts
```

`VgmSharp.csproj` picks up whatever's in `runtimes/{linux-x64,win-x64}/native/` at pack time (see
comments in the csproj and `build/VgmSharp.targets`).

## Usage

```csharp
using VgmSharp;

// Decode a file straight to a .wav
using var stream = VgmStream.Open("bgm01.adx", config: VgmStreamConfig.PlayOnceNoLoop());
Console.WriteLine(stream.Format); // channels, sample rate, codec, loop points, etc.
stream.DecodeToWavFile("bgm01.wav");

// Or stream it (e.g. for real-time playback)
using var s2 = VgmStream.Open("bgm01.adx");
foreach (var pcmBlock in s2.RenderBlocks())
{
    // feed pcmBlock (interleaved PCM16/24/32/float, per s2.Format) to your audio output
}

// Or decode from an arbitrary Stream (archive entry, embedded resource, network...) --
// note: "virtualFilename" must have a real extension, vgmstream uses it to pick a format parser
using var s3 = VgmStream.OpenFromStream(myStream, "bgm01.adx");
byte[] pcm = s3.DecodeAll();
```

### Looping

Game audio commonly loops. By default (`config: null`) vgmstream applies its own default loop
behavior. Useful presets:

```csharp
VgmStreamConfig.PlayOnceNoLoop();      // ignore loop points entirely
VgmStreamConfig.TwoLoopsWithFade();    // 2 loops + a 10s fade-out, like vgmstream-cli's defaults
```

Or build a custom `VgmStreamConfig` — see its XML docs for every option (`LoopCount`, `FadeTime`,
`ForceLoop`, `AllowPlayForever`, etc.), all mapped 1:1 to `libvgmstream_config_t`.

## A real bug found and fixed along the way

While validating `OpenFromStream()`, format detection consistently failed for a file that opened
fine via a real path. Traced it (via a minimal pure-C repro against the built `.so`, to rule out a
marshaling bug) to `src/meta/riff.c`'s `vgmstream_open_stream()` step, which needs the streamfile's
`open()` callback to succeed when vgmstream reopens *the same filename* to get an independent handle
for the main decode stream — this is required for virtually every format, not just an optional
companion-file lookup like the header's phrasing suggests. `ManagedStreamFile.OpenImpl` now supports
self-reopen (same virtual filename → a new handle sharing the underlying `Stream` + a lock), while
still correctly refusing to open genuinely different companion files (which a single `Stream` can't
satisfy anyway).

Separately, `libstreamfile_open_buffered()` currently has an upstream bug (passes the wrong
`user_data` pointer into the wrapped streamfile's `read()`, causing a crash) — this wrapper doesn't
use it.

## Known limitations

- `OpenFromStream` only supports self-reopen, not genuinely different companion files (e.g. TXTH
  sidecar files, `.dsp`+`.dsh` pairs). Use `VgmStream.Open(path)` for those.
- Only linux-x64 and win-x64 are built by the provided scripts/CI. Other RIDs (osx-x64/arm64,
  linux-arm64) would need their own native build legs — the managed code itself is fully portable,
  it's just native asset packaging that'd need extending.

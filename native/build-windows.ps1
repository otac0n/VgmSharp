# Builds vgmstream.dll for win-x64 and drops it into runtimes/win-x64/native/.
#
# Requires: git, CMake, and Visual Studio (Desktop C++ workload) or the VS Build Tools.
# Run from a "Developer PowerShell for VS" prompt so cl.exe/MSBuild are on PATH,
# or just have CMake pick up VS via its default generator.
#
# By default this forces a Visual Studio generator (trying 2022 then 2019) with -A x64,
# since -A only works with VS generators -- if your machine's CMake default generator is
# Ninja (common if you have Ninja installed/on PATH), passing -A x64 against it fails.
# If you'd rather use Ninja yourself, pass -Generator Ninja (and run this from an "x64
# Native Tools Command Prompt for VS" so cl.exe already targets x64 -- Ninja is single-config
# and has no -A equivalent, it just builds whatever architecture the active compiler targets).

param(
    [switch]$UseFfmpeg = $false,
    [string]$Generator = ""
)

$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = Split-Path -Parent $ScriptDir
$SrcDir = Join-Path $ScriptDir "vgmstream-src"
$BuildDir = Join-Path $SrcDir "build-win-x64"
$OutDir = Join-Path $RepoRoot "runtimes\win-x64\native"

git submodule update --init --recursive
Push-Location $SrcDir
#git submodule update --init --recursive

if (Test-Path $BuildDir) { Remove-Item -Recurse -Force $BuildDir }
New-Item -ItemType Directory -Path $BuildDir | Out-Null
Push-Location $BuildDir

$ffmpegFlag = if ($UseFfmpeg) { "ON" } else { "OFF" }

# -A x64 (platform selection) only works with the Visual Studio generator, not Ninja/etc.
# Force VS explicitly instead of trusting whatever CMake's default generator is on this
# machine (e.g. it'll pick Ninja if that's installed, which -A x64 then breaks on) --
# unless the caller explicitly asked for a specific generator via -Generator.
$generators = if ($Generator) { @($Generator) } else { @("Visual Studio 17 2022", "Visual Studio 16 2019") }
$configured = $false
foreach ($gen in $generators) {
    Write-Host "Trying generator: $gen"
    $isVs = $gen -like "Visual Studio*"
    if ($isVs) {
        cmake -G $gen -A x64 `
            -DBUILD_SHARED_LIBS=ON -DBUILD_CLI=OFF -DBUILD_WINAMP=OFF -DBUILD_XMPLAY=OFF -DBUILD_FB2K=OFF `
            -DUSE_MPEG=ON -DUSE_VORBIS=ON -DUSE_FFMPEG=$ffmpegFlag ..
    } else {
        # Non-VS generators (e.g. Ninja) have no -A; the active compiler (set up via
        # vcvarsall/x64 Native Tools prompt) determines the target architecture instead.
        cmake -G $gen `
            -DBUILD_SHARED_LIBS=ON -DBUILD_CLI=OFF -DBUILD_WINAMP=OFF -DBUILD_XMPLAY=OFF -DBUILD_FB2K=OFF `
            -DUSE_MPEG=ON -DUSE_VORBIS=ON -DUSE_FFMPEG=$ffmpegFlag ..
    }
    if ($LASTEXITCODE -eq 0) {
        $configured = $true
        break
    }
    Write-Warning "Generator '$gen' failed or isn't installed, trying next..."
}
if (-not $configured) {
    throw "Could not configure with any known generator. Do you have VS 2019/2022 (Desktop C++ workload) or the Build Tools installed? You can also pass one explicitly, e.g.:`n  .\build-windows.ps1 -Generator `"Visual Studio 17 2022`""
}

cmake --build . --config Release --target libvgmstream_shared

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

# The libvgmstream_shared CMake target already wires up __declspec(dllexport) via
# LIBVGMSTREAM_EXPORT and a SHARED library type. The DLL name/path below is still
# unconfirmed against a successful Windows build (the first attempt failed at the
# configure step -- see the generator-selection fix above); if the filter below doesn't
# find your DLL, check $BuildDir for what actually got produced and let me know the
# real name/path so this can be tightened up.
$dll = Get-ChildItem -Recurse -Filter "libvgmstream.dll" -Path . | Select-Object -First 1
if (-not $dll) { $dll = Get-ChildItem -Recurse -Filter "vgmstream.dll" -Path . | Select-Object -First 1 }
if (-not $dll) { throw "Could not find built DLL under $BuildDir -- see notes above." }

Copy-Item $dll.FullName (Join-Path $OutDir "vgmstream.dll") -Force

# vgmstream.dll dynamically links against these on Windows -- they're prebuilt binaries
# checked directly into the vgmstream repo (ext_libs/dll-x64), not built from source, since
# the CMake libvgmstream_shared target itself doesn't copy them (only the CLI/plugin targets'
# install_dlls step does, and we build with BUILD_CLI=OFF). Without them you'll hit
# DllNotFoundException at libvgmstream_init() -- LoadLibrary reports "module not found" for
# vgmstream.dll itself even though vgmstream.dll IS present, because one of *its* dependencies
# is missing; Windows doesn't tell you which one directly, but this is almost always it.
# This list mirrors cmake/vgmstream.cmake's install_dlls() macro, gated the same way our USE_*
# cmake flags above are (all on by default except FFmpeg).
$extLibsDir = Join-Path $SrcDir "ext_libs\dll-x64"
$companionDlls = @(
    "libmpg123-0.dll",      # USE_MPEG
    "libvorbis.dll",        # USE_VORBIS
    "libg719_decode.dll",   # USE_G719
    "libatrac9.dll",        # USE_ATRAC9
    "libcelt-0061.dll",     # USE_CELT
    "libcelt-0110.dll",     # USE_CELT
    "libspeex-1.dll"        # USE_SPEEX
)
if ($UseFfmpeg) {
    $companionDlls += @(
        "avcodec-vgmstream-59.dll",
        "avformat-vgmstream-59.dll",
        "avutil-vgmstream-57.dll",
        "swresample-vgmstream-4.dll"
    )
}
foreach ($name in $companionDlls) {
    $src = Join-Path $extLibsDir $name
    if (Test-Path $src) {
        Copy-Item $src (Join-Path $OutDir $name) -Force
    } else {
        Write-Warning "Expected companion DLL not found: $src (if you toggled a USE_* cmake flag off above, remove the matching entry from `$companionDlls here too)"
    }
}

Write-Host "Done: $(Join-Path $OutDir 'vgmstream.dll') + $($companionDlls.Count) companion DLL(s)"

Pop-Location
Pop-Location

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
    [string]$VgmstreamRef = "79fda53d6887e4a3dfe962c65c2e9291792da2fc",
    [switch]$UseFfmpeg = $false,
    [string]$Generator = ""
)

$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = Split-Path -Parent $ScriptDir
$SrcDir = Join-Path $ScriptDir "vgmstream-src"
$BuildDir = Join-Path $SrcDir "build-win-x64"
$OutDir = Join-Path $RepoRoot "runtimes\win-x64\native"

if (-not (Test-Path $SrcDir)) {
    git clone https://github.com/vgmstream/vgmstream.git $SrcDir
}
Push-Location $SrcDir
git fetch --depth 1 origin $VgmstreamRef 2>$null
if ($LASTEXITCODE -ne 0) { git fetch }
git checkout $VgmstreamRef

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
Write-Host "Done: $(Join-Path $OutDir 'vgmstream.dll')"

Pop-Location
Pop-Location

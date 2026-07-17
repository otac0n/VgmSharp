#!/usr/bin/env bash
# Builds libvgmstream.so for linux-x64 and drops it, plus its non-libc runtime
# dependencies (libmpg123/libvorbis/libvorbisfile/libspeex/libogg), into
# runtimes/linux-x64/native/. Verified end-to-end in isolation with the system copies
# of those libs hidden -- without bundling them, this only works by coincidence on a
# machine that happens to already have those exact apt packages installed.
#
# Requires: git, cmake, a C/C++ toolchain, and (for full codec coverage) the -dev packages below.
#   sudo apt-get install -y git cmake build-essential libmpg123-dev libvorbis-dev libspeex-dev
#
# FFmpeg (USE_FFMPEG) is intentionally left off by default: it pulls in a much heavier/slower
# build and a bunch of extra runtime .so deps to ship. Flip USE_FFMPEG=ON below (and install
# libavformat-dev libavcodec-dev libavutil-dev libswresample-dev) if you need the extra codecs
# it unlocks (many movie/streaming formats) -- note this script doesn't yet bundle FFmpeg's own
# .so dependencies the way it does for mpg123/vorbis/speex below; extend the ldd-filtering loop
# near the bottom if you turn this on.
#
# Note: USE_CELT/USE_G719/USE_ATRAC9/USE_G7221 pull their source via CMake FetchContent at
# configure time (git clone from github.com/gitlab.xiph.org), so this needs unrestricted
# internet access on whatever machine/CI runner does the build.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

VGMSTREAM_REF="${VGMSTREAM_REF:-79fda53d6887e4a3dfe962c65c2e9291792da2fc}"
SRC_DIR="$SCRIPT_DIR/vgmstream-src"
BUILD_DIR="$SRC_DIR/build-linux-x64"
OUT_DIR="$REPO_ROOT/runtimes/linux-x64/native"

if [ ! -d "$SRC_DIR" ]; then
  git clone https://github.com/vgmstream/vgmstream.git "$SRC_DIR"
fi
cd "$SRC_DIR"
git fetch --depth 1 origin "$VGMSTREAM_REF" 2>/dev/null || git fetch
git checkout "$VGMSTREAM_REF"

rm -rf "$BUILD_DIR"
mkdir -p "$BUILD_DIR"
cd "$BUILD_DIR"

cmake \
  -DBUILD_SHARED_LIBS=ON \
  -DBUILD_CLI=OFF \
  -DBUILD_V123=OFF \
  -DBUILD_AUDACIOUS=OFF \
  -DUSE_MPEG=ON \
  -DUSE_VORBIS=ON \
  -DUSE_SPEEX=ON \
  -DUSE_FFMPEG=${USE_FFMPEG:-OFF} \
  -DUSE_CELT=${USE_CELT:-ON} \
  -DUSE_G719=${USE_G719:-ON} \
  -DUSE_ATRAC9=${USE_ATRAC9:-ON} \
  -DUSE_G7221=${USE_G7221:-ON} \
  -DCMAKE_BUILD_TYPE=Release \
  -DCMAKE_INSTALL_RPATH='$ORIGIN' \
  -DCMAKE_BUILD_WITH_INSTALL_RPATH=TRUE \
  -DCMAKE_SHARED_LINKER_FLAGS="-Wl,--disable-new-dtags" \
  ..
# CMAKE_INSTALL_RPATH + BUILD_WITH_INSTALL_RPATH bakes $ORIGIN into the binary immediately
# (we never run `make install`, we ship the raw build output directly). --disable-new-dtags
# forces the older, transitively-inherited DT_RPATH instead of the modern DT_RUNPATH, which
# matters here: libogg is a dependency of libvorbis/libvorbisfile, not of libvgmstream.so
# itself, and DT_RUNPATH does NOT propagate to a library's own dependencies the way DT_RPATH does.

make -j"$(nproc)" libvgmstream_shared

mkdir -p "$OUT_DIR"
cp -v src/libvgmstream.so "$OUT_DIR/libvgmstream.so"

# Bundle the non-libc/libm runtime deps so this doesn't silently rely on the build
# machine's apt packages happening to also be present on whatever machine runs it.
echo "Bundling runtime dependencies..."
ldd src/libvgmstream.so | awk '{print $1, $3}' | while read -r name path; do
  case "$name" in
    libc.so.*|libm.so.*|linux-vdso.so.*|ld-linux*.so.*)
      continue ;; # part of glibc itself, always present on any glibc-based Linux
  esac
  if [ -n "$path" ] && [ -f "$path" ]; then
    cp -v "$path" "$OUT_DIR/$name"
  fi
done

echo "Done: $OUT_DIR/libvgmstream.so + bundled deps"
ls -la "$OUT_DIR"

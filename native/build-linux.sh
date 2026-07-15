#!/usr/bin/env bash
# Builds libvgmstream.so for linux-x64 and drops it into runtimes/linux-x64/native/.
#
# Requires: git, cmake, a C/C++ toolchain, and (for full codec coverage) the -dev packages below.
#   sudo apt-get install -y git cmake build-essential libmpg123-dev libvorbis-dev libspeex-dev
#
# FFmpeg (USE_FFMPEG) is intentionally left off by default: it pulls in a much heavier/slower
# build and a bunch of extra runtime .so deps to ship. Flip USE_FFMPEG=ON below (and install
# libavformat-dev libavcodec-dev libavutil-dev libswresample-dev) if you need the extra codecs
# it unlocks (many movie/streaming formats).
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
  ..

make -j"$(nproc)" libvgmstream_shared

mkdir -p "$OUT_DIR"
cp -v src/libvgmstream.so "$OUT_DIR/libvgmstream.so"

echo "Done: $OUT_DIR/libvgmstream.so"

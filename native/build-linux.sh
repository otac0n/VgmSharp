#!/usr/bin/env bash
set -euo pipefail

for pkg in libmpg123 vorbisfile speex; do
  if ! pkg-config --exists "$pkg" 2>/dev/null; then
    echo "Missing development package for $pkg."
    echo "Install the required packages before building."
    exit 1
  fi
done

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

SRC_DIR="$SCRIPT_DIR/vgmstream-src"
BUILD_DIR="$SRC_DIR/build-linux-x64"
OUT_DIR="$REPO_ROOT/runtimes/linux-x64/native"

git submodule update --init --recursive
cd "$SRC_DIR"
#git submodule update --init --recursive

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

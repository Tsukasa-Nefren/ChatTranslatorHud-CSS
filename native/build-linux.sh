#!/bin/bash
set -e

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT_DIR="$ROOT_DIR/runtimes/linux-x64/native"
mkdir -p "$OUT_DIR"

select_existing_path() {
    local desc="$1"
    shift
    for p in "$@"; do
        if [ -d "$p" ]; then
            echo "$p"
            return 0
        fi
    done
    echo "Error: $desc was not found. Checked:" >&2
    for p in "$@"; do
        echo "  $p" >&2
    done
    exit 1
}

HL2SDK_INC=$(select_existing_path "CounterStrikeSharp hl2sdk-cs2 public include directory" \
    "$ROOT_DIR/../CounterStrikeSharp/libraries/hl2sdk-cs2/public" \
    "$ROOT_DIR/../../_tmp/CounterStrikeSharp/libraries/hl2sdk-cs2/public" \
)

HL2SDK_ROOT=$(dirname "$HL2SDK_INC")

PROTOBUF_INC=$(select_existing_path "protobuf include directory" \
    "$ROOT_DIR/../CounterStrikeSharp/libraries/hl2sdk-cs2/thirdparty/protobuf-3.21.8/src" \
    "$ROOT_DIR/../../_tmp/CounterStrikeSharp/libraries/hl2sdk-cs2/thirdparty/protobuf-3.21.8/src" \
)

PROTOBUF_LIB=$(select_existing_path "protobuf library directory" \
    "$ROOT_DIR/../CounterStrikeSharp/libraries/hl2sdk-cs2/lib/linux64/release" \
    "$ROOT_DIR/../CounterStrikeSharp/libraries/hl2sdk-cs2/lib/public/linux64" \
    "$ROOT_DIR/../../_tmp/CounterStrikeSharp/libraries/hl2sdk-cs2/lib/linux64/release" \
    "$ROOT_DIR/../../_tmp/CounterStrikeSharp/libraries/hl2sdk-cs2/lib/public/linux64" \
    "$ROOT_DIR/../../modsharp-public-master/Engine/lib" \
)

CXX=${CXX:-g++}

echo "Compiling ChatTranslatorHud.Native.so..."
$CXX -O2 -shared -fPIC -std=c++20 \
    -DMETA_IS_SOURCE2 \
    -D_LINUX \
    -DPOSIX \
    -DLINUX \
    -DGNUC \
    -DCOMPILER_GCC \
    -DPLATFORM_64BITS \
    -D_FILE_OFFSET_BITS=64 \
    -D_GLIBCXX_USE_CXX11_ABI=0 \
    -Dstricmp=strcasecmp \
    -D_stricmp=strcasecmp \
    -Dstrnicmp=strncasecmp \
    -D_strnicmp=strncasecmp \
    -D_snprintf=snprintf \
    -D_vsnprintf=vsnprintf \
    -D_alloca=alloca \
    -Dstrcmpi=strcasecmp \
    -I"$HL2SDK_INC" \
    -I"$HL2SDK_ROOT/common" \
    -I"$HL2SDK_INC/tier0" \
    -I"$HL2SDK_INC/tier1" \
    -I"$HL2SDK_INC/engine" \
    -I"$HL2SDK_INC/mathlib" \
    -I"$HL2SDK_INC/entity2" \
    -I"$HL2SDK_INC/schemasystem" \
    -I"$PROTOBUF_INC" \
    -L"$PROTOBUF_LIB" \
    -fvisibility=hidden \
    "$ROOT_DIR/native/ChatTranslatorHud.Native.Linux.cpp" \
    -o "$OUT_DIR/ChatTranslatorHud.Native.so" \
    -lprotobuf \
    -pthread

echo "Build complete: $OUT_DIR/ChatTranslatorHud.Native.so"

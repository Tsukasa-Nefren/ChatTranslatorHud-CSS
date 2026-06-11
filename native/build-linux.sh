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

PROTO_INC=$(select_existing_path "ModSharp generated protobuf include directory" \
    "$ROOT_DIR/../../modsharp-public-master/Engine/src/proto" \
)

PROTOBUF_INC=$(select_existing_path "protobuf include directory" \
    "$ROOT_DIR/../CounterStrikeSharp/libraries/hl2sdk-cs2/thirdparty/protobuf-3.21.8/src" \
    "$ROOT_DIR/../../_tmp/CounterStrikeSharp/libraries/hl2sdk-cs2/thirdparty/protobuf-3.21.8/src" \
)

PROTOBUF_LIB=$(select_existing_path "protobuf library directory" \
    "$ROOT_DIR/../CounterStrikeSharp/libraries/hl2sdk-cs2/lib/public/linux64" \
    "$ROOT_DIR/../../_tmp/CounterStrikeSharp/libraries/hl2sdk-cs2/lib/public/linux64" \
    "$ROOT_DIR/../../modsharp-public-master/Engine/lib" \
)

DYNOHOOK_INC=$(select_existing_path "DynoHook include directory" \
    "$ROOT_DIR/../CounterStrikeSharp/libraries/DynoHook/src" \
    "$ROOT_DIR/../../_tmp/CounterStrikeSharp/libraries/DynoHook/src" \
)

CXX=${CXX:-g++}

echo "Compiling ChatTranslatorHud.Native.so..."
$CXX -O2 -shared -fPIC -std=c++20 \
    -I"$PROTO_INC" \
    -I"$PROTOBUF_INC" \
    -I"$DYNOHOOK_INC" \
    -L"$PROTOBUF_LIB" \
    -fvisibility=hidden \
    "$ROOT_DIR/native/ChatTranslatorHud.Native.cpp" \
    -o "$OUT_DIR/ChatTranslatorHud.Native.so" \
    -lprotobuf

echo "Build complete: $OUT_DIR/ChatTranslatorHud.Native.so"

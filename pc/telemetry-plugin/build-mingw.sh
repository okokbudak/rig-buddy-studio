#!/usr/bin/env bash
# Builds rigbuddy-telemetry.dll without Visual Studio: MinGW-w64 on Linux/WSL (apt: g++-mingw-w64-x86-64-posix).
# One DLL, two plugins: scs-sdk-plugin + the bus job (here) and the FMOD sound plugin (../sound-plugin), joined by
# rigbuddy_plugin.cpp. FMOD's C++ classes are replaced by ../sound-plugin/rigbuddy-sound/fmod_cpp_shim.cpp (thin calls of FMOD's C API).
# Output: pc/telemetry-plugin/dist/rigbuddy-telemetry.dll (system DLLs and the game's own FMOD DLLs only, C runtime linked in)
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
CXX=${CXX:-x86_64-w64-mingw32-g++}
command -v "$CXX" >/dev/null || { echo "need $CXX (g++-mingw-w64-x86-64-posix)"; exit 1; }
OBJ=$(mktemp -d); trap 'rm -rf "$OBJ"' EXIT
T="$HERE/scs-sdk-plugin"; S="$HERE/../sound-plugin"

# telemetry: scs-sdk-plugin + the bus job (-I order: its own headers first)
for f in "$T"/scs-telemetry/src/*.cpp; do
  "$CXX" -std=c++17 -O2 -DUNICODE -D_UNICODE -I "$T/scs-telemetry/inc" -I "$T/scs_sdk/include" -c "$f" -o "$OBJ/t_$(basename "$f").o"
done
# sound: ts-fmod-plugin as Rig Buddy changed it
SFLAGS=(-std=c++17 -O2 -DNDEBUG -DTSFMODPLUGIN_EXPORTS -D_WINDOWS -D_USRDLL -I "$S/mingw" -I "$S/inc" -I "$S/rigbuddy-sound")
for f in config dllmain fmod_event fmod_manager fmod_cpp_shim; do
  "$CXX" "${SFLAGS[@]}" -include "$S/rigbuddy-sound/pch.h" -c "$S/rigbuddy-sound/$f.cpp" -o "$OBJ/s_$f.o"
done
# the glue: the only entry points the game sees
"$CXX" -std=c++17 -O2 -I "$T/scs_sdk/include" -c "$HERE/rigbuddy_plugin.cpp" -o "$OBJ/glue.o"

mkdir -p "$HERE/dist"
"$CXX" -s -shared -static -static-libgcc -static-libstdc++ "$OBJ"/*.o "$HERE/rigbuddy-telemetry.def" \
  "$S/lib/fmod_vc.lib" "$S/lib/fmodstudio_vc.lib" -lversion -lole32 -o "$HERE/dist/rigbuddy-telemetry.dll"
echo "dist/rigbuddy-telemetry.dll $(stat -c %s "$HERE/dist/rigbuddy-telemetry.dll") bytes"

#!/usr/bin/env bash
# Builds the Windows installer in WSL / Linux. Nothing needs to be installed on Windows.
#
#   setup/build.sh [-v 0.1.0] [--copy-to /mnt/c/Users/<you>/Downloads]
#
# Needs in PATH: node (>= 22.13) + npm, dotnet (SDK 8), python3, curl, sha256sum.
# Output: local/release/RigBuddyStudio-Setup-<version>.exe  (one file: the installer with the whole app inside)
#
# What goes in (see also the notes at the end):
#   RigBuddy.exe + .NET runtime (self-contained)   pc/host
#   node\node.exe                                  downloaded, SHA-256 checked
#   dist\agent                                    pc/agent (esbuild)
#   plugin\rigbuddy-telemetry.dll                  the game plugin, one DLL: scs-sdk-plugin (MIT) + the bus job + the FMOD sound plugin,
#                                                  built here with MinGW from pc/telemetry-plugin and pc/sound-plugin (required)
#   plugin\sound\                                  what goes next to it in the games: master.bank per game, sound_levels.txt
#   dist\server, navigator, parser, generator      only when vendor/tm-maps and pc/native/win-x64 exist (setup/bundle.mjs)
set -euo pipefail

VERSION=0.1.0
COPY_TO=
while [ $# -gt 0 ]; do
  case "$1" in
    -v) VERSION="$2"; shift 2 ;;
    --copy-to) COPY_TO="$2"; shift 2 ;;
    *) echo "unknown argument: $1" >&2; exit 2 ;;
  esac
done
[[ "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || { echo "version must look like 1.2.3" >&2; exit 2; }

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
LOCAL="$ROOT/local"
CACHE="$LOCAL/cache"
STAGE="$LOCAL/stage"
OUT="$LOCAL/release"
NODE_VERSION=v22.16.0

export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1
step() { printf '\n=== %s\n' "$*"; }
need() { command -v "$1" >/dev/null || { echo "missing in PATH: $1" >&2; exit 1; }; }
for t in node npm dotnet python3 curl sha256sum; do need "$t"; done

rm -rf "$STAGE" "$OUT" "$LOCAL/publish" "$LOCAL/installer"
mkdir -p "$CACHE" "$STAGE" "$OUT"

step "tests"
(cd "$ROOT/pc/agent" && npm ci --ignore-scripts --no-audit --no-fund >/dev/null)
(cd "$ROOT" && node dev/test-pair.mjs && node dev/test-update.mjs && node dev/test-busjob.mjs && node dev/test-sound.mjs)

step "Node services (esbuild)"
(cd "$ROOT/setup" && npm ci --no-audit --no-fund >/dev/null)
node "$ROOT/setup/bundle-lite.mjs" "$STAGE/dist"
if [ -d "$ROOT/vendor/tm-maps" ] && [ -f "$ROOT/pc/native/win-x64/cityhash.node" ]; then
  echo "tm-maps + addons found: full bundle (server, navigator, parser, generator)"
  (cd "$ROOT/pipeline" && npm ci --no-audit --no-fund >/dev/null)
  node "$ROOT/setup/bundle.mjs"
  cp -r "$ROOT/dist/." "$STAGE/dist/"
else
  echo "no vendor/tm-maps or pc/native/win-x64: this build has no navigation server and no map building"
fi

step "Node.js runtime (Windows)"
if [ ! -f "$CACHE/node-$NODE_VERSION-win-x64.zip" ]; then
  curl -fsSL -o "$CACHE/node-$NODE_VERSION-win-x64.zip" "https://nodejs.org/dist/$NODE_VERSION/node-$NODE_VERSION-win-x64.zip"
fi
(cd "$CACHE" && curl -fsSL "https://nodejs.org/dist/$NODE_VERSION/SHASUMS256.txt" | grep " node-$NODE_VERSION-win-x64.zip\$" | sha256sum -c -)

step "PC app (self-contained, no .NET install needed)"
dotnet publish "$ROOT/pc/host/RigBuddy.csproj" -c Release -o "$LOCAL/publish" --nologo -v q \
  -p:EnableWindowsTargeting=true -p:SelfContained=true -p:EnableCompressionInSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=false -p:DebugType=none -p:Version="$VERSION"

step "game plugins (MinGW, no Visual Studio needed)"
command -v x86_64-w64-mingw32-g++ >/dev/null || { echo "need MinGW (apt: g++-mingw-w64-x86-64-posix): rigbuddy-telemetry.dll is the game plugin, the installer is not complete without it" >&2; exit 1; }
bash "$ROOT/pc/telemetry-plugin/build-mingw.sh" 2>&1 | grep -v 'warning\|^ \|In file\|In function\|At global\|In static\|NULL\|\^' || true
[ -f "$ROOT/pc/telemetry-plugin/dist/rigbuddy-telemetry.dll" ] || { echo "rigbuddy-telemetry.dll was not built" >&2; exit 1; }

step "Android app (vendor/jdk17 + gradle + androidsdk, signed with android/keystore.properties)"
if [ -x "$ROOT/vendor/gradle/bin/gradle" ] && [ -x "$ROOT/vendor/jdk17/bin/java" ] && [ -d "$ROOT/vendor/androidsdk/platforms" ]; then
  (cd "$ROOT/android" && JAVA_HOME="$ROOT/vendor/jdk17" ANDROID_HOME="$ROOT/vendor/androidsdk" PATH="$ROOT/vendor/jdk17/bin:$PATH" \
    "$ROOT/vendor/gradle/bin/gradle" --no-daemon -q assembleRelease -PappVersion="$VERSION")
else echo "note: no Android toolchain in vendor/ (jdk17, gradle, androidsdk): the app is not built; the phone can not get it from the PC"; fi

step "stage files"
cp "$LOCAL/publish/"*.exe "$LOCAL/publish/"*.dll "$STAGE/"
mkdir -p "$STAGE/node" "$STAGE/plugin" "$STAGE/licenses"
python3 -I - "$CACHE" "$STAGE" "$NODE_VERSION" "$ROOT" <<'PY'
import sys, zipfile, os, shutil
cache, stage, nv, root = sys.argv[1:5]
z = zipfile.ZipFile(f'{cache}/node-{nv}-win-x64.zip')
base = f'node-{nv}-win-x64/'
open(f'{stage}/node/node.exe', 'wb').write(z.read(base + 'node.exe'))
open(f'{stage}/licenses/node.js-LICENSE.txt', 'wb').write(z.read(base + 'LICENSE'))
shutil.copy(f'{root}/LICENSE', f'{stage}/licenses/LICENSE.txt')
shutil.copy(f'{root}/pc/telemetry-plugin/scs-sdk-plugin/LICENSE', f'{stage}/licenses/scs-sdk-plugin-LICENSE.txt')  # MIT: the telemetry part is built from it
shutil.copy(f'{root}/pc/telemetry-plugin/scs-sdk-plugin/scs_sdk/sdk_license.txt', f'{stage}/licenses/scs-sdk-LICENSE.txt')
shutil.copy(f'{root}/pc/sound-plugin/LICENSE.md', f'{stage}/licenses/ts-fmod-plugin-LICENSE.txt')  # MIT: the sound part is built from it
shutil.copy(f'{root}/pc/telemetry-plugin/dist/rigbuddy-telemetry.dll', f'{stage}/plugin/rigbuddy-telemetry.dll')  # telemetry + bus job + sound
os.makedirs(f'{stage}/plugin/sound/ETS2', exist_ok=True); os.makedirs(f'{stage}/plugin/sound/ATS', exist_ok=True)
g = f'{root}/pc/sound-plugin/game-files'
for game in ('ETS2', 'ATS'):
    if os.path.exists(f'{g}/{game}/master.bank'): shutil.copy(f'{g}/{game}/master.bank', f'{stage}/plugin/sound/{game}/')
if os.path.exists(f'{g}/sound_levels.txt'): shutil.copy(f'{g}/sound_levels.txt', f'{stage}/plugin/sound/')
apk = f'{root}/android/app/build/outputs/apk/release/app-release.apk'
if os.path.exists(apk):
    os.makedirs(f'{stage}/app', exist_ok=True); shutil.copy(apk, f'{stage}/app/RigBuddy.apk')
else: print('note: no RigBuddy.apk')
PY
if [ -f "$ROOT/dist/THIRD-PARTY-LICENSES.txt" ]; then cp "$ROOT/dist/THIRD-PARTY-LICENSES.txt" "$STAGE/licenses/"; fi
du -sh "$STAGE" | cut -f1 | xargs echo "staged:"

step "payload.zip"
python3 -I - "$STAGE" "$LOCAL/payload.zip" <<'PY'
import sys, os, zipfile
stage, out = sys.argv[1:3]
if os.path.exists(out): os.remove(out)
with zipfile.ZipFile(out, 'w', zipfile.ZIP_DEFLATED, compresslevel=9) as z:
    for root, _, files in os.walk(stage):
        for f in sorted(files):
            p = os.path.join(root, f)
            z.write(p, os.path.relpath(p, stage).replace(os.sep, '/'))
print(f'{os.path.getsize(out)/1e6:.1f} MB')
PY

step "installer"
dotnet publish "$ROOT/setup/installer/Setup.csproj" -c Release -o "$LOCAL/installer" --nologo -v q \
  -p:EnableWindowsTargeting=true -p:PayloadZip="$LOCAL/payload.zip" -p:DebugType=none -p:Version="$VERSION"
cp "$LOCAL/installer/RigBuddyStudio-Setup.exe" "$OUT/RigBuddyStudio-Setup-$VERSION.exe"
(cd "$OUT" && sha256sum *.exe | tee SHA256SUMS.txt)
ls -la "$OUT"

if [ -n "$COPY_TO" ]; then
  cp "$OUT/RigBuddyStudio-Setup-$VERSION.exe" "$COPY_TO/"
  echo "copied to $COPY_TO"
fi

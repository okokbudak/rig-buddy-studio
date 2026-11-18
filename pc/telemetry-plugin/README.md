# rigbuddy-telemetry.dll (telemetry + bus job + sound, one DLL)

Rig Buddy's one game plugin: a single DLL that goes into each game's `bin\win_x64\plugins` folder. It is named and built
so that it never clashes with other tools: it does **not** share anything with `scs-telemetry.dll`, the plain plugin that
SimHub and other programs use. The two can sit in the same folder and run side by side; Rig Buddy never touches the other.

It holds two plugins, joined by `rigbuddy_plugin.cpp` (the only entry points the game sees, `scs_telemetry_init` / `_shutdown`):

| Part | Source | What it does |
|---|---|---|
| telemetry | `scs-sdk-plugin/` | [scs-sdk-plugin](https://github.com/truckermudgeon/scs-sdk-plugin) v1.12.1 (MIT), writing the shared memory **`Local\RigBuddyTelemetry`** (not `Local\SCSTelemetry`) that Rig Buddy reads, plus the bus job below |
| sound | `../sound-plugin/` | ts-fmod-plugin (MIT, Dario Wouters) as changed for Rig Buddy: plays FMOD sound mods. See its README |

The telemetry part alone decides whether the DLL loads: if the sound part cannot start (game update, FMOD missing, or
`plugins\rigbuddy-sound\off.txt` exists, which is what the Sound mods window's switch writes) the DLL still loads and reports telemetry.

Rig Buddy's addition to scs-sdk-plugin is the bus DLC job, which that plugin predates (telemetry 1.20: the `bus_job`
configuration and the `bus_job.*` gameplay events).

The addition is `scs-sdk-plugin/scs-telemetry/src/rigbuddy_bus.cpp` (`inc/rigbuddy_bus.hpp`), called from four places of
`scs_telemetry.cpp` (search for `rigbuddy_bus`; everything else is upstream's file unchanged). It writes
`<install folder>\data\live\<ets2|ats>-bus.json` whenever the bus job changes (the folder comes from
`plugins\rigbuddy-live.txt`, which the installer writes); the PC agent (`pc/agent`) reads it and the app shows the line and
routes to its next stop. It reads no game memory, so it does not depend on the game version.

## Build

MinGW-w64 on Linux/WSL (apt: `g++-mingw-w64-x86-64-posix`): `bash build-mingw.sh` → `dist/rigbuddy-telemetry.dll`
(the C runtime is linked in; it needs the game's own `fmod.dll` and `fmodstudio.dll`, which sit next to the game exe). `setup/build.sh` does it and ships the result; it is required there.

Checked without the game (before the rename of the shared memory): a small program that plays the game's side (registers the
plugin, feeds all 1413 channels, frame, configuration and gameplay events) gave **byte-identical shared memory** with this
build and with upstream's release DLL; the bus job came out as expected. To take a newer scs-sdk-plugin: copy its `scs-telemetry/` and `scs_sdk/`
over `scs-sdk-plugin/`, put the four `rigbuddy_bus` calls back into `scs_telemetry.cpp`, and repeat that check.

## What the app does with it

`pc/host/PluginCheck.cs` puts this DLL into the installed games and takes out Rig Buddy's older plugin files: its own
`scs-telemetry.dll` of earlier versions (recognised by the `[rigbuddy-telemetry]` log text it contains; a plain
`scs-telemetry.dll` that is not ours stays) and `rigbuddy-sound.dll`. Replacing only works while the game is not running.

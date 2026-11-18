# Rig Buddy sound plugin (part of `rigbuddy-telemetry.dll`)

The sound part of Rig Buddy's game plugin for Euro Truck Simulator 2 / American Truck Simulator. It plays **FMOD sound mods** (`.bank`
files) in the game. Rig Buddy builds it, puts it into each game's `bin\win_x64\plugins` folder and manages the mods from
its **Sound mods…** window.

## Credit

This is [ts-fmod-plugin](https://github.com/dariowouters/ts-fmod-plugin) by **Dario Wouters**, MIT license (see
`LICENSE.md`), renamed and changed for Rig Buddy. The commit it is based on is in `UPSTREAM`. All the work of finding
the game's memory structures and wiring the sounds is his.

FMOD Studio by Firelight Technologies Pty Ltd. `inc/fmod` and `lib/` are FMOD API files, as the upstream project ships
them; the plugin uses the FMOD libraries that come with the game.

## What Rig Buddy changed

- **One DLL:** it is built into `rigbuddy-telemetry.dll` together with the telemetry (`../telemetry-plugin`, `rigbuddy_plugin.cpp`); there is no separate sound DLL any more. Its files stay in `plugins\rigbuddy-sound\` (so it never clashes with a ts-fmod-plugin a user
  installed by hand); `off.txt` in that folder switches it off at the next game start; log lines start with `[rigbuddy-sound]`.
- **Live reload:** about once a second the plugin looks at `selected.bank.txt` and `sound_levels.txt`; when one changed,
  it unloads the old sound mods and loads the new ones (or the new levels) while the game runs. No `sdk reload`, no
  restart.
- **No mod, no failure:** an empty or missing `selected.bank.txt`, a mod that cannot be loaded or one without its
  `.bank.guids` file is skipped (and logged) instead of keeping the whole plugin from loading, so a mod picked later
  can still be loaded live.
- **Names with spaces:** `selected.bank.txt` is read one mod per line (the original split at every space, so a mod
  called `Pipe Mod` could not be loaded). The `.bank.guids` file still has to carry the same name as the `.bank`.
- **The game's own engine sounds:** the plugin finds the game's own FMOD system (log lines `[rigbuddy-sound][game-fmod]`) and, for
  every one of engine / exhaust / turbo that the loaded sound mod brings an event for, mutes the game's bus of that name; they
  come back when the mod is switched off. `keep-game-sounds.txt` in `plugins\rigbuddy-sound\` switches this off, `debug.txt` adds
  the list of the game's buses to the log.
- **The game's buses stay muted:** the game sets its own buses again now and then (its pause, menus, sound settings); a bus
  the plugin muted that plays again is muted again within half a second (logged as "was on again"). Upstream leaves the
  game's engine / exhaust / turbo channels to the player, who turns them down in the game's settings.
- **No Studio thread:** FMOD Studio is initialised with `FMOD_STUDIO_INIT_SYNCHRONOUS_UPDATE` and updated on every frame
  (upstream: a Studio thread of its own, which crashed the game on quit when it outlived the game's sound).
- **Safety:** memory the plugin reads is checked to be readable first (a wrong offset after a game update logs and stays
  silent instead of crashing the game).
- **Fix:** the sound-levels file path was kept as a reference to a temporary.

Everything else is upstream's, line for line: `memory_structure.h` is upstream's file (only the namespace renamed), and
the telemetry values feed the sound as upstream's channels do (rpm, throttle, clutch, retarder and gear are 0 while the
game has no value for them; they come from the telemetry part of the DLL, which has the game's one callback per channel).

## Game updates

The plugin reads values from the game's memory and only loads on the game version it was made for
(`common::supported_game_version`). When the game updates: take over the upstream changes for the new version
(memory offsets / patterns in `dllmain.cpp` and `memory_structure.h`, the version in `common.h` and the `.rc`), keep
the Rig Buddy changes above, and release a new Rig Buddy.

## Build

`bash ../telemetry-plugin/build-mingw.sh` (MinGW-w64, Linux/WSL) builds the sound part together with the telemetry into
`../telemetry-plugin/dist/rigbuddy-telemetry.dll`. The Visual Studio project here (`rigbuddy-sound.sln`) is the old stand-alone
build of this part alone and is not used for releases. `game-files\` holds what goes next to the DLL in the game
(`master.bank` per game, the default `sound_levels.txt`).

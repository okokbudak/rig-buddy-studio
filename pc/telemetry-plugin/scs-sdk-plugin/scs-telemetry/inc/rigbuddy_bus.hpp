// Rig Buddy: the bus DLC's job, handed to scs-telemetry.dll's own event handlers (one plugin DLL instead of two).
#pragma once
#include "scssdk_telemetry.h"

namespace rigbuddy_bus
{
    /** At plugin start: remembers the game's log function and which game this is, writes "no bus job". */
    void init(const scs_telemetry_init_params_v100_t* params);
    /** The game's configuration event (the "bus_job" configuration is the one that matters). */
    void configuration(const void* event_info);
    /** The game's gameplay event (the "bus_job.*" events). */
    void gameplay(const void* event_info);
    /** The game is closing: no job any more. Safe to call more than once. */
    void shutdown();
}

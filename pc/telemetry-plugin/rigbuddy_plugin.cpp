// rigbuddy-telemetry.dll: Rig Buddy's one game plugin. It is two plugins in one DLL, each with its own entry points
// (renamed so they can sit side by side) and this file as the only thing the game sees:
//
//   telemetry   scs-sdk-plugin (MIT) + the bus job: fills the shared memory "Local\RigBuddyTelemetry" that Rig Buddy reads
//   sound       ts-fmod-plugin (MIT, Dario Wouters), changed: plays FMOD sound mods
//
// The telemetry must never depend on the sound: when the sound part cannot start (game update, FMOD missing, switched off)
// the plugin still loads and reports telemetry. The telemetry part alone decides whether the DLL loads.
#include <windows.h>
#include <string>
#include "scssdk_telemetry.h"

// the entry points of the two parts keep their C++ linkage (the SDK's SCSAPI macros do not add extern "C"), the three hooks are plain C
SCSAPI_RESULT rigbuddy_telemetry_init(scs_u32_t version, const scs_telemetry_init_params_t* params);
SCSAPI_VOID rigbuddy_telemetry_shutdown(void);
SCSAPI_RESULT rigbuddy_sound_init(scs_u32_t version, const scs_telemetry_init_params_t* params);
SCSAPI_VOID rigbuddy_sound_shutdown(void);
extern "C" void rigbuddy_telemetry_unload();
extern "C" void rigbuddy_sound_attach();

static HMODULE self = nullptr;
static bool sound_started = false;

/** `plugins\rigbuddy-sound\off.txt` next to this DLL: the sound part stays off (Rig Buddy's switch, takes effect on the next game start). */
static bool sound_switched_off()
{
    char path[MAX_PATH] = {};
    if (GetModuleFileNameA(self, path, MAX_PATH) == 0) return false;
    std::string p(path);
    p = p.substr(0, p.find_last_of("\\/") + 1) + "rigbuddy-sound\\off.txt";
    return GetFileAttributesA(p.c_str()) != INVALID_FILE_ATTRIBUTES;
}

extern "C"
{
    SCSAPI_RESULT scs_telemetry_init(const scs_u32_t version, const scs_telemetry_init_params_t* const params)
    {
        const auto result = rigbuddy_telemetry_init(version, params);
        if (result != SCS_RESULT_ok) return result;
        if (version < SCS_TELEMETRY_VERSION_1_01) return result; // the sound part needs the 1.01 parameters
        const auto* const p = reinterpret_cast<const scs_telemetry_init_params_v101_t*>(params);
        if (sound_switched_off())
        {
            p->common.log(SCS_LOG_TYPE_message, "[rigbuddy-sound] switched off (rigbuddy-sound\\off.txt)");
            return result;
        }
        try { sound_started = rigbuddy_sound_init(version, params) == SCS_RESULT_ok; }
        catch (...) { p->common.log(SCS_LOG_TYPE_error, "[rigbuddy-sound] failed to start; telemetry carries on"); }
        return result;
    }

    SCSAPI_VOID scs_telemetry_shutdown(void)
    {
        if (sound_started) { rigbuddy_sound_shutdown(); sound_started = false; }
        rigbuddy_telemetry_shutdown();
    }

    BOOL APIENTRY DllMain(HMODULE module, DWORD reason, LPVOID)
    {
        if (reason == DLL_PROCESS_ATTACH) { self = module; rigbuddy_sound_attach(); }
        else if (reason == DLL_PROCESS_DETACH) { rigbuddy_sound_shutdown(); rigbuddy_telemetry_unload(); }
        return TRUE;
    }
}

// dllmain.cpp : Defines the entry point for the DLL application.
#include "pch.h"

#include "sdk_stores.h"
#include "telemetry_data.h"
#include "memory_structure.h"
#include "memory.h"
#include <algorithm>
#include <map>
#include <set>

fmod_manager* fmod_manager_instance = nullptr;

telemetry_data_t telemetry_data;

scs_log_t scs_log;

uintptr_t base_ctrl_ptr = NULL;
uintptr_t sound_system_ptr = NULL;
uint32_t game_actor_offset = 0;
uintptr_t game_base = NULL;
voice_navigation_sound_t* last_played = nullptr;
DWORD image_size = 0;

uint32_t stored_engine_state = 0;
byte indicator_stick_state = false;
bool was_indicator_light_on = false;
bool was_park_brake_on = false;
uint32_t prev_retarder_level = 0;
bool high_beams_enabled = false;
bool wipers_enabled = false;
float hazard_warning_state = 0.0f;
float light_horn_state = 0.0f;
float light_stick_state = 0.0f;
float wipers_stick_state = 0.0f;
float last_left_window_pos = 0.f;
float last_right_window_pos = 0.f;
bool is_air_pressure_warning_on = true;
bool is_window_moving = false;
bool is_left_window_button_active = false;
bool is_right_window_button_active = false;

// Rig Buddy: the game's objects are read through pointers whose layout can change with a game update. Never read
// memory that is not mapped: a wrong offset then gives a wrong value, not a crash.
static bool readable(const void* p, const size_t n)
{
    if (p == nullptr) return false;
    const auto* c = static_cast<const char*>(p);
    const auto* const end = c + n;
    MEMORY_BASIC_INFORMATION mbi;
    while (c < end)
    {
        if (VirtualQuery(c, &mbi, sizeof(mbi)) == 0) return false;
        if (mbi.State != MEM_COMMIT || (mbi.Protect & (PAGE_NOACCESS | PAGE_GUARD)) != 0) return false;
        c = static_cast<const char*>(mbi.BaseAddress) + mbi.RegionSize;
    }
    return true;
}

// Rig Buddy: a file called debug.txt in the plugin's folder (<game>\bin\win_x64\plugins\rigbuddy-sound) switches on a
// log line every 3 s with the raw floats of the truck's control block and what the plugin makes of them. After a game
// update this is how the offsets in memory_structure.h are checked against the running game.
static void debug_dump(const game_actor_u* actor, const sound_library_t* interior)
{
    static ULONGLONG next_check = 0;
    static bool enabled = false;
    static ULONGLONG next_dump = 0;
    const auto now = GetTickCount64();
    if (now >= next_check)
    {
        next_check = now + 5000;
        char dll_path[MAX_PATH] = {};
        HMODULE module = nullptr;
        GetModuleHandleExA(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
                           reinterpret_cast<LPCSTR>(&debug_dump), &module);
        GetModuleFileNameA(module, dll_path, MAX_PATH);
        std::string path(dll_path);
        path = path.substr(0, path.find_last_of("\\/") + 1) + "rigbuddy-sound\\debug.txt";
        enabled = GetFileAttributesA(path.c_str()) != INVALID_FILE_ATTRIBUTES;
    }
    if (!enabled || now < next_dump || !readable(actor, 0x480)) return;
    next_dump = now + 3000;

    const auto* raw = reinterpret_cast<const uint8_t*>(actor);
    char line[512];
    for (uint32_t base = 0x370; base < 0x470; base += 0x40)
    {
        std::string text;
        for (uint32_t o = base; o < base + 0x40; o += 4)
        {
            float f;
            memcpy(&f, raw + o, sizeof(f));
            snprintf(line, sizeof(line), " %03X=%.3f", o, f);
            text += line;
        }
        scs_log(SCS_LOG_TYPE_message, ("[rigbuddy-sound][debug] actor" + text).c_str());
    }
    snprintf(line, sizeof(line),
             "[rigbuddy-sound][debug] engine_state=%u turbo=%.3f lights=%.3f horn=%.3f hazard=%.3f wipers=%.3f "
             "engine_brake=%.3f | win L dir=%u mov=%u state=%.3f btn=%.3f | R dir=%u mov=%u state=%.3f btn=%.3f | rpm=%.0f gear=%d",
             actor->get_engine_state(), actor->get_turbo_pressure(), actor->get_light_switch_state(),
             actor->get_light_horn_state(), actor->get_hazard_warning_state(), actor->get_wipers_state(),
             actor->get_engine_brake_state(), actor->left_window_moving_direction, actor->is_left_window_moving,
             actor->left_window_state, actor->left_window_btn_state, actor->right_window_moving_direction,
             actor->is_right_window_moving, actor->right_window_state, actor->right_window_btn_state,
             telemetry_data.rpm, telemetry_data.gear);
    scs_log(SCS_LOG_TYPE_message, line);
    if (interior != nullptr && readable(interior, 0x1CC8))
    {
        snprintf(line, sizeof(line),
                 "[rigbuddy-sound][debug] sound_library interior_cam=%d camera_inside=%d window_state=(%.3f, %.3f) "
                 "cabin_out=%.3f cabin_rot=%.3f echo=%u now_playing_nav=%p",
                 interior->get_is_on_interior_cam(), interior->get_is_camera_inside(), interior->get_window_state().x,
                 interior->get_window_state().y, interior->cabin_out, interior->get_camera_rotation_in_cabin(),
                 interior->get_has_echo(), static_cast<const void*>(interior->get_now_playing_navigation_sound()));
        scs_log(SCS_LOG_TYPE_message, line);
    }
}

scs_telemetry_register_for_channel_t register_for_channel = nullptr;
scs_telemetry_unregister_from_channel_t unregister_from_channel = nullptr;
#define register_channel(name, index, type, field) register_for_channel(SCS_TELEMETRY_##name, index, SCS_VALUE_TYPE_##type, SCS_TELEMETRY_CHANNEL_FLAG_no_value, telemetry_store_##type, &telemetry_data.field);

// Rig Buddy: the game takes one callback per event, and the telemetry part of this DLL already has the paused / started ones
// (registering them here too failed with "specified event is already registered" and left the sound off). So the telemetry's
// callback hands those two events on to this function.
// Rig Buddy: the plugin's FMOD system has to be gone before the game releases FMOD at quit (see fmod_manager::init). A small
// thread watches the game's own FMOD Studio system and lets go of ours as soon as the game's is released; the lock keeps
// it from doing that while a frame of the plugin is using FMOD.
static CRITICAL_SECTION fmod_lock;
static bool fmod_lock_ready = false;
static volatile bool released_for_quit = false;

struct fmod_guard
{
    fmod_guard() { if (fmod_lock_ready) EnterCriticalSection(&fmod_lock); }
    ~fmod_guard() { if (fmod_lock_ready) LeaveCriticalSection(&fmod_lock); }
};

extern "C" void rigbuddy_sound_pause(const int paused)
{
    fmod_guard guard;
    if (fmod_manager_instance == nullptr) return; // sound did not start, telemetry carries on in this DLL
    fmod_manager_instance->set_paused(paused != 0);
}

bool should_engine_brake_sound_play()
{
    return telemetry_data.engine_brake && telemetry_data.effective_throttle < 0.05f && telemetry_data.gear != 0 &&
        telemetry_data.effective_clutch < 0.05f;
}

// Rig Buddy: the game takes one callback per channel too, and the telemetry part of this DLL already has them (registering
// them here as well failed silently, so rpm and throttle stayed 0: only the idle sounded). The values are read from the
// telemetry part's own shared memory on every frame instead (rigbuddy_telemetry_pull, same layout on both sides).
struct rb_feed
{
    float rpm, throttle, clutch;
    int motor_brake, air_warning, engine_enabled, lblinker, rblinker, light_lblinker, light_rblinker, park_brake;
    unsigned int retarder;
    int high_beam, wipers, gear;
};
extern "C" int rigbuddy_telemetry_pull(rb_feed* out);

static void pull_telemetry()
{
    rb_feed f = {};
    if (!rigbuddy_telemetry_pull(&f)) return;
    telemetry_data.rpm = f.rpm;
    telemetry_data.effective_throttle = f.throttle;
    telemetry_data.effective_clutch = f.clutch;
    telemetry_data.engine_brake = f.motor_brake != 0;
    telemetry_data.brake_air_pressure_warning = f.air_warning != 0;
    telemetry_data.engine_enabled = f.engine_enabled != 0;
    telemetry_data.lblinker = f.lblinker != 0;
    telemetry_data.rblinker = f.rblinker != 0;
    telemetry_data.light_lblinker = f.light_lblinker != 0;
    telemetry_data.light_rblinker = f.light_rblinker != 0;
    telemetry_data.park_brake_on = f.park_brake != 0;
    telemetry_data.retarder_level = f.retarder;
    telemetry_data.high_beam = f.high_beam != 0;
    telemetry_data.wipers = f.wipers != 0;
    telemetry_data.gear = f.gear;
}


// --- the game's own sounds --------------------------------------------------------------------------------------------
// The plugin plays the mods in its own FMOD system, so the game's engine, exhaust and turbo keep playing next to them. While a
// sound mod brings the matching event (engine/engine, engine/exhaust, engine/turbo), the game's bus for it is muted, and let go
// of when the mod is switched off (a bus whose event the mod lacks stays on, so nothing goes silent). The game's FMOD system
// is found by looking at the values around the game's sound library for a valid FMOD Studio handle (FMOD answers "invalid" for
// anything else). `rigbuddy-sound\keep-game-sounds.txt` switches this off; `debug.txt` adds the list of the game's buses to the log.
static FMOD_STUDIO_SYSTEM* game_studio = nullptr;
static int game_scan_tries = 0;
static ULONGLONG game_scan_next = 0;

struct game_bus_t
{
    const char* path;       // the game's bus
    const char* mod_event;  // the mod's event that replaces it
    FMOD_STUDIO_BUS* bus;
    int muted;              // -1: not applied yet
};
static game_bus_t game_buses[] = {
    {"bus:/outside/exterior/truck_engine", "engine/engine", nullptr, -1},
    {"bus:/outside/exterior/truck_exhaust", "engine/exhaust", nullptr, -1},
    {"bus:/outside/exterior/truck_turbo", "engine/turbo", nullptr, -1},
};

static bool looks_like_pointer(const uint64_t v)
{
    return v > 0x10000 && v < 0x00007FFFFFFFFFFFull;
}

static FMOD_STUDIO_SYSTEM* scan_for_studio(const void* base, const size_t bytes)
{
    const auto* q = static_cast<const uint64_t*>(base);
    for (size_t i = 0; i < bytes / 8; i++)
    {
        const uint64_t v = q[i];
        if (v == 0) continue;
        if (FMOD_Studio_System_IsValid(reinterpret_cast<FMOD_STUDIO_SYSTEM*>(v)))
        {
            std::stringstream ss;
            ss << "[rigbuddy-sound][game-fmod] Studio system found at sound library +0x" << std::hex << i * 8;
            scs_log(SCS_LOG_TYPE_message, ss.str().c_str());
            return reinterpret_cast<FMOD_STUDIO_SYSTEM*>(v);
        }
        if (!looks_like_pointer(v) || !readable(reinterpret_cast<const void*>(v), 0x80)) continue;
        const auto* r = reinterpret_cast<const uint64_t*>(v);
        for (int j = 0; j < 16; j++)
        {
            if (r[j] != 0 && FMOD_Studio_System_IsValid(reinterpret_cast<FMOD_STUDIO_SYSTEM*>(r[j])))
            {
                std::stringstream ss;
                ss << "[rigbuddy-sound][game-fmod] Studio system found at sound library +0x" << std::hex << i * 8
                    << " -> +0x" << j * 8;
                scs_log(SCS_LOG_TYPE_message, ss.str().c_str());
                return reinterpret_cast<FMOD_STUDIO_SYSTEM*>(r[j]);
            }
        }
    }
    return nullptr;
}

static void log_game_buses(FMOD_STUDIO_SYSTEM* sys)
{
    FMOD_STUDIO_BANK* banks[32];
    int bank_count = 0;
    if (FMOD_Studio_System_GetBankList(sys, banks, 32, &bank_count) != FMOD_OK) return;
    int logged = 0;
    for (int b = 0; b < bank_count; b++)
    {
        FMOD_STUDIO_BUS* buses[256];
        int n = 0;
        if (FMOD_Studio_Bank_GetBusList(banks[b], buses, 256, &n) != FMOD_OK) continue;
        for (int i = 0; i < n && logged < 150; i++)
        {
            char path[256] = {};
            int got = 0;
            if (FMOD_Studio_Bus_GetPath(buses[i], path, sizeof(path), &got) == FMOD_OK)
            {
                scs_log(SCS_LOG_TYPE_message, (std::string("[rigbuddy-sound][game-fmod] bus ") + path).c_str());
                logged++;
            }
        }
    }
}

/** Is `rigbuddy-sound\<name>` there? Looked at every 5 seconds. */
static bool sound_flag(const char* name, bool* cache, ULONGLONG* next)
{
    const auto now = GetTickCount64();
    if (now >= *next)
    {
        *next = now + 5000;
        char dll_path[MAX_PATH] = {};
        HMODULE module = nullptr;
        GetModuleHandleExA(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
                           reinterpret_cast<LPCSTR>(&sound_flag), &module);
        GetModuleFileNameA(module, dll_path, MAX_PATH);
        std::string path(dll_path);
        path = path.substr(0, path.find_last_of("\\/") + 1) + "rigbuddy-sound\\" + name;
        *cache = GetFileAttributesA(path.c_str()) != INVALID_FILE_ATTRIBUTES;
    }
    return *cache;
}

void shutdown();

static DWORD WINAPI quit_watch(LPVOID)
{
    while (!released_for_quit)
    {
        Sleep(50);
        FMOD_STUDIO_SYSTEM* sys = game_studio;
        if (sys == nullptr || FMOD_Studio_System_IsValid(sys)) continue;
        fmod_guard guard;
        if (fmod_manager_instance != nullptr)
        {
            shutdown();
            scs_log(SCS_LOG_TYPE_message, "[rigbuddy-sound] the game released its sound: the plugin's FMOD is released too");
        }
        released_for_quit = true;
    }
    return 0;
}

static void start_quit_watch()
{
    static bool started = false;
    if (started) return;
    started = true;
    if (HANDLE t = CreateThread(nullptr, 0, quit_watch, nullptr, 0, nullptr)) CloseHandle(t);
}

// The buses alone are not enough: a truck's own sound bank (its sound mod too) can bring its own mixer, and its engine then
// plays on a bus of the same name that is not the one found above. So the game's events of the same name as the mod's
// (event:/engine/engine, ...) are also turned down themselves, every instance of them in every bank, and back up when the
// mod is switched off.
struct game_event_t
{
    std::string mod_event;
    FMOD_STUDIO_EVENTDESCRIPTION* desc;
};
static std::vector<game_event_t> game_events;
static FMOD_STUDIO_SYSTEM* game_events_of = nullptr;

static void find_game_events(FMOD_STUDIO_SYSTEM* sys, const bool log)
{
    game_events.clear();
    game_events_of = sys;
    FMOD_STUDIO_BANK* banks[128];
    int bank_count = 0;
    if (FMOD_Studio_System_GetBankList(sys, banks, 128, &bank_count) != FMOD_OK) return;
    std::vector<FMOD_STUDIO_EVENTDESCRIPTION*> list;
    for (int b = 0; b < bank_count; b++)
    {
        int n = 0;
        if (FMOD_Studio_Bank_GetEventCount(banks[b], &n) != FMOD_OK || n <= 0) continue;
        list.resize(n);
        int got = 0;
        if (FMOD_Studio_Bank_GetEventList(banks[b], list.data(), n, &got) != FMOD_OK) continue;
        for (int i = 0; i < got; i++)
        {
            char path[256] = {};
            int len = 0;
            if (FMOD_Studio_EventDescription_GetPath(list[i], path, sizeof(path), &len) != FMOD_OK) continue;
            const std::string pth(path);
            for (const auto& g : game_buses)
                if (pth == std::string("event:/") + g.mod_event)
                {
                    game_events.push_back({g.mod_event, list[i]});
                    if (log) scs_log(SCS_LOG_TYPE_message, ("[rigbuddy-sound][game-fmod] event " + pth).c_str());
                }
        }
    }
}

static void set_game_events(const char* mod_event, const float volume)
{
    FMOD_STUDIO_EVENTINSTANCE* inst[64];
    for (const auto& e : game_events)
    {
        if (e.mod_event != mod_event || !FMOD_Studio_EventDescription_IsValid(e.desc)) continue;
        int n = 0;
        if (FMOD_Studio_EventDescription_GetInstanceList(e.desc, inst, 64, &n) != FMOD_OK) continue;
        for (int i = 0; i < n; i++) FMOD_Studio_EventInstance_SetVolume(inst[i], volume);
    }
}

static void update_game_mute()
{
    static bool keep = false, debug = false;
    static ULONGLONG next_keep = 0, next_debug = 0;
    if (sound_flag("keep-game-sounds.txt", &keep, &next_keep) && game_studio == nullptr) return; // switched off
    const bool off = keep;
    const auto now = GetTickCount64();

    if (game_studio == nullptr)
    {
        if (game_scan_tries >= 5 || now < game_scan_next || sound_system_ptr == 0) return;
        game_scan_tries++;
        game_scan_next = now + 5000;
        if (!readable(reinterpret_cast<const void*>(sound_system_ptr), 8)) return;
        const auto* lib = *reinterpret_cast<const uint8_t* const*>(sound_system_ptr);
        if (lib == nullptr || !readable(lib, 0x2000)) return;
        game_studio = scan_for_studio(lib, 0x2000);
        if (game_studio == nullptr)
        {
            scs_log(SCS_LOG_TYPE_warning, "[rigbuddy-sound][game-fmod] the game's FMOD Studio system was not found: its own sounds stay on");
            return;
        }
        if (sound_flag("debug.txt", &debug, &next_debug)) log_game_buses(game_studio);
        start_quit_watch();
        for (auto& g : game_buses)
        {
            g.bus = nullptr;
            g.muted = -1;
            const auto res = FMOD_Studio_System_GetBus(game_studio, g.path, &g.bus);
            if (res != FMOD_OK) scs_log(SCS_LOG_TYPE_warning, (std::string("[rigbuddy-sound][game-fmod] no bus ") + g.path + ": " + FMOD_ErrorString(res)).c_str());
        }
    }
    if (!FMOD_Studio_System_IsValid(game_studio)) // the game's system went away: look for it again
    {
        game_studio = nullptr;
        game_scan_tries = 0;
        return;
    }
    // the events: looked up again every 10 s (a truck change loads other banks), their instances turned down twice a second
    static ULONGLONG next_find = 0, next_events = 0;
    if (now >= next_find || game_events_of != game_studio)
    {
        next_find = now + 10000;
        static bool dbg = false;
        static ULONGLONG next_dbg = 0;
        static size_t logged = static_cast<size_t>(-1);
        find_game_events(game_studio, false);
        if (game_events.size() != logged && sound_flag("debug.txt", &dbg, &next_dbg))
        {
            logged = game_events.size();
            find_game_events(game_studio, true);
        }
    }
    const bool events_now = now >= next_events;
    if (events_now) next_events = now + 500;
    for (auto& g : game_buses)
    {
        const int want = !off && fmod_manager_instance->has_mod() && fmod_manager_instance->has_event(g.mod_event) ? 1 : 0;
        if (want && events_now) set_game_events(g.mod_event, 0.0f);       // new instances (a restarted engine) too
        else if (!want && g.muted == 1) set_game_events(g.mod_event, 1.0f); // the mod went off: once, back to full
        if (g.bus == nullptr) { g.muted = want; continue; }
        if (want == g.muted)
        {
            // The game sets its own buses again (its pause, menus, sound settings, a truck change): a bus we muted that is
            // playing again is muted again, twice a second. (Upstream leaves this to the player, who turns these channels
            // down in the game's settings for good.)
            FMOD_BOOL now_muted = 0;
            if (!want || !events_now || FMOD_Studio_Bus_GetMute(g.bus, &now_muted) != FMOD_OK || now_muted) continue;
            FMOD_Studio_Bus_SetMute(g.bus, 1);
            scs_log(SCS_LOG_TYPE_message, (std::string("[rigbuddy-sound][game-fmod] ") + g.path + " was on again (the game), muted again").c_str());
            continue;
        }
        FMOD_Studio_Bus_SetMute(g.bus, want);
        g.muted = want;
        scs_log(SCS_LOG_TYPE_message, (std::string("[rigbuddy-sound][game-fmod] ") + g.path + (want ? " muted" : " back on")).c_str());
    }
}

// Rig Buddy: what the game's radio plays, for the tablet. The SDK has no radio, and only the internet streams can be seen
// from outside (their connection); the game's own stations and the music player play files. All three go through the
// game's bus:/radio, so its channels are looked at every 2 seconds: each playing sound's name (a file name, or a track in a
// sound bank) and its tags (TITLE / ARTIST, ...). The list goes to %LOCALAPPDATA%\RigBuddyStudio\game-radio.json when it
// changes (the Rig Buddy agent reads it); debug.txt also writes it to the log.
static std::string utf16_to_utf8(const wchar_t* w, const int chars)
{
    if (chars <= 0) return {};
    const int n = WideCharToMultiByte(CP_UTF8, 0, w, chars, nullptr, 0, nullptr, nullptr);
    std::string s(n, '\0');
    WideCharToMultiByte(CP_UTF8, 0, w, chars, s.data(), n, nullptr, nullptr);
    while (!s.empty() && s.back() == '\0') s.pop_back();
    return s;
}

static std::string tag_text(const FMOD_TAG& t)
{
    if (t.data == nullptr || t.datalen == 0) return {};
    switch (t.datatype)
    {
    case FMOD_TAGDATATYPE_STRING:
    case FMOD_TAGDATATYPE_STRING_UTF8:
    {
        std::string s(static_cast<const char*>(t.data), t.datalen);
        while (!s.empty() && s.back() == '\0') s.pop_back();
        if (s.size() >= 3 && static_cast<unsigned char>(s[0]) == 0xEF && static_cast<unsigned char>(s[1]) == 0xBB) s = s.substr(3);
        return s;
    }
    case FMOD_TAGDATATYPE_STRING_UTF16:
    {
        const auto* w = static_cast<const wchar_t*>(t.data);
        int chars = static_cast<int>(t.datalen / 2);
        if (chars > 0 && w[0] == 0xFEFF) { w++; chars--; }
        return utf16_to_utf8(w, chars);
    }
    default:
        return {};
    }
}

static void radio_channels(FMOD_CHANNELGROUP* group, nlohmann::json& out, const int depth)
{
    if (group == nullptr || depth > 6) return;
    int n = 0;
    if (FMOD_ChannelGroup_GetNumChannels(group, &n) == FMOD_OK)
        for (int i = 0; i < n && out.size() < 8; i++)
        {
            FMOD_CHANNEL* ch = nullptr;
            FMOD_BOOL playing = 0, paused = 0;
            if (FMOD_ChannelGroup_GetChannel(group, i, &ch) != FMOD_OK || ch == nullptr) continue;
            if (FMOD_Channel_IsPlaying(ch, &playing) != FMOD_OK || !playing) continue;
            FMOD_Channel_GetPaused(ch, &paused);
            FMOD_SOUND* snd = nullptr;
            if (FMOD_Channel_GetCurrentSound(ch, &snd) != FMOD_OK || snd == nullptr) continue;
            nlohmann::json s;
            char name[512] = {};
            if (FMOD_Sound_GetName(snd, name, sizeof(name)) == FMOD_OK) s["name"] = std::string(name);
            FMOD_SOUND* parent = nullptr; // a track inside a sound bank: the bank's name too
            if (FMOD_Sound_GetSubSoundParent(snd, &parent) == FMOD_OK && parent != nullptr)
            {
                char pname[512] = {};
                if (FMOD_Sound_GetName(parent, pname, sizeof(pname)) == FMOD_OK) s["bank"] = std::string(pname);
            }
            int tags = 0;
            if (FMOD_Sound_GetNumTags(snd, &tags, nullptr) == FMOD_OK)
                for (int k = 0; k < tags && k < 40; k++)
                {
                    FMOD_TAG t{};
                    if (FMOD_Sound_GetTag(snd, nullptr, k, &t) != FMOD_OK || t.name == nullptr) continue;
                    const std::string v = tag_text(t);
                    if (!v.empty() && v.size() < 300) s["tags"][t.name] = v;
                }
            s["paused"] = paused != 0;
            out.push_back(s);
        }
    int groups = 0;
    if (FMOD_ChannelGroup_GetNumGroups(group, &groups) == FMOD_OK)
        for (int i = 0; i < groups; i++)
        {
            FMOD_CHANNELGROUP* sub = nullptr;
            if (FMOD_ChannelGroup_GetGroup(group, i, &sub) == FMOD_OK) radio_channels(sub, out, depth + 1);
        }
}

static void update_game_radio()
{
    static ULONGLONG next = 0;
    static std::string last = "-";
    static FMOD_STUDIO_BUS* radio_bus = nullptr;
    static FMOD_STUDIO_SYSTEM* bus_of = nullptr;
    static bool debug = false;
    static ULONGLONG next_debug = 0;
    const auto now = GetTickCount64();
    if (now < next || game_studio == nullptr) return;
    next = now + 2000;
    if (bus_of != game_studio)
    {
        radio_bus = nullptr;
        bus_of = game_studio;
        if (FMOD_Studio_System_GetBus(game_studio, "bus:/radio", &radio_bus) != FMOD_OK) radio_bus = nullptr;
    }
    nlohmann::json sounds = nlohmann::json::array();
    FMOD_CHANNELGROUP* group = nullptr;
    if (radio_bus != nullptr && FMOD_Studio_Bus_GetChannelGroup(radio_bus, &group) == FMOD_OK) radio_channels(group, sounds, 0);
    const std::string text = nlohmann::json{{"sounds", sounds}}.dump();
    if (text == last) return;
    last = text;
    if (sound_flag("debug.txt", &debug, &next_debug))
        scs_log(SCS_LOG_TYPE_message, ("[rigbuddy-sound][radio] " + text).c_str());
    char base[MAX_PATH] = {};
    if (GetEnvironmentVariableA("LOCALAPPDATA", base, MAX_PATH) == 0) return;
    const std::string dir = std::string(base) + "\\RigBuddyStudio";
    CreateDirectoryA(dir.c_str(), nullptr);
    const std::string file = dir + "\\game-radio.json", tmp = file + ".tmp";
    {
        std::ofstream f(tmp, std::ios::binary | std::ios::trunc);
        if (!f) return;
        f << text;
    }
    MoveFileExA(tmp.c_str(), file.c_str(), MOVEFILE_REPLACE_EXISTING);
}

// --- measuring the mod's layers (debug.txt) ----------------------------------------------------------------------------
// A sound mod can put a sound inside another event (a nested event): the turbo whistle of a mod inside its engine or
// exhaust event, which plays on that event's bus, so the turbo level cannot reach it. FMOD has no API for a nested event,
// but every playing event has a tree of channel groups underneath, nested events included. With debug.txt in the plugin
// folder this writes that tree for the engine, exhaust and turbo events every 5 s while the engine runs: each group with
// its volume and how loud it really is (audibility), and the sounds playing in it. layer-test.txt (one word in it, a part
// of a sound's name such as 550t) turns the groups playing such a sound to 0 while the file is there, to hear whether a
// layer can be turned down like this; without the file they are back at 1.
struct layer_probe_state
{
    std::vector<FMOD_CHANNELGROUP*> muted;
    std::string word;
};
static layer_probe_state layer_probe_;

static std::string read_flag_text(const char* name)
{
    char dll_path[MAX_PATH] = {};
    HMODULE module = nullptr;
    GetModuleHandleExA(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
                       reinterpret_cast<LPCSTR>(&read_flag_text), &module);
    GetModuleFileNameA(module, dll_path, MAX_PATH);
    std::string path(dll_path);
    path = path.substr(0, path.find_last_of("\\/") + 1) + "rigbuddy-sound\\" + name;
    std::ifstream f(path);
    if (!f.is_open()) return {};
    std::string w;
    f >> w;
    return w.empty() ? std::string("?") : w; // an empty file: the probe is on, matching nothing
}

/** The sounds playing directly in a group ("name@channel volume"). */
static std::string group_sounds(FMOD_CHANNELGROUP* g, const std::string& word, bool* matches)
{
    std::string out;
    int n = 0;
    if (FMOD_ChannelGroup_GetNumChannels(g, &n) != FMOD_OK) return out;
    for (int i = 0; i < n && i < 24; i++)
    {
        FMOD_CHANNEL* ch = nullptr;
        FMOD_BOOL playing = 0;
        if (FMOD_ChannelGroup_GetChannel(g, i, &ch) != FMOD_OK || ch == nullptr) continue;
        if (FMOD_Channel_IsPlaying(ch, &playing) != FMOD_OK || !playing) continue;
        FMOD_SOUND* snd = nullptr;
        char name[256] = {};
        if (FMOD_Channel_GetCurrentSound(ch, &snd) == FMOD_OK && snd != nullptr) FMOD_Sound_GetName(snd, name, sizeof(name));
        float vol = 0, aud = 0;
        FMOD_Channel_GetVolume(ch, &vol);
        FMOD_Channel_GetAudibility(ch, &aud);
        char buf[400];
        snprintf(buf, sizeof(buf), " '%s'(v%.2f a%.3f)", name, vol, aud);
        out += buf;
        if (!word.empty() && word != "?" && std::string(name).find(word) != std::string::npos) *matches = true;
    }
    return out;
}

static void walk_layers(const char* event_name, FMOD_CHANNELGROUP* g, const std::string& path, const int depth, const bool log)
{
    if (g == nullptr || depth > 10) return;
    char name[128] = {};
    FMOD_ChannelGroup_GetName(g, name, sizeof(name));
    float vol = 0, aud = 0;
    FMOD_ChannelGroup_GetVolume(g, &vol);
    FMOD_ChannelGroup_GetAudibility(g, &aud);
    int groups = 0, channels = 0;
    FMOD_ChannelGroup_GetNumGroups(g, &groups);
    FMOD_ChannelGroup_GetNumChannels(g, &channels);
    bool matches = false;
    const auto sounds = group_sounds(g, layer_probe_.word, &matches);
    if (matches && std::find(layer_probe_.muted.begin(), layer_probe_.muted.end(), g) == layer_probe_.muted.end())
    {
        FMOD_ChannelGroup_SetVolume(g, 0.0f);
        layer_probe_.muted.push_back(g);
        scs_log(SCS_LOG_TYPE_message, (std::string("[rigbuddy-sound][layers] layer-test: turned down ") + event_name + " " + path + " (plays '" + layer_probe_.word + "')").c_str());
    }
    if (log)
    {
        char line[600];
        snprintf(line, sizeof(line), "[rigbuddy-sound][layers] %s %s name='%s' vol=%.2f aud=%.3f groups=%d channels=%d%s",
                 event_name, path.c_str(), name, vol, aud, groups, channels, matches ? " [layer-test]" : "");
        scs_log(SCS_LOG_TYPE_message, (std::string(line) + sounds).c_str());
    }
    for (int i = 0; i < groups && i < 32; i++)
    {
        FMOD_CHANNELGROUP* sub = nullptr;
        if (FMOD_ChannelGroup_GetGroup(g, i, &sub) == FMOD_OK) walk_layers(event_name, sub, path + "." + std::to_string(i), depth + 1, log);
    }
}

static void layer_probe()
{
    static bool debug = false, test = false;
    static ULONGLONG next_debug = 0, next_test = 0, next_dump = 0;
    const bool on = sound_flag("debug.txt", &debug, &next_debug);
    const bool testing = sound_flag("layer-test.txt", &test, &next_test);
    if (!testing && !layer_probe_.muted.empty())
    {
        for (auto* g : layer_probe_.muted) FMOD_ChannelGroup_SetVolume(g, 1.0f); // (a group that is gone answers invalid)
        layer_probe_.muted.clear();
        scs_log(SCS_LOG_TYPE_message, "[rigbuddy-sound][layers] layer-test.txt removed: the layers are back");
    }
    if (!on && !testing) return;
    const auto now = GetTickCount64();
    const bool dump = on && now >= next_dump && stored_engine_state > 0;
    static ULONGLONG next_word = 0;
    if (testing && now >= next_word) { next_word = now + 5000; layer_probe_.word = read_flag_text("layer-test.txt"); }
    if (!testing) layer_probe_.word.clear();
    if (!dump && !testing) return;
    if (dump) next_dump = now + 5000;
    for (const char* name : {"engine/engine", "engine/exhaust", "engine/turbo"})
    {
        auto ev = fmod_manager_instance->get_event(name);
        if (ev == nullptr) continue;
        FMOD::Studio::EventInstance* inst = nullptr;
        if (ev->get_instance(&inst) != FMOD_OK || inst == nullptr) continue;
        auto* ci = reinterpret_cast<FMOD_STUDIO_EVENTINSTANCE*>(inst);
        if (dump)
        {
            std::string params;
            for (const char* p : {"rpm", "load", "brake", "turbo", "boost", "play"})
            {
                float v = 0, fin = 0;
                const auto r = FMOD_Studio_EventInstance_GetParameterByName(ci, p, &v, &fin);
                char b[64];
                if (r == FMOD_OK) snprintf(b, sizeof(b), " %s=%.3f(%.3f)", p, v, fin); else snprintf(b, sizeof(b), " %s=-", p);
                params += b;
            }
            scs_log(SCS_LOG_TYPE_message, (std::string("[rigbuddy-sound][layers] ") + name + " parameters:" + params).c_str());
        }
        FMOD_CHANNELGROUP* root = nullptr;
        if (FMOD_Studio_EventInstance_GetChannelGroup(ci, &root) != FMOD_OK || root == nullptr)
        {
            if (dump) scs_log(SCS_LOG_TYPE_message, (std::string("[rigbuddy-sound][layers] ") + name + ": no channel group (not playing)").c_str());
            continue;
        }
        walk_layers(name, root, "g", 0, dump);
    }
}

// --- the mod's layers (Rig Buddy) -----------------------------------------------------------------------------------------
// A sound mod can put sounds inside its engine or exhaust event as a nested event (Pipe Mod: the turbo whistle and the
// blow-off inside its exhaust). They play on that event's bus, so the turbo level never reached them. Each nested event
// has its own branch in the event's channel-group tree ("Master Bus" under one of the event's tracks): it is found there,
// named by the sounds it plays, and gets a fader of our own at its head (FMOD Studio sets the group's own volume from the
// mod's automation; our fader leaves that alone).
//   gain = its own level (layer_levels.json, Rig Buddy's Sound mods window; 1 when not set)
//        x for a turbo layer (a sound named turbo, bov, whistle, wastegate, blow off, or like "550t"): turbo / the level
//          of the event's own bus, so the turbo slider sets the loudness of every turbo sound of the mod
// The layers found go to layers.json in the plugin folder (the window lists them).
struct mod_layer
{
    std::string event;              // "engine/exhaust"
    std::set<std::string> sounds;   // every sound seen playing in it
    bool turbo = false;
    FMOD_CHANNELGROUP* group = nullptr;
    FMOD_DSP* fader = nullptr;
    float gain = -1.0f;
};
static std::map<std::string, mod_layer> mod_layers; // "exhaust#2" -> layer
static std::map<std::string, double> layer_levels;  // "<mods>|<layer id>" -> level
static std::string layers_mods;
static bool layers_dirty = false;

static std::string plugin_file(const char* name)
{
    char dll_path[MAX_PATH] = {};
    HMODULE module = nullptr;
    GetModuleHandleExA(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
                       reinterpret_cast<LPCSTR>(&plugin_file), &module);
    GetModuleFileNameA(module, dll_path, MAX_PATH);
    std::string path(dll_path);
    return path.substr(0, path.find_last_of("\\/") + 1) + "rigbuddy-sound\\" + name;
}

static bool is_turbo_sound(std::string n)
{
    for (auto& c : n) c = static_cast<char>(tolower(static_cast<unsigned char>(c)));
    for (const char* w : {"turbo", "bov", "whistle", "wastegate", "blowoff", "blow off", "blow-off", "spool", "pssh"})
        if (n.find(w) != std::string::npos) return true;
    // "550t", "fkml6 1300t": a number with a t (the turbo recording at that rpm)
    while (!n.empty() && (n.back() == ' ' || n.back() == '_')) n.pop_back();
    return n.size() >= 2 && n.back() == 't' && isdigit(static_cast<unsigned char>(n[n.size() - 2]));
}

static void collect_sounds(FMOD_CHANNELGROUP* g, std::set<std::string>& out, bool* added, const int depth)
{
    if (g == nullptr || depth > 8) return;
    int n = 0;
    if (FMOD_ChannelGroup_GetNumChannels(g, &n) == FMOD_OK)
        for (int i = 0; i < n && i < 32; i++)
        {
            FMOD_CHANNEL* ch = nullptr;
            FMOD_BOOL playing = 0;
            if (FMOD_ChannelGroup_GetChannel(g, i, &ch) != FMOD_OK || ch == nullptr) continue;
            if (FMOD_Channel_IsPlaying(ch, &playing) != FMOD_OK || !playing) continue;
            FMOD_SOUND* snd = nullptr;
            char name[256] = {};
            if (FMOD_Channel_GetCurrentSound(ch, &snd) != FMOD_OK || snd == nullptr) continue;
            if (FMOD_Sound_GetName(snd, name, sizeof(name)) != FMOD_OK || name[0] == 0) continue;
            if (out.insert(name).second) *added = true;
        }
    int groups = 0;
    if (FMOD_ChannelGroup_GetNumGroups(g, &groups) == FMOD_OK)
        for (int i = 0; i < groups && i < 32; i++)
        {
            FMOD_CHANNELGROUP* sub = nullptr;
            if (FMOD_ChannelGroup_GetGroup(g, i, &sub) == FMOD_OK) collect_sounds(sub, out, added, depth + 1);
        }
}

static void detach_fader(mod_layer& l)
{
    if (l.fader == nullptr) return;
    if (l.group != nullptr) FMOD_ChannelGroup_RemoveDSP(l.group, l.fader); // (a group that is gone answers invalid)
    FMOD_DSP_DisconnectAll(l.fader, 1, 1);
    l.group = nullptr;
}

/** The mods changed or the plugin stops: the faders go (their groups go with the old events). */
static void layers_reset()
{
    for (auto& [id, l] : mod_layers)
    {
        detach_fader(l);
        if (l.fader != nullptr) FMOD_DSP_Release(l.fader);
        l.fader = nullptr;
    }
    mod_layers.clear();
    layers_dirty = true;
}

static void read_layer_levels()
{
    static std::filesystem::file_time_type seen{};
    std::error_code ec;
    const auto path = plugin_file("layer_levels.json");
    const auto t = std::filesystem::last_write_time(path, ec);
    if (ec) { if (!layer_levels.empty()) { layer_levels.clear(); for (auto& [id, l] : mod_layers) l.gain = -1; } return; }
    if (t == seen) return;
    seen = t;
    layer_levels.clear();
    try
    {
        std::ifstream f(path);
        nlohmann::json j;
        f >> j;
        if (j.is_object())
            for (auto& [k, v] : j.items())
                if (v.is_number()) layer_levels[k] = std::clamp(v.get<double>(), 0.0, 1.5);
    }
    catch (...) { scs_log(SCS_LOG_TYPE_warning, "[rigbuddy-sound][layers] layer_levels.json is not valid JSON: the layers stay at 1"); }
    for (auto& [id, l] : mod_layers) l.gain = -1; // applied again below
}

static void write_layers()
{
    nlohmann::json list = nlohmann::json::array();
    for (const auto& [id, l] : mod_layers)
        list.push_back({{"id", id}, {"event", l.event}, {"turbo", l.turbo}, {"sounds", std::vector<std::string>(l.sounds.begin(), l.sounds.end())}});
    const nlohmann::json j = {{"mods", layers_mods}, {"layers", list}};
    const auto file = plugin_file("layers.json"), tmp = file + ".tmp";
    {
        std::ofstream f(tmp, std::ios::trunc);
        if (!f.is_open()) return;
        f << j.dump(2);
    }
    MoveFileExA(tmp.c_str(), file.c_str(), MOVEFILE_REPLACE_EXISTING);
}

static void layers_update()
{
    static ULONGLONG next = 0;
    const auto now = GetTickCount64();
    if (now < next || fmod_manager_instance->config == nullptr) return;
    next = now + 500;
    std::string mods;
    for (const auto& m : fmod_manager_instance->mod_names()) mods += (mods.empty() ? "" : "+") + m;
    if (mods != layers_mods) { layers_reset(); layers_mods = mods; }
    read_layer_levels();
    const auto* cfg = fmod_manager_instance->config;
    for (const char* host : {"engine/engine", "engine/exhaust"})
    {
        auto ev = fmod_manager_instance->get_event(host);
        FMOD::Studio::EventInstance* inst = nullptr;
        if (ev == nullptr || ev->get_instance(&inst) != FMOD_OK || inst == nullptr) continue;
        FMOD_CHANNELGROUP* root = nullptr;
        if (FMOD_Studio_EventInstance_GetChannelGroup(reinterpret_cast<FMOD_STUDIO_EVENTINSTANCE*>(inst), &root) != FMOD_OK || root == nullptr) continue;
        const std::string short_name = std::string(host).substr(7); // "engine" / "exhaust"
        const double host_level = std::string(host) == "engine/engine" ? cfg->engine : cfg->exhaust;
        int tracks = 0;
        FMOD_ChannelGroup_GetNumGroups(root, &tracks);
        for (int i = 0; i < tracks && i < 32; i++)
        {
            FMOD_CHANNELGROUP* track = nullptr;
            if (FMOD_ChannelGroup_GetGroup(root, i, &track) != FMOD_OK || track == nullptr) continue;
            int subs = 0;
            FMOD_ChannelGroup_GetNumGroups(track, &subs);
            for (int k = 0; k < subs && k < 8; k++)
            {
                FMOD_CHANNELGROUP* nested = nullptr;
                char gname[64] = {};
                if (FMOD_ChannelGroup_GetGroup(track, k, &nested) != FMOD_OK || nested == nullptr) continue;
                FMOD_ChannelGroup_GetName(nested, gname, sizeof(gname));
                if (std::string(gname) != "Master Bus") continue; // a nested event's own master
                const std::string id = short_name + "#" + std::to_string(i) + (k > 0 ? "." + std::to_string(k) : "");
                const bool fresh = mod_layers.find(id) == mod_layers.end();
                auto& l = mod_layers[id];
                if (fresh) { l.event = host; layers_dirty = true; }
                bool added = false;
                collect_sounds(nested, l.sounds, &added, 0);
                if (added)
                {
                    const bool was = l.turbo;
                    l.turbo = std::any_of(l.sounds.begin(), l.sounds.end(), [](const std::string& n) { return is_turbo_sound(n); });
                    layers_dirty = true;
                    if (l.turbo != was || fresh) l.gain = -1;
                    std::string names;
                    for (const auto& n : l.sounds) names += (names.empty() ? "" : ", ") + n;
                    scs_log(SCS_LOG_TYPE_message, ("[rigbuddy-sound][layers] " + id + (l.turbo ? " (turbo)" : "") + ": " + names).c_str());
                }
                if (l.group != nested) // a new instance of the nested event: our fader onto its group
                {
                    detach_fader(l);
                    if (l.fader == nullptr)
                    {
                        FMOD_SYSTEM* core = nullptr;
                        if (FMOD_ChannelGroup_GetSystemObject(nested, &core) != FMOD_OK || FMOD_System_CreateDSPByType(core, FMOD_DSP_TYPE_FADER, &l.fader) != FMOD_OK) l.fader = nullptr;
                    }
                    if (l.fader != nullptr && FMOD_ChannelGroup_AddDSP(nested, FMOD_CHANNELCONTROL_DSP_HEAD, l.fader) == FMOD_OK) l.group = nested;
                    l.gain = -1;
                }
                double gain = 1.0;
                if (const auto it = layer_levels.find(layers_mods + "|" + id); it != layer_levels.end()) gain = it->second;
                if (l.turbo) gain *= host_level > 0.001 ? std::clamp(cfg->turbo / host_level, 0.0, 4.0) : 0.0;
                if (l.fader != nullptr && l.group != nullptr && std::fabs(gain - l.gain) > 0.001)
                {
                    const float db = gain <= 0.0001 ? -80.0f : static_cast<float>(std::clamp(20.0 * std::log10(gain), -80.0, 10.0));
                    FMOD_DSP_SetParameterFloat(l.fader, FMOD_DSP_FADER_GAIN, db);
                    l.gain = static_cast<float>(gain);
                }
            }
        }
    }
    if (layers_dirty) { layers_dirty = false; write_layers(); }
}

SCSAPI_VOID telemetry_tick(const scs_event_t event, const void* const event_info, scs_context_t context)
{
    fmod_guard guard;
    if (fmod_manager_instance == nullptr) return; // Rig Buddy: sound did not start, telemetry carries on in this DLL
    pull_telemetry();
    update_game_mute();
    update_game_radio();
    layer_probe(); // debug.txt / layer-test.txt only
    // Rig Buddy: new sound mods while the game runs. The new events start silent, so forget what was playing: a running
    // engine is started again below on this very frame, the warnings on theirs.
    if (fmod_manager_instance->check_for_changes())
    {
        layers_reset(); // the old events and their layers are gone
        stored_engine_state = 0;
        last_played = nullptr;
        is_air_pressure_warning_on = false;
        is_window_moving = false;
    }

    fmod_manager_instance->set_event_parameter("engine/engine", "rpm", telemetry_data.rpm);
    fmod_manager_instance->set_event_parameter("engine/exhaust", "rpm", telemetry_data.rpm);
    fmod_manager_instance->set_event_parameter("engine/engine", "load", telemetry_data.effective_throttle);
    // The game might use some other value, but this seems close enough
    fmod_manager_instance->set_event_parameter("engine/exhaust", "load", telemetry_data.effective_throttle);

    if (base_ctrl_ptr != NULL)
    {
        if (game_actor_offset != 0)
        {
            const auto base_ctrl_address = *reinterpret_cast<uint64_t*>(base_ctrl_ptr);
            const auto game_actor_ptr = base_ctrl_address + game_actor_offset;
            const auto* game_actor = *reinterpret_cast<game_actor_u**>(game_actor_ptr);

            if (game_actor != nullptr && readable(game_actor, 0x480))
            {
                debug_dump(game_actor, *reinterpret_cast<sound_library_t**>(sound_system_ptr));
                const auto turbo_pressure = game_actor->get_turbo_pressure();
                if (turbo_pressure >= 0 && turbo_pressure <= 1)
                {
                    fmod_manager_instance->set_event_parameter("engine/turbo", "turbo", turbo_pressure);
                }

                const auto engine_state = game_actor->get_engine_state();
                if (engine_state != stored_engine_state) // engine state changed TODO: Find start_bad
                {
                    if (engine_state > 0 && stored_engine_state == 0)
                    {
                        // engine is starting/running
                        fmod_manager_instance->set_event_parameter("engine/engine", "play", 1);
                        fmod_manager_instance->set_event_parameter("engine/exhaust", "play", 1);
                        fmod_manager_instance->set_event_state("engine/engine", true);
                        fmod_manager_instance->set_event_state("engine/exhaust", true);

                        fmod_manager_instance->set_event_parameter("engine/turbo", "play", 1);
                        fmod_manager_instance->set_event_state("engine/turbo", true);
                        fmod_manager_instance->engine_running = true;
                    }
                    else if (engine_state == 0 || engine_state == 3) // engine is no longer running
                    {
                        fmod_manager_instance->set_event_parameter("engine/engine", "play", 0);
                        fmod_manager_instance->set_event_parameter("engine/exhaust", "play", 0);
                        fmod_manager_instance->set_event_parameter("engine/turbo", "play", 0);
                        fmod_manager_instance->engine_running = false;
                    }
                    stored_engine_state = engine_state;
                }

                fmod_manager_instance->set_event_parameter("engine/engine",
                                                           "brake",
                                                           should_engine_brake_sound_play()
                                                               ? game_actor->get_engine_brake_state()
                                                               : 0.0f);

                const auto hazard_warning = game_actor->get_hazard_warning_state();
                if (!common::cmpf(hazard_warning, hazard_warning_state))
                {
                    fmod_manager_instance->set_event_state("interior/stick_hazard_warning", true);
                    hazard_warning_state = hazard_warning;
                }

                const auto light_horn = game_actor->get_light_horn_state();
                if (!common::cmpf(light_horn, light_horn_state))
                {
                    fmod_manager_instance->set_event_state("interior/stick_light_horn", true);
                    light_horn_state = light_horn;
                }

                const auto stick_lights = game_actor->get_light_switch_state();
                if (!common::cmpf(stick_lights, light_stick_state))
                {
                    fmod_manager_instance->set_event_state("interior/stick_lights", true);
                    light_stick_state = stick_lights;
                }

                const auto wipers_stick = game_actor->get_wipers_state();
                if (!common::cmpf(wipers_stick, wipers_stick_state))
                {
                    fmod_manager_instance->set_event_state("interior/stick_wipers", true);
                    wipers_stick_state = wipers_stick;
                }

                if ((game_actor->is_left_window_moving != 0 || game_actor->is_right_window_moving != 0) &&
                    (game_actor->left_window_moving_direction > 2 || game_actor->right_window_moving_direction > 2))
                {
                    is_window_moving = true;
                    fmod_manager_instance->set_global_parameter("window_stop", 0.f);
                    fmod_manager_instance->set_event_state("interior/window_move", true, true);
                }
                else if (is_window_moving)
                {
                    is_window_moving = false;
                    fmod_manager_instance->set_global_parameter("window_stop", 1.0f);
                    fmod_manager_instance->set_event_state("interior/window_move", false);
                }
                if ((game_actor->left_window_state == 1.f && game_actor->left_window_btn_state == 1.f) ||
                    (game_actor->left_window_state == 0.f && game_actor->left_window_btn_state == 0.f))
                {
                    if (!is_left_window_button_active)
                    {
                        fmod_manager_instance->set_event_state("interior/window_click", true, false);
                    }
                    is_left_window_button_active = true;
                }
                else if (is_left_window_button_active)
                {
                    is_left_window_button_active = false;
                }
                if ((game_actor->right_window_state == 1.f && game_actor->right_window_btn_state == 1.f) ||
                    (game_actor->right_window_state == 0.f && game_actor->right_window_btn_state == 0.f))
                {
                    if (!is_right_window_button_active)
                    {
                        fmod_manager_instance->set_event_state("interior/window_click", true, false);
                    }
                    is_right_window_button_active = true;
                }
                else if (is_right_window_button_active)
                {
                    is_right_window_button_active = false;
                }
            }
        }

        const auto* interior = *reinterpret_cast<sound_library_t**>(sound_system_ptr);

        if (interior != nullptr && readable(interior, 0x1CC8))
        {
            const auto window_pos = interior->get_window_state();
            if (window_pos.x >= 0 && window_pos.x <= 1)
                fmod_manager_instance->set_global_parameter(
                    "wnd_left",
                    window_pos.x);
            if (window_pos.y >= 0 && window_pos.y <= 1)
                fmod_manager_instance->set_global_parameter(
                    "wnd_right",
                    window_pos.y);

            if (common::cmpf(window_pos.x, 0) && common::cmpf(window_pos.y, 0) && interior->
                get_is_camera_inside())
            {
                fmod_manager_instance->set_bus_volume("outside", fmod_manager_instance->config->windows_closed);
                fmod_manager_instance->set_bus_volume("exterior", fmod_manager_instance->config->windows_closed);
                // backward compatibility
            }
            else
            {
                fmod_manager_instance->set_bus_volume("outside", 1);
                fmod_manager_instance->set_bus_volume("exterior", 1); // backward compatibility
            }

            if (interior->get_is_on_interior_cam())
            {
                fmod_manager_instance->set_bus_volume("cabin/interior", fmod_manager_instance->config->interior);
            }
            else
            {
                fmod_manager_instance->set_bus_volume("cabin/interior", 0);
            }

            fmod_manager_instance->set_global_parameter("cabin_out", interior->get_cabin_out());
            fmod_manager_instance->set_global_parameter("cabin_rot",
                                                        interior->get_camera_rotation_in_cabin());
            fmod_manager_instance->set_global_parameter("surr_type", interior->get_has_echo());

            const auto now_playing_navigation_sound = interior->get_now_playing_navigation_sound();
            // (readable: a wrong offset would otherwise hand a wild pointer to get_event_name())
            if (now_playing_navigation_sound != nullptr && last_played != now_playing_navigation_sound &&
                readable(now_playing_navigation_sound, sizeof(voice_navigation_sound_t)) &&
                readable(now_playing_navigation_sound->get_event_name(), 1))
            {
                fmod_manager_instance->set_event_state(now_playing_navigation_sound->get_event_name(), true, true);
            }
            last_played = now_playing_navigation_sound;
        }
    }

#pragma region interior
    if (telemetry_data.brake_air_pressure_warning && telemetry_data.engine_enabled)
    {
        is_air_pressure_warning_on = true;
        fmod_manager_instance->set_event_state("interior/air_warning", true, true);
    }
    else if (is_air_pressure_warning_on)
    {
        is_air_pressure_warning_on = false;
        fmod_manager_instance->set_event_state("interior/air_warning", false);
    }

    if ((telemetry_data.light_lblinker || telemetry_data.light_rblinker) && !was_indicator_light_on)
    {
        fmod_manager_instance->set_event_state("interior/blinker_on", true);
        was_indicator_light_on = true;
    }
    else if (!telemetry_data.light_lblinker && !telemetry_data.light_rblinker && was_indicator_light_on)
    {
        fmod_manager_instance->set_event_state("interior/blinker_off", true);
        was_indicator_light_on = false;
    }

    if (telemetry_data.park_brake_on != was_park_brake_on)
    {
        if (was_park_brake_on) fmod_manager_instance->set_event_state("interior/stick_park_brake_off", true);
        else fmod_manager_instance->set_event_state("interior/stick_park_brake", true);
        was_park_brake_on = telemetry_data.park_brake_on;
    }

    const byte current_blinker_stick = telemetry_data.lblinker ? 1 : telemetry_data.rblinker ? 2 : 0;
    // 1 if lblinker, 2 if rblinker, 0 if off
    if (current_blinker_stick != indicator_stick_state && current_blinker_stick != 0)
    {
        fmod_manager_instance->set_event_state("interior/stick_blinker", true);
        indicator_stick_state = current_blinker_stick;
    }
    else if (current_blinker_stick == 0 && indicator_stick_state != 0)
    {
        fmod_manager_instance->set_event_state("interior/stick_blinker_off", true);
        indicator_stick_state = 0;
    }

    if (telemetry_data.retarder_level != prev_retarder_level)
    {
        fmod_manager_instance->set_event_state("interior/stick_retarder", true);
        prev_retarder_level = telemetry_data.retarder_level;
    }

    if (telemetry_data.high_beam != high_beams_enabled)
    {
        fmod_manager_instance->set_event_state("interior/stick_high_beam", true);
        high_beams_enabled = telemetry_data.high_beam;
    }
#pragma endregion
    layers_update(); // the mod's nested layers: found, named, their levels (after the events' parameters of this frame)
    fmod_manager_instance->update();
}


// https://stackoverflow.com/questions/940707/how-do-i-programmatically-get-the-version-of-a-dll-or-exe-file
DWORD get_product_version()
{
    DWORD ver_handle = 0;
    UINT size = 0;
    LPBYTE lp_buffer = nullptr;
    char module_file_name[MAX_PATH];
    module_file_name[0] = '\0';
    GetModuleFileNameA(nullptr, module_file_name, sizeof(module_file_name));
    const DWORD ver_size = GetFileVersionInfoSizeA(module_file_name, &ver_handle);

    DWORD version = 0;

    if (ver_size != NULL)
    {
        const auto ver_data = new char[ver_size];

        if (GetFileVersionInfoA(module_file_name, ver_handle, ver_size, ver_data))
        {
            if (VerQueryValueA(ver_data, "\\", reinterpret_cast<void* *>(&lp_buffer), &size))
            {
                if (size)
                {
                    if (const auto ver_info = reinterpret_cast<VS_FIXEDFILEINFO*>(lp_buffer); ver_info->dwSignature ==
                        0xfeef04bd)
                    {
                        version = (ver_info->dwFileVersionMS >> 0) & 0xffff;
                    }
                }
            }
        }
        delete[] ver_data;
    }
    return version;
}

void register_telem_channels()
{
    register_channel(TRUCK_CHANNEL_engine_rpm, SCS_U32_NIL, float, rpm)
    register_channel(TRUCK_CHANNEL_effective_throttle, SCS_U32_NIL, float, effective_throttle)
    register_channel(TRUCK_CHANNEL_effective_clutch, SCS_U32_NIL, float, effective_clutch)
    register_channel(TRUCK_CHANNEL_motor_brake, SCS_U32_NIL, bool, engine_brake)
    register_channel(TRUCK_CHANNEL_brake_air_pressure_warning, SCS_U32_NIL, bool, brake_air_pressure_warning)
    register_channel(TRUCK_CHANNEL_engine_enabled, SCS_U32_NIL, bool, engine_enabled)
    register_channel(TRUCK_CHANNEL_lblinker, SCS_U32_NIL, bool, lblinker)
    register_channel(TRUCK_CHANNEL_rblinker, SCS_U32_NIL, bool, rblinker)
    register_channel(TRUCK_CHANNEL_light_lblinker, SCS_U32_NIL, bool, light_lblinker)
    register_channel(TRUCK_CHANNEL_light_rblinker, SCS_U32_NIL, bool, light_rblinker)
    register_channel(TRUCK_CHANNEL_head_offset, SCS_U32_NIL, fplacement, head_offset)
    register_channel(TRUCK_CHANNEL_parking_brake, SCS_U32_NIL, bool, park_brake_on)
    register_channel(TRUCK_CHANNEL_retarder_level, SCS_U32_NIL, u32, retarder_level)
    register_channel(TRUCK_CHANNEL_light_high_beam, SCS_U32_NIL, bool, high_beam)
    register_channel(TRUCK_CHANNEL_wipers, SCS_U32_NIL, bool, wipers)
    register_channel(TRUCK_CHANNEL_engine_gear, SCS_U32_NIL, s32, gear)
}

SCSAPI_RESULT rigbuddy_sound_init(const scs_u32_t version, const scs_telemetry_init_params_t* const params)
{
    const auto* const version_params = reinterpret_cast<const scs_telemetry_init_params_v101_t*>(params);

    scs_log = version_params->common.log;
    const auto game_version = get_product_version();

    if (game_version != common::supported_game_version)
    {
        std::stringstream ss;
        ss << "[rigbuddy-sound V" << common::plugin_version << "] Detected game version 1." << game_version
            << " while plugin is made for version 1." << common::supported_game_version <<
            ". The plugin will not load to prevent crashes.";
        scs_log(SCS_LOG_TYPE_error, ss.str().c_str());
        return SCS_RESULT_generic_error;
    }
    std::stringstream ss;
    ss << "[rigbuddy-sound V" << common::plugin_version <<
        "] Searching for memory offsets... If this is one of the last messages in the log after a crash, try disabling this plugin.";
    scs_log(SCS_LOG_TYPE_message, ss.str().c_str());

    const auto base_ctrl_ptr_offset = pattern::scan("48 8b ? ? ? ? ? 48 8b ? 48 8b ? 48 8b 80", game_base, image_size);
    if (base_ctrl_ptr_offset == NULL)
    {
        version_params->common.log(SCS_LOG_TYPE_error, "[rigbuddy-sound] Unable to find base_ctrl pointer offset");
        return SCS_RESULT_generic_error;
    }
    base_ctrl_ptr = base_ctrl_ptr_offset + *reinterpret_cast<int32_t*>(base_ctrl_ptr_offset + 3) + 7;
    game_actor_offset = *reinterpret_cast<uint32_t*>(base_ctrl_ptr_offset + 16);

    const auto sound_system_ptr_offset = pattern::scan("44 38 3b 0f 84 ? ? ? ? 8b 05 ? ? ? ? 48 8b 3d ? ? ? ? 85 c0 74", game_base, image_size);
    if (sound_system_ptr_offset == NULL)
    {
        version_params->common.log(SCS_LOG_TYPE_error, "[rigbuddy-sound] Unable to find sound_system pointer offset");
        return SCS_RESULT_generic_error;
    }
    sound_system_ptr = sound_system_ptr_offset + *reinterpret_cast<int32_t*>(sound_system_ptr_offset + 18) + 22;

    ss.str("");
    ss << "[rigbuddy-sound] Found base_ctrl @ " << std::hex << (base_ctrl_ptr - game_base) << " and sound_system @ " << (sound_system_ptr - game_base);
    scs_log(SCS_LOG_TYPE_message, ss.str().c_str());

    const auto events_registered =
        (version_params->register_for_event(SCS_TELEMETRY_EVENT_frame_end, telemetry_tick, nullptr) == SCS_RESULT_ok);

    if (!events_registered)
    {
        version_params->common.log(SCS_LOG_TYPE_error, "[rigbuddy-sound] Unable to register event callbacks");
        return SCS_RESULT_generic_error;
    }
    register_for_channel = version_params->register_for_channel;

    fmod_manager_instance = new fmod_manager(scs_log);

    if (!fmod_manager_instance->init())
    {
        scs_log(SCS_LOG_TYPE_error, "[rigbuddy-sound] Could not init fmod");
        delete fmod_manager_instance; // Rig Buddy: the callbacks above stay registered (the DLL stays loaded for the telemetry): they do nothing without it
        fmod_manager_instance = nullptr;
        return SCS_RESULT_generic_error;
    }

    // Rig Buddy: no register_telem_channels() any more, see pull_telemetry()

    scs_log(0, "[rigbuddy-sound] Plugin loaded");

    return SCS_RESULT_ok;
}

void shutdown()
{
    if (!released_for_quit && game_studio != nullptr && FMOD_Studio_System_IsValid(game_studio))
    {
        for (auto& g : game_buses)
        {
            if (g.bus != nullptr && g.muted == 1) FMOD_Studio_Bus_SetMute(g.bus, 0); // the game's own sounds come back with the plugin
            if (g.muted == 1 && FMOD_Studio_System_IsValid(game_studio)) set_game_events(g.mod_event, 1.0f);
        }
    }
    if (fmod_manager_instance != nullptr)
    {
        layers_reset();
        delete fmod_manager_instance;
        fmod_manager_instance = nullptr;
    }
}

SCSAPI_VOID rigbuddy_sound_shutdown(void)
{
    fmod_guard guard;
    shutdown();
}

// Rig Buddy: the DLL's one DllMain lives in telemetry-plugin/rigbuddy_plugin.cpp (telemetry and sound in one DLL).
extern "C" void rigbuddy_sound_attach()
{
    InitializeCriticalSection(&fmod_lock);
    fmod_lock_ready = true;
    game_base = reinterpret_cast<uintptr_t>(GetModuleHandleA(nullptr));
    const auto* header = reinterpret_cast<const IMAGE_DOS_HEADER*>(game_base);
    const auto* nt_header = reinterpret_cast<const IMAGE_NT_HEADERS64*>(reinterpret_cast<const uint8_t*>(
        header) + header->e_lfanew);
    image_size = nt_header->OptionalHeader.SizeOfImage;
}

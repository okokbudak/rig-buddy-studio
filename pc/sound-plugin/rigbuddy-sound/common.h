#pragma once

namespace common
{
    // Rig Buddy sound plugin, based on ts-fmod-plugin 1.62 by Dario Wouters (MIT); "rbN" = Rig Buddy's own changes
    // (1.62: the truck's control fields, from the flags on, sit 8 bytes higher; see memory_structure.h)
    inline const char* plugin_version = "1.62-rb4";
    constexpr uint32_t supported_game_version = 62;

    // The GUIDs of the .bank.guids files, "{6698e52e-c475-4ecb-9f28-b46d4e6962be}". Read into plain unsigned ints: the
    // "%hhx" length modifier of the original is not supported by the C runtime a MinGW build uses (it wrote 4 bytes per
    // field and trashed the stack: the game crashed when the first event was loaded).
    inline FMOD_GUID get_guid(const std::string& s_guid)
    {
        FMOD_GUID guid{};
        unsigned d1 = 0, d2 = 0, d3 = 0, d4[8] = {};
        const int n = sscanf(s_guid.c_str(),
                             "{%8x-%4x-%4x-%2x%2x-%2x%2x%2x%2x%2x%2x}",
                             &d1, &d2, &d3, &d4[0], &d4[1], &d4[2], &d4[3], &d4[4], &d4[5], &d4[6], &d4[7]);
        if (n != 11) return guid;
        guid.Data1 = d1;
        guid.Data2 = static_cast<unsigned short>(d2);
        guid.Data3 = static_cast<unsigned short>(d3);
        for (int i = 0; i < 8; ++i) guid.Data4[i] = static_cast<unsigned char>(d4[i]);
        return guid;
    }

    inline bool cmpf(const float a, const float b, const float epsilon = 0.005f)
    {
        return (fabs(a - b) < epsilon);
    }
}

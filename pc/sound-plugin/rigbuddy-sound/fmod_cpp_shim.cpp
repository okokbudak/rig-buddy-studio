// The FMOD C++ classes (FMOD::Studio::System, ...) are exported from the DLL under Visual C++ names, which a MinGW build
// cannot link to. FMOD documents that its C and C++ handles are the same object, so the 21 methods this plugin uses are
// defined here as thin calls of the C API (exported under plain names). Not used by the Visual Studio build.
#ifdef __MINGW32__
#include "fmod/fmod_studio.hpp"

namespace FMOD
{
namespace Studio
{
FMOD_RESULT Bank::unload() { return FMOD_Studio_Bank_Unload(reinterpret_cast<FMOD_STUDIO_BANK*>(this)); }
FMOD_RESULT Bus::setPaused(bool paused) { return FMOD_Studio_Bus_SetPaused(reinterpret_cast<FMOD_STUDIO_BUS*>(this), paused); }
FMOD_RESULT Bus::setVolume(float volume) { return FMOD_Studio_Bus_SetVolume(reinterpret_cast<FMOD_STUDIO_BUS*>(this), volume); }
FMOD_RESULT EventDescription::createInstance(EventInstance** instance) const
{
    return FMOD_Studio_EventDescription_CreateInstance(reinterpret_cast<FMOD_STUDIO_EVENTDESCRIPTION*>(const_cast<EventDescription*>(this)), reinterpret_cast<FMOD_STUDIO_EVENTINSTANCE**>(instance));
}
FMOD_RESULT EventInstance::getPlaybackState(FMOD_STUDIO_PLAYBACK_STATE* state) const
{
    return FMOD_Studio_EventInstance_GetPlaybackState(reinterpret_cast<FMOD_STUDIO_EVENTINSTANCE*>(const_cast<EventInstance*>(this)), state);
}
FMOD_RESULT EventInstance::release() { return FMOD_Studio_EventInstance_Release(reinterpret_cast<FMOD_STUDIO_EVENTINSTANCE*>(this)); }
FMOD_RESULT EventInstance::setParameterByName(const char* name, float value, bool ignoreSeekSpeed)
{
    return FMOD_Studio_EventInstance_SetParameterByName(reinterpret_cast<FMOD_STUDIO_EVENTINSTANCE*>(this), name, value, ignoreSeekSpeed);
}
FMOD_RESULT EventInstance::setVolume(float volume) { return FMOD_Studio_EventInstance_SetVolume(reinterpret_cast<FMOD_STUDIO_EVENTINSTANCE*>(this), volume); }
FMOD_RESULT EventInstance::start() { return FMOD_Studio_EventInstance_Start(reinterpret_cast<FMOD_STUDIO_EVENTINSTANCE*>(this)); }
FMOD_RESULT EventInstance::stop(FMOD_STUDIO_STOP_MODE mode) { return FMOD_Studio_EventInstance_Stop(reinterpret_cast<FMOD_STUDIO_EVENTINSTANCE*>(this), mode); }

FMOD_RESULT System::create(System** system, unsigned int headerVersion)
{
    return FMOD_Studio_System_Create(reinterpret_cast<FMOD_STUDIO_SYSTEM**>(system), headerVersion);
}
FMOD_RESULT System::getBusByID(const FMOD_GUID* id, Bus** bus) const
{
    return FMOD_Studio_System_GetBusByID(reinterpret_cast<FMOD_STUDIO_SYSTEM*>(const_cast<System*>(this)), id, reinterpret_cast<FMOD_STUDIO_BUS**>(bus));
}
FMOD_RESULT System::getCoreSystem(FMOD::System** core) const
{
    return FMOD_Studio_System_GetCoreSystem(reinterpret_cast<FMOD_STUDIO_SYSTEM*>(const_cast<System*>(this)), reinterpret_cast<FMOD_SYSTEM**>(core));
}
FMOD_RESULT System::getEventByID(const FMOD_GUID* id, EventDescription** event) const
{
    return FMOD_Studio_System_GetEventByID(reinterpret_cast<FMOD_STUDIO_SYSTEM*>(const_cast<System*>(this)), id, reinterpret_cast<FMOD_STUDIO_EVENTDESCRIPTION**>(event));
}
FMOD_RESULT System::initialize(int maxChannels, FMOD_STUDIO_INITFLAGS studioFlags, FMOD_INITFLAGS flags, void* extraDriverData)
{
    return FMOD_Studio_System_Initialize(reinterpret_cast<FMOD_STUDIO_SYSTEM*>(this), maxChannels, studioFlags, flags, extraDriverData);
}
FMOD_RESULT System::loadBankFile(const char* filename, FMOD_STUDIO_LOAD_BANK_FLAGS flags, Bank** bank)
{
    return FMOD_Studio_System_LoadBankFile(reinterpret_cast<FMOD_STUDIO_SYSTEM*>(this), filename, flags, reinterpret_cast<FMOD_STUDIO_BANK**>(bank));
}
FMOD_RESULT System::release() { return FMOD_Studio_System_Release(reinterpret_cast<FMOD_STUDIO_SYSTEM*>(this)); }
FMOD_RESULT System::setParameterByName(const char* name, float value, bool ignoreSeekSpeed)
{
    return FMOD_Studio_System_SetParameterByName(reinterpret_cast<FMOD_STUDIO_SYSTEM*>(this), name, value, ignoreSeekSpeed);
}
FMOD_RESULT System::update() { return FMOD_Studio_System_Update(reinterpret_cast<FMOD_STUDIO_SYSTEM*>(this)); }
} // namespace Studio

FMOD_RESULT System::getDriverInfo(int id, char* name, int nameLen, FMOD_GUID* guid, int* systemRate, FMOD_SPEAKERMODE* speakerMode, int* speakerModeChannels)
{
    return FMOD_System_GetDriverInfo(reinterpret_cast<FMOD_SYSTEM*>(this), id, name, nameLen, guid, systemRate, speakerMode, speakerModeChannels);
}
FMOD_RESULT System::getNumDrivers(int* numDrivers) { return FMOD_System_GetNumDrivers(reinterpret_cast<FMOD_SYSTEM*>(this), numDrivers); }
} // namespace FMOD
#endif

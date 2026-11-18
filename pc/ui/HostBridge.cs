namespace RigBuddy.Ui;

/** What the window needs from the tray host (pc/host implements it); the library never references the host. */
public interface IHostBridge
{
    string Version { get; }
    /** Two-letter UI language code ("tr", "en", ...). */
    string Language { get; }
    /** The agent (pc/agent): the game's live values for the window (LiveClient). */
    int AgentPort { get; }
    HostSnapshot Snapshot();
    void RestartService(string name);
    void BuildMap(bool force);
    void ShowLogs();
    void CopyAddress();
    void Exit();

    /** A text of the host's own (seven languages), for labels the window shares with the tray menu. */
    string Text(string key, params object?[] args);
    IReadOnlyList<(string Code, string Name)> Languages { get; }
    void SetGame(string game);
    void PickMods();
    void ClearMods();
    void SetLanguage(string code);
    string ThemeSetting { get; }
    IReadOnlyList<(string Code, string Name)> Themes { get; }
    void SetTheme(string code);
    void SetAutostart(bool on);
    void SetUpdateCheck(bool on);
    void ForgetDevices();
    void OpenSound();
    void ShowUpdate();
    /** The "Check now" button: asks GitHub at once; Kind is "newer", "current" or "error". */
    Task<UpdateResult> CheckUpdatesNow();

    // ---- wheel buttons that switch the screens ("next", "prev") of the tablets ----
    /** "tablet.next", "tablet.prev". */
    IReadOnlyList<string> WheelActions { get; }
    /** "MOZA R12 · button 14", or null when nothing is bound. */
    string? WheelLabel(string action);
    /** Waits up to 10 s for a button press on any controller and binds it; the new label, or null when nothing was pressed. */
    Task<string?> WheelLearn(string action);
    void WheelClear(string action);

    // ---- sound mods (the plugin's mod list and levels in each game) ----
    IReadOnlyList<string> SoundGames();
    bool SoundEnabled { get; }
    /** Switches the sound plugin on or off; returns the text to show. */
    string SoundSetEnabled(bool on);
    SoundState SoundLoad(int game);
    void SoundSetLevel(int game, int index, double value);
    /** A layer of the sound mod (a sound the mod put inside its engine or exhaust event): its own level, 0 to 1.5. */
    void SoundSetLayer(int game, string id, double value);
    SoundResult SoundAdd(int game, IReadOnlyList<string> files);
    string SoundRemove(int game, string name);
    string SoundSave(int game, IReadOnlyList<string> names);
    void SoundOpenFolder(int game);
    void SoundOpenLevels(int game);
    /** The button of the attention banner (install the plugin, make the network private). */
    void FixAttention();
}

public sealed record UpdateResult(string Kind, string? Version);

/** One .bank file of a game's plugin folder; On = it is in the list the plugin loads. */
public sealed record SoundMod(string Name, bool On, bool HasGuids);
/** Mods in load order (selected ones first), the levels (0 to 1.5, in SoundLevelCount order) and a warning text or "". */
public sealed record SoundState(IReadOnlyList<SoundMod> Mods, IReadOnlyList<double> Levels, string Warning, IReadOnlyList<SoundLayer>? Layers = null);
/**
 * A layer the game plugin found in the sound mods in use (layers.json): Event "engine/exhaust", the sounds it plays,
 * Turbo when they are turbo sounds (the turbo level applies to it too), its own Level (layer_levels.json, 1 when not set).
 */
public sealed record SoundLayer(string Id, string Event, IReadOnlyList<string> Sounds, bool Turbo, double Level);
public sealed record SoundResult(string Message, IReadOnlyList<string> Added);

public enum Health { Idle, Busy, Ok, Bad }

public sealed record ServiceRow(string Name, string Title, Health Health, string State);

/** Preferences the Settings page shows. */
public sealed record Prefs(string Game, int ModCount, bool MapBuilding, bool Autostart, bool UpdateCheck, string Language, string? UpdateVersion);

/** Something that needs the user (plugin missing, network is "Public", restart the game): shown as a banner. */
public sealed record Attention(string Text, Health Health, string? Button, bool Busy);

public sealed record HostSnapshot(
    string Headline, Health HeadlineHealth, string Detail, int Percent,
    string Address, string Pairing, int Clients, bool GameConnected,
    IReadOnlyList<ServiceRow> Services,
    string MapText, int MapPercent, bool MapBusy, bool MapCanBuild, bool MapCanUpdate,
    Prefs Prefs, Attention? Attention, string? AppUrl = null);

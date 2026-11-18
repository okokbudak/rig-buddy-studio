// Which game's map Rig Buddy builds, serves and loads (data\settings.json,
// Game: "ets2" or "ats"). Unset = both installed games, like before the choice existed.

static class GameChoice
{
    public static string? Setting
    {
        get
        {
            string? g = Settings.Get("Game");
            return g is "ets2" or "ats" ? g : null;
        }
        set
        {
            Settings.Set("Game", value is "ets2" or "ats" ? value : null);
        }
    }

    /** Name the map data files carry: europe-navigation.zip, usa-... */
    public static string MapName(string game) => game == "ats" ? "usa" : "europe";

    /** Is this game's map wanted? (everything is, until a game has been chosen) */
    public static bool Wants(string game) => Setting is not { } chosen || chosen == game;
}

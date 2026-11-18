// Rig Buddy's game plugin (rigbuddy-telemetry.dll) lives in each game's bin\win_x64\plugins folder. A game
// verify/update can delete it, and without it the game sends Rig Buddy nothing ("No data from the game"). This finds the
// games where it is missing or out of date and puts it back; the installer's own copy stays the source.
//
// It is one DLL with both parts: the telemetry (scs-sdk-plugin plus the bus DLC job, writing its own shared memory,
// Local\RigBuddyTelemetry) and the sound plugin (pc\telemetry-plugin, pc\sound-plugin). The game's scs-telemetry.dll that
// other tools use is never touched, except Rig Buddy's own build of it from older versions, which is taken out (and the
// separate rigbuddy-sound.dll). Replacing only works while the game is not running.

using System.Diagnostics;

static class PluginCheck
{
    public sealed record Problem(string Game, string GameDir);

    const string DllName = "rigbuddy-telemetry.dll";

    static string Shipped => Path.Combine(AppContext.BaseDirectory, @"plugin\" + DllName);

    static string Plugins(string gameDir) => Path.Combine(gameDir, @"bin\win_x64\plugins");

    static string Target(string gameDir) => Path.Combine(Plugins(gameDir), DllName);

    /** Rig Buddy's own scs-telemetry.dll of versions before the rename (scs-sdk-plugin + the bus job): recognised by its log lines. */
    const string OwnOldMarker = "[rigbuddy-telemetry]";

    static IEnumerable<string> OldFiles(string gameDir)
    {
        string old = Path.Combine(Plugins(gameDir), "scs-telemetry.dll");
        if (IsOwnOld(old)) yield return old;
        string sound = Path.Combine(Plugins(gameDir), "rigbuddy-sound.dll");
        if (File.Exists(sound)) yield return sound;
    }

    static bool IsOwnOld(string path)
    {
        try { return File.Exists(path) && File.ReadAllBytes(path).AsSpan().IndexOf(System.Text.Encoding.ASCII.GetBytes(OwnOldMarker)) >= 0; }
        catch (Exception) { return false; }
    }

    /** Whether this game needs the shipped plugin: it has none, or another build of it. */
    static bool NeedsPlugin(string gameDir) =>
        File.Exists(Shipped) && !SameFile(Shipped, Target(gameDir));

    static IEnumerable<(string Name, string Dir)> Games()
    {
        var (ets2, ats) = MapBuilder.FindGames();
        foreach (var (name, dir) in new[] { ("ETS2", ets2), ("ATS", ats) })
            if (dir != null) yield return (name, dir);
    }

    /** Installed games without the telemetry plugin, or with another build of it than the shipped one. */
    public static List<Problem> Find() =>
        Games()
            .Where(g => NeedsPlugin(g.Dir) || OldFiles(g.Dir).Any())
            .Select(g => new Problem(g.Name, g.Dir))
            .ToList();

    public static bool GameRunning() =>
        Process.GetProcessesByName("eurotrucks2").Length > 0 || Process.GetProcessesByName("amtrucks").Length > 0;

    /**
     * Puts the shipped plugin into every game that needs it and removes Rig Buddy's older plugin files;
     * returns how many games could not be done (no rights, the game running).
     */
    public static int CopyMissing()
    {
        if (!File.Exists(Shipped)) return 1;
        int failed = 0;
        foreach (var g in Games())
        {
            try
            {
                string target = Target(g.Dir);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                if (NeedsPlugin(g.Dir)) File.Copy(Shipped, target, overwrite: true);
                foreach (string old in OldFiles(g.Dir)) File.Delete(old);
                WriteLiveDir(Path.GetDirectoryName(target)!);
            }
            catch (Exception) { failed++; }
        }
        return failed;
    }

    /** Tells the plugin where to write the bus job: data\live in the install folder (not the user's profile). */
    public static void WriteLiveDir(string pluginsDir)
    {
        string live = Path.Combine(AppPaths.Current.Data, "live");
        Directory.CreateDirectory(live);
        File.WriteAllText(Path.Combine(pluginsDir, "rigbuddy-live.txt"), live, new System.Text.UTF8Encoding(false));
    }

    /**
     * `RigBuddy.exe --plugin-status` (the installer checks with it what it did): one line per game,
     * "<game>|ok|<dir>|<notes>" or "<game>|problem|<dir>|<what>", "none" when no game was found; returns the number of
     * games with a problem. Checked: the shipped rigbuddy-telemetry.dll is in the game, none of Rig Buddy's older plugin
     * files are left, and the sound part's folder has its master.bank and lists; a ts-fmod-plugin installed by hand is noted
     * (every sound would play twice).
     */
    public static int Status(TextWriter o)
    {
        int problems = 0;
        var games = SoundMods.Games();
        if (games.Count == 0) { o.WriteLine("none"); return 0; }
        foreach (var g in games)
        {
            var what = new List<string>();
            var notes = new List<string>();
            if (!File.Exists(Shipped)) what.Add("this install has no plugin\\" + DllName);
            else if (!File.Exists(Target(g.Dir))) what.Add(DllName + " missing");
            else if (!SameFile(Shipped, Target(g.Dir))) what.Add(DllName + " is another version");
            if (OldFiles(g.Dir).Any()) what.Add("old plugin files left");
            if (SoundMods.Shipped)
            {
                string dir = SoundMods.FolderOf(g);
                foreach (string f in new[] { "master.bank", "selected.bank.txt", "sound_levels.txt" })
                    if (!File.Exists(Path.Combine(dir, f))) what.Add($"rigbuddy-sound\\{f} missing");
            }
            if (SoundMods.ForeignPlugin(g)) notes.Add("ts-fmod-plugin.dll is installed too (sounds twice)");
            if (what.Count > 0) problems++;
            o.WriteLine($"{g.Master}|{(what.Count > 0 ? "problem" : "ok")}|{g.Dir}|{string.Join("; ", what.Concat(notes))}");
        }
        return problems;
    }

    static bool SameFile(string a, string b)
    {
        if (!File.Exists(b)) return false;
        if (new FileInfo(a).Length != new FileInfo(b).Length) return false;
        return File.ReadAllBytes(a).AsSpan().SequenceEqual(File.ReadAllBytes(b));
    }

    /**
     * The Fix now button: tries with the user's own rights (a Steam library on another drive
     * needs none), then once more elevated for games under Program Files.
     */
    public static bool Fix()
    {
        if (CopyMissing() == 0 && Find().Count == 0) return true;
        if (Find().Count == 0) return true;
        try
        {
            using var p = Process.Start(new ProcessStartInfo(Environment.ProcessPath!, "--fix-plugin")
            {
                UseShellExecute = true,
                Verb = "runas",
            });
            p?.WaitForExit();
        }
        catch (Exception) { } // the user declined the prompt
        return Find().Count == 0;
    }
}

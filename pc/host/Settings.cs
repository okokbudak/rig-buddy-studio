// The app's settings (language, theme, chosen game, switches): one JSON file, data\settings.json in the install
// folder, so everything the app keeps is in one place the user can see (nothing in the registry or the user's profile).

using System.Text.Json;

static class Settings
{
    static readonly object Lock = new();
    static Dictionary<string, string>? _values;

    static string FilePath => Path.Combine(AppPaths.Current.Data, "settings.json");

    static Dictionary<string, string> Load()
    {
        if (_values != null) return _values;
        try
        {
            _values = File.Exists(FilePath)
                ? JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(FilePath)) ?? new()
                : new();
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            _values = new();
        }
        return _values;
    }

    public static string? Get(string name)
    {
        lock (Lock) return Load().TryGetValue(name, out var v) ? v : null;
    }

    /** "1"/"0" switch: `fallback` while it has never been set. */
    public static bool GetBool(string name, bool fallback) =>
        Get(name) is { } v ? v != "0" : fallback;

    /** null removes the setting. */
    public static void Set(string name, string? value)
    {
        lock (Lock)
        {
            var values = Load();
            if (value == null) values.Remove(name); else values[name] = value;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                string tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, JsonSerializer.Serialize(values, new JsonSerializerOptions { WriteIndented = true }));
                File.Move(tmp, FilePath, overwrite: true);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Console.WriteLine($"settings: cannot save {FilePath}: {e.Message}");
            }
        }
    }

    public static void SetBool(string name, bool value) => Set(name, value ? "1" : "0");
}

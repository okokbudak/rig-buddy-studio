using System.Text.Json.Nodes;

namespace RigBuddy.Ui;

/** One readable telemetry value: its name ("truck.speed.kph", "x.fuelPercent"), "number" / "bool" / "text", and a note. */
public sealed record TelemetryPath(string Path, string Kind, string? Info);

/** Every telemetry name the game reports, from the agent's /catalog (pc/agent/props.mjs); loaded once. */
public sealed class TelemetryCatalog
{
    public IReadOnlyList<TelemetryPath> Paths { get; private set; } = [];

    public async Task Load(LiveClient c)
    {
        if (Paths.Count > 0) return;
        if (await c.GetAsync("catalog") is not JsonArray a) return;
        Paths = a.OfType<JsonObject>().Select(o => new TelemetryPath(Forms.Str(o, "path"), Forms.Str(o, "kind"), o["info"]?.ToString())).ToList();
    }
}

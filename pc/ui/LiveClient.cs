using System.IO;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json.Nodes;

namespace RigBuddy.Ui;

/**
 * The game's live values from the agent (pc/agent, the service the tablets use too) over its loopback WebSocket:
 * {"type":"watch","paths":[...]} once, then {"type":"live","status":{game},"values":{...}} five times a second.
 * Feeds the live telemetry page, the inspector and the top bar; GET /catalog lists every name (the game data page).
 */
public sealed class LiveClient : IDisposable
{
    readonly HttpClient _http;
    readonly Uri _ws;
    readonly CancellationTokenSource _stop = new();
    readonly SemaphoreSlim _send = new(1, 1);
    string[] _watch = [];
    volatile ClientWebSocket? _socket;

    /** A push from the agent: { status: { game }, values }. Raised on a background thread. */
    public event Action<JsonNode>? Pushed;
    public bool Connected { get; private set; }

    public LiveClient(int port)
    {
        _http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}/"), Timeout = TimeSpan.FromSeconds(5) };
        _ws = new Uri($"ws://127.0.0.1:{port}/ws");
        _ = Task.Run(Loop);
    }

    public async Task<JsonNode?> GetAsync(string path)
    {
        try { return JsonNode.Parse(await _http.GetStringAsync(path)); }
        catch { return null; }
    }

    /** The names to receive values for (the agent pushes only these). */
    public void Watch(IEnumerable<string> paths)
    {
        _watch = paths.Distinct().ToArray();
        _ = SendWatch();
    }

    // one sender (this) and one receiver (Loop) may use a WebSocket at the same time
    async Task SendWatch()
    {
        await _send.WaitAsync();
        try
        {
            if (_socket is not { State: WebSocketState.Open } ws) return;
            var msg = new JsonObject { ["type"] = "watch", ["paths"] = new JsonArray(_watch.Select(p => (JsonNode)JsonValue.Create(p)!).ToArray()) };
            await ws.SendAsync(Encoding.UTF8.GetBytes(msg.ToJsonString()), WebSocketMessageType.Text, true, _stop.Token);
        }
        catch { /* the receive loop notices a dead socket */ }
        finally { _send.Release(); }
    }

    async Task Loop()
    {
        var buf = new byte[64 * 1024];
        while (!_stop.IsCancellationRequested)
        {
            try
            {
                using var ws = new ClientWebSocket();
                await ws.ConnectAsync(_ws, _stop.Token);
                _socket = ws;
                Connected = true;
                await SendWatch();
                using var msg = new MemoryStream();
                while (ws.State == WebSocketState.Open)
                {
                    // the agent also sends the tablets' messages (telemetry summary, radio, save): any size, only "live" is ours
                    msg.SetLength(0);
                    WebSocketReceiveResult r;
                    do
                    {
                        r = await ws.ReceiveAsync(buf, _stop.Token);
                        msg.Write(buf, 0, r.Count);
                    } while (!r.EndOfMessage);
                    if (r.MessageType == WebSocketMessageType.Close) break;
                    JsonNode? n;
                    try { n = JsonNode.Parse(msg.GetBuffer().AsSpan(0, (int)msg.Length)); } catch { continue; }
                    if ((string?)n?["type"] == "live") Pushed?.Invoke(n!);
                }
            }
            catch (Exception) when (!_stop.IsCancellationRequested) { }
            _socket = null;
            Connected = false;
            try { await Task.Delay(2000, _stop.Token); } catch { }
        }
    }

    public void Dispose()
    {
        _stop.Cancel();
        _http.Dispose();
    }
}

package tr.rigbuddy.agent;

import android.os.Handler;
import android.os.Looper;

import org.json.JSONException;
import org.json.JSONObject;

import java.io.IOException;
import java.util.concurrent.TimeUnit;

import okhttp3.Call;
import okhttp3.Callback;
import okhttp3.MediaType;
import okhttp3.OkHttpClient;
import okhttp3.Request;
import okhttp3.RequestBody;
import okhttp3.Response;
import okhttp3.WebSocket;
import okhttp3.WebSocketListener;

import tr.rigbuddy.R;
import tr.rigbuddy.ui.Ui;

/**
 * Client for the PC-side Rig Buddy agent (pc/agent): full telemetry and the in-game
 * radio over WebSocket, save-game data (profile, jobs) over HTTP. Callbacks run on
 * the main thread.
 */
public final class AgentClient {
  public static final int PORT = 62843;

  public interface Listener {
    /** Latest telemetry summary, or null when the game isn't sending data. */
    void onTelemetry(JSONObject t);

    /** A newer save was loaded on the PC (jobs/profile changed). */
    void onSaveChanged();

    /** The in-game radio station and song ({"name","genre","country","song"}), or null when it is off. */
    void onRadio(JSONObject radio);

    void onAgentConnected(boolean connected);

    /** The PC's answer to a button press: {"id", "ok", "error"?}. */
    void onButton(JSONObject result);

    /** The PC does not know this device (no token yet, or it was forgotten): ask for the code shown on the PC. */
    void onAuthRequired();
  }

  public interface PairCallback {
    /** token: the device's token, or null with error set (already in the app's language). */
    void onResult(String token, String error);
  }

  public interface JsonCallback {
    void onResult(JSONObject json, String error);
  }

  private final OkHttpClient http = new OkHttpClient.Builder()
      .connectTimeout(4, TimeUnit.SECONDS)
      .readTimeout(15, TimeUnit.SECONDS)
      .addInterceptor(AgentAuth.INTERCEPTOR)
      .build();
  private final Handler main = new Handler(Looper.getMainLooper());
  private final Listener listener;
  private String host;
  private WebSocket ws;
  private boolean running;
  private boolean connected;

  public AgentClient(Listener listener) {
    this.listener = listener;
  }

  public boolean isConnected() {
    return connected;
  }

  public void start(String host) {
    this.host = host;
    running = true;
    connect();
  }

  public void stop() {
    running = false;
    connected = false;
    main.removeCallbacksAndMessages(null);
    if (ws != null) ws.close(1000, "bye");
    ws = null;
  }

  private void connect() {
    if (!running) return;
    Request req = new Request.Builder().url("ws://" + host + ":" + PORT + "/ws").build();
    ws = http.newWebSocket(req, new WebSocketListener() {
      @Override
      public void onOpen(WebSocket s, Response r) {
        main.post(() -> {
          connected = true;
          listener.onAgentConnected(true);
        });
      }

      @Override
      public void onMessage(WebSocket s, String text) {
        final JSONObject msg;
        try {
          msg = new JSONObject(text);
        } catch (JSONException e) {
          return;
        }
        main.post(() -> {
          if (s != ws) return;
          String type = msg.optString("type");
          if ("telemetry".equals(type)) listener.onTelemetry(msg.optJSONObject("data"));
          else if ("save".equals(type)) listener.onSaveChanged();
          else if ("radio".equals(type)) listener.onRadio(msg.optJSONObject("data"));
          else if ("button".equals(type) || "front".equals(type) || "screen".equals(type)) listener.onButton(msg);
        });
      }

      @Override
      public void onFailure(WebSocket s, Throwable t, Response r) {
        if (r != null && r.code() == 401) {
          main.post(() -> unauthorized(s));
          return;
        }
        main.post(() -> reconnect(s));
      }

      @Override
      public void onClosed(WebSocket s, int code, String reason) {
        main.post(() -> reconnect(s));
      }
    });
  }

  /** Asks the PC whether the game is in front; the answer comes through Listener.onButton as {"type":"front","front":bool|null}. */
  public boolean sendFrontQuery() {
    WebSocket s = ws;
    return s != null && connected && s.send("{\"type\":\"front\"}");
  }

  /**
   * Presses (down = true), releases (down = false) or taps (down = null) a key on the PC: the button box. The answer comes
   * back through Listener.onButton with the same id. False when there is no connection.
   */
  public boolean sendButton(int id, String key, Boolean down) {
    WebSocket s = ws;
    if (s == null || !connected) return false;
    try {
      JSONObject m = new JSONObject().put("type", "button").put("id", id).put("key", key);
      if (down != null) m.put("down", down.booleanValue());
      return s.send(m.toString());
    } catch (JSONException e) {
      return false;
    }
  }

  /** The agent refused the WebSocket: stop retrying until the user has entered the code. */
  private void unauthorized(WebSocket s) {
    if (s != ws || !running) return;
    ws = null;
    connected = false;
    listener.onAgentConnected(false);
    listener.onTelemetry(null);
    listener.onRadio(null);
    listener.onAuthRequired();
  }

  /** Trades the 6-digit code shown on the PC for this device's token. */
  public void pair(String code, String deviceName, PairCallback cb) {
    JSONObject body = new JSONObject();
    try {
      body.put("code", code);
      body.put("name", deviceName);
    } catch (JSONException e) {
      return;
    }
    Request req = new Request.Builder().url("http://" + host + ":" + PORT + "/pair")
        .post(RequestBody.create(body.toString(), MediaType.parse("application/json; charset=utf-8"))).build();
    http.newCall(req).enqueue(new Callback() {
      @Override
      public void onFailure(Call call, IOException e) {
        main.post(() -> cb.onResult(null, Ui.s(R.string.agent_unreachable)));
      }

      @Override
      public void onResponse(Call call, Response response) throws IOException {
        String text = response.body() != null ? response.body().string() : "";
        String token = null, error;
        try {
          JSONObject j = new JSONObject(text);
          token = response.isSuccessful() ? j.optString("token", "") : null;
          String e = j.optString("error");
          error = "locked".equals(e) ? Ui.s(R.string.pair_locked, j.optInt("retryAfter", 60))
              : "full".equals(e) ? Ui.s(R.string.pair_full)
              : "code".equals(e) ? Ui.s(R.string.pair_wrong) : Ui.s(R.string.agent_error, response.code());
        } catch (JSONException e) {
          error = Ui.s(R.string.agent_bad_reply);
        }
        final String t = token == null || token.isEmpty() ? null : token, err = error;
        main.post(() -> cb.onResult(t, t == null ? err : null));
      }
    });
  }

  private void reconnect(WebSocket s) {
    if (s != ws || !running) return;
    ws = null;
    connected = false;
    listener.onAgentConnected(false);
    listener.onTelemetry(null);
    listener.onRadio(null);
    main.postDelayed(this::connect, 3000);
  }

  /** The agent's error codes in the app's language; other messages are shown as they are. */
  private static String errorText(String error, int httpCode) {
    if ("nosave".equals(error)) return Ui.s(R.string.home_waiting_save);
    return error.isEmpty() ? Ui.s(R.string.agent_error, httpCode) : error;
  }

  public void get(String path, JsonCallback cb) {
    call(new Request.Builder().url("http://" + host + ":" + PORT + path).build(), cb);
  }

  /** POST with a JSON body (the LED menu); the answer is handled like get(). */
  public void post(String path, JSONObject body, JsonCallback cb) {
    call(new Request.Builder().url("http://" + host + ":" + PORT + path)
        .post(RequestBody.create(body.toString(), MediaType.parse("application/json; charset=utf-8"))).build(), cb);
  }

  private void call(Request req, JsonCallback cb) {
    http.newCall(req).enqueue(new Callback() {
      @Override
      public void onFailure(Call call, IOException e) {
        main.post(() -> cb.onResult(null, Ui.s(R.string.agent_unreachable)));
      }

      @Override
      public void onResponse(Call call, Response response) throws IOException {
        String body = response.body() != null ? response.body().string() : "";
        JSONObject json = null;
        try {
          json = new JSONObject(body);
        } catch (JSONException ignored) {
        }
        final JSONObject result = json;
        final boolean ok = response.isSuccessful();
        final boolean unauthorized = response.code() == 401;
        main.post(() -> {
          if (unauthorized) {
            listener.onAuthRequired();
            cb.onResult(null, Ui.s(R.string.pair_needed));
          } else if (result == null) cb.onResult(null, Ui.s(R.string.agent_bad_reply));
          else if (!ok) cb.onResult(null, errorText(result.optString("error"), response.code()));
          else cb.onResult(result, null);
        });
      }
    });
  }
}

package tr.rigbuddy.agent;

import okhttp3.Interceptor;

/**
 * The token this device got when it was paired with the PC (see pc/agent/pair.mjs). Every client that talks to the
 * agent (AgentClient, TileDownloader, AppUpdater) adds it through {@link #INTERCEPTOR}; it is only ever sent to the
 * agent's port.
 */
public final class AgentAuth {
  private static volatile String token = "";

  private AgentAuth() {}

  public static void setToken(String t) {
    token = t == null ? "" : t;
  }

  public static boolean hasToken() {
    return !token.isEmpty();
  }

  public static final Interceptor INTERCEPTOR = chain -> {
    okhttp3.Request req = chain.request();
    String t = token;
    if (!t.isEmpty() && req.url().port() == AgentClient.PORT) {
      req = req.newBuilder().header("Authorization", "Bearer " + t).build();
    }
    return chain.proceed(req);
  };
}

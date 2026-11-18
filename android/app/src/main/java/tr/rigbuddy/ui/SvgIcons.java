package tr.rigbuddy.ui;

import android.content.Context;
import android.graphics.Canvas;
import android.graphics.Paint;
import android.graphics.Path;
import android.graphics.RectF;

import org.json.JSONArray;
import org.json.JSONObject;

import java.io.ByteArrayOutputStream;
import java.io.InputStream;
import java.util.ArrayList;
import java.util.Iterator;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

/**
 * The button box's own line icons (art/buttonbox/icons.json, packaged as an asset): 24x24 units, drawn with a round stroke.
 * The paths use absolute M / L / C / Z only (the arcs were turned into curves when the file was made), so reading them
 * needs no SVG library.
 */
public final class SvgIcons {
  private static final Pattern TOKEN = Pattern.compile("[MLCZ]|-?\\d*\\.?\\d+");
  private static Map<String, Path> icons;

  private SvgIcons() {}

  /** The ids of all icons, in file order. */
  public static synchronized List<String> ids(Context c) {
    load(c);
    return new ArrayList<>(icons.keySet());
  }

  public static synchronized boolean has(Context c, String id) {
    load(c);
    return icons.containsKey(id);
  }

  /** Draws the icon (stroke only) scaled into `box`; the paint's stroke width is set in 24-unit space by this method. */
  public static synchronized void draw(Context c, Canvas canvas, String id, RectF box, Paint paint) {
    load(c);
    Path p = icons.get(id);
    if (p == null) p = icons.get("circle");
    if (p == null) return;
    float scale = Math.min(box.width(), box.height()) / 24f;
    float width = paint.getStrokeWidth();
    canvas.save();
    canvas.translate(box.centerX() - 12 * scale, box.centerY() - 12 * scale);
    canvas.scale(scale, scale);
    paint.setStrokeWidth(2f);
    canvas.drawPath(p, paint);
    paint.setStrokeWidth(width);
    canvas.restore();
  }

  private static void load(Context c) {
    if (icons != null) return;
    Map<String, Path> map = new LinkedHashMap<>();
    try (InputStream in = c.getAssets().open("icons.json")) {
      ByteArrayOutputStream out = new ByteArrayOutputStream();
      byte[] buf = new byte[8192];
      for (int n; (n = in.read(buf)) > 0; ) out.write(buf, 0, n);
      JSONObject root = new JSONObject(out.toString("UTF-8"));
      for (Iterator<String> it = root.keys(); it.hasNext(); ) {
        String id = it.next();
        JSONArray parts = root.getJSONArray(id);
        Path path = new Path();
        for (int i = 0; i < parts.length(); i++) add(path, parts.getString(i));
        map.put(id, path);
      }
    } catch (Exception e) {
      // no icons: the buttons show their label only
    }
    icons = map;
  }

  private static void add(Path path, String d) {
    Matcher m = TOKEN.matcher(d);
    List<String> t = new ArrayList<>();
    while (m.find()) t.add(m.group());
    int i = 0;
    while (i < t.size()) {
      String cmd = t.get(i++);
      switch (cmd) {
        case "M":
          path.moveTo(f(t, i), f(t, i + 1));
          i += 2;
          break;
        case "L":
          path.lineTo(f(t, i), f(t, i + 1));
          i += 2;
          break;
        case "C":
          path.cubicTo(f(t, i), f(t, i + 1), f(t, i + 2), f(t, i + 3), f(t, i + 4), f(t, i + 5));
          i += 6;
          break;
        case "Z":
          path.close();
          break;
        default:
          return; // not a command this reader knows: stop with what was drawn so far
      }
    }
  }

  private static float f(List<String> t, int i) {
    return i < t.size() ? Float.parseFloat(t.get(i)) : 0f;
  }
}

package tr.rigbuddy.ui;

import android.app.AlertDialog;
import android.content.Context;
import android.content.SharedPreferences;
import android.graphics.Canvas;
import android.graphics.Paint;
import android.graphics.RectF;
import android.graphics.Typeface;
import android.os.Handler;
import android.os.Looper;
import android.text.TextUtils;
import android.view.Gravity;
import android.view.MotionEvent;
import android.view.View;
import android.view.ViewGroup;
import android.widget.EditText;
import android.widget.HorizontalScrollView;
import android.widget.LinearLayout;
import android.widget.ScrollView;
import android.widget.TextView;

import org.json.JSONArray;
import org.json.JSONException;
import org.json.JSONObject;

import java.io.ByteArrayOutputStream;
import java.io.InputStream;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Locale;
import java.util.Map;

import tr.rigbuddy.R;
import tr.rigbuddy.agent.AgentClient;

/**
 * A button box on the screen: pages of buttons that press keys on the gaming PC. A button carries the key the player has for
 * that function in the game (or follows the game's own binding while it has none of its own), a touch goes to the PC agent
 * over the WebSocket and RigBuddy.exe presses the key while the game is in front. The layout is kept on the device.
 */
public final class ButtonBoxScreen {
  private static final String PREFS = "buttonbox";
  private static final String KEY_LAYOUT = "layout";
  private static final int MAX_W = 4, MAX_H = 3;   // the biggest a button gets, in cells
  private static final int[] COLORS = {0, 0xFF2E7D32, 0xFFC62828, 0xFFEF6C00, 0xFF1565C0, 0xFF6A1B9A};

  /** A function of the game from art/buttonbox/actions.json. */
  private static final class Action {
    String id, icon, category, tr, en, mode;
    String label() {
      return "tr".equals(Ui.LOCALE.getLanguage()) ? tr : en;
    }
  }

  private static final class Btn {
    String label = "", icon = "circle", key = "", action = "", mode = "tap";
    int color;
    int w = 1, h = 1;   // size in cells (dragged by the corner in the edit mode)
  }

  private static final class Page {
    String name;
    final List<Btn> buttons = new ArrayList<>();
  }

  private final Context c;
  private final AgentClient agent;
  private final SharedPreferences prefs;
  private final LinearLayout root, pageBar, grid;
  private final TextView editToggle, status;
  private final List<Page> pages = new ArrayList<>();
  private final List<Action> actions = new ArrayList<>();
  private final Map<String, String> gameKeys = new HashMap<>();   // function -> key the game has for it
  private final Map<Integer, Btn> pending = new HashMap<>();
  private final List<ButtonView> held = new ArrayList<>();
  private int page, nextId = 1, columns = 5;
  private boolean edit, active;
  private Boolean gameFront;   // is the game in front on the PC: null = not known yet
  private long lastKeysLoad;
  private final Handler handler = new Handler(Looper.getMainLooper());
  private final Runnable frontPoll = new Runnable() {
    @Override
    public void run() {
      if (!active) return;
      if (!agent.sendFrontQuery()) setFront(null);
      handler.postDelayed(this, 2000);
    }
  };

  public ButtonBoxScreen(Context c, AgentClient agent) {
    this.c = c;
    this.agent = agent;
    this.prefs = c.getSharedPreferences(PREFS, Context.MODE_PRIVATE);
    loadActions();
    loadLayout();

    root = Ui.column(c);
    int pad = Ui.dp(c, 18);
    root.setPadding(pad, pad, pad, pad);
    LinearLayout top = Ui.row(c);
    top.addView(Ui.text(c, Ui.s(R.string.bb_title), 24, Ui.TEXT, true));
    pageBar = Ui.row(c);
    HorizontalScrollView pageScroll = new HorizontalScrollView(c);
    pageScroll.setHorizontalScrollBarEnabled(false);
    pageScroll.addView(pageBar);
    top.addView(pageScroll, Ui.margins(new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1), c, 18, 0, 12, 0));
    editToggle = chip(Ui.s(R.string.bb_edit), false);
    editToggle.setOnClickListener(v -> setEdit(!edit));
    top.addView(editToggle);
    root.addView(top, Ui.margins(Ui.matchWrap(), c, 0, 0, 0, 12));

    ScrollView scroll = new ScrollView(c);
    grid = Ui.column(c);
    scroll.addView(grid);
    root.addView(scroll, Ui.hweight(1));
    status = Ui.text(c, 14, Ui.TEXT2, false);
    root.addView(status, Ui.margins(Ui.matchWrap(), c, 0, 8, 0, 0));
    // the number of columns follows the width: about 4.5 cm per button on a tablet
    root.addOnLayoutChangeListener((v, l, t, r, b, ol, ot, or, ob) -> {
      int cols = Math.max(3, Math.min(8, (r - l - 2 * pad) / Ui.dp(c, 118)));
      if (cols != columns) {
        columns = cols;
        rebuildGrid();
      }
    });
    rebuildPages();
    rebuildGrid();
  }

  public View view() {
    return root;
  }

  /** The screen was opened or left. Leaving lets go of every button that is held. */
  public void setActive(boolean active) {
    this.active = active;
    handler.removeCallbacks(frontPoll);
    if (active) {
      if (System.currentTimeMillis() - lastKeysLoad > 30_000) loadGameKeys();
      handler.post(frontPoll);
    } else {
      releaseAll();
      if (edit) setEdit(false);
    }
  }

  /** The PC's answer to a press ({"id","ok","error"?}). */
  public void onResult(JSONObject r) {
    if ("front".equals(r.optString("type"))) {
      setFront(r.isNull("front") ? null : r.optBoolean("front"));
      return;
    }
    Btn b = pending.remove(r.optInt("id"));
    if (r.optBoolean("ok")) {
      status.setText("");
      return;
    }
    String error = r.optString("error");
    switch (error) {
      case "game is not in front":
      case "the game is not in front":
        status.setText(Ui.s(R.string.bb_err_front));
        break;
      case "host":
        status.setText(Ui.s(R.string.bb_err_host));
        break;
      default:
        status.setText(error.contains("not allowed") ? Ui.s(R.string.bb_err_denied) : Ui.s(R.string.bb_err_other, error));
    }
  }

  // --- layout storage -------------------------------------------------------------------------

  /** The game came to the front or went away: the buttons are dimmed while it is not in front (the PC would refuse them). */
  private void setFront(Boolean front) {
    if (front == null ? gameFront == null : front.equals(gameFront)) return;
    gameFront = front;
    float alpha = !edit && Boolean.FALSE.equals(front) ? 0.4f : 1f;
    for (int i = 0; i < grid.getChildCount(); i++) grid.getChildAt(i).setAlpha(alpha);
    if (!edit) status.setText(Boolean.FALSE.equals(front) ? Ui.s(R.string.bb_not_front) : "");
  }

  private void loadActions() {
    try (InputStream in = c.getAssets().open("actions.json")) {
      ByteArrayOutputStream out = new ByteArrayOutputStream();
      byte[] buf = new byte[8192];
      for (int n; (n = in.read(buf)) > 0; ) out.write(buf, 0, n);
      JSONArray a = new JSONArray(out.toString("UTF-8"));
      for (int i = 0; i < a.length(); i++) {
        JSONObject o = a.getJSONObject(i);
        Action x = new Action();
        x.id = o.getString("id");
        x.icon = o.optString("icon", "circle");
        x.category = o.optString("category");
        x.tr = o.optString("tr", x.id);
        x.en = o.optString("en", x.id);
        x.mode = o.optString("mode", "tap");
        actions.add(x);
      }
    } catch (Exception e) {
      // no catalogue: buttons can still be made with a key of one's own
    }
  }

  private Action actionById(String id) {
    for (Action a : actions) if (a.id.equals(id)) return a;
    return null;
  }

  private void loadLayout() {
    pages.clear();
    try {
      JSONObject root = new JSONObject(prefs.getString(KEY_LAYOUT, "{}"));
      JSONArray ps = root.optJSONArray("pages");
      if (ps != null) {
        for (int i = 0; i < ps.length(); i++) {
          JSONObject po = ps.getJSONObject(i);
          Page p = new Page();
          p.name = po.optString("name", "");
          JSONArray bs = po.optJSONArray("buttons");
          for (int j = 0; bs != null && j < bs.length(); j++) {
            JSONObject bo = bs.getJSONObject(j);
            Btn b = new Btn();
            b.label = bo.optString("label");
            b.icon = bo.optString("icon", "circle");
            b.key = bo.optString("key");
            b.action = bo.optString("action");
            b.mode = "hold".equals(bo.optString("mode")) ? "hold" : "tap";
            b.color = bo.optInt("color", 0);
            b.w = Math.max(1, Math.min(MAX_W, bo.optInt("w", 1)));
            b.h = Math.max(1, Math.min(MAX_H, bo.optInt("h", 1)));
            p.buttons.add(b);
          }
          pages.add(p);
        }
      }
      page = Math.max(0, Math.min(root.optInt("page", 0), pages.size() - 1));
    } catch (JSONException e) {
      pages.clear();
    }
    if (pages.isEmpty()) {
      pages.add(starterPage());
      page = 0;
    }
  }

  /** A first page with the functions most used while driving; they follow whatever keys the game has. */
  private Page starterPage() {
    Page p = new Page();
    p.name = Ui.s(R.string.bb_page_driving);
    for (String id : new String[] {"horn", "hblight", "lblinker", "rblinker", "flasher4way", "wipers", "beacon",
        "parkingbrake", "engine", "cruiectrl", "diflock", "liftaxle", "camcycle", "retarderup", "retarderdown"}) {
      Action a = actionById(id);
      if (a == null) continue;
      Btn b = new Btn();
      fillFrom(b, a);
      p.buttons.add(b);
    }
    return p;
  }

  private void fillFrom(Btn b, Action a) {
    b.action = a.id;
    b.label = a.label();
    b.icon = a.icon;
    b.mode = "hold".equals(a.mode) ? "hold" : "tap";
    b.key = "";   // follow the game's own key
  }

  private void saveLayout() {
    try {
      JSONArray ps = new JSONArray();
      for (Page p : pages) {
        JSONArray bs = new JSONArray();
        for (Btn b : p.buttons) {
          bs.put(new JSONObject().put("label", b.label).put("icon", b.icon).put("key", b.key).put("action", b.action)
              .put("mode", b.mode).put("color", b.color).put("w", b.w).put("h", b.h));
        }
        ps.put(new JSONObject().put("name", p.name).put("buttons", bs));
      }
      prefs.edit().putString(KEY_LAYOUT, new JSONObject().put("pages", ps).put("page", page).toString()).apply();
    } catch (JSONException e) {
      // nothing to save
    }
  }

  private void loadGameKeys() {
    agent.get("/controls", (json, error) -> {
      if (json == null) return;
      lastKeysLoad = System.currentTimeMillis();
      JSONObject b = json.optJSONObject("bindings");
      gameKeys.clear();
      if (b != null) {
        for (java.util.Iterator<String> it = b.keys(); it.hasNext(); ) {
          String k = it.next();
          gameKeys.put(k, b.optString(k));
        }
      }
    });
  }

  /** The key a button presses: its own, else the one the game has for its function. */
  /** Another button of this page presses the same key: both fire the same game function, almost always a mistake. */
  private boolean sameKeyElsewhere(Btn b, String key) {
    for (Btn o : pages.get(page).buttons) if (o != b && key.equals(keyOf(o))) return true;
    return false;
  }

  private String keyOf(Btn b) {
    if (!b.key.isEmpty()) return b.key;
    String g = b.action.isEmpty() ? null : gameKeys.get(b.action);
    return g == null ? "" : g;
  }

  // --- screen -----------------------------------------------------------------------------------

  private TextView chip(String text, boolean on) {
    TextView t = Ui.text(c, text, 15, on ? Ui.ON_ACCENT : Ui.TEXT, true);
    t.setGravity(Gravity.CENTER);
    int h = Ui.dp(c, 14), v = Ui.dp(c, 9);
    t.setPadding(h, v, h, v);
    t.setBackground(Ui.rounded(on ? Ui.ACCENT : Ui.CARD, Ui.dp(c, 20)));
    t.setClickable(true);
    return t;
  }

  private void setEdit(boolean on) {
    edit = on;
    editToggle.setText(Ui.s(on ? R.string.bb_done : R.string.bb_edit));
    editToggle.setTextColor(on ? Ui.ON_ACCENT : Ui.TEXT);
    editToggle.setBackground(Ui.rounded(on ? Ui.ACCENT : Ui.CARD, Ui.dp(c, 20)));
    releaseAll();
    rebuildPages();
    rebuildGrid();
  }

  private void rebuildPages() {
    pageBar.removeAllViews();
    for (int i = 0; i < pages.size(); i++) {
      final int index = i;
      TextView t = chip(pages.get(i).name, i == page);
      t.setOnClickListener(v -> {
        if (index == page && edit) {
          editPage(pages.get(index));
          return;
        }
        releaseAll();
        page = index;
        saveLayout();
        rebuildPages();
        rebuildGrid();
      });
      pageBar.addView(t, Ui.margins(Ui.wrap(), c, 0, 0, 8, 0));
    }
    if (edit) {
      TextView add = chip("+", false);
      add.setOnClickListener(v -> addPage());
      pageBar.addView(add);
    }
  }

  /** Ready-made pages: name, the functions on it. They follow whatever keys the game has. */
  private static final String[][] TEMPLATES = {
      {"lights", "hblight", "light", "lightpark", "beacon", "cabinlight", "lighthorn", "flasher4way", "lblinker", "rblinker"},
      {"drive", "gearup", "geardown", "geardrive", "gearreverse", "parkingbrake", "trailerbrake", "motorbrake", "retarderup",
          "retarderdown", "transemi", "diflock", "liftaxle"},
      {"cameras", "camcycle", "cam1", "cam2", "cam3", "cam4", "cam5", "cam6", "showmirrors"},
      {"cabin", "wipers", "wipers0", "wipers1", "wipers2", "winlshadeup", "winlshadedwn", "winrshadeup", "winrshadedwn",
          "entrdoors", "radiotoggle", "radionext", "radioprev"},
      {"assist", "cruiectrl", "cruiectrlinc", "cruiectrldec", "cruiectrlres", "accmode", "laneassist", "quicksave", "quickload",
          "tripreset", "pause"},
  };

  private int templateName(String id) {
    switch (id) {
      case "lights": return R.string.bb_tpl_lights;
      case "drive": return R.string.bb_tpl_drive;
      case "cameras": return R.string.bb_tpl_cameras;
      case "cabin": return R.string.bb_tpl_cabin;
      default: return R.string.bb_tpl_assist;
    }
  }

  /** The "+" of the edit mode: an empty page or one of the ready-made ones. */
  private void addPage() {
    String[] names = new String[TEMPLATES.length + 1];
    names[0] = Ui.s(R.string.bb_tpl_empty);
    for (int i = 0; i < TEMPLATES.length; i++) names[i + 1] = Ui.s(templateName(TEMPLATES[i][0]));
    new AlertDialog.Builder(c).setTitle(Ui.s(R.string.bb_add_page)).setItems(names, (d, which) -> {
      Page p = new Page();
      p.name = which == 0 ? Ui.s(R.string.bb_page_n, pages.size() + 1) : names[which];
      if (which > 0) {
        String[] t = TEMPLATES[which - 1];
        for (int i = 1; i < t.length; i++) {
          Action a = actionById(t[i]);
          if (a == null) continue;
          Btn b = new Btn();
          fillFrom(b, a);
          p.buttons.add(b);
        }
      }
      pages.add(p);
      page = pages.size() - 1;
      saveLayout();
      rebuildPages();
      rebuildGrid();
    }).setNegativeButton(Ui.s(R.string.bb_cancel), null).show();
  }

  private void rebuildGrid() {
    releaseAll();
    grid.removeAllViews();
    Page p = pages.get(page);
    PackLayout pack = new PackLayout(c);
    for (Btn b : p.buttons) pack.addView(new ButtonView(c, b), new ViewGroup.LayoutParams(0, 0));
    if (edit) {
      ButtonView add = new ButtonView(c, null);
      add.setOnClickListener(v -> editButton(null));
      pack.addView(add, new ViewGroup.LayoutParams(0, 0));
    }
    grid.addView(pack, Ui.matchWrap());
    pack.setAlpha(!edit && Boolean.FALSE.equals(gameFront) ? 0.4f : 1f);
    status.setText(edit ? Ui.s(R.string.bb_resize_hint) : Boolean.FALSE.equals(gameFront) ? Ui.s(R.string.bb_not_front) : "");
    if (p.buttons.isEmpty() && !edit) {
      grid.addView(Ui.text(c, Ui.s(R.string.bb_empty), 16, Ui.TEXT2, false), Ui.margins(Ui.matchWrap(), c, 6, 16, 6, 0));
    }
  }

  /**
   * Lays the buttons out in reading order on a grid of equal cells; a button covers w x h cells. The first free place from
   * the previous button on takes the next one, so a big button never leaves a hole that the order would have to jump over.
   */
  private final class PackLayout extends ViewGroup {
    private final int cell = Ui.dp(c, 104), gap = Ui.dp(c, 10);
    private int[][] place = new int[0][];   // per child: column, row, columns wide, rows high
    float pitchX = 1, pitchY = 1;

    PackLayout(Context c) {
      super(c);
    }

    @Override
    protected void onMeasure(int wSpec, int hSpec) {
      int width = MeasureSpec.getSize(wSpec);
      int cols = Math.max(1, columns);
      float cellW = (width - (cols - 1) * gap) / (float) cols;
      pitchX = cellW + gap;
      pitchY = cell + gap;
      int n = getChildCount();
      place = new int[n][];
      List<boolean[]> used = new ArrayList<>();
      int cr = 0, cc = 0, rowsUsed = 0;
      for (int i = 0; i < n; i++) {
        View v = getChildAt(i);
        int[] sp = ((ButtonView) v).span();
        int sw = Math.min(sp[0], cols), sh = sp[1];
        int r = cr, col = cc;
        while (true) {
          if (col + sw > cols) {
            col = 0;
            r++;
          }
          while (used.size() < r + sh) used.add(new boolean[cols]);
          boolean free = true;
          for (int y = r; y < r + sh && free; y++)
            for (int x = col; x < col + sw; x++)
              if (used.get(y)[x]) {
                free = false;
                break;
              }
          if (free) break;
          col++;
        }
        for (int y = r; y < r + sh; y++)
          for (int x = col; x < col + sw; x++) used.get(y)[x] = true;
        place[i] = new int[] {col, r, sw, sh};
        cr = r;
        cc = col + sw;
        rowsUsed = Math.max(rowsUsed, r + sh);
        v.measure(MeasureSpec.makeMeasureSpec(Math.round(sw * cellW + (sw - 1) * gap), MeasureSpec.EXACTLY),
            MeasureSpec.makeMeasureSpec(sh * cell + (sh - 1) * gap, MeasureSpec.EXACTLY));
      }
      setMeasuredDimension(width, rowsUsed == 0 ? 0 : rowsUsed * cell + (rowsUsed - 1) * gap);
    }

    @Override
    protected void onLayout(boolean changed, int l, int t, int r, int b) {
      for (int i = 0; i < getChildCount() && i < place.length; i++) {
        View v = getChildAt(i);
        int x = Math.round(place[i][0] * pitchX), y = Math.round(place[i][1] * pitchY);
        v.layout(x, y, x + v.getMeasuredWidth(), y + v.getMeasuredHeight());
      }
    }
  }

  private void releaseAll() {
    for (ButtonView v : new ArrayList<>(held)) v.release();
    held.clear();
  }

  private void press(Btn b, Boolean down, ButtonView view) {
    String key = keyOf(b);
    if (key.isEmpty()) {
      status.setText(Ui.s(R.string.bb_err_nokey));
      return;
    }
    int id = nextId++;
    pending.put(id, b);
    if (!agent.sendButton(id, key, down)) {
      pending.remove(id);
      status.setText(Ui.s(R.string.bb_err_host));
    }
  }

  /** One button of the grid (or, with no Btn, the "add" cell of the edit mode). */
  private final class ButtonView extends View {
    private final Btn b;
    private final Paint fill = new Paint(Paint.ANTI_ALIAS_FLAG), stroke = new Paint(Paint.ANTI_ALIAS_FLAG),
        textPaint = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final RectF rect = new RectF(), iconBox = new RectF();
    private boolean down, resizing;
    private float startX, startY;
    private int startW, startH;

    int[] span() {
      return b == null ? new int[] {1, 1} : new int[] {b.w, b.h};
    }

    ButtonView(Context c, Btn b) {
      super(c);
      this.b = b;
      stroke.setStyle(Paint.Style.STROKE);
      stroke.setStrokeCap(Paint.Cap.ROUND);
      stroke.setStrokeJoin(Paint.Join.ROUND);
      textPaint.setTextAlign(Paint.Align.CENTER);
      textPaint.setTypeface(Typeface.create("sans-serif-medium", Typeface.NORMAL));
      textPaint.setTextSize(Ui.dp(c, 13));
      setClickable(true);
    }

    void release() {
      if (down && b != null && edit == false && "hold".equals(b.mode)) press(b, false, this);
      down = false;
      invalidate();
    }

    @Override
    public boolean onTouchEvent(MotionEvent e) {
      if (b != null && edit && onResizeTouch(e)) return true;
      if (b == null || edit) return super.onTouchEvent(e);   // the cells of the edit mode use their click listeners
      switch (e.getActionMasked()) {
        case MotionEvent.ACTION_DOWN:
          down = true;
          performHapticFeedback(android.view.HapticFeedbackConstants.KEYBOARD_TAP);
          if ("hold".equals(b.mode)) {
            held.add(this);
            press(b, true, this);
          } else {
            press(b, null, this);
          }
          invalidate();
          return true;
        case MotionEvent.ACTION_UP:
        case MotionEvent.ACTION_CANCEL:
          if (down && "hold".equals(b.mode)) {
            held.remove(this);
            press(b, false, this);
          }
          down = false;
          invalidate();
          return true;
        default:
          return true;
      }
    }

    /** Dragging the corner handle in the edit mode: one cell more or less for every pitch moved; saved on release. */
    private boolean onResizeTouch(MotionEvent e) {
      float handle = Ui.dp(getContext(), 40);
      if (e.getActionMasked() == MotionEvent.ACTION_DOWN && e.getX() > getWidth() - handle && e.getY() > getHeight() - handle) {
        resizing = true;
        startX = e.getRawX();
        startY = e.getRawY();
        startW = b.w;
        startH = b.h;
        getParent().requestDisallowInterceptTouchEvent(true);
        return true;
      }
      if (!resizing) return false;
      if (e.getActionMasked() == MotionEvent.ACTION_MOVE && getParent() instanceof PackLayout) {
        PackLayout pl = (PackLayout) getParent();
        int w = Math.max(1, Math.min(Math.min(MAX_W, columns), startW + Math.round((e.getRawX() - startX) / pl.pitchX)));
        int h = Math.max(1, Math.min(MAX_H, startH + Math.round((e.getRawY() - startY) / pl.pitchY)));
        if (w != b.w || h != b.h) {
          b.w = w;
          b.h = h;
          pl.requestLayout();
        }
      } else if (e.getActionMasked() == MotionEvent.ACTION_UP || e.getActionMasked() == MotionEvent.ACTION_CANCEL) {
        resizing = false;
        saveLayout();
      }
      return true;
    }

    @Override
    public boolean performClick() {
      if (b != null && edit) editButton(b);
      return super.performClick();
    }

    @Override
    protected void onDraw(Canvas canvas) {
      float w = getWidth(), h = getHeight(), r = Ui.dp(getContext(), 16);
      rect.set(0, 0, w, h);
      if (b == null) {   // "add"
        stroke.setColor(Ui.TEXT2);
        stroke.setStrokeWidth(Ui.dp(getContext(), 2));
        canvas.drawRoundRect(rect.left + 2, rect.top + 2, rect.right - 2, rect.bottom - 2, r, r, stroke);
        float cx = w / 2, cy = h / 2, s = Ui.dp(getContext(), 12);
        canvas.drawLine(cx - s, cy, cx + s, cy, stroke);
        canvas.drawLine(cx, cy - s, cx, cy + s, stroke);
        return;
      }
      int base = b.color != 0 ? b.color : Ui.CARD;
      fill.setColor(down ? (b.color != 0 ? Ui.ACCENT : Ui.CARD_PRESSED) : base);
      if (down) fill.setColor(Ui.ACCENT);
      canvas.drawRoundRect(rect, r, r, fill);
      int ink = down || b.color != 0 ? (down ? Ui.ON_ACCENT : 0xFFFFFFFF) : Ui.TEXT;
      stroke.setColor(ink);
      stroke.setStrokeWidth(Ui.dp(getContext(), 2));
      float iconSize = Math.min(w, h) * 0.46f;
      iconBox.set(w / 2 - iconSize / 2, h * 0.14f, w / 2 + iconSize / 2, h * 0.14f + iconSize);
      SvgIcons.draw(getContext(), canvas, b.icon, iconBox, stroke);
      textPaint.setColor(ink);
      String label = TextUtils.ellipsize(b.label, new android.text.TextPaint(textPaint), w - Ui.dp(getContext(), 10),
          TextUtils.TruncateAt.END).toString();
      canvas.drawText(label, w / 2, h - Ui.dp(getContext(), 13), textPaint);
      if (edit) {   // the key it presses, so a missing one is easy to see
        String k = keyOf(b);
        textPaint.setTextSize(Ui.dp(getContext(), 10));
        boolean clash = !k.isEmpty() && sameKeyElsewhere(b, k);
        textPaint.setColor(k.isEmpty() ? Ui.RED : clash ? 0xFFFFA726 : (down ? Ui.ON_ACCENT : Ui.TEXT2));
        canvas.drawText(k.isEmpty() ? "!" : clash ? keyLabel(k) + " \u26A0" : keyLabel(k), w / 2, Ui.dp(getContext(), 14), textPaint);
        textPaint.setTextSize(Ui.dp(getContext(), 13));
        // the resize handle: two short diagonal strokes in the corner
        stroke.setColor(Ui.TEXT2);
        stroke.setStrokeWidth(Ui.dp(getContext(), 2));
        float m = Ui.dp(getContext(), 8), a = Ui.dp(getContext(), 14), a2 = Ui.dp(getContext(), 8);
        canvas.drawLine(w - m, h - m - a, w - m - a, h - m, stroke);
        canvas.drawLine(w - m, h - m - a2, w - m - a2, h - m, stroke);
      }
    }
  }

  // --- editing ----------------------------------------------------------------------------------

  private void editPage(Page p) {
    EditText name = new EditText(c);
    name.setText(p.name);
    name.setSingleLine(true);
    AlertDialog.Builder d = new AlertDialog.Builder(c).setTitle(Ui.s(R.string.bb_page_title)).setView(name)
        .setPositiveButton(Ui.s(R.string.bb_save), (x, y) -> {
          String n = name.getText().toString().trim();
          if (!n.isEmpty()) p.name = n;
          saveLayout();
          rebuildPages();
        })
        .setNegativeButton(Ui.s(R.string.bb_cancel), null);
    if (pages.size() > 1) {
      d.setNeutralButton(Ui.s(R.string.bb_delete), (x, y) -> {
        pages.remove(p);
        page = Math.max(0, Math.min(page, pages.size() - 1));
        saveLayout();
        rebuildPages();
        rebuildGrid();
      });
    }
    d.show();
  }

  /** The editor of one button; null makes a new one. */
  private void editButton(Btn existing) {
    final Btn b = existing != null ? existing : new Btn();
    if (existing == null) {
      b.label = Ui.s(R.string.bb_new_button);
    }
    final Page p = pages.get(page);
    final Btn[] work = {copy(b)};

    LinearLayout col = Ui.column(c);
    int pad = Ui.dp(c, 20);
    col.setPadding(pad, pad / 2, pad, 0);

    EditText label = new EditText(c);
    label.setSingleLine(true);
    label.setText(work[0].label);
    label.setHint(Ui.s(R.string.bb_label));
    col.addView(label, Ui.matchWrap());

    TextView functionBtn = chipLine(col, Ui.s(R.string.bb_function));
    TextView iconBtn = chipLine(col, Ui.s(R.string.bb_icon));
    TextView keyBtn = chipLine(col, Ui.s(R.string.bb_key));
    TextView modeBtn = chipLine(col, Ui.s(R.string.bb_mode));

    LinearLayout colors = Ui.row(c);
    final List<View> swatches = new ArrayList<>();
    Runnable paintSwatches = () -> {
      for (int i = 0; i < swatches.size(); i++) swatches.get(i).setAlpha(COLORS[i] == work[0].color ? 1f : 0.35f);
    };
    for (int color : COLORS) {
      View sw = new View(c);
      sw.setBackground(Ui.rounded(color != 0 ? color : Ui.TEXT2, Ui.dp(c, 16)));
      sw.setOnClickListener(v -> {
        work[0].color = color;
        paintSwatches.run();
      });
      swatches.add(sw);
      colors.addView(sw, Ui.margins(new LinearLayout.LayoutParams(Ui.dp(c, 32), Ui.dp(c, 32)), c, 0, 10, 10, 0));
    }
    col.addView(colors);
    paintSwatches.run();

    Runnable refresh = () -> {
      Btn w = work[0];
      Action a = actionById(w.action);
      functionBtn.setText(Ui.s(R.string.bb_function) + ": " + (a != null ? a.label() : Ui.s(R.string.bb_none)));
      iconBtn.setText(Ui.s(R.string.bb_icon) + ": " + w.icon);
      String own = w.key.isEmpty() ? "" : keyLabel(w.key);
      String g = w.action.isEmpty() ? null : gameKeys.get(w.action);
      keyBtn.setText(Ui.s(R.string.bb_key) + ": " + (!w.key.isEmpty() ? own
          : g != null ? keyLabel(g) + " (" + Ui.s(R.string.bb_from_game) + ")" : Ui.s(R.string.bb_no_key)));
      modeBtn.setText(Ui.s(R.string.bb_mode) + ": " + Ui.s("hold".equals(w.mode) ? R.string.bb_mode_hold : R.string.bb_mode_tap));
    };
    functionBtn.setOnClickListener(v -> pickAction(a -> {
      fillFrom(work[0], a);
      label.setText(work[0].label);
      refresh.run();
    }));
    iconBtn.setOnClickListener(v -> pickIcon(id -> {
      work[0].icon = id;
      refresh.run();
    }));
    keyBtn.setOnClickListener(v -> pickKey(work[0].key, k -> {
      work[0].key = k;
      refresh.run();
    }));
    modeBtn.setOnClickListener(v -> {
      work[0].mode = "hold".equals(work[0].mode) ? "tap" : "hold";
      refresh.run();
    });
    refresh.run();

    ScrollView sv = new ScrollView(c);
    sv.addView(col);
    AlertDialog.Builder d = new AlertDialog.Builder(c)
        .setTitle(Ui.s(existing == null ? R.string.bb_add : R.string.bb_edit_button))
        .setView(sv)
        .setPositiveButton(Ui.s(R.string.bb_save), (x, y) -> {
          work[0].label = label.getText().toString().trim();
          if (existing == null) p.buttons.add(work[0]);
          else p.buttons.set(p.buttons.indexOf(existing), work[0]);
          saveLayout();
          rebuildGrid();
        })
        .setNegativeButton(Ui.s(R.string.bb_cancel), null);
    if (existing != null) {
      d.setNeutralButton(Ui.s(R.string.bb_delete), (x, y) -> {
        p.buttons.remove(existing);
        saveLayout();
        rebuildGrid();
      });
    }
    d.show();
  }

  private TextView chipLine(LinearLayout parent, String text) {
    TextView t = Ui.text(c, text, 16, Ui.TEXT, false);
    int pad = Ui.dp(c, 12);
    t.setPadding(pad, pad, pad, pad);
    t.setBackground(Ui.rounded(Ui.CARD, Ui.dp(c, 12)));
    t.setClickable(true);
    parent.addView(t, Ui.margins(Ui.matchWrap(), c, 0, 10, 0, 0));
    return t;
  }

  private static Btn copy(Btn b) {
    Btn n = new Btn();
    n.label = b.label;
    n.icon = b.icon;
    n.key = b.key;
    n.action = b.action;
    n.mode = b.mode;
    n.color = b.color;
    n.w = b.w;
    n.h = b.h;
    return n;
  }

  private interface Choose<T> {
    void picked(T value);
  }

  private void pickAction(Choose<Action> done) {
    LinearLayout list = Ui.column(c);
    Map<String, List<Action>> byCategory = new LinkedHashMap<>();
    for (Action a : actions) {
      if (!byCategory.containsKey(a.category)) byCategory.put(a.category, new ArrayList<>());
      byCategory.get(a.category).add(a);
    }
    final AlertDialog[] dialog = new AlertDialog[1];
    for (Map.Entry<String, List<Action>> e : byCategory.entrySet()) {
      list.addView(Ui.text(c, categoryName(e.getKey()), 14, Ui.ACCENT, true), Ui.margins(Ui.matchWrap(), c, 4, 14, 0, 4));
      for (Action a : e.getValue()) {
        LinearLayout row = Ui.row(c);
        row.addView(new IconView(c, a.icon), new LinearLayout.LayoutParams(Ui.dp(c, 28), Ui.dp(c, 28)));
        String g = gameKeys.get(a.id);
        row.addView(Ui.text(c, a.label() + (g != null ? "   [" + keyLabel(g) + "]" : ""), 16, Ui.TEXT, false),
            Ui.margins(Ui.wrap(), c, 12, 0, 0, 0));
        int pad = Ui.dp(c, 10);
        row.setPadding(pad, pad, pad, pad);
        row.setClickable(true);
        row.setOnClickListener(v -> {
          dialog[0].dismiss();
          done.picked(a);
        });
        list.addView(row, Ui.matchWrap());
      }
    }
    ScrollView sv = new ScrollView(c);
    sv.addView(list);
    dialog[0] = new AlertDialog.Builder(c).setTitle(Ui.s(R.string.bb_function)).setView(sv)
        .setNegativeButton(Ui.s(R.string.bb_cancel), null).create();
    dialog[0].show();
  }

  private String categoryName(String category) {
    int id = c.getResources().getIdentifier("bb_cat_" + category, "string", c.getPackageName());
    return id != 0 ? Ui.s(id) : category;
  }

  private void pickIcon(Choose<String> done) {
    LinearLayout col = Ui.column(c);
    final AlertDialog[] dialog = new AlertDialog[1];
    List<String> ids = SvgIcons.ids(c);
    int per = 5;
    for (int i = 0; i < ids.size(); i += per) {
      LinearLayout row = Ui.row(c);
      for (int j = 0; j < per; j++) {
        if (i + j < ids.size()) {
          String id = ids.get(i + j);
          IconView v = new IconView(c, id);
          v.setOnClickListener(x -> {
            dialog[0].dismiss();
            done.picked(id);
          });
          row.addView(v, Ui.margins(new LinearLayout.LayoutParams(0, Ui.dp(c, 52), 1), c, 4, 4, 4, 4));
        } else {
          row.addView(new View(c), new LinearLayout.LayoutParams(0, Ui.dp(c, 52), 1));
        }
      }
      col.addView(row, Ui.matchWrap());
    }
    ScrollView sv = new ScrollView(c);
    sv.addView(col);
    dialog[0] = new AlertDialog.Builder(c).setTitle(Ui.s(R.string.bb_icon)).setView(sv)
        .setNegativeButton(Ui.s(R.string.bb_cancel), null).create();
    dialog[0].show();
  }

  /** The key picker: modifiers on top, then the keys of the game's own key names (an on-screen keyboard has no F-keys or numpad). */
  private void pickKey(String current, Choose<String> done) {
    final String[] mods = {"lshift", "lctrl", "lalt"};
    final boolean[] on = new boolean[mods.length];
    final String[] main = {""};
    String[] parts = current.isEmpty() ? new String[0] : current.split("\\+");
    for (String part : parts) {
      boolean isMod = false;
      for (int i = 0; i < mods.length; i++) {
        if (mods[i].equals(part)) {
          on[i] = true;
          isMod = true;
        }
      }
      if (!isMod) main[0] = part;
    }

    LinearLayout col = Ui.column(c);
    int pad = Ui.dp(c, 12);
    col.setPadding(pad, pad, pad, pad);
    TextView shown = Ui.text(c, 20, Ui.TEXT, true);
    col.addView(shown, Ui.margins(Ui.matchWrap(), c, 0, 0, 0, 8));
    final List<TextView> keyChips = new ArrayList<>();
    final List<TextView> modChips = new ArrayList<>();
    Runnable update = () -> {
      StringBuilder sb = new StringBuilder();
      for (int i = 0; i < mods.length; i++) if (on[i]) sb.append(keyLabel(mods[i])).append(" + ");
      sb.append(main[0].isEmpty() ? "…" : keyLabel(main[0]));
      shown.setText(sb);
      for (int i = 0; i < modChips.size(); i++) paintChip(modChips.get(i), on[i]);
      for (TextView t : keyChips) paintChip(t, t.getTag().equals(main[0]));
    };

    LinearLayout modRow = Ui.row(c);
    for (int i = 0; i < mods.length; i++) {
      final int index = i;
      TextView t = chip(keyLabel(mods[i]), false);
      t.setOnClickListener(v -> {
        on[index] = !on[index];
        update.run();
      });
      modChips.add(t);
      modRow.addView(t, Ui.margins(Ui.wrap(), c, 0, 0, 8, 8));
    }
    col.addView(modRow);

    List<String> keys = new ArrayList<>();
    for (char ch = 'a'; ch <= 'z'; ch++) keys.add(String.valueOf(ch));
    for (int i = 1; i <= 9; i++) keys.add("key" + i);
    keys.add("key0");
    for (int i = 1; i <= 12; i++) keys.add("f" + i);
    for (String k : new String[] {"space", "enter", "tab", "esc", "backspace", "uarrow", "darrow", "larrow", "rarrow", "home", "end",
        "pgup", "pgdn", "ins", "del", "lbracket", "rbracket", "semicolon", "apostrophe", "comma", "period", "slash", "backslash",
        "minus", "equal", "grave", "num0", "num1", "num2", "num3", "num4", "num5", "num6", "num7", "num8", "num9", "numplus",
        "numminus", "nummultiply", "numslash", "numenter", "numperiod"}) keys.add(k);
    int per = 6;
    for (int i = 0; i < keys.size(); i += per) {
      LinearLayout row = Ui.row(c);
      for (int j = 0; j < per; j++) {
        if (i + j < keys.size()) {
          String k = keys.get(i + j);
          TextView t = chip(keyLabel(k), false);
          t.setTag(k);
          t.setSingleLine(true);
          t.setOnClickListener(v -> {
            main[0] = k;
            update.run();
          });
          keyChips.add(t);
          row.addView(t, Ui.margins(new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1), c, 3, 3, 3, 3));
        } else {
          row.addView(new View(c), new LinearLayout.LayoutParams(0, 1, 1));
        }
      }
      col.addView(row, Ui.matchWrap());
    }
    update.run();
    ScrollView sv = new ScrollView(c);
    sv.addView(col);
    new AlertDialog.Builder(c).setTitle(Ui.s(R.string.bb_key)).setView(sv)
        .setPositiveButton(Ui.s(R.string.bb_save), (x, y) -> {
          StringBuilder sb = new StringBuilder();
          if (!main[0].isEmpty()) {
            for (int i = 0; i < mods.length; i++) if (on[i]) sb.append(mods[i]).append('+');
            sb.append(main[0]);
          }
          done.picked(sb.toString());
        })
        .setNeutralButton(Ui.s(R.string.bb_use_game_key), (x, y) -> done.picked(""))
        .setNegativeButton(Ui.s(R.string.bb_cancel), null)
        .show();
  }

  private void paintChip(TextView t, boolean on) {
    t.setTextColor(on ? Ui.ON_ACCENT : Ui.TEXT);
    t.setBackground(Ui.rounded(on ? Ui.ACCENT : Ui.CARD, Ui.dp(c, 20)));
  }

  /** The key name as a key cap shows it. */
  static String keyLabel(String key) {
    if (key.startsWith("key") && key.length() == 4) return key.substring(3);
    if (key.startsWith("num") && key.length() == 4) return "Num" + key.substring(3);
    switch (key) {
      case "uarrow": return "↑";
      case "darrow": return "↓";
      case "larrow": return "←";
      case "rarrow": return "→";
      case "lbracket": return "[";
      case "rbracket": return "]";
      case "semicolon": return ";";
      case "apostrophe": return "'";
      case "comma": return ",";
      case "period": return ".";
      case "slash": return "/";
      case "backslash": return "\\";
      case "minus": return "-";
      case "equal": return "=";
      case "grave": return "`";
      case "numplus": return "Num+";
      case "numminus": return "Num-";
      case "nummultiply": return "Num*";
      case "numslash": return "Num/";
      case "numenter": return "NumEnter";
      case "numperiod": return "Num.";
      case "lshift": return "Shift";
      case "lctrl": return "Ctrl";
      case "lalt": return "Alt";
      case "pgup": return "PgUp";
      case "pgdn": return "PgDn";
      case "ins": return "Ins";
      case "del": return "Del";
      case "esc": return "Esc";
      case "backspace": return "⌫";
      default:
        if (key.length() == 1 || key.matches("f\\d+")) return key.toUpperCase(Locale.ROOT);
        return key.substring(0, 1).toUpperCase(Locale.ROOT) + key.substring(1);
    }
  }

  /** A small view that draws one icon. */
  private final class IconView extends View {
    private final String id;
    private final Paint paint = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final RectF box = new RectF();

    IconView(Context c, String id) {
      super(c);
      this.id = id;
      paint.setStyle(Paint.Style.STROKE);
      paint.setStrokeCap(Paint.Cap.ROUND);
      paint.setStrokeJoin(Paint.Join.ROUND);
      paint.setColor(Ui.TEXT);
    }

    @Override
    protected void onDraw(Canvas canvas) {
      float s = Math.min(getWidth(), getHeight()) * 0.9f;
      box.set(getWidth() / 2f - s / 2, getHeight() / 2f - s / 2, getWidth() / 2f + s / 2, getHeight() / 2f + s / 2);
      SvgIcons.draw(getContext(), canvas, id, box, paint);
    }
  }
}

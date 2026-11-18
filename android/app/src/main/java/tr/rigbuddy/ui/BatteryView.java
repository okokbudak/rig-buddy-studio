package tr.rigbuddy.ui;

import android.content.Context;
import android.content.Intent;
import android.content.IntentFilter;
import android.graphics.Canvas;
import android.graphics.Paint;
import android.graphics.Path;
import android.graphics.RectF;
import android.graphics.Typeface;
import android.os.BatteryManager;
import android.view.View;

/** This device's own battery: a small battery icon filled to the level, a bolt while it charges, and the percentage beside it. */
public final class BatteryView extends View {
  private final Paint line = new Paint(Paint.ANTI_ALIAS_FLAG), fill = new Paint(Paint.ANTI_ALIAS_FLAG),
      text = new Paint(Paint.ANTI_ALIAS_FLAG);
  private final RectF body = new RectF(), level = new RectF(), tip = new RectF();
  private final Path bolt = new Path();
  private int percent = -1;
  private boolean charging;

  public BatteryView(Context c, float textSp) {
    super(c);
    line.setStyle(Paint.Style.STROKE);
    line.setStrokeWidth(Ui.dp(c, 1.5f));
    text.setTypeface(Typeface.create("sans-serif-medium", Typeface.NORMAL));
    text.setTextSize(Ui.dp(c, textSp));
    text.setTextAlign(Paint.Align.LEFT);
    setContentDescription("battery");
  }

  /** Reads the battery state (a sticky broadcast: no receiver to register) and redraws when it changed. */
  public void update() {
    Intent i = getContext().registerReceiver(null, new IntentFilter(Intent.ACTION_BATTERY_CHANGED));
    if (i == null) return;
    int lvl = i.getIntExtra(BatteryManager.EXTRA_LEVEL, -1), scale = i.getIntExtra(BatteryManager.EXTRA_SCALE, 100);
    int status = i.getIntExtra(BatteryManager.EXTRA_STATUS, -1);
    int p = lvl < 0 || scale <= 0 ? -1 : Math.round(lvl * 100f / scale);
    boolean ch = status == BatteryManager.BATTERY_STATUS_CHARGING || status == BatteryManager.BATTERY_STATUS_FULL
        || i.getIntExtra(BatteryManager.EXTRA_PLUGGED, 0) != 0;
    if (p != percent || ch != charging) {
      percent = p;
      charging = ch;
      setContentDescription("battery " + p + "%");
      invalidate();
    }
  }

  @Override
  protected void onMeasure(int w, int h) {
    setMeasuredDimension(MeasureSpec.getSize(w), Math.round(text.getTextSize() * 1.5f));
  }

  @Override
  protected void onDraw(Canvas canvas) {
    if (percent < 0) return;
    float h = getHeight(), bh = h * 0.62f, bw = bh * 1.9f, tipW = bh * 0.12f;
    String label = percent + "%";
    float gap = Ui.dp(getContext(), 5), total = bw + tipW + gap + text.measureText(label);
    float x = (getWidth() - total) / 2f, y = (h - bh) / 2f;
    int color = charging ? Ui.GREEN : percent <= 15 ? Ui.RED : percent <= 30 ? Ui.YELLOW : Ui.TEXT;
    line.setColor(Ui.TEXT2);
    body.set(x, y, x + bw, y + bh);
    float r = bh * 0.22f;
    canvas.drawRoundRect(body, r, r, line);
    tip.set(x + bw + 1, y + bh * 0.3f, x + bw + tipW, y + bh * 0.7f);
    fill.setColor(Ui.TEXT2);
    canvas.drawRoundRect(tip, 1, 1, fill);
    float pad = line.getStrokeWidth() * 1.6f;
    level.set(x + pad, y + pad, x + pad + (bw - 2 * pad) * Math.max(0.04f, percent / 100f), y + bh - pad);
    fill.setColor(color);
    canvas.drawRoundRect(level, r * 0.5f, r * 0.5f, fill);
    if (charging) {   // a bolt over the middle
      float cx = x + bw / 2, cy = y + bh / 2, s = bh * 0.28f;
      bolt.reset();
      bolt.moveTo(cx + s * 0.25f, cy - s * 1.1f);
      bolt.lineTo(cx - s * 0.55f, cy + s * 0.15f);
      bolt.lineTo(cx - s * 0.05f, cy + s * 0.15f);
      bolt.lineTo(cx - s * 0.25f, cy + s * 1.1f);
      bolt.lineTo(cx + s * 0.55f, cy - s * 0.2f);
      bolt.lineTo(cx + s * 0.05f, cy - s * 0.2f);
      bolt.close();
      fill.setColor(Ui.BG);
      canvas.drawPath(bolt, fill);
    }
    text.setColor(Ui.TEXT);
    Paint.FontMetrics fm = text.getFontMetrics();
    canvas.drawText(label, x + bw + tipW + gap, h / 2f - (fm.ascent + fm.descent) / 2f, text);
  }
}

using System.Globalization;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace RigBuddy.Ui;

/** Builds the editor controls; each one reads and writes a key of a JsonObject and calls `touch` after a change. */
static class Forms
{
    public static Style Sty(string key) => (Style)Application.Current.FindResource(key);
    public static Brush Brh(string key) => (Brush)Application.Current.FindResource(key);

    // ---- reading JSON loosely -------------------------------------------------------------------------------
    public static string Str(JsonNode? o, string key, string fb = "") => o?[key] is JsonValue v && v.TryGetValue<string>(out var s) ? s : o?[key]?.ToString() is { } t && o[key] is JsonValue ? t : fb;
    public static double? Dbl(JsonNode? o, string key) => o?[key] is JsonValue v && v.TryGetValue<double>(out var d) ? d : null;
    /** A JSON value itself as a number. */
    public static double Val(JsonNode? n, double fb = 0) => n is JsonValue v && v.TryGetValue<double>(out var d) ? d : fb;
    public static int Int(JsonNode? o, string key, int fb = 0) => Dbl(o, key) is { } d ? (int)Math.Round(d) : fb;
    public static bool Bool(JsonNode? o, string key, bool fb = false) => o?[key] is JsonValue v && v.TryGetValue<bool>(out var b) ? b : fb;

    // ---- layout ---------------------------------------------------------------------------------------------
    public static TextBlock Dim(string text, double top = 0, double bottom = 0) =>
        new() { Text = text, Style = Sty("Dim"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, top, 0, bottom) };

    public static TextBlock Head(string text) =>
        new() { Text = Upper(text), FontSize = 12, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 8) };

    /** Upper case the way the language writes it (Turkish: i -> İ, ı -> I). */
    public static string Upper(string text) =>
        Strings.Code == "tr" ? text.ToUpper(new CultureInfo("tr-TR")) : text.ToUpperInvariant();

    public static Border Card(params UIElement[] children)
    {
        var sp = new StackPanel();
        foreach (var c in children) if (c != null) sp.Children.Add(c);
        return new Border { Style = Sty("Panel"), Child = sp };
    }

    public static StackPanel Stack(params UIElement?[] children)
    {
        var sp = new StackPanel();
        foreach (var c in children) if (c != null) sp.Children.Add(c);
        return sp;
    }

    public static WrapPanel Wrap(params UIElement?[] children)
    {
        var w = new WrapPanel();
        foreach (var c in children) if (c != null) w.Children.Add(c);
        return w;
    }

    public static StackPanel Line(params UIElement?[] children)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var c in children) if (c != null) sp.Children.Add(c);
        return sp;
    }

    public static FrameworkElement Field(string label, FrameworkElement input, double width = 180, string? hint = null)
    {
        if (width > 0) input.Width = width;
        input.HorizontalAlignment = HorizontalAlignment.Left;
        var p = new StackPanel { Margin = new Thickness(0, 0, 16, 10) };
        p.Children.Add(new TextBlock { Text = label, Style = Sty("Dim"), Margin = new Thickness(0, 0, 0, 4) });
        p.Children.Add(input);
        if (hint != null) p.Children.Add(new TextBlock { Text = hint, Style = Sty("Dim"), FontSize = 11, TextWrapping = TextWrapping.Wrap, MaxWidth = Math.Max(width, 220), Margin = new Thickness(0, 3, 0, 0) });
        return p;
    }

    public static Button Btn(string text, Action click, bool primary = false, bool danger = false)
    {
        var b = new Button { Content = text, Margin = new Thickness(0, 0, 8, 0), HorizontalAlignment = HorizontalAlignment.Left };
        if (primary) b.Style = Sty("Primary"); else if (danger) b.Style = Sty("Danger");
        b.Click += (_, _) => click();
        return b;
    }

    public static Border Pill(string text, Brush color) =>
        new() { Background = color, CornerRadius = new CornerRadius(2), Padding = new Thickness(7, 1, 7, 2), Child = new TextBlock { Text = text.Length <= 28 ? Upper(text) : text, Foreground = Brh("OnAccent"), FontSize = 10, FontWeight = FontWeights.Bold }, VerticalAlignment = VerticalAlignment.Center };

    // ---- bound inputs ---------------------------------------------------------------------------------------
    public static TextBox Text(JsonObject o, string key, Action touch, bool mono = false, string? placeholder = null, Action? after = null)
    {
        var tb = new TextBox { Text = Str(o, key) };
        if (mono) tb.FontFamily = (FontFamily)Application.Current.FindResource("MonoFont");
        if (placeholder != null) tb.ToolTip = placeholder;
        tb.TextChanged += (_, _) => { o[key] = tb.Text; touch(); after?.Invoke(); };
        return tb;
    }

    /** A number box; an empty box stores null. `integer` rounds, `min`/`max` clamp when the box loses focus. */
    public static TextBox Num(JsonObject o, string key, Action touch, double? min = null, double? max = null, bool integer = false, Action? after = null)
    {
        var tb = new TextBox { Text = Dbl(o, key) is { } d ? d.ToString(CultureInfo.InvariantCulture) : "" };
        tb.TextChanged += (_, _) =>
        {
            string t = tb.Text.Trim().Replace(',', '.');
            if (t.Length == 0) { o[key] = null; touch(); after?.Invoke(); return; }
            if (!double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var n)) return;
            if (min != null) n = Math.Max(min.Value, n);
            if (max != null) n = Math.Min(max.Value, n);
            if (integer) n = Math.Round(n);
            o[key] = JsonValue.Create(n);
            touch();
            after?.Invoke();
        };
        return tb;
    }

    public static CheckBox Check(JsonObject o, string key, string label, Action touch, bool fb = false, Action? after = null)
    {
        var c = new CheckBox { Content = label, IsChecked = Bool(o, key, fb), Margin = new Thickness(0, 4, 18, 8) };
        c.Click += (_, _) => { o[key] = c.IsChecked == true; touch(); after?.Invoke(); };
        return c;
    }

    public static ComboBox Pick(JsonObject o, string key, IEnumerable<(string Value, string Label)> items, Action touch, string fb = "", Action? after = null) =>
        PickText(Str(o, key, fb), items, v => { o[key] = v; touch(); after?.Invoke(); });

    public static ComboBox PickText(string current, IEnumerable<(string Value, string Label)> items, Action<string> set)
    {
        var cb = new ComboBox();
        var list = items.ToList();
        if (!list.Any(i => i.Value == current)) list.Add((current, current));
        foreach (var (v, l) in list) cb.Items.Add(new ComboBoxItem { Content = l, Tag = v });
        cb.SelectedItem = cb.Items.OfType<ComboBoxItem>().First(i => (string)i.Tag == current);
        cb.SelectionChanged += (_, _) => { if (cb.SelectedItem is ComboBoxItem it) set((string)it.Tag); };
        return cb;
    }

    /** A slider with its value next to it; `live` runs while dragging, `touch` on every change. */
    public static FrameworkElement Slide(JsonObject o, string key, double min, double max, Action touch, double fb, Action? live = null, double width = 160, Func<double, string>? fmt = null)
    {
        var s = new Slider { Minimum = min, Maximum = max, Value = Dbl(o, key) ?? fb, Width = width, VerticalAlignment = VerticalAlignment.Center, IsMoveToPointEnabled = true };
        var val = new TextBlock { Width = 44, Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Style = Sty("Dim") };
        string F(double v) => fmt != null ? fmt(v) : Math.Round(v).ToString(CultureInfo.InvariantCulture);
        val.Text = F(s.Value);
        s.ValueChanged += (_, e) => { o[key] = Math.Round(e.NewValue, 2); val.Text = F(e.NewValue); touch(); live?.Invoke(); };
        return Line(s, val);
    }

    // ---- colour ---------------------------------------------------------------------------------------------
    static readonly string[] Swatches =
    [
        "#ff0000", "#ff6a00", "#ff9329", "#ffd000", "#a0ff00", "#00c000", "#00ffa0", "#00ffff", "#0080ff", "#0000ff", "#7000ff", "#ff00ff",
        "#ff0080", "#ffffff", "#c0c0c0", "#808080", "#000000", "#ffb070", "#a0e0ff", "#b5004b",
    ];

    public static Color Parse(string hex)
    {
        try { return (Color)ColorConverter.ConvertFromString(hex.StartsWith('#') ? hex : "#" + hex); } catch { return Colors.Black; }
    }

    /** A swatch that opens a palette plus a hex box. Stores "#rrggbb". */
    public static FrameworkElement ColorPick(JsonObject o, string key, Action touch, Action? live = null)
    {
        string cur = Str(o, key, "#000000");
        var sw = new Border { Width = 34, Height = 28, CornerRadius = new CornerRadius(5), BorderBrush = Brh("Line"), BorderThickness = new Thickness(1), Background = new SolidColorBrush(Parse(cur)), Cursor = Cursors.Hand };
        var hex = new TextBox { Text = cur, Width = 86, FontFamily = (FontFamily)Application.Current.FindResource("MonoFont"), Margin = new Thickness(8, 0, 0, 0) };
        void Apply(string v, bool fromBox)
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(v, "^#[0-9a-fA-F]{6}$")) return;
            v = v.ToLowerInvariant();
            sw.Background = new SolidColorBrush(Parse(v));
            if (!fromBox) hex.Text = v;
            o[key] = v;
            touch();
            live?.Invoke();
        }
        hex.TextChanged += (_, _) => Apply(hex.Text.Trim(), true);

        var grid = new WrapPanel { Width = 6 * 32 };
        var pop = new Popup { PlacementTarget = sw, Placement = PlacementMode.Bottom, StaysOpen = false, AllowsTransparency = true };
        foreach (var c in Swatches)
        {
            string col = c;
            var cell = new Border { Width = 26, Height = 26, Margin = new Thickness(3), CornerRadius = new CornerRadius(4), Background = new SolidColorBrush(Parse(col)), Cursor = Cursors.Hand, BorderBrush = Brh("Line"), BorderThickness = new Thickness(1) };
            cell.MouseLeftButtonUp += (_, _) => { Apply(col, false); pop.IsOpen = false; };
            grid.Children.Add(cell);
        }
        pop.Child = new Border { Background = Brh("Card"), BorderBrush = Brh("Line"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Padding = new Thickness(6), Child = grid };
        sw.MouseLeftButtonUp += (_, _) => pop.IsOpen = true;
        return Line(sw, hex);
    }
}

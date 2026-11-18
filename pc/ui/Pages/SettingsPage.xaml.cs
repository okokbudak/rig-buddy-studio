using System.Windows;
using System.Windows.Controls;

namespace RigBuddy.Ui.Pages;

/** Game and map mods, language, start with Windows, updates, forgetting phones, sound mods. */
public partial class SettingsPage : UserControl
{
    readonly IHostBridge _host;
    Prefs? _shown;
    string? _mapKey;

    public SettingsPage(IHostBridge host)
    {
        _host = host;
        InitializeComponent();
    }

    public void Localize() { _shown = null; _mapKey = null; }

    public void Apply(HostSnapshot s)
    {
        // rebuilt only when something changed: a rebuild while a list is open would close it
        var p = s.Prefs;
        string key = $"{p.Game}|{p.ModCount}|{p.MapBuilding}|{p.Autostart}|{p.UpdateCheck}|{p.Language}|{p.UpdateVersion}|{_host.ThemeSetting}|{s.MapText}|{s.MapBusy}|{s.MapCanBuild}|{s.MapCanUpdate}|{Strings.Code}";
        if (key == _mapKey) { Progress?.SetValue(ProgressBar.ValueProperty, (double)s.MapPercent); return; }
        _mapKey = key;
        Build(s);
    }

    ProgressBar? Progress;

    void Build(HostSnapshot s)
    {
        var p = s.Prefs;
        Root.Children.Clear();
        string T(string k, params object?[] a) => Strings.T(k, a);
        Root.Children.Add(new TextBlock { Text = Strings.B("Ayarlar", "Settings"), Style = (Style)FindResource("H1"), Margin = new Thickness(0, 0, 0, 16) });

        // game and map
        var game = Forms.PickText(p.Game, new[] { ("", Strings.B("İkisi de", "Both")), ("ets2", "Euro Truck Simulator 2"), ("ats", "American Truck Simulator") }, v => _host.SetGame(v));
        game.IsEnabled = !p.MapBuilding;
        var mods = Forms.Line(
            new TextBlock { Text = T("tray.mods_count", p.ModCount), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 14, 0) },
            Forms.Btn(T("tray.mods_pick"), _host.PickMods), Forms.Btn(T("tray.mods_clear"), _host.ClearMods));
        ((Button)mods.Children[1]).IsEnabled = !p.MapBuilding;
        ((Button)mods.Children[2]).IsEnabled = !p.MapBuilding && p.ModCount > 0;
        var bar = new ProgressBar { Maximum = 100, Value = s.MapPercent, Margin = new Thickness(0, 4, 0, 10), Visibility = s.MapBusy ? Visibility.Visible : Visibility.Collapsed };
        Progress = bar;
        var build = Forms.Btn(Strings.T(s.MapCanUpdate ? "sys.update" : "sys.build"), () => _host.BuildMap(s.MapCanUpdate), primary: true);
        build.Visibility = s.MapCanBuild || s.MapCanUpdate ? Visibility.Visible : Visibility.Collapsed;
        Root.Children.Add(Forms.Card(Forms.Stack(
            Forms.Head(Strings.B("Oyun ve harita", "Game and map")),
            Forms.Field(T("tray.game"), game, 280),
            Forms.Dim(Strings.B("Harita verisi yalnızca seçilen oyun için üretilir.", "Map data is built only for the chosen game."), 0, 8),
            new TextBlock { Text = T("tray.mods"), Style = Forms.Sty("Dim"), Margin = new Thickness(0, 0, 0, 4) }, mods,
            new TextBlock { Text = s.MapText, Margin = new Thickness(0, 12, 0, 4), TextWrapping = TextWrapping.Wrap },
            bar, build)));

        // general
        var lang = Forms.PickText(p.Language, _host.Languages.Select(l => (l.Code, l.Name)), v => _host.SetLanguage(v));
        var theme = Forms.PickText(_host.ThemeSetting, _host.Themes, v => _host.SetTheme(v));
        var auto = new CheckBox { Content = T("win.autostart"), IsChecked = p.Autostart, Margin = new Thickness(0, 0, 0, 8) };
        auto.Click += (_, _) => _host.SetAutostart(auto.IsChecked == true);
        var upd = new CheckBox { Content = T("update.check"), IsChecked = p.UpdateCheck, ToolTip = T("update.check_hint"), Margin = new Thickness(0), VerticalAlignment = VerticalAlignment.Center };
        upd.Click += (_, _) => _host.SetUpdateCheck(upd.IsChecked == true);
        var updNote = new TextBlock { Style = Forms.Sty("Dim"), VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap, MaxWidth = 360 };
        var updNow = new Button { Content = Strings.B("Şimdi denetle", "Check updates now"), Margin = new Thickness(14, 0, 10, 0) };
        updNow.Click += async (_, _) =>
        {
            updNow.IsEnabled = false;
            updNote.Text = Strings.B("Denetleniyor…", "Checking…");
            UpdateResult r;
            try { r = await _host.CheckUpdatesNow(); }
            catch (Exception e) { r = new UpdateResult("error", e.Message); }
            updNote.Text = r.Kind switch
            {
                "newer" => Strings.B("Yeni sürüm var: {0}", "New version available: {0}", r.Version),
                "current" => Strings.B("Güncelsin ({0}).", "You are up to date ({0}).", r.Version),
                _ => Strings.B("Denetlenemedi: {0}", "Could not check: {0}", r.Version ?? ""),
            };
            updNow.IsEnabled = true;
        };
        var updLine = Forms.Line(upd, updNow, updNote);
        updLine.Margin = new Thickness(0, 0, 0, 4);
        Root.Children.Add(Forms.Card(Forms.Stack(
            Forms.Head(Strings.B("Genel", "General")),
            Forms.Field(T("tray.language"), lang, 220), Forms.Field(T("tray.theme"), theme, 220), auto, updLine,
            Forms.Dim(T("update.check_hint"), 0, 8),
            p.UpdateVersion != null ? Forms.Btn(T("update.balloon", p.UpdateVersion), _host.ShowUpdate, primary: true) : null)));

        // wheel buttons: next / previous screen on the tablets
        FrameworkElement WheelRow(string action, string title)
        {
            var label = new TextBlock { Width = 260, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
            // ClearValue, not Style = null: a null style also drops the theme's text color (black text on the dark card)
            void Show(string? l) { label.Text = l ?? Strings.B("atanmadı", "not set"); if (l == null) label.Style = Forms.Sty("Dim"); else label.ClearValue(FrameworkElement.StyleProperty); }
            Show(_host.WheelLabel(action));
            var learn = new Button { Content = Strings.B("Ata…", "Assign…"), Margin = new Thickness(0, 0, 8, 0) };
            var clear = new Button { Content = Strings.B("Temizle", "Clear") };
            learn.Click += async (_, _) =>
            {
                learn.IsEnabled = clear.IsEnabled = false;
                label.ClearValue(FrameworkElement.StyleProperty);
                label.Text = Strings.B("Direksiyonda bir tuşa bas… (10 sn)", "Press a button on the wheel… (10 s)");
                var got = await _host.WheelLearn(action);
                Show(got ?? _host.WheelLabel(action));
                learn.IsEnabled = clear.IsEnabled = true;
            };
            clear.Click += (_, _) => { _host.WheelClear(action); Show(null); };
            var row = Forms.Line(new TextBlock { Text = title, Width = 150, VerticalAlignment = VerticalAlignment.Center }, label, learn, clear);
            row.Margin = new Thickness(0, 0, 0, 8);
            return row;
        }
        Root.Children.Add(Forms.Card(Forms.Stack([
            Forms.Head(Strings.B("Direksiyon tuşları", "Wheel buttons")),
            Forms.Dim(Strings.B("Direksiyonundaki, tuş kutundaki ya da Windows'un tanıdığı herhangi bir oyun kumandasındaki tuşları tabletin ekran geçişine ata. Oyun öndeyken de çalışır; direksiyon simidindekiler dahil her tuş kullanılabilir.",
                "Bind buttons of your wheel, button box or any game controller Windows knows to the tablet's screen switching. Works with the game in front; every button can be used, the wheel rim's too."), 0, 10),
            .. _host.WheelActions.Select(a => WheelRow(a, WheelTitle(a)))])));

        // devices, sound, logs
        Root.Children.Add(Forms.Card(Forms.Stack(
            Forms.Head(Strings.B("Araçlar", "Tools")),
            Forms.Line(Forms.Btn(T("tray.logs"), _host.ShowLogs)),
            Forms.Dim(T("tray.forget_hint"), 10, 6),
            Forms.Btn(T("tray.forget"), () =>
            {
                if (MessageBox.Show(T("tray.forget_ask"), "Rig Buddy", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes) _host.ForgetDevices();
            }, danger: true))));
    }

    /** "wheel.tablet.next" and so on in Strings. */
    static string WheelTitle(string action) => Strings.T("wheel." + action);
}

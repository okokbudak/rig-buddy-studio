using System.ComponentModel;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using RigBuddy.Ui.Pages;

namespace RigBuddy.Ui;

/**
 * Rig Buddy Studio's one window. Closing it only hides it; the app keeps running in the tray.
 * (The dashboard hardware has a program and a window of its own: RigHardware.exe, rig-buddy-hardware.)
 */
public partial class MainWindow : Window
{
    readonly IHostBridge _host;
    readonly LiveClient _liveClient;
    readonly OverviewPage _overview;
    readonly SoundPage _sound;
    readonly InspectorPanel _inspector; // made in the constructor: it needs the theme resources UiHost.Init() loads
    bool _inspectorWanted = true;
    readonly Dictionary<string, string> _svcState = new();
    string _lastHeadline = "", _lastAttention = "";
    int _lastClients = -1;
    readonly SettingsPage _settings;
    readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    TimeSpan _cpuLast; DateTime _cpuAt = DateTime.UtcNow;
    bool _exiting, _game;

    public MainWindow(IHostBridge host)
    {
        UiHost.Init();
        _host = host;
        Strings.Code = host.Language;
        InitializeComponent();

        // whether the game sends data (the top bar, the event log): from the agent, the service the tablets use too
        _liveClient = new LiveClient(host.AgentPort);
        _liveClient.Watch(["truck.speed.kph"]); // any one name: the push carries status.game
        _liveClient.Pushed += n => Dispatcher.BeginInvoke(() => OnPush(n));

        _overview = new OverviewPage(host);
        _sound = new SoundPage(host);
        _settings = new SettingsPage(host);
        Strings.Host = (k, a) => host.Text(k, a);
        _inspector = new InspectorPanel();
        InspectorHost.Child = _inspector;
        SizeChanged += (_, _) => ApplyInspector();
        Host.Content = _overview;
        Localize();

        _timer.Tick += (_, _) => Tick();
        _timer.Start();
        Tick();
    }

    /** Re-applies texts after a language change. */
    public void Localize()
    {
        Strings.Code = _host.Language;
        Version.Text = _host.Version.Split('·').Last().Trim();
        if (string.IsNullOrEmpty(TelemText.Text))
        {
            TelemText.Text = Strings.B("TELEMETRİ: ", "TELEMETRY: ") + Strings.B("BEKLİYOR", "WAITING"); TelemDot.Fill = HealthBrush.Of(Health.Idle);
        }
        GroupHome.Text = Strings.B("ANA SAYFA", "HOME");
        GroupSys.Text = Strings.B("SİSTEM", "SYSTEM");
        NavOverview.Content = Nav("\uE80F", Strings.T("nav.overview"));
        NavSound.Content = Nav("\uE767", Strings.B("Ses modları", "Sound mods"));
        NavSettings.Content = Nav("\uE713", Strings.B("Ayarlar", "Settings"));
        _overview.Localize();
        _sound.Localize();
        _inspector.Localize();
        InspectorButton.Content = Strings.B("OLAYLAR", "EVENTS");
        _settings.Localize();
    }

    /** A menu entry: icon glyph and text; both take the colour of the menu button (white-on-orange when open). */
    internal static StackPanel Nav(string glyph, string text)
    {
        TextBlock T(string t, bool icon)
        {
            var tb = new TextBlock { Text = t, VerticalAlignment = VerticalAlignment.Center };
            if (icon) { tb.Style = Forms.Sty("Icon"); tb.Width = 24; }
            tb.SetBinding(TextBlock.ForegroundProperty, new System.Windows.Data.Binding("Foreground") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.FindAncestor, typeof(RadioButton), 1) });
            return tb;
        }
        return new StackPanel { Orientation = Orientation.Horizontal, Children = { T(glyph, true), T(text, false) } };
    }

    /** The inspector shows when wanted and when the window is wide enough (it folds away below ~1100 px). */
    void ApplyInspector() => InspectorHost.Visibility = _inspectorWanted && ActualWidth >= 1100 ? Visibility.Visible : Visibility.Collapsed;

    void OnInspector(object sender, RoutedEventArgs e) { _inspectorWanted = !_inspectorWanted; ApplyInspector(); }

    /** The tray menu's "Sound mods": the page opens in this window. */
    public void ShowSoundPage() => NavSound.IsChecked = true;

    void OnBanner(object sender, RoutedEventArgs e) => _host.FixAttention();

    void OnNav(object sender, RoutedEventArgs e)
    {
        if (Host == null) return;
        if (sender == NavSound) _sound.OnShown();
        object page = sender == NavSound ? _sound : sender == NavSettings ? _settings : _overview;
        Host.Content = page;
    }

    void Tick()
    {
        var s = _host.Snapshot();
        StatusText.Text = s.Headline;
        StatusDot.Fill = HealthBrush.Of(s.HeadlineHealth);
        {
            var proc = System.Diagnostics.Process.GetCurrentProcess();
            var now = DateTime.UtcNow;
            double cpu = _cpuLast == TimeSpan.Zero ? 0 : (proc.TotalProcessorTime - _cpuLast).TotalSeconds / Math.Max(0.001, (now - _cpuAt).TotalSeconds) / Environment.ProcessorCount * 100;
            _cpuLast = proc.TotalProcessorTime; _cpuAt = now;
            PerfText.Text = $"MEM: {proc.WorkingSet64 / 1048576} MB  ·  CPU: {cpu:0.0}%";
        }
        ClientsText.Text = Strings.T("sys.clients", s.Clients);
        GameText.Text = s.Prefs.Game.ToUpperInvariant();
        _overview.Apply(s, _game);
        foreach (var row in s.Services)
            if (!_svcState.TryGetValue(row.Name, out var ps) || ps != row.State)
            {
                _svcState[row.Name] = row.State;
                _inspector.Log("SVC", $"{row.Title}: {row.State}", row.Health == Health.Bad ? Health.Bad : row.Health == Health.Ok ? Health.Ok : Health.Idle);
            }
        if (s.Headline != _lastHeadline) { _lastHeadline = s.Headline; _inspector.Log("SYS", s.Headline + (s.Detail.Length > 0 && s.HeadlineHealth != Health.Ok ? $" · {s.Detail}" : ""), s.HeadlineHealth == Health.Bad ? Health.Bad : s.HeadlineHealth == Health.Ok ? Health.Ok : Health.Idle); }
        if (s.Clients != _lastClients) { if (_lastClients >= 0) _inspector.Log("NET", Strings.T("sys.clients", s.Clients)); _lastClients = s.Clients; }
        string att = s.Attention?.Text ?? "";
        if (att != _lastAttention) { _lastAttention = att; if (att.Length > 0) _inspector.Log("SYS", att, s.Attention!.Health); }
        _settings.Apply(s);
        if (s.Attention is { } a)
        {
            Banner.Visibility = Visibility.Visible;
            BannerText.Text = a.Text;
            Banner.BorderBrush = HealthBrush.Of(a.Health);
            BannerButton.Visibility = a.Button != null ? Visibility.Visible : Visibility.Collapsed;
            BannerButton.Content = a.Button;
            BannerButton.IsEnabled = !a.Busy;
        }
        else Banner.Visibility = Visibility.Collapsed;
    }

    /** A push from the agent ({status: {game}, ...}): whether the game sends data, for the top bar and the event log. */
    void OnPush(JsonNode push)
    {
        bool game = (bool?)push["status"]?["game"] ?? false;
        if (game != _game) _inspector.Log("GAME", Strings.B(game ? "Oyundan veri geliyor" : "Oyun verisi kesildi", game ? "Receiving game data" : "Game data stopped"), game ? Health.Ok : Health.Idle);
        _game = game;
        TelemText.Text = Strings.B("TELEMETRİ: ", "TELEMETRY: ") + Strings.B(game ? "ÇALIŞIYOR" : "BEKLİYOR", game ? "RUNNING" : "WAITING");
        TelemDot.Fill = HealthBrush.Of(game ? Health.Ok : Health.Idle);
    }

    public void ShowInFront()
    {
        if (!IsVisible) Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
    }

    public void CloseForExit()
    {
        _exiting = true;
        _timer.Stop();
        _liveClient.Dispose();
        Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_exiting) { e.Cancel = true; Hide(); }
        base.OnClosing(e);
    }
}

// The native Rig Buddy Studio window (pc/ui, WPF) as the tray host uses it: replaces the old WinForms status
// window. The window itself knows nothing about this host; HostBridge
// below answers what it asks (state of the services, the map build, the PC's address) and does what it requests.

using RigBuddy.Ui;

sealed class StudioWindow : IDisposable
{
    readonly MainWindow _w;

    /** Raised when the user closes the window (the app keeps running). */
    public event Action? HiddenToTray;

    public StudioWindow(Supervisor sup, TelemetryBridge bridge, MapBuilder maps, TrayApp app)
    {
        _w = new MainWindow(new HostBridge(sup, bridge, maps, app));
        _w.IsVisibleChanged += (_, e) => { if (e.NewValue is false) HiddenToTray?.Invoke(); };
        // a WPF window inside this WinForms message loop: keys (Tab, arrows, text boxes) need this
        System.Windows.Forms.Integration.ElementHost.EnableModelessKeyboardInterop(_w);
        L.Changed += OnLanguage;
        UiHost.ApplyTheme(Theme.Current.Dark);
        _w.SourceInitialized += (_, _) => TitleBar();
        Theme.Changed += OnTheme;
    }

    void OnTheme() => _w.Dispatcher.BeginInvoke(() => { UiHost.ApplyTheme(Theme.Current.Dark); TitleBar(); });
    void TitleBar() { var h = new System.Windows.Interop.WindowInteropHelper(_w).Handle; if (h != IntPtr.Zero) Theme.ApplyTitleBar(h); }

    void OnLanguage() => _w.Dispatcher.BeginInvoke(_w.Localize);

    public bool Visible => _w.IsVisible;
    public void ShowInFront() => _w.ShowInFront();
    /** The Sound mods page of the window (the tray menu entry). */
    public void ShowSound() { _w.ShowInFront(); _w.ShowSoundPage(); }
    public void CloseForExit() { L.Changed -= OnLanguage; Theme.Changed -= OnTheme; _w.CloseForExit(); }
    public void Dispose() { }
}

sealed class HostBridge(Supervisor sup, TelemetryBridge bridge, MapBuilder maps, TrayApp app) : IHostBridge
{
    // plugin / network check (ported from the old status window)
    DateTime _nextPluginCheck, _nextNetCheck;
    bool _netChecking, _restartGame, _fixBusy;
    string? _fixFailed;
    volatile NetworkCheck.Problem? _netProblem;
    bool _fixForNetwork;
    List<PluginCheck.Problem> _plugin = new();

    public string Version => L.T("win.subtitle", Application.ProductVersion.Split('+')[0]);
    public string Language => L.Code;
    public int AgentPort => 62843;

    Attention? FindAttention()
    {
        if (DateTime.UtcNow >= _nextPluginCheck)
        {
            _nextPluginCheck = DateTime.UtcNow.AddSeconds(3);
            _plugin = PluginCheck.Find();
            if (!_netChecking && DateTime.UtcNow >= _nextNetCheck)
            {
                _nextNetCheck = DateTime.UtcNow.AddSeconds(30);
                _netChecking = true;
                Task.Run(() => { try { _netProblem = NetworkCheck.FindPublicLan(); } finally { _netChecking = false; } });
            }
        }
        _fixForNetwork = false;
        if (_plugin.Count > 0)
            return new Attention(_fixFailed ?? L.T("plugin.missing", string.Join(", ", _plugin.Select(x => x.Game))), Health.Bad, L.T("plugin.fix"), _fixBusy);
        if (_restartGame && PluginCheck.GameRunning())
            return new Attention(L.T("plugin.restart"), Health.Busy, null, false);
        if (_netProblem is { } net)
        {
            _fixForNetwork = true;
            return new Attention(L.T("net.public", net.Name), Health.Busy, L.T("net.fix"), _fixBusy);
        }
        _restartGame = false;
        _fixFailed = null;
        return null;
    }

    public async void FixAttention()
    {
        if (_fixBusy) return;
        _fixBusy = true;
        try
        {
            if (_fixForNetwork && _netProblem is { } net)
            {
                await Task.Run(() => NetworkCheck.MakePrivate(net));
                _nextNetCheck = DateTime.MinValue;
            }
            else
            {
                bool ok = await Task.Run(PluginCheck.Fix);
                _restartGame = ok && PluginCheck.GameRunning();
                _fixFailed = ok ? null : L.T(PluginCheck.GameRunning() ? "plugin.closegame" : "plugin.failed");
            }
        }
        finally { _fixBusy = false; _nextPluginCheck = DateTime.MinValue; }
    }

    public string Text(string key, params object?[] args) => args.Length == 0 ? L.T(key) : L.T(key, args);
    public IReadOnlyList<(string Code, string Name)> Languages => new[] { ("system", L.T("lang.system")) }.Concat(L.Languages).ToList();
    public void SetGame(string game) { if (maps.State != MapState.Building) maps.SelectGame(game); }
    public void PickMods() => app.PickMapMods();
    public void ClearMods() { MapMods.Paths = []; maps.Check(); }
    public void SetLanguage(string code) => L.Setting = code;
    public string ThemeSetting => Theme.Setting;
    public IReadOnlyList<(string Code, string Name)> Themes => Theme.Choices.Select(c => (c.Value, L.T(c.Label))).ToList();
    public void SetTheme(string code) => Theme.Setting = code;
    public void SetAutostart(bool on) => TrayApp.SetAutostart(on);
    public void SetUpdateCheck(bool on)
    {
        UpdateCheck.Enabled = on; // off: nothing is sent to GitHub (the agent is told at its next start)
        sup.Restart("agent");
        if (on) UpdateCheck.CheckNow();
    }
    public async void ForgetDevices() => await sup.ForgetDevices();
    public void OpenSound() => app.ShowSound();
    public IReadOnlyList<string> WheelActions => WheelButtons.Actions;
    public string? WheelLabel(string action) => WheelButtons.Label(action);
    public Task<string?> WheelLearn(string action) => WheelButtons.Learn(action, TimeSpan.FromSeconds(10));
    public void WheelClear(string action) => WheelButtons.Clear(action);
    public async Task<UpdateResult> CheckUpdatesNow()
    {
        var (kind, version) = await UpdateCheck.CheckNowAsync();
        return new UpdateResult(kind, version);
    }

    // ---- sound mods ----
    List<SoundMods.Game>? _soundGames;
    List<SoundMods.Game> SoundGamesList => _soundGames ??= SoundMods.Games();
    SoundMods.Game? SoundGame(int i) => i >= 0 && i < SoundGamesList.Count ? SoundGamesList[i] : null;
    public IReadOnlyList<string> SoundGames() { _soundGames = null; return SoundGamesList.Select(g => g.Name).ToList(); }
    public bool SoundEnabled => SoundMods.Enabled;
    public string SoundSetEnabled(bool on)
    {
        SoundMods.Enabled = on;
        return SoundMods.Sync() == 0 ? L.T("sound.synced") : L.T("sound.sync_failed");
    }
    public SoundState SoundLoad(int game)
    {
        if (SoundGame(game) is not { } g) return new SoundState([], SoundMods.LevelKeys.Select(k => k.Default).ToList(), "");
        var banks = SoundMods.Banks(g);
        // names in selected.bank.txt without a .bank file (the plugin's placeholder, a removed mod) are not shown
        var selected = SoundMods.Selected(g).Where(s => banks.Contains(s, StringComparer.OrdinalIgnoreCase)).ToList();
        var mods = selected.Select(n => new SoundMod(n, true, SoundMods.HasGuids(g, n)))
            .Concat(banks.Where(b => !selected.Contains(b, StringComparer.OrdinalIgnoreCase)).Select(n => new SoundMod(n, false, SoundMods.HasGuids(g, n))))
            .ToList();
        var levels = SoundMods.ReadLevels(g);
        string warning = SoundMods.ForeignPlugin(g) ? L.T("sound.foreign") : banks.Count == 0 ? L.T("sound.empty") : "";
        // the layers the plugin found, when they belong to the mods in use (the plugin's own order: the ones it could load)
        var found = SoundMods.Layers(g, out string layerMods);
        var layers = layerMods == string.Join("+", selected)
            ? found.Select(l => new SoundLayer(l.Id, l.Event, l.Sounds, l.Turbo, SoundMods.LayerLevel(g, layerMods + "|" + l.Id))).ToList()
            : [];
        return new SoundState(mods, SoundMods.LevelKeys.Select(k => levels[k.Key]).ToList(), warning, layers);
    }
    public void SoundSetLayer(int game, string id, double value)
    {
        if (SoundGame(game) is not { } g) return;
        SoundMods.Layers(g, out string layerMods);
        SoundMods.SetLayerLevel(g, layerMods + "|" + id, value);
    }
    public void SoundSetLevel(int game, int index, double value)
    {
        if (SoundGame(game) is { } g && index >= 0 && index < SoundMods.LevelKeys.Length) SoundMods.SetLevel(g, SoundMods.LevelKeys[index].Key, value);
    }
    public SoundResult SoundAdd(int game, IReadOnlyList<string> files)
    {
        if (SoundGame(game) is not { } g) return new SoundResult(L.T("sound.nogame"), []);
        var chosen = new List<string>();
        string? problem = null;
        foreach (string f in files)
        {
            try
            {
                var added = SoundMods.AddMod(g, f);
                if (added.Count == 0) problem = L.T("sound.none_in_scs", Path.GetFileName(f));
                chosen.AddRange(added);
            }
            catch (InvalidDataException) { problem = L.T("sound.not_zip", Path.GetFileName(f)); }
            catch (Exception e) { problem = e.Message; }
        }
        var noGuids = chosen.Where(n => !SoundMods.HasGuids(g, n)).ToList();
        string message = noGuids.Count > 0 ? L.T("sound.no_guids", string.Join(", ", noGuids)) : problem ?? "";
        return new SoundResult(message, chosen);
    }
    public string SoundRemove(int game, string name)
    {
        if (SoundGame(game) is not { } g) return "";
        try { SoundMods.RemoveMod(g, name); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return L.T("sound.remove_failed", name); }
        catch (Exception e) { return e.Message; }
        return L.T("sound.removed", name);
    }
    public string SoundSave(int game, IReadOnlyList<string> names)
    {
        if (SoundGame(game) is not { } g) return "";
        try
        {
            SoundMods.SaveSelected(g, names);
            var noGuids = names.Where(n => !SoundMods.HasGuids(g, n)).ToList();
            return noGuids.Count > 0 ? L.T("sound.no_guids", string.Join(", ", noGuids)) : L.T("sound.saved");
        }
        catch (Exception e) { return e.Message; }
    }
    public void SoundOpenFolder(int game) { if (SoundGame(game) is { } g) TrayApp.OpenPath(SoundMods.FolderOf(g)); }
    public void SoundOpenLevels(int game) { if (SoundGame(game) is { } g) TrayApp.OpenPath(Path.Combine(SoundMods.FolderOf(g), "sound_levels.txt")); }
    public void ShowUpdate() { if (UpdateCheck.Available is { } a) new UpdateForm(a).Show(); }

    public HostSnapshot Snapshot()
    {
        var st = sup.Stage;
        var server = sup.Find("server")!;
        var telemetry = sup.Find("telemetry")!;
        bool navReady = st.Done && server.State == ServiceState.Running && telemetry.State == ServiceState.Running;
        var map = maps.State;
        bool building = map == MapState.Building;
        bool noGame = map == MapState.NoGame && !server.CanStart();

        string head, detail;
        Health health;
        int percent;
        if (sup.SetupProblem != null)
        { head = L.T("win.setup"); detail = L.T(sup.SetupProblem); health = Health.Bad; percent = 0; }
        else if (noGame)
        { head = L.T("win.no_game"); detail = L.T("win.no_game_hint"); health = Health.Bad; percent = 0; }
        else if (building)
        { head = L.T("win.map_building"); detail = L.T(maps.StepKey, maps.StepArg); health = Health.Busy; percent = maps.Percent; }
        else if (map == MapState.Failed)
        { head = L.T("win.map_failed"); detail = maps.Error ?? ""; health = Health.Bad; percent = 0; }
        else if (navReady)
        { head = L.T("win.ready"); detail = L.T(bridge.GameConnected ? "win.game_connected" : "win.open_game"); health = Health.Ok; percent = 100; }
        else
        {
            head = L.T("win.preparing");
            detail = server.State == ServiceState.Stopped ? L.T("win.server_stopped") : st.Label;
            health = Health.Busy;
            percent = (int)Math.Round(Math.Min(st.Estimate(), 0.99) * 100);
        }

        var ips = TrayApp.LanAddresses().ToList();
        string mapText = building ? L.T(maps.StepKey, maps.StepArg)
            : map == MapState.UpToDate ? L.T("fs.ready")
            : map == MapState.UpdateAvailable ? L.T("win.map_update")
            : map == MapState.Failed ? maps.Error ?? L.T("win.map_failed")
            : map == MapState.NoGame ? L.T("win.no_game")
            : L.T("map.check");

        return new HostSnapshot(
            head, health, detail, percent,
            ips.Count > 0 ? ips[0] : L.T("win.no_network"),
            sup.AppCode != null ? L.T("win.pair", sup.AppCode) : "",
            sup.Clients, bridge.GameConnected,
            sup.Services.Select(s => new ServiceRow(s.Name, L.T(s.Title), Map(s.State), State(s.State))).ToList(),
            mapText, maps.Percent, building,
            map is MapState.Unknown or MapState.Failed, map == MapState.UpdateAvailable || map == MapState.UpToDate,
            new Prefs(GameChoice.Setting ?? "", MapMods.Paths.Length, building, TrayApp.AutostartEnabled(), UpdateCheck.Enabled, L.Setting, UpdateCheck.Available?.Version),
            FindAttention(),
            ips.Count > 0 && File.Exists(Path.Combine(AppPaths.Current.Root, "app", "RigBuddy.apk")) ? $"http://{ips[0]}:62843/get-app" : null);
    }

    static Health Map(ServiceState s) => s switch
    {
        ServiceState.Running => Health.Ok,
        ServiceState.Stopped => Health.Idle,
        _ => Health.Busy,
    };

    static string State(ServiceState s) => L.T(s switch
    {
        ServiceState.Running => "fs.ready",
        ServiceState.Stopped => "fs.stopped",
        _ => "fs.starting",
    });

    public void RestartService(string name) => sup.Restart(name);
    public void BuildMap(bool force) => maps.Build(force: maps.State == MapState.UpToDate);
    public void ShowLogs() => app.ShowLogs();

    public void CopyAddress()
    {
        if (TrayApp.LanAddresses().FirstOrDefault() is string ip) Clipboard.SetText(ip);
    }

    public void Exit() => app.Exit();
}

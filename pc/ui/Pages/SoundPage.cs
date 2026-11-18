using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace RigBuddy.Ui.Pages;

/**
 * Sound mods: which FMOD sound mods the game plugin loads in each game (the list is written to selected.bank.txt; the
 * plugin loads changes within a second, also while the game runs), add / remove mods and the sound levels.
 */
public sealed class SoundPage : UserControl
{
    static readonly string[] LevelLabels = ["sound.lv.master", "sound.lv.engine", "sound.lv.exhaust", "sound.lv.turbo", "sound.lv.interior",
        "sound.lv.navigation", "sound.lv.outside"];

    readonly IHostBridge _host;
    readonly StackPanel _root = new() { Margin = new Thickness(14, 12, 6, 14) };
    readonly ComboBox _game = new() { Width = 300, HorizontalAlignment = HorizontalAlignment.Left };
    readonly CheckBox _on = new();
    readonly ListBox _list = new() { MinHeight = 170, MaxHeight = 320 };
    readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
    readonly List<Slider> _sliders = new();
    readonly List<TextBlock> _percents = new();
    readonly DispatcherTimer _levelTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    readonly HashSet<int> _dirty = new();
    readonly StackPanel _layers = new();
    readonly Dictionary<string, Slider> _layerSliders = new();
    readonly HashSet<string> _layerDirty = new();
    readonly DispatcherTimer _layerPoll = new() { Interval = TimeSpan.FromSeconds(3) };
    string _layerKey = "";
    bool _loading;
    int Game => _game.SelectedIndex;

    public SoundPage(IHostBridge host)
    {
        _host = host;
        Content = new ScrollViewer { Content = _root };
        _levelTimer.Tick += (_, _) =>
        {
            _levelTimer.Stop();
            foreach (int i in _dirty.ToList())
            {
                try { _host.SoundSetLevel(Game, i, _sliders[i].Value / 100.0); }
                catch (Exception e) { _status.Text = e.Message; }
            }
            _dirty.Clear();
            foreach (string id in _layerDirty.ToList())
            {
                try { if (_layerSliders.TryGetValue(id, out var s)) _host.SoundSetLayer(Game, id, s.Value / 100.0); }
                catch (Exception e) { _status.Text = e.Message; }
            }
            _layerDirty.Clear();
        };
        // the plugin finds the layers while the engine runs: the list follows without reopening the page
        _layerPoll.Tick += (_, _) => { if (IsVisible && !_loading && _layerDirty.Count == 0) ShowLayers(_host.SoundLoad(Game).Layers); };
        IsVisibleChanged += (_, _) => { if (IsVisible) _layerPoll.Start(); else _layerPoll.Stop(); };
        _game.SelectionChanged += (_, _) => { if (!_loading) Reload(); };
        _on.Click += (_, _) => _status.Text = _host.SoundSetEnabled(_on.IsChecked == true);
        Build();
    }

    public void Localize() => Build();

    /** The page was opened: the list of mods may have changed on disk (a mod added by hand, a game installed). */
    public void OnShown() => Build();

    string T(string key, params object?[] a) => _host.Text(key, a);

    /** The controls live on for the next Build: they have to leave the cards of the last one first. */
    static void Detach(FrameworkElement e)
    {
        if (e.Parent is Panel p) p.Children.Remove(e);
        else if (e.Parent is Border b) b.Child = null;
    }

    void Build()
    {
        _loading = true;
        foreach (FrameworkElement e in new FrameworkElement[] { _game, _on, _list, _status }) Detach(e);
        _root.Children.Clear();
        _sliders.Clear();
        _percents.Clear();
        var games = _host.SoundGames();
        _game.Items.Clear();
        foreach (var g in games) _game.Items.Add(g);
        _game.SelectedIndex = games.Count > 0 ? 0 : -1;

        _root.Children.Add(new TextBlock { Text = T("sound.title").Replace("Rig Buddy: ", "").Replace("Rig Buddy:", ""), Style = Forms.Sty("H1"), Margin = new Thickness(0, 0, 0, 16) });

        _on.Content = T("sound.on");
        _on.IsChecked = _host.SoundEnabled;
        _on.Margin = new Thickness(0, 8, 0, 0);
        _root.Children.Add(Forms.Card(Forms.Stack(
            Forms.Head(Strings.B("Oyun", "Game")),
            _game, _on)));

        _list.Margin = new Thickness(0, 0, 0, 10);
        _root.Children.Add(Forms.Card(Forms.Stack(
            Forms.Head(Strings.B("Ses modları", "Sound mods")),
            Forms.Dim(T("sound.hint"), 0, 8),
            _list,
            Forms.Wrap(
                Forms.Btn(T("sound.add"), AddMods), Forms.Btn(T("sound.remove"), Remove),
                Forms.Btn(T("sound.up"), () => Move(-1)), Forms.Btn(T("sound.down"), () => Move(1)),
                Forms.Btn(T("sound.folder"), () => _host.SoundOpenFolder(Game)),
                Forms.Btn(T("sound.levels"), () => _host.SoundOpenLevels(Game)),
                Forms.Btn(T("sound.save"), Save, primary: true)),
            new Border { Margin = new Thickness(0, 6, 0, 0), Child = _status })));

        var levels = new Grid { Margin = new Thickness(0, 0, 0, 2) };
        levels.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
        levels.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        levels.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(56) });
        for (int i = 0; i < LevelLabels.Length; i++)
        {
            int index = i;
            levels.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var name = new TextBlock { Text = T(LevelLabels[i]), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 4, 0, 4) };
            var slider = new Slider { Minimum = 0, Maximum = 150, SmallChange = 1, LargeChange = 10, TickFrequency = 25, IsMoveToPointEnabled = true, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 4, 8, 4) };
            var percent = new TextBlock { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right, Style = Forms.Sty("Dim") };
            slider.ValueChanged += (_, e) =>
            {
                percent.Text = $"{(int)Math.Round(e.NewValue)} %";
                if (_loading) return;
                _dirty.Add(index); // written when the slider has stopped for a moment
                _levelTimer.Stop();
                _levelTimer.Start();
            };
            _sliders.Add(slider);
            _percents.Add(percent);
            Grid.SetRow(name, i); Grid.SetColumn(name, 0);
            Grid.SetRow(slider, i); Grid.SetColumn(slider, 1);
            Grid.SetRow(percent, i); Grid.SetColumn(percent, 2);
            levels.Children.Add(name); levels.Children.Add(slider); levels.Children.Add(percent);
        }
        _root.Children.Add(Forms.Card(Forms.Stack(Forms.Head(Strings.B("Ses seviyeleri", "Sound levels")), levels)));
        Detach(_layers);
        _layerKey = "";
        _root.Children.Add(Forms.Card(Forms.Stack(Forms.Head(Strings.B("Modun katmanları", "The mod's layers")),
            Forms.Dim(Strings.B("Modun motor ya da egzoz sesinin içine koyduğu sesler (ör. turbo ıslığı). Seviyeler o sesin kendi seviyesinin üstüne gelir; turbo katmanları Turbo seviyesine de uyar.",
                "Sounds the mod put inside its engine or exhaust sound (e.g. the turbo whistle). These levels come on top of that sound's own; turbo layers follow the Turbo level too."), 0, 8),
            _layers)));
        _loading = false;
        Reload();
        if (games.Count == 0) _status.Text = T("sound.nogame");
    }

    /** The list: the mods in use first (in load order, checked), then the other .bank files in the folder; and the sliders. */
    void Reload(IEnumerable<string>? alsoCheck = null)
    {
        _loading = true;
        _list.Items.Clear();
        var state = _host.SoundLoad(Game);
        var check = new HashSet<string>(alsoCheck ?? [], StringComparer.OrdinalIgnoreCase);
        foreach (var m in state.Mods) _list.Items.Add(new CheckBox { Content = m.Name, IsChecked = m.On || check.Contains(m.Name), Margin = new Thickness(2, 3, 2, 3) });
        for (int i = 0; i < _sliders.Count; i++)
        {
            double v = i < state.Levels.Count ? state.Levels[i] : 0.5;
            _sliders[i].Value = Math.Clamp(Math.Round(v * 100), 0, 150);
            _sliders[i].IsEnabled = Game >= 0;
            _percents[i].Text = $"{(int)_sliders[i].Value} %";
        }
        _dirty.Clear();
        _loading = false;
        _status.Text = state.Warning;
        ShowLayers(state.Layers);
    }

    /** One slider per layer; rebuilt only when the list of layers changed (the plugin finds more while the engine runs). */
    void ShowLayers(IReadOnlyList<SoundLayer>? layers)
    {
        layers ??= [];
        string key = string.Join(";", layers.Select(l => l.Id + ":" + string.Join(",", l.Sounds) + ":" + l.Turbo));
        if (key == _layerKey && _layers.Children.Count > 0) return;
        _layerKey = key;
        _layers.Children.Clear();
        _layerSliders.Clear();
        if (layers.Count == 0)
        {
            _layers.Children.Add(Forms.Dim(Strings.B("Seçili modda henüz katman bulunmadı. Oyunda motoru çalıştırıp biraz sür: Rig Buddy katmanları oyun sırasında bulur ve burada gösterir. Katmanı olmayan modlarda bu liste boş kalır.",
                "No layers found in the selected mod yet. Start the engine in the game and drive a bit: Rig Buddy finds the layers while the game runs and shows them here. A mod without layers keeps this list empty.")));
            return;
        }
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(320) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(56) });
        int row = 0;
        foreach (var l in layers)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            string where = l.Event == "engine/exhaust" ? Strings.B("Egzoz içinde", "Inside the exhaust") : l.Event == "engine/engine" ? Strings.B("Motor içinde", "Inside the engine") : l.Event;
            var name = new StackPanel { Margin = new Thickness(0, 4, 8, 4), VerticalAlignment = VerticalAlignment.Center };
            name.Children.Add(new TextBlock { Text = where + (l.Turbo ? Strings.B(" · turbo", " · turbo") : ""), FontWeight = FontWeights.SemiBold });
            name.Children.Add(new TextBlock { Text = string.Join(", ", l.Sounds), Style = Forms.Sty("Dim"), FontSize = 11, TextWrapping = TextWrapping.Wrap });
            var slider = new Slider { Minimum = 0, Maximum = 150, SmallChange = 1, LargeChange = 10, IsMoveToPointEnabled = true, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 4, 8, 4), Value = Math.Clamp(Math.Round(l.Level * 100), 0, 150) };
            var percent = new TextBlock { Text = $"{(int)slider.Value} %", VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right, Style = Forms.Sty("Dim") };
            string id = l.Id;
            slider.ValueChanged += (_, e) =>
            {
                percent.Text = $"{(int)Math.Round(e.NewValue)} %";
                _layerDirty.Add(id);
                _levelTimer.Stop();
                _levelTimer.Start();
            };
            _layerSliders[id] = slider;
            Grid.SetRow(name, row); Grid.SetColumn(name, 0);
            Grid.SetRow(slider, row); Grid.SetColumn(slider, 1);
            Grid.SetRow(percent, row); Grid.SetColumn(percent, 2);
            grid.Children.Add(name); grid.Children.Add(slider); grid.Children.Add(percent);
            row++;
        }
        _layers.Children.Add(grid);
    }

    void AddMods()
    {
        if (Game < 0) return;
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = T("sound.add"),
            Filter = "Sound mods (*.scs;*.bank)|*.scs;*.bank|SCS mod archive (*.scs)|*.scs|FMOD sound bank (*.bank)|*.bank",
            Multiselect = true,
        };
        if (dlg.ShowDialog() != true) return;
        var result = _host.SoundAdd(Game, dlg.FileNames);
        Reload(result.Added);
        if (result.Message.Length > 0) _status.Text = result.Message;
    }

    void Remove()
    {
        if (_list.SelectedItem is not CheckBox cb || cb.Content is not string name) return;
        if (MessageBox.Show(T("sound.remove_confirm", name), T("sound.title"), MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        string message = _host.SoundRemove(Game, name);
        Reload();
        _status.Text = message;
    }

    void Move(int delta)
    {
        int i = _list.SelectedIndex, j = i + delta;
        if (i < 0 || j < 0 || j >= _list.Items.Count) return;
        var item = _list.Items[i];
        _list.Items.RemoveAt(i);
        _list.Items.Insert(j, item);
        _list.SelectedIndex = j;
    }

    void Save()
    {
        var names = _list.Items.OfType<CheckBox>().Where(c => c.IsChecked == true).Select(c => (string)c.Content).ToList();
        _status.Text = _host.SoundSave(Game, names);
    }
}

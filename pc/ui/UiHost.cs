using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace RigBuddy.Ui;

public static class UiHost
{
    static bool _done;

    // brush key -> dark colour, light colour
    static readonly (string Key, string Dark, string Light)[] Palette =
    {
        ("Bg", "#0D0F12", "#EEF0F3"), ("Side", "#14171C", "#E3E6EB"), ("Card", "#1A1E24", "#FFFFFF"),
        ("CardLo", "#16191F", "#F6F7F9"), ("CardHi", "#232831", "#E8EBF0"), ("Line", "#303640", "#CDD2DA"),
        ("Text1", "#E1E4EA", "#1B1F26"), ("Text2", "#7D8799", "#5B6472"),
        ("Accent", "#FF6B2C", "#E5530F"), ("Cyan", "#00C8FF", "#0086B3"),
        ("Ok", "#2ECC71", "#1B8A4A"), ("Warn", "#FFBE3D", "#B26A00"),
        ("Bad", "#FF4D5A", "#D32F3C"), ("Idle", "#5F6368", "#9AA0A6"), ("OnAccent", "#0D0F12", "#FFFFFF"),
    };

    /**
     * WPF needs an Application object for its pack:// resources; the tray host runs a WinForms message loop, so
     * ours is never Run: it only exists, and never shuts the process down.
     */
    public static void Init()
    {
        if (_done) return;
        _done = true;
        var app = Application.Current ?? new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        // the assembly this file is built into: RigBuddy.Ui, or RigHardware.exe (rig-buddy-hardware builds these files into itself)
        string asm = typeof(UiHost).Assembly.GetName().Name!;
        app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri($"pack://application:,,,/{asm};component/Theme.xaml") });
        // Every brush gets its colour through a binding to a holder object. WPF freezes a brush the moment a style uses it,
        // and a frozen brush can never change colour (only a few parts of the window followed the theme before); a brush
        // with a binding cannot be frozen, so the one instance every style and template shares changes in place.
        foreach (var (key, dark, _) in Palette)
        {
            var holder = new ThemeColor((Color)ColorConverter.ConvertFromString(dark));
            Holders[key] = holder;
            // a new brush of our own (the ones read from Theme.xaml are read-only); the styles and windows refer to the brushes
            // with {DynamicResource}, so they find this one.
            var brush = new SolidColorBrush(holder.Color);
            BindingOperations.SetBinding(brush, SolidColorBrush.ColorProperty, new Binding(nameof(ThemeColor.Color)) { Source = holder });
            app.Resources[key] = brush; // ahead of Theme.xaml's own: every {DynamicResource} and Forms.Brh gets this one
        }
    }

    static readonly Dictionary<string, ThemeColor> Holders = new();

    sealed class ThemeColor(Color color) : System.ComponentModel.INotifyPropertyChanged
    {
        Color _color = color;
        public Color Color
        {
            get => _color;
            set { _color = value; PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Color))); }
        }
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }

    /** Switches the whole window between the dark and the light palette; call on the UI thread. */
    public static void ApplyTheme(bool dark)
    {
        Init();
        foreach (var (key, d, l) in Palette)
            if (Holders.TryGetValue(key, out var holder)) holder.Color = (Color)ColorConverter.ConvertFromString(dark ? d : l);
    }
}

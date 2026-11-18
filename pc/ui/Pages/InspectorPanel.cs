using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RigBuddy.Ui.Pages;

/** The docked panel on the right: the program's event log (games, devices, services). */
public sealed class InspectorPanel : UserControl
{
    readonly StackPanel _log = new();
    readonly ScrollViewer _logScroll;
    readonly TextBlock _title = new() { Style = Forms.Sty("Caps"), Foreground = Forms.Brh("Text1") };
    readonly Button _clear = new();
    readonly TextBlock _logCount = new() { Style = Forms.Sty("Dim"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
    int _lines;

    public InspectorPanel()
    {
        _logScroll = new ScrollViewer { Content = _log, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        _clear.Click += (_, _) => { _log.Children.Clear(); _lines = 0; _logCount.Text = ""; };
        var logHead = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
        logHead.Children.Add(_clear);
        logHead.Children.Add(_logCount);

        var root = new DockPanel { Margin = new Thickness(10, 10, 10, 10) };
        var head = new StackPanel { Margin = new Thickness(4, 0, 0, 8) };
        head.Children.Add(_title);
        DockPanel.SetDock(head, Dock.Top);
        DockPanel.SetDock(logHead, Dock.Top);
        root.Children.Add(head);
        root.Children.Add(logHead);
        root.Children.Add(_logScroll);
        Content = root;
        Localize();
    }

    public void Localize()
    {
        _title.Text = Strings.B("OLAYLAR", "EVENTS");
        _clear.Content = Strings.B("Temizle", "Clear");
        if (_lines > 0) _logCount.Text = Strings.B("{0} satır", "{0} lines", _lines);
    }

    /** Adds a line to the event log: where it comes from (GAME, DEV, SVC, SYS), what happened, how it went. */
    public void Log(string source, string text, Health health = Health.Idle)
    {
        var line = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 3) };
        var wrap = new TextBlock { Style = Forms.Sty("Mono"), FontSize = 11, TextWrapping = TextWrapping.Wrap };
        wrap.Inlines.Add(new System.Windows.Documents.Run($"{DateTime.Now:HH:mm:ss} ") { Foreground = Forms.Brh("Text2") });
        wrap.Inlines.Add(new System.Windows.Documents.Run($"{source} ") { Foreground = Forms.Brh("Cyan"), FontWeight = FontWeights.Bold });
        wrap.Inlines.Add(new System.Windows.Documents.Run(text) { Foreground = health == Health.Idle ? Forms.Brh("Text1") : HealthBrush.Of(health) });
        var host = new Border { Child = wrap, MaxWidth = 292 };
        line.Children.Add(host);
        _log.Children.Add(line);
        if (++_lines > 300) { _log.Children.RemoveAt(0); _lines--; }
        _logCount.Text = Strings.B("{0} satır", "{0} lines", _lines);
        _logScroll.ScrollToEnd();
    }
}

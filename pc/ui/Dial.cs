using System.Windows;
using System.Windows.Media;

namespace RigBuddy.Ui;

/** A small gauge: a 240° arc with a needle, the way the physical gauge shows the value. */
sealed class Dial : FrameworkElement
{
    double _fraction;
    string _text = "";

    public Dial() { Width = 74; Height = 62; }

    public void Set(double fraction, string text)
    {
        fraction = double.IsFinite(fraction) ? Math.Clamp(fraction, 0, 1) : 0;
        if (Math.Abs(fraction - _fraction) < 0.002 && text == _text) return;
        _fraction = fraction;
        _text = text;
        InvalidateVisual();
    }

    static Point On(Point c, double r, double deg)
    {
        double a = deg * Math.PI / 180;
        return new Point(c.X + r * Math.Cos(a), c.Y + r * Math.Sin(a));
    }

    protected override void OnRender(DrawingContext dc)
    {
        var c = new Point(ActualWidth / 2, ActualHeight * 0.58);
        double r = Math.Min(ActualWidth / 2 - 4, ActualHeight * 0.55);
        // the arc runs from 150° (lower left) over the top to 30° (lower right), 240° in all
        const double start = 150, sweep = 240;
        Geometry Arc(double fraction)
        {
            var g = new StreamGeometry();
            using var ctx = g.Open();
            ctx.BeginFigure(On(c, r, start), false, false);
            double deg = sweep * fraction;
            ctx.ArcTo(On(c, r, start + deg), new Size(r, r), 0, deg > 180, SweepDirection.Clockwise, true, false);
            g.Freeze();
            return g;
        }
        var track = new Pen((Brush)Application.Current.FindResource("Line"), 5) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        dc.DrawGeometry(null, track, Arc(1));
        if (_fraction > 0.003)
        {
            var fill = new Pen((Brush)Application.Current.FindResource("Accent"), 5) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            dc.DrawGeometry(null, fill, Arc(_fraction));
        }
        var needle = new Pen((Brush)Application.Current.FindResource("Text1"), 2) { EndLineCap = PenLineCap.Round };
        dc.DrawLine(needle, c, On(c, r - 8, start + sweep * _fraction));
        dc.DrawEllipse((Brush)Application.Current.FindResource("Text1"), null, c, 3, 3);
        var ft = new FormattedText(_text, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 11,
            (Brush)Application.Current.FindResource("Text2"), VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawText(ft, new Point(c.X - ft.Width / 2, ActualHeight - ft.Height));
    }
}

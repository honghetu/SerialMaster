using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SerialMaster.UI.Controls;

public class RadarControl : Canvas
{
    private float[] _values = Array.Empty<float>();
    private string[] _labels = Array.Empty<string>();
    private float _maxValue = 255f;

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(RadarControl),
            new PropertyMetadata("雷达视图"));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public RadarControl()
    {
        Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x2E));
        ClipToBounds = true;
        MinHeight = 200;
        MinWidth = 200;
    }

    public void SetValues(float[] values, string[] labels, float maxValue = 255f)
    {
        _values = (float[])values.Clone();
        _labels = (string[])labels.Clone();
        _maxValue = maxValue > 0 ? maxValue : 255f;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        double w = ActualWidth, h = ActualHeight;
        if (w <= 0 || h <= 0) return;

        double cx = w / 2, cy = h / 2;
        double radius = Math.Min(cx, cy) - 55;
        int n = Math.Max(_values.Length, 3);
        if (n < 3) n = 3;

        double angleStep = 2 * Math.PI / n;
        double startAngle = -Math.PI / 2;

        var gridPen = new Pen(new SolidColorBrush(Color.FromRgb(0x45, 0x47, 0x5A)), 0.8);
        var axisPen = new Pen(new SolidColorBrush(Color.FromRgb(0x58, 0x5B, 0x70)), 1.2);
        var dataPen = new Pen(new SolidColorBrush(Color.FromRgb(0xA6, 0xE3, 0xA1)), 2.5);
        var dataFill = new SolidColorBrush(Color.FromArgb(50, 0xA6, 0xE3, 0xA1));
        var dotBrush = new SolidColorBrush(Color.FromRgb(0x58, 0x5B, 0x70));
        var labelBrush = new SolidColorBrush(Color.FromRgb(0xA6, 0xAD, 0xC8));
        var titleBrush = new SolidColorBrush(Color.FromRgb(0xCD, 0xD6, 0xF4));
        var scaleBrush = new SolidColorBrush(Color.FromRgb(0x6C, 0x70, 0x86));

        // Draw concentric rings
        int rings = 5;
        for (int ring = 1; ring <= rings; ring++)
        {
            double r = radius * ring / rings;
            Point[] ringPts = new Point[n + 1];
            for (int i = 0; i <= n; i++)
            {
                double a = startAngle + (i % n) * angleStep;
                ringPts[i] = new Point(cx + r * Math.Cos(a), cy + r * Math.Sin(a));
            }

            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                ctx.BeginFigure(ringPts[0], false, false);
                for (int i = 1; i <= n; i++)
                    ctx.LineTo(ringPts[i], true, false);
            }
            dc.DrawGeometry(null, gridPen, geo);

            // Scale label on first axis direction
            double pct = (double)ring / rings * 100;
            var scaleText = new FormattedText($"{pct:F0}%",
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"), 9, scaleBrush, 1.0);
            dc.DrawText(scaleText, new Point(cx - scaleText.Width / 2,
                cy - r - scaleText.Height - 2));
        }

        // Draw axis lines from center + intersection dots
        for (int i = 0; i < n; i++)
        {
            double a = startAngle + i * angleStep;
            Point end = new(cx + radius * Math.Cos(a), cy + radius * Math.Sin(a));
            dc.DrawLine(axisPen, new Point(cx, cy), end);

            for (int ring = 1; ring <= rings; ring++)
            {
                double r = radius * ring / rings;
                dc.DrawEllipse(dotBrush, null,
                    new Point(cx + r * Math.Cos(a), cy + r * Math.Sin(a)), 2, 2);
            }
        }

        // Draw data polygon (only when data matches axis count)
        if (_values.Length == n && _values.Length >= 3)
        {
            Point[] dataPts = new Point[n + 1];
            for (int i = 0; i < n; i++)
            {
                double a = startAngle + i * angleStep;
                double val = Math.Clamp(_values[i] / _maxValue, 0, 1);
                double r = radius * val;
                dataPts[i] = new Point(cx + r * Math.Cos(a), cy + r * Math.Sin(a));
            }
            dataPts[n] = dataPts[0];

            var dataGeo = new StreamGeometry();
            using (var ctx = dataGeo.Open())
            {
                ctx.BeginFigure(dataPts[0], true, true);
                for (int i = 1; i <= n; i++)
                    ctx.LineTo(dataPts[i], true, true);
            }
            dc.DrawGeometry(dataFill, dataPen, dataGeo);

            for (int i = 0; i < n; i++)
            {
                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(0xA6, 0xE3, 0xA1)), null,
                    dataPts[i], 4, 4);
            }
        }

        // Axis endpoint labels
        for (int i = 0; i < n && i < _labels.Length; i++)
        {
            double a = startAngle + i * angleStep;
            double labelR = radius + 22;
            Point labelPos = new(cx + labelR * Math.Cos(a), cy + labelR * Math.Sin(a));
            var ft = new FormattedText(_labels[i],
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"), 11, labelBrush, 1.0);
            dc.DrawText(ft, new Point(labelPos.X - ft.Width / 2, labelPos.Y - ft.Height / 2));
        }

        // Title at top
        var titleFt = new FormattedText(Title,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI"), 14, titleBrush, 1.0);
        dc.DrawText(titleFt, new Point(cx - titleFt.Width / 2, 10));

        // Center dot
        dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(0xCD, 0xD6, 0xF4)), null,
            new Point(cx, cy), 3, 3);
    }
}

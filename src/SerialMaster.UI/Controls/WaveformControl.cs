using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace SerialMaster.UI.Controls;

public class WaveformControl : Canvas
{
    private readonly List<CircularBuffer> _channels = new();
    private readonly List<Polyline> _polylines = new();
    private bool _isInitialized;

    private static readonly Color[] ChannelColors =
    {
        Color.FromRgb(0xA6, 0xE3, 0xA1), // green
        Color.FromRgb(0x89, 0xB4, 0xFA), // blue
        Color.FromRgb(0xFA, 0xBE, 0x61), // yellow
        Color.FromRgb(0xF3, 0x8B, 0xA8), // red
        Color.FromRgb(0xCB, 0xA6, 0xF7), // purple
        Color.FromRgb(0x94, 0xE2, 0xD5), // teal
    };

    public static readonly DependencyProperty ChannelCountProperty =
        DependencyProperty.Register(nameof(ChannelCount), typeof(int), typeof(WaveformControl),
            new PropertyMetadata(1, (d, _) => ((WaveformControl)d).RebuildChannels()));

    public int ChannelCount
    {
        get => (int)GetValue(ChannelCountProperty);
        set => SetValue(ChannelCountProperty, value);
    }

    public static readonly DependencyProperty MaxPointsProperty =
        DependencyProperty.Register(nameof(MaxPoints), typeof(int), typeof(WaveformControl),
            new PropertyMetadata(500));

    public int MaxPoints
    {
        get => (int)GetValue(MaxPointsProperty);
        set => SetValue(MaxPointsProperty, value);
    }

    private readonly Pen _gridPen = new(new SolidColorBrush(Color.FromRgb(0x45, 0x47, 0x5A)), 0.6) { DashStyle = DashStyles.Dash };
    private readonly Pen _axisPen = new(new SolidColorBrush(Color.FromRgb(0x58, 0x5B, 0x70)), 1);
    private readonly Brush _labelBrush = new SolidColorBrush(Color.FromRgb(0xA6, 0xAD, 0xC8));
    private readonly Brush _mutedBrush = new SolidColorBrush(Color.FromRgb(0x6C, 0x70, 0x86));

    private float _lastMin = 0, _lastMax = 1;

    public WaveformControl()
    {
        Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x2E));
        ClipToBounds = true;
        Loaded += (_, _) => InitializeChannels();
    }

    private void InitializeChannels()
    {
        if (_isInitialized) return;
        _isInitialized = true;
        RebuildChannels();
    }

    private void RebuildChannels()
    {
        _channels.Clear();
        _polylines.Clear();
        Children.Clear();

        for (int i = 0; i < ChannelCount; i++)
        {
            _channels.Add(new CircularBuffer(MaxPoints));
            var color = ChannelColors[i % ChannelColors.Length];
            var poly = new Polyline
            {
                Stroke = new SolidColorBrush(color),
                StrokeThickness = 1.5,
                StrokeLineJoin = PenLineJoin.Round,
                Points = new PointCollection()  // assigned once, mutated in Refresh
            };
            _polylines.Add(poly);
            Children.Add(poly);
        }
    }

    public void AddDataPoint(int channel, float value)
    {
        if (channel < 0 || channel >= _channels.Count) return;
        _channels[channel].Add(value);
    }

    public void AddMultiChannelData(params float[] values)
    {
        for (int i = 0; i < values.Length && i < _channels.Count; i++)
            _channels[i].Add(values[i]);
    }

    public void Refresh()
    {
        if (!_isInitialized) InitializeChannels();
        if (_channels.Count == 0) return;

        double w = ActualWidth;
        double h = ActualHeight;

        if (w <= 0 || h <= 0) return;

        // Find global min/max for all channels
        float globalMin = float.MaxValue, globalMax = float.MinValue;
        bool hasData = false;
        foreach (var buf in _channels)
        {
            if (buf.Count == 0) continue;
            hasData = true;
            for (int i = 0; i < buf.Count; i++)
            {
                float v = buf[i];
                if (v < globalMin) globalMin = v;
                if (v > globalMax) globalMax = v;
            }
        }

        if (!hasData) return;

        float range = globalMax - globalMin;
        if (range < 0.001f) range = 1f;
        globalMin -= range * 0.05f;
        globalMax += range * 0.05f;
        range = globalMax - globalMin;

        _lastMin = globalMin;
        _lastMax = globalMax;
        InvalidateVisual();  // refresh axis labels on each frame

        // Match OnRender's plot area (margin=36 left, topPad=8 top, rightPad=10 right)
        double margin = 36, topPad = 8, rightPad = 10;
        double plotW = w - margin - rightPad;
        double plotH = h - margin - topPad;
        double plotX = margin;
        double plotY = topPad;

        for (int ch = 0; ch < _channels.Count; ch++)
        {
            var buf = _channels[ch];
            var pts = _polylines[ch].Points;
            if (buf.Count < 2)
            {
                pts.Clear();
                continue;
            }

            int count = buf.Count;
            int skip = count > MaxPoints ? count - MaxPoints : 0;
            int visible = count - skip;

            // Resize in-place to avoid GC churn from new PointCollection per frame.
            while (pts.Count > visible) pts.RemoveAt(pts.Count - 1);
            while (pts.Count < visible) pts.Add(new Point(0, 0));

            for (int i = 0; i < visible; i++)
            {
                float val = buf[i + skip];
                double x = plotX + (plotW * i / (visible - 1));
                double y = plotY + plotH - (plotH * ((val - globalMin) / range));
                pts[i] = new Point(x, y);
            }
        }
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        double w = ActualWidth;
        double h = ActualHeight;
        if (w <= 0 || h <= 0) return;

        double margin = 36;        // leave room for Y labels (left) and X labels (bottom)
        double topPad = 8;
        double rightPad = 10;
        double plotW = w - margin - rightPad;
        double plotH = h - margin - topPad;
        double plotX = margin;
        double plotY = topPad;
        double plotBottom = plotY + plotH;

        // Horizontal grid + Y-axis numeric labels (5 ticks)
        for (int i = 0; i <= 4; i++)
        {
            double y = plotY + plotH * i / 4;
            dc.DrawLine(_gridPen, new Point(plotX, y), new Point(plotX + plotW, y));
            float value = _lastMax - (_lastMax - _lastMin) * (float)i / 4f;
            var text = FormatNumber(value);
            var ft = new FormattedText(text,
                System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface("Consolas"), 10, _labelBrush, 1.0);
            dc.DrawText(ft, new Point(plotX - ft.Width - 4, y - ft.Height / 2));
        }

        // Vertical grid + X-axis "−Ns ... 0s" relative time labels (5 ticks)
        // X represents the rolling window: leftmost = oldest, rightmost = newest.
        // We don't know the actual time-per-sample without timestamps, so we label as samples.
        int maxPts = MaxPoints;
        int curPts = _channels.Count > 0 ? _channels[0].Count : 0;
        int shown = Math.Min(curPts, maxPts);
        for (int i = 0; i <= 4; i++)
        {
            double x = plotX + plotW * i / 4;
            dc.DrawLine(_gridPen, new Point(x, plotY), new Point(x, plotBottom));
            int sampleIdx = (int)((double)shown * i / 4);
            string label = shown == 0 ? "0" : $"-{shown - sampleIdx}";
            var ft = new FormattedText(label,
                System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface("Consolas"), 9, _mutedBrush, 1.0);
            dc.DrawText(ft, new Point(x - ft.Width / 2, plotBottom + 4));
        }

        // Y axis line
        dc.DrawLine(_axisPen, new Point(plotX, plotY), new Point(plotX, plotBottom));
        // X axis line at bottom
        dc.DrawLine(_axisPen, new Point(plotX, plotBottom), new Point(plotX + plotW, plotBottom));

        // Axis legend (top-left corner)
        var legend = new FormattedText($"Y range  ch={_channels.Count}",
            System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface("Segoe UI"), 10, _mutedBrush, 1.0);
        dc.DrawText(legend, new Point(plotX + 4, 2));
    }

    private static string FormatNumber(float v)
    {
        if (Math.Abs(v) >= 1000) return v.ToString("F0");
        if (Math.Abs(v) >= 10)   return v.ToString("F1");
        return v.ToString("F2");
    }

    private sealed class CircularBuffer
    {
        private readonly float[] _data;
        private int _head;
        private int _count;

        public CircularBuffer(int capacity) => _data = new float[capacity];

        public int Count => _count;

        public float this[int index]
        {
            get
            {
                int i = (_head - _count + 1 + index + _data.Length) % _data.Length;
                if (i < 0) i += _data.Length;
                return _data[i];
            }
        }

        public void Add(float value)
        {
            _head = (_head + 1) % _data.Length;
            _data[_head] = value;
            if (_count < _data.Length) _count++;
        }
    }
}

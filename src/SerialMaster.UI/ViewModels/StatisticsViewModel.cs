using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SerialMaster.Core.Services;
using System.Windows;
using System.Windows.Threading;

namespace SerialMaster.UI.ViewModels;

/// <summary>
/// Live statistics panel that reflects the currently active SessionViewModel.
/// Polls 4x/sec via DispatcherTimer; rolls 1s / 10s / 60s rate windows.
/// </summary>
public partial class StatisticsViewModel : ObservableObject, IDisposable
{
    private readonly Func<SessionViewModel?> _activeSessionAccessor;
    private readonly Action _closeCallback;
    private readonly DispatcherTimer _timer;
    private readonly DateTime _openedAt = DateTime.Now;

    // Rolling samples: (timestamp, totalRx, totalTx)
    private readonly Queue<(DateTime t, long rx, long tx)> _samples = new();

    // Anchor counters used for "since-this-session" stats.
    private long? _rxAnchor;
    private long? _txAnchor;
    private string? _anchorPortName;

    [ObservableProperty]
    private string _tabTitle = "📊 统计面板";

    [ObservableProperty]
    private string _connectedPortName = "(无活动会话)";

    [ObservableProperty]
    private string _uptimeText = "--:--:--";

    [ObservableProperty]
    private long _receivedBytes;

    [ObservableProperty]
    private long _sentBytes;

    [ObservableProperty]
    private int _errorCount;

    [ObservableProperty]
    private int _warningCount;

    [ObservableProperty]
    private double _rxRate1s;
    [ObservableProperty]
    private double _txRate1s;

    [ObservableProperty]
    private double _rxRate10s;
    [ObservableProperty]
    private double _txRate10s;

    [ObservableProperty]
    private double _rxRate60s;
    [ObservableProperty]
    private double _txRate60s;

    [ObservableProperty]
    private double _peakRxRate;
    [ObservableProperty]
    private double _peakTxRate;

    [ObservableProperty]
    private long _parsedFrameCount;

    [ObservableProperty]
    private string _activeProtocolName = "(未应用)";

    [ObservableProperty]
    private string _statusText = "面板已打开，等待会话数据...";

    public StatisticsViewModel(Func<SessionViewModel?> activeSessionAccessor, Action closeCallback)
    {
        _activeSessionAccessor = activeSessionAccessor;
        _closeCallback = closeCallback;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _timer.Tick += (_, _) => Tick();
        _timer.Start();
    }

    private void Tick()
    {
        var s = _activeSessionAccessor();
        if (s == null)
        {
            ConnectedPortName = "(无活动会话)";
            UptimeText = "--:--:--";
            return;
        }

        // Reset anchor if we switched to a different session.
        if (_anchorPortName != s.TabTitle)
        {
            _anchorPortName = s.TabTitle;
            _rxAnchor = s.ReceivedBytes;
            _txAnchor = s.SentBytes;
            _samples.Clear();
            PeakRxRate = 0;
            PeakTxRate = 0;
        }

        ConnectedPortName = $"{s.TabTitle} ({(s.SerialService.IsOpen ? "已连接" : "已断开")})";

        var now = DateTime.Now;
        var uptime = now - _openedAt;
        UptimeText = $"{(int)uptime.TotalHours:D2}:{uptime.Minutes:D2}:{uptime.Seconds:D2}";

        ReceivedBytes = s.ReceivedBytes - (_rxAnchor ?? 0);
        SentBytes = s.SentBytes - (_txAnchor ?? 0);
        ErrorCount = s.ErrorCount;
        WarningCount = s.WarningCount;
        ParsedFrameCount = s.ParsedFrames.Count;
        ActiveProtocolName = s.ActiveProtocol?.Name ?? "(未应用)";

        _samples.Enqueue((now, ReceivedBytes, SentBytes));
        while (_samples.Count > 0 && (now - _samples.Peek().t).TotalSeconds > 65)
            _samples.Dequeue();

        RxRate1s = ComputeRate(now, TimeSpan.FromSeconds(1), useRx: true);
        TxRate1s = ComputeRate(now, TimeSpan.FromSeconds(1), useRx: false);
        RxRate10s = ComputeRate(now, TimeSpan.FromSeconds(10), useRx: true);
        TxRate10s = ComputeRate(now, TimeSpan.FromSeconds(10), useRx: false);
        RxRate60s = ComputeRate(now, TimeSpan.FromSeconds(60), useRx: true);
        TxRate60s = ComputeRate(now, TimeSpan.FromSeconds(60), useRx: false);

        if (RxRate1s > PeakRxRate) PeakRxRate = RxRate1s;
        if (TxRate1s > PeakTxRate) PeakTxRate = TxRate1s;
    }

    private double ComputeRate(DateTime now, TimeSpan window, bool useRx)
    {
        var cutoff = now - window;
        (DateTime t, long rx, long tx)? oldest = null;
        foreach (var sample in _samples)
        {
            if (sample.t >= cutoff)
            {
                oldest = sample;
                break;
            }
        }
        if (oldest == null) return 0;

        var elapsed = (now - oldest.Value.t).TotalSeconds;
        if (elapsed <= 0.05) return 0;

        long delta = useRx
            ? ReceivedBytes - oldest.Value.rx
            : SentBytes - oldest.Value.tx;
        return delta / elapsed;
    }

    [RelayCommand]
    private void ResetAnchors()
    {
        _rxAnchor = null;
        _txAnchor = null;
        _anchorPortName = null;
        _samples.Clear();
        PeakRxRate = 0;
        PeakTxRate = 0;
        ReceivedBytes = 0;
        SentBytes = 0;
        RxRate1s = TxRate1s = RxRate10s = TxRate10s = RxRate60s = TxRate60s = 0;
        StatusText = $"[{DateTime.Now:HH:mm:ss}] 已重置计数器，下次刷新重新锚定";
        FileLogger.Info("Statistics: counters reset");
    }

    public void Close()
    {
        Dispose();
        _closeCallback();
    }

    public void Dispose()
    {
        _timer.Stop();
    }
}

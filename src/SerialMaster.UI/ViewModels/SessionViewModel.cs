using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SerialMaster.Core.Models;
using SerialMaster.Core.Services;
using SerialMaster.UI.Models;
using SerialMaster.UI.Windows;
using System.Collections.ObjectModel;
using System.Windows;

namespace SerialMaster.UI.ViewModels;

public partial class SessionViewModel : ObservableObject, IDisposable
{
    private readonly ISerialPortService _serialService;
    private readonly DeviceInfo _deviceInfo;
    private CancellationTokenSource? _readCts;
    private System.Timers.Timer? _rateTimer;
    private System.Windows.Threading.DispatcherTimer? _renderTimer;
    private System.Windows.Threading.DispatcherTimer? _timedSendTimer;
    private readonly ProtocolParser _protocolParser = new();
    private long _lastReceivedBytes;
    private long _lastSentBytes;
    private bool _disposed;

    // Popup windows
    private WaveformWindow? _waveformWindow;
    private RadarWindow? _radarWindow;
    private View3DWindow? _view3DWindow;

    // Radar data buffers
    private float[][]? _radarBuffers;
    private int _radarIndex;

    [ObservableProperty]
    private string _tabTitle = string.Empty;

    [ObservableProperty]
    private ObservableCollection<DataRecord> _records = new();

    [ObservableProperty]
    private long _receivedBytes;

    [ObservableProperty]
    private long _sentBytes;

    [ObservableProperty]
    private int _errorCount;

    [ObservableProperty]
    private int _warningCount;

    [ObservableProperty]
    private bool _isPaused;

    [ObservableProperty]
    private double _receiveRate;

    [ObservableProperty]
    private double _sendRate;

    [ObservableProperty]
    private long _packetCount;

    [ObservableProperty]
    private bool _isTerminalMode;

    [ObservableProperty]
    private string _terminalText = string.Empty;

    [ObservableProperty]
    private int _waveformChannelCount = 1;

    [ObservableProperty]
    private string _sendText = string.Empty;

    [ObservableProperty]
    private SendMode _sendMode = SendMode.Auto;

    [ObservableProperty]
    private LineEnding _lineEnding = LineEnding.CRLF;

    [ObservableProperty]
    private int _selectedBaudRate = 115200;

    [ObservableProperty]
    private DisplayMode _currentDisplayMode = DisplayMode.Hex;

    public IReadOnlyList<int> AvailableBaudRates { get; } = new[]
    {
        300, 600, 1200, 2400, 4800, 9600, 14400, 19200,
        38400, 57600, 74880, 115200, 128000, 230400, 250000,
        256000, 460800, 921600, 1000000, 1500000, 2000000
    };

    public IReadOnlyList<DisplayMode> AvailableDisplayModes { get; } =
        new[] { DisplayMode.Hex, DisplayMode.Ascii, DisplayMode.Dual, DisplayMode.Terminal };

    private bool _ignoreBaudRateChange;

    partial void OnSelectedBaudRateChanged(int value)
    {
        if (_ignoreBaudRateChange) return;
        if (_deviceInfo.Config == null) return;
        if (value == _deviceInfo.Config.BaudRate) return;

        try
        {
            var newCfg = _deviceInfo.Config with { BaudRate = value };
            _deviceInfo.Config = newCfg;
            _serialService.Open(newCfg);
            _deviceInfo.StatusText = $"已连接 ({value})";
            FileLogger.Info($"Baud changed to {value} on {_deviceInfo.PortName}");
        }
        catch (Exception ex)
        {
            FileLogger.Error($"Baud change failed for {_deviceInfo.PortName}", ex);
            _deviceInfo.StatusText = $"切换波特率失败: {ex.Message}";
            _deviceInfo.HasError = true;
        }
    }

    partial void OnCurrentDisplayModeChanged(DisplayMode value)
    {
        // Keep IsTerminalMode in sync so existing terminal logic (ASCII appending) still works.
        IsTerminalMode = value == DisplayMode.Terminal;
        FileLogger.Info($"Display mode -> {value}");
    }

    public IReadOnlyList<SendMode> SendModes { get; } =
        new[] { SendMode.Auto, SendMode.Hex, SendMode.Ascii, SendMode.Utf8 };

    public IReadOnlyList<LineEnding> LineEndings { get; } =
        new[] { LineEnding.None, LineEnding.CR, LineEnding.LF, LineEnding.CRLF };

    [ObservableProperty]
    private bool _dtrEnabled;

    [ObservableProperty]
    private bool _rtsEnabled;

    [ObservableProperty]
    private bool _isWaveformVisible;

    [ObservableProperty]
    private bool _isRadarVisible;

    [ObservableProperty]
    private bool _isView3DVisible;

    [ObservableProperty]
    private ProtocolDefinition? _activeProtocol;

    [ObservableProperty]
    private ObservableCollection<ParsedFrame> _parsedFrames = new();

    [ObservableProperty]
    private bool _showParsedPane;

    [ObservableProperty]
    private bool _timedSendEnabled;

    [ObservableProperty]
    private int _timedSendIntervalMs = 1000;

    partial void OnActiveProtocolChanged(ProtocolDefinition? value)
    {
        _protocolParser.Definition = value ?? new ProtocolDefinition();
        ParsedFrames.Clear();
        if (value != null) ShowParsedPane = true;
    }

    partial void OnTimedSendEnabledChanged(bool value)
    {
        if (value) StartTimedSend();
        else StopTimedSend();
    }

    partial void OnTimedSendIntervalMsChanged(int value)
    {
        if (_timedSendTimer != null && TimedSendEnabled)
        {
            _timedSendTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(10, value));
        }
    }

    public void ApplyProtocol(ProtocolDefinition? definition)
    {
        ActiveProtocol = definition;
    }

    public DeviceInfo DeviceInfo => _deviceInfo;

    public ISerialPortService SerialService => _serialService;

    public async Task SendHexDataAsync(byte[] data)
    {
        try
        {
            await _serialService.SendAsync(data);
        }
        catch
        {
            ErrorCount++;
        }
    }

    [RelayCommand]
    private async Task QuickSend(SerialMaster.UI.Models.FavoriteItem? item)
    {
        if (item == null || string.IsNullOrEmpty(item.Data)) return;
        // Quick-send semantics: use the favorite's own IsHex flag, ignore the session's
        // SendMode/LineEnding. HEX favorites never auto-append; text favorites default to CRLF.
        var mode = item.IsHex ? SendMode.Hex : SendMode.Utf8;
        var ending = item.IsHex ? LineEnding.None : LineEnding.CRLF;
        var bytes = InputParser.Parse(item.Data, mode, ending);
        if (bytes.Length == 0) return;
        try { await _serialService.SendAsync(bytes); }
        catch { ErrorCount++; }
    }

    public void RefreshQuickSends(IEnumerable<SerialMaster.UI.Models.FavoriteItem> items)
    {
        QuickSends.Clear();
        foreach (var i in items) QuickSends.Add(i);
    }

    [ObservableProperty]
    private ObservableCollection<SerialMaster.UI.Models.FavoriteItem> _quickSends = new();

    public SessionViewModel(ISerialPortService serialService, DeviceInfo deviceInfo)
    {
        _serialService = serialService;
        _deviceInfo = deviceInfo;
        TabTitle = $"{deviceInfo.PortName}";

        if (deviceInfo.Config != null)
        {
            _serialService.Open(deviceInfo.Config);
            _ignoreBaudRateChange = true;
            SelectedBaudRate = deviceInfo.Config.BaudRate;
            _ignoreBaudRateChange = false;
        }

        DtrEnabled = _serialService.GetPinState("DTR");
        RtsEnabled = _serialService.GetPinState("RTS");

        _serialService.ErrorOccurred += OnError;
        _serialService.ConnectionStateChanged += (_, _) =>
        {
            TabTitle = _serialService.IsOpen
                ? $"{deviceInfo.PortName} ●"
                : $"{deviceInfo.PortName} ○";
        };

        StartReading();
        StartRateTimer();
        StartRenderTimer();
    }

    private void StartRenderTimer()
    {
        _renderTimer = new System.Windows.Threading.DispatcherTimer(
            TimeSpan.FromMilliseconds(33),
            System.Windows.Threading.DispatcherPriority.Render,
            (_, _) =>
            {
                if (_waveformWindow?.IsVisible == true)
                    _waveformWindow.Refresh();
                if (_radarWindow?.IsVisible == true)
                    _radarWindow.RadarCanvas.InvalidateVisual();
            },
            Application.Current.Dispatcher);
        _renderTimer.Start();
    }

    private void StartReading()
    {
        _readCts = new CancellationTokenSource();
        var token = _readCts.Token;

        Task.Run(async () =>
        {
            var reader = _serialService.ReceivedData;
            await foreach (var record in reader.ReadAllAsync(token))
            {
                if (IsPaused) continue;

                // Off-UI-thread side effect: write to disk if logging is on.
                // Doing this before the dispatcher hop keeps long Records lists from
                // bottlenecking disk throughput.
                try { _dataLogger?.Write(record); } catch { /* logged via FileLogger if needed */ }

                Application.Current.Dispatcher.Invoke(() =>
                {
                    Records.Add(record);
                    while (Records.Count > 50_000)
                        Records.RemoveAt(0);

                    if (record.Direction == DataDirection.Receive)
                    {
                        ReceivedBytes += record.Data.Length;
                        if (IsTerminalMode)
                        {
                            TerminalText += System.Text.Encoding.ASCII.GetString(record.Data);
                            if (TerminalText.Length > 50_000)
                                TerminalText = TerminalText[^30_000..];
                        }
                        ProcessVisualizationData(record.Data);
                        ProcessProtocolData(record.Data);
                    }
                    else
                        SentBytes += record.Data.Length;

                    if (record.Status == RecordStatus.Failed) ErrorCount++;
                    if (record.Status == RecordStatus.Timeout) WarningCount++;
                });
            }
        }, token);
    }

    private void OnError(object? sender, string message)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            ErrorCount++;
            _deviceInfo.HasError = true;
            _deviceInfo.StatusText = message;
        });
    }

    private DataLoggerService? _dataLogger;

    [ObservableProperty]
    private bool _isLoggingToFile;

    [ObservableProperty]
    private string _loggerStatus = string.Empty;

    partial void OnIsLoggingToFileChanged(bool value)
    {
        if (value)
        {
            try
            {
                _dataLogger = new DataLoggerService(_deviceInfo.PortName);
                LoggerStatus = $"录制到: {_dataLogger.CurrentFilePath}";
                FileLogger.Info($"DataLogger started: {_dataLogger.CurrentFilePath}");
            }
            catch (Exception ex)
            {
                IsLoggingToFile = false;
                LoggerStatus = $"无法开始录制: {ex.Message}";
                FileLogger.Error("DataLogger start failed", ex);
            }
        }
        else
        {
            _dataLogger?.Dispose();
            _dataLogger = null;
            LoggerStatus = string.Empty;
        }
    }

    [ObservableProperty]
    private string _sendStatus = string.Empty;

    [ObservableProperty]
    private SendOutcome _sendStatusKind = SendOutcome.None;

    public enum SendOutcome { None, Info, Success, Warning, Error }

    [RelayCommand]
    private async Task SendData()
    {
        if (string.IsNullOrEmpty(SendText))
        {
            SetSendStatus(SendOutcome.Warning, "发送文本为空");
            return;
        }

        var ending = SendMode == SendMode.Hex ? LineEnding.None : LineEnding;
        byte[] data;
        try
        {
            data = InputParser.Parse(SendText, SendMode, ending);
        }
        catch (Exception ex)
        {
            SetSendStatus(SendOutcome.Error, $"解析输入失败: {ex.Message}");
            FileLogger.Error($"InputParser.Parse failed for mode={SendMode}", ex);
            return;
        }

        if (data.Length == 0)
        {
            SetSendStatus(SendOutcome.Warning,
                SendMode == SendMode.Hex
                    ? "HEX 模式: 输入里没有有效 HEX 字符"
                    : "解析后字节为空");
            return;
        }

        if (!_serialService.IsOpen)
        {
            SetSendStatus(SendOutcome.Error, "串口未打开");
            FileLogger.Warn($"Send rejected: port not open ({_deviceInfo.PortName})");
            ErrorCount++;
            return;
        }

        try
        {
            await _serialService.SendAsync(data);
            SetSendStatus(SendOutcome.Success,
                $"已发送 {data.Length} 字节 [{Convert.ToHexString(data, 0, Math.Min(8, data.Length))}{(data.Length > 8 ? "..." : "")}]");
            FileLogger.Info($"Send OK {_deviceInfo.PortName} {data.Length}B mode={SendMode}");
        }
        catch (Exception ex)
        {
            ErrorCount++;
            SetSendStatus(SendOutcome.Error, $"发送失败: {ex.Message}");
            FileLogger.Error($"Send failed on {_deviceInfo.PortName}", ex);
        }
    }

    private void SetSendStatus(SendOutcome kind, string message)
    {
        SendStatusKind = kind;
        SendStatus = $"[{DateTime.Now:HH:mm:ss}] {message}";
    }

    [RelayCommand]
    private void ClearRecords()
    {
        Records.Clear();
        TerminalText = string.Empty;
        ReceivedBytes = 0;
        SentBytes = 0;
        ErrorCount = 0;
        WarningCount = 0;
        ReceiveRate = 0;
        SendRate = 0;
    }

    [RelayCommand]
    private void Disconnect()
    {
        StopTimedSend();
        TimedSendEnabled = false;
        _serialService.Close();
        _deviceInfo.IsConnected = false;
        _deviceInfo.StatusText = "未连接";
        _deviceInfo.HasError = false;
    }

    [RelayCommand]
    private void ToggleParsedPane() => ShowParsedPane = !ShowParsedPane;

    [RelayCommand]
    private void ClearParsedFrames() => ParsedFrames.Clear();

    [RelayCommand]
    private void TogglePause()
    {
        IsPaused = !IsPaused;
    }

    // Pin state changes flow through the property setter — works for both ToggleButton
    // (IsChecked TwoWay binding) and menu/command path. The previous design that ALSO
    // bound Command="ToggleDtrCommand" double-toggled on click and made the button look
    // unresponsive.
    partial void OnDtrEnabledChanged(bool value)
    {
        try
        {
            _serialService.SetPinState("DTR", value);
            FileLogger.Info($"DTR -> {(value ? "ON" : "OFF")} on {_deviceInfo.PortName}");
        }
        catch (Exception ex) { FileLogger.Warn($"DTR set failed: {ex.Message}"); }
    }

    partial void OnRtsEnabledChanged(bool value)
    {
        try
        {
            _serialService.SetPinState("RTS", value);
            FileLogger.Info($"RTS -> {(value ? "ON" : "OFF")} on {_deviceInfo.PortName}");
        }
        catch (Exception ex) { FileLogger.Warn($"RTS set failed: {ex.Message}"); }
    }

    [RelayCommand]
    private void ToggleDtr() => DtrEnabled = !DtrEnabled;

    [RelayCommand]
    private void ToggleRts() => RtsEnabled = !RtsEnabled;

    [RelayCommand]
    private void ToggleTerminalMode()
    {
        IsTerminalMode = !IsTerminalMode;
        if (!IsTerminalMode) TerminalText = string.Empty;
    }

    [RelayCommand]
    private void ToggleWaveform()
    {
        try
        {
            if (_waveformWindow == null)
            {
                _waveformWindow = new WaveformWindow();
                _waveformWindow.Closing += (_, e) => { e.Cancel = true; _waveformWindow.Hide(); IsWaveformVisible = false; };
            }

            if (_waveformWindow.IsVisible)
            {
                _waveformWindow.Hide();
                IsWaveformVisible = false;
            }
            else
            {
                _waveformWindow.Show();
                IsWaveformVisible = true;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ToggleWaveform error: {ex}");
            _waveformWindow = null;
        }
    }

    [RelayCommand]
    private void ToggleRadar()
    {
        try
        {
            if (_radarWindow == null)
            {
                _radarWindow = new RadarWindow();
                _radarWindow.Closing += (_, e) => { e.Cancel = true; _radarWindow.Hide(); IsRadarVisible = false; };
            }

            if (_radarWindow.IsVisible)
            {
                _radarWindow.Hide();
                IsRadarVisible = false;
            }
            else
            {
                _radarWindow.Show();
                IsRadarVisible = true;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ToggleRadar error: {ex}");
            _radarWindow = null;
        }
    }

    [RelayCommand]
    private void ToggleView3D()
    {
        try
        {
            if (_view3DWindow == null)
            {
                _view3DWindow = new View3DWindow();
                _view3DWindow.Closing += (_, e) => { e.Cancel = true; _view3DWindow.Hide(); IsView3DVisible = false; };
            }

            if (_view3DWindow.IsVisible)
            {
                _view3DWindow.Hide();
                IsView3DVisible = false;
            }
            else
            {
                _view3DWindow.Show();
                IsView3DVisible = true;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ToggleView3D error: {ex}");
            _view3DWindow = null;
        }
    }

    private void ProcessProtocolData(byte[] data)
    {
        if (ActiveProtocol == null) return;
        foreach (var frame in _protocolParser.Feed(data))
        {
            ParsedFrames.Add(frame);
            while (ParsedFrames.Count > 2000) ParsedFrames.RemoveAt(0);

            // Field → Waveform channel binding: push numeric values to mapped channels.
            int maxChannel = -1;
            foreach (var v in frame.FieldValues)
            {
                if (v.WaveformChannel is int ch && v.NumericValue is double num)
                {
                    if (ch > maxChannel) maxChannel = ch;
                    if (_waveformWindow?.IsVisible == true)
                        _waveformWindow.AddDataPoint(ch, (float)num);
                }
            }
            // Auto-grow channel count if the protocol declares more.
            if (maxChannel + 1 > WaveformChannelCount)
                WaveformChannelCount = maxChannel + 1;
        }
    }

    private void StartTimedSend()
    {
        StopTimedSend();
        _timedSendTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(Math.Max(10, TimedSendIntervalMs))
        };
        _timedSendTimer.Tick += async (_, _) =>
        {
            if (!_serialService.IsOpen) { TimedSendEnabled = false; return; }
            await SendData();
        };
        _timedSendTimer.Start();
    }

    private void StopTimedSend()
    {
        _timedSendTimer?.Stop();
        _timedSendTimer = null;
    }

    private void ProcessVisualizationData(byte[] data)
    {
        if (_waveformWindow?.IsVisible != true &&
            _radarWindow?.IsVisible != true &&
            _view3DWindow?.IsVisible != true)
            return;

        var text = System.Text.Encoding.ASCII.GetString(data).Trim();
        var parts = text.Split(new[] { ' ', ',', '\r', '\n', '\t' },
            StringSplitOptions.RemoveEmptyEntries);

        int ch = 0;
        foreach (var part in parts)
        {
            if (float.TryParse(part,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out float value))
            {
                if (_waveformWindow?.IsVisible == true)
                    _waveformWindow.AddDataPoint(ch, value);

                FeedRadar(ch, value);

                ch++;
                if (ch >= WaveformChannelCount) ch = 0;
            }
        }

        if (ch == 0 && parts.Length == 0)
        {
            foreach (var b in data)
            {
                if (_waveformWindow?.IsVisible == true)
                    _waveformWindow.AddDataPoint(0, b);
                FeedRadar(0, b);
            }
        }
    }

    private void FeedRadar(int channel, float value)
    {
        if (_radarWindow?.IsVisible != true && _view3DWindow?.IsVisible != true)
            return;

        int chCount = Math.Max(channel + 1, 1);
        if (_radarBuffers == null || _radarBuffers.Length < chCount)
        {
            _radarBuffers = new float[Math.Max(chCount, 6)][];
            for (int i = 0; i < _radarBuffers.Length; i++)
                _radarBuffers[i] = new float[50];
        }

        if (channel < _radarBuffers.Length)
            _radarBuffers[channel][_radarIndex % 50] = value;

        _radarIndex++;

        if (_radarWindow?.IsVisible == true)
        {
            var radarValues = new float[_radarBuffers.Length];
            var labels = new string[_radarBuffers.Length];
            for (int i = 0; i < _radarBuffers.Length; i++)
            {
                float sum = 0;
                int count = Math.Min(_radarIndex, 50);
                for (int j = 0; j < count; j++) sum += _radarBuffers[i][j];
                radarValues[i] = count > 0 ? sum / count : 0;
                labels[i] = $"CH{i + 1}";
            }
            _radarWindow.SetValues(radarValues, labels);
        }

        if (_radarIndex % 5 == 0 && _view3DWindow?.IsVisible == true)
        {
            float x = 0, y = 0, z = 0;
            for (int i = 0; i < _radarBuffers.Length; i++)
            {
                float sum = 0;
                int count = Math.Min(_radarIndex, 50);
                for (int j = 0; j < count; j++) sum += _radarBuffers[i][j];
                float v = (count > 0 ? sum / count : 0) / 255f * 4 - 2;
                if (i % 3 == 0) x += v;
                else if (i % 3 == 1) y += v;
                else z += v;
            }
            _view3DWindow.AddPoint(x, y, z);
        }
    }

    private void StartRateTimer()
    {
        _rateTimer = new System.Timers.Timer(1000);
        _rateTimer.Elapsed += (_, _) =>
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var rx = ReceivedBytes - _lastReceivedBytes;
                var tx = SentBytes - _lastSentBytes;
                ReceiveRate = rx;
                SendRate = tx;
                _lastReceivedBytes = ReceivedBytes;
                _lastSentBytes = SentBytes;
            });
        };
        _rateTimer.Start();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _renderTimer?.Stop();
        _rateTimer?.Stop();
        _rateTimer?.Dispose();
        StopTimedSend();
        try { _dataLogger?.Dispose(); } catch { }
        _readCts?.Cancel();
        _readCts?.Dispose();
        _serialService.ErrorOccurred -= OnError;
        _serialService.Close();

        if (_waveformWindow != null)
        {
            _waveformWindow.Closing -= (_, e) => e.Cancel = true;
            _waveformWindow.Close();
        }
        if (_radarWindow != null)
        {
            _radarWindow.Closing -= (_, e) => e.Cancel = true;
            _radarWindow.Close();
        }
        if (_view3DWindow != null)
        {
            _view3DWindow.Closing -= (_, e) => e.Cancel = true;
            _view3DWindow.Close();
        }
    }
}

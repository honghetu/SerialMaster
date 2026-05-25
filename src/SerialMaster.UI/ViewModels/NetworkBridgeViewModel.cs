using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SerialMaster.Core.Models;
using SerialMaster.Core.Services;
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Windows;
using System.Windows.Threading;

namespace SerialMaster.UI.ViewModels;

public partial class NetworkBridgeViewModel : ObservableObject, IDisposable
{
    private readonly IDeviceEnumerator _enumerator;
    private readonly ISerialPortService _serial;
    private readonly NetworkBridgeService _bridge;
    private readonly Action _closeCallback;
    private DispatcherTimer? _statsTimer;
    private bool _disposed;

    [ObservableProperty]
    private string _tabTitle = "🌐 TCP/UDP 桥";

    [ObservableProperty]
    private ObservableCollection<string> _availablePorts = new();

    [ObservableProperty]
    private string _selectedPort = string.Empty;

    [ObservableProperty]
    private int _selectedBaudRate = 115200;

    [ObservableProperty]
    private BridgeMode _selectedMode = BridgeMode.TcpServer;

    [ObservableProperty]
    private string _host = "127.0.0.1";

    [ObservableProperty]
    private int _port = 9000;

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private ObservableCollection<string> _logLines = new();

    [ObservableProperty]
    private long _serialRx;
    [ObservableProperty]
    private long _serialTx;
    [ObservableProperty]
    private long _netRx;
    [ObservableProperty]
    private long _netTx;

    public IReadOnlyList<int> BaudRates { get; } =
        new[] { 9600, 38400, 57600, 115200, 230400, 460800, 921600 };

    public IReadOnlyList<BridgeMode> Modes { get; } =
        new[] { BridgeMode.TcpServer, BridgeMode.TcpClient, BridgeMode.Udp };

    public NetworkBridgeViewModel(IDeviceEnumerator enumerator, ISerialPortService serial, Action closeCallback)
    {
        _enumerator = enumerator;
        _serial = serial;
        _bridge = new NetworkBridgeService(serial);
        _closeCallback = closeCallback;

        RefreshPorts();

        _bridge.Log += (_, line) =>
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                LogLines.Add($"[{DateTime.Now:HH:mm:ss}] {line}");
                while (LogLines.Count > 300) LogLines.RemoveAt(0);
            });
    }

    [RelayCommand]
    private void RefreshPorts()
    {
        AvailablePorts.Clear();
        foreach (var p in _enumerator.GetAvailablePorts())
            AvailablePorts.Add(p.PortName);
        if (!AvailablePorts.Contains(SelectedPort)) SelectedPort = string.Empty;
        if (string.IsNullOrEmpty(SelectedPort) && AvailablePorts.Count > 0)
            SelectedPort = AvailablePorts[0];
    }

    [RelayCommand]
    private async Task StartBridge()
    {
        if (IsRunning) return;
        if (string.IsNullOrEmpty(SelectedPort))
        {
            MessageBox.Show("请先选择串口", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var cfg = new SerialConfig(SelectedPort, SelectedBaudRate, 8, Parity.None, StopBits.One);
            _serial.Open(cfg);

            await _bridge.StartAsync(SelectedMode, Host, Port);
            IsRunning = true;
            StartStatsTimer();
        }
        catch (Exception ex)
        {
            LogLines.Add($"启动失败: {ex.Message}");
            try { _serial.Close(); } catch { }
            IsRunning = false;
        }
    }

    [RelayCommand]
    private void StopBridge()
    {
        if (!IsRunning) return;
        _bridge.Stop();
        try { _serial.Close(); } catch { }
        StopStatsTimer();
        IsRunning = false;
    }

    [RelayCommand]
    private void ClearLog()
    {
        LogLines.Clear();
    }

    private void StartStatsTimer()
    {
        StopStatsTimer();
        _statsTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _statsTimer.Tick += (_, _) =>
        {
            SerialRx = _bridge.Stats.ReceivedFromSerial;
            SerialTx = _bridge.Stats.ForwardedToSerial;
            NetRx = _bridge.Stats.ReceivedFromNetwork;
            NetTx = _bridge.Stats.ForwardedToNetwork;
        };
        _statsTimer.Start();
    }

    private void StopStatsTimer()
    {
        _statsTimer?.Stop();
        _statsTimer = null;
    }

    public void Close()
    {
        Dispose();
        _closeCallback();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        StopStatsTimer();
        try { _bridge.Dispose(); } catch { }
        try { _serial.Dispose(); } catch { }
    }
}

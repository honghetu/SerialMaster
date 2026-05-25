using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SerialMaster.Core.Models;
using SerialMaster.Core.Services;
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Windows;

namespace SerialMaster.UI.ViewModels;

public partial class CanBusViewModel : ObservableObject, IDisposable
{
    private readonly IDeviceEnumerator _enumerator;
    private readonly ISerialPortService _serial;
    private readonly SlcanCodec _codec = new();
    private readonly Action _closeCallback;
    private CancellationTokenSource? _readCts;
    private bool _disposed;

    [ObservableProperty]
    private string _tabTitle = "🚌 CAN 总线";

    [ObservableProperty]
    private ObservableCollection<string> _availablePorts = new();

    [ObservableProperty]
    private string _selectedPort = string.Empty;

    [ObservableProperty]
    private int _serialBaudRate = 115200;

    [ObservableProperty]
    private int _selectedCanBitrateIndex = 6;  // 500k

    [ObservableProperty]
    private bool _isOpen;

    [ObservableProperty]
    private ObservableCollection<CanFrame> _frames = new();

    [ObservableProperty]
    private long _rxFrameCount;

    [ObservableProperty]
    private string _sendId = "123";

    [ObservableProperty]
    private string _sendData = "DEAD BEEF";

    [ObservableProperty]
    private bool _sendExtended;

    [ObservableProperty]
    private bool _sendRtr;

    public IReadOnlyList<int> SerialBaudRates { get; } =
        new[] { 9600, 38400, 57600, 115200, 230400, 460800, 921600 };

    public IReadOnlyList<(int Index, int Kbps, string Cmd)> CanBitrates { get; } =
        SlcanCodec.BitrateTable;

    public CanBusViewModel(IDeviceEnumerator enumerator, ISerialPortService serial, Action closeCallback)
    {
        _enumerator = enumerator;
        _serial = serial;
        _closeCallback = closeCallback;

        RefreshPorts();

        _serial.ErrorOccurred += (_, msg) =>
            Application.Current.Dispatcher.BeginInvoke(() => LastError = msg);
    }

    [ObservableProperty]
    private string _lastError = string.Empty;

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
    private async Task OpenAdapter()
    {
        if (IsOpen) return;
        if (string.IsNullOrEmpty(SelectedPort))
        {
            MessageBox.Show("请先选择串口", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var cfg = new SerialConfig(SelectedPort, SerialBaudRate, 8, Parity.None, StopBits.One);
            _serial.Open(cfg);

            // SLCAN: send "C\r" close (in case), "Snn\r" set bitrate, "O\r" open
            var cmd = SlcanCodec.BitrateTable[SelectedCanBitrateIndex].Cmd;
            await _serial.SendAsync(System.Text.Encoding.ASCII.GetBytes("C\r"));
            await Task.Delay(50);
            await _serial.SendAsync(System.Text.Encoding.ASCII.GetBytes(cmd + "\r"));
            await Task.Delay(50);
            await _serial.SendAsync(System.Text.Encoding.ASCII.GetBytes("O\r"));

            IsOpen = true;
            StartReading();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"打开适配器失败: {ex.Message}", "错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task CloseAdapter()
    {
        if (!IsOpen) return;
        try
        {
            await _serial.SendAsync(System.Text.Encoding.ASCII.GetBytes("C\r"));
        }
        catch { }
        _readCts?.Cancel();
        _serial.Close();
        IsOpen = false;
    }

    private void StartReading()
    {
        _readCts?.Cancel();
        _readCts = new CancellationTokenSource();
        var token = _readCts.Token;

        Task.Run(async () =>
        {
            await foreach (var record in _serial.ReceivedData.ReadAllAsync(token))
            {
                if (record.Direction != DataDirection.Receive) continue;

                foreach (var frame in _codec.Decode(record.Data))
                {
                    Application.Current.Dispatcher.BeginInvoke(() =>
                    {
                        Frames.Add(frame);
                        RxFrameCount++;
                        while (Frames.Count > 5000) Frames.RemoveAt(0);
                    });
                }
            }
        }, token);
    }

    [RelayCommand]
    private async Task SendFrame()
    {
        if (!IsOpen)
        {
            MessageBox.Show("请先打开 CAN 适配器", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!uint.TryParse(SendId, System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture, out uint id))
        {
            MessageBox.Show("ID 必须是 HEX 数字", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var data = InputParser.ParseHex(SendData);
        if (data.Length > 8)
        {
            MessageBox.Show("数据最多 8 字节", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var kind = (SendExtended, SendRtr) switch
        {
            (true,  true)  => CanFrameKind.ExtendedRtr,
            (true,  false) => CanFrameKind.Extended,
            (false, true)  => CanFrameKind.StandardRtr,
            _              => CanFrameKind.Standard
        };

        var frame = new CanFrame
        {
            Kind = kind,
            Id = id,
            Dlc = (byte)data.Length,
            Data = data
        };

        try
        {
            await _serial.SendAsync(SlcanCodec.EncodeWithTerminator(frame));
            // Echo locally for visibility
            Application.Current.Dispatcher.BeginInvoke(() => Frames.Add(frame));
        }
        catch (Exception ex)
        {
            MessageBox.Show($"发送失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void ClearFrames()
    {
        Frames.Clear();
        RxFrameCount = 0;
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
        _readCts?.Cancel();
        try { if (_serial.IsOpen) _serial.Close(); } catch { }
        _serial.Dispose();
    }
}

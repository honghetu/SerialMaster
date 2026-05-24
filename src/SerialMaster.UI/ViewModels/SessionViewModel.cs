using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SerialMaster.Core.Models;
using SerialMaster.Core.Services;
using System.Collections.ObjectModel;
using System.Threading.Channels;
using System.Windows;

namespace SerialMaster.UI.ViewModels;

public partial class SessionViewModel : ObservableObject, IDisposable
{
    private readonly ISerialPortService _serialService;
    private readonly DeviceInfo _deviceInfo;
    private CancellationTokenSource? _readCts;
    private bool _disposed;

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
    private string _sendText = string.Empty;

    public DeviceInfo DeviceInfo => _deviceInfo;

    public SessionViewModel(ISerialPortService serialService, DeviceInfo deviceInfo)
    {
        _serialService = serialService;
        _deviceInfo = deviceInfo;
        TabTitle = $"{deviceInfo.PortName}";

        if (deviceInfo.Config != null)
            _serialService.Open(deviceInfo.Config);

        _serialService.ErrorOccurred += OnError;
        _serialService.ConnectionStateChanged += (_, _) =>
        {
            TabTitle = _serialService.IsOpen
                ? $"{deviceInfo.PortName} ●"
                : $"{deviceInfo.PortName} ○";
        };

        StartReading();
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

                Application.Current.Dispatcher.Invoke(() =>
                {
                    Records.Add(record);

                    if (record.Direction == DataDirection.Receive)
                        ReceivedBytes += record.Data.Length;
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

    [RelayCommand]
    private async Task SendData()
    {
        if (string.IsNullOrWhiteSpace(SendText)) return;

        byte[] data;
        string text = SendText.Trim();

        if (text.Replace(" ", "").All(c => "0123456789ABCDEFabcdef".Contains(c)) &&
            text.Contains(' '))
        {
            string hex = text.Replace(" ", "");
            if (hex.Length % 2 != 0) hex = "0" + hex;
            data = new byte[hex.Length / 2];
            for (int i = 0; i < data.Length; i++)
                data[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        }
        else
        {
            data = System.Text.Encoding.ASCII.GetBytes(text);
            data = data.Concat(new byte[] { 0x0D, 0x0A }).ToArray();
        }

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
    private void ClearRecords()
    {
        Records.Clear();
        ReceivedBytes = 0;
        SentBytes = 0;
        ErrorCount = 0;
        WarningCount = 0;
    }

    [RelayCommand]
    private void TogglePause()
    {
        IsPaused = !IsPaused;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _readCts?.Cancel();
        _readCts?.Dispose();
        _serialService.ErrorOccurred -= OnError;
        _serialService.Close();
    }
}

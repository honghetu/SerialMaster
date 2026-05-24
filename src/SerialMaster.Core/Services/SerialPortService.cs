using SerialMaster.Core.Models;
using System.IO.Ports;
using System.Threading.Channels;

namespace SerialMaster.Core.Services;

public sealed class SerialPortService : ISerialPortService
{
    private SerialPort? _port;
    private readonly Channel<DataRecord> _channel;
    private bool _disposed;
    private volatile bool _closing;

    public SerialConfig Config { get; private set; } = null!;
    public bool IsOpen => _port?.IsOpen ?? false;
    public ChannelReader<DataRecord> ReceivedData => _channel.Reader;

    public event EventHandler<string>? ErrorOccurred;
    public event EventHandler? ConnectionStateChanged;

    public SerialPortService()
    {
        _channel = Channel.CreateBounded<DataRecord>(new BoundedChannelOptions(100_000)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });
    }

    public void Open(SerialConfig config)
    {
        Close();
        Config = config;

        _port = new SerialPort(config.PortName, config.BaudRate, config.Parity, config.DataBits, config.StopBits)
        {
            ReadTimeout = config.ReadTimeoutMs,
            WriteTimeout = config.WriteTimeoutMs,
            DtrEnable = true,
            RtsEnable = true,
            NewLine = "\n"
        };

        _port.DataReceived += OnDataReceived;
        _port.ErrorReceived += OnErrorReceived;

        try
        {
            _port.Open();
            ConnectionStateChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"打开串口失败: {ex.Message}");
            throw;
        }
    }

    private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        if (_closing) return;

        var sp = (SerialPort)sender;
        int bytesToRead = sp.BytesToRead;

        if (bytesToRead <= 0) return;

        byte[] buffer = new byte[bytesToRead];
        try
        {
            sp.Read(buffer, 0, bytesToRead);
        }
        catch (TimeoutException)
        {
            return;
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"读取错误: {ex.Message}");
            return;
        }

        var record = new DataRecord(DateTime.Now, DataDirection.Receive, buffer, RecordStatus.Success);
        _channel.Writer.TryWrite(record);
    }

    private void OnErrorReceived(object sender, SerialErrorReceivedEventArgs e)
    {
        ErrorOccurred?.Invoke(this, $"串口错误: {e.EventType}");
    }

    public async Task SendAsync(byte[] data)
    {
        if (_port == null || !_port.IsOpen)
            throw new InvalidOperationException("串口未打开");

        try
        {
            await Task.Run(() => _port.Write(data, 0, data.Length));

            var record = new DataRecord(DateTime.Now, DataDirection.Send, data, RecordStatus.Success);
            _channel.Writer.TryWrite(record);
        }
        catch (Exception ex)
        {
            var record = new DataRecord(DateTime.Now, DataDirection.Send, data, RecordStatus.Failed);
            _channel.Writer.TryWrite(record);
            ErrorOccurred?.Invoke(this, $"发送失败: {ex.Message}");
            throw;
        }
    }

    public void SetPinState(string pin, bool state)
    {
        if (_port == null) return;

        switch (pin.ToUpper())
        {
            case "DTR": _port.DtrEnable = state; break;
            case "RTS": _port.RtsEnable = state; break;
            default: throw new ArgumentException($"Unknown pin name: {pin}", nameof(pin));
        }
    }

    public bool GetPinState(string pin)
    {
        if (_port == null) return false;

        return pin.ToUpper() switch
        {
            "DTR" => _port.DtrEnable,
            "RTS" => _port.RtsEnable,
            "CTS" => _port.CtsHolding,
            "DSR" => _port.DsrHolding,
            _ => throw new ArgumentException($"Unknown pin name: {pin}", nameof(pin))
        };
    }

    public void Close()
    {
        if (_port != null)
        {
            _closing = true;
            _port.DataReceived -= OnDataReceived;
            _port.ErrorReceived -= OnErrorReceived;

            bool wasOpen = _port.IsOpen;
            try
            {
                if (wasOpen) _port.Close();
            }
            catch { }

            _port.Dispose();
            _port = null;

            if (wasOpen)
                ConnectionStateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _channel.Writer.TryComplete();
        Close();
    }
}

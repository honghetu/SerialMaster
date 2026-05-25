using SerialMaster.Core.Models;
using System.IO.Ports;
using System.Threading.Channels;

namespace SerialMaster.Core.Services;

public sealed class SerialPortService : ISerialPortService
{
    private SerialPort? _port;
    private readonly Channel<DataRecord> _channel;
    private CancellationTokenSource? _readCts;
    private Task? _readLoop;
    private bool _disposed;

    public SerialConfig Config { get; private set; } = null!;
    public bool IsOpen => _port?.IsOpen ?? false;
    public ChannelReader<DataRecord> ReceivedData => _channel.Reader;

    public event EventHandler<string>? ErrorOccurred;
    public event EventHandler? ConnectionStateChanged;
    public event EventHandler<DataRecord>? DataReceived;

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

        _port.ErrorReceived += OnErrorReceived;

        try
        {
            _port.Open();
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"打开串口失败: {ex.Message}");
            _port.Dispose();
            _port = null;
            throw;
        }

        _readCts = new CancellationTokenSource();
        _readLoop = Task.Run(() => ReadLoopAsync(_port, _readCts.Token));
        ConnectionStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private async Task ReadLoopAsync(SerialPort port, CancellationToken token)
    {
        var buffer = new byte[4096];
        var stream = port.BaseStream;
        int consecutiveErrors = 0;

        while (!token.IsCancellationRequested)
        {
            int bytesRead;
            try
            {
                bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), token)
                    .ConfigureAwait(false);
                consecutiveErrors = 0;  // success — reset error counter
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (TimeoutException)
            {
                continue;
            }
            catch (Exception ex)
            {
                if (token.IsCancellationRequested) break;

                FileLogger.Warn($"Read transient: {ex.GetType().Name}: {ex.Message}");

                // Port is permanently gone → exit loop and signal disconnect.
                bool portDead = !port.IsOpen ||
                                (ex is UnauthorizedAccessException) ||
                                (ex is IOException && ex.Message.Contains("device", StringComparison.OrdinalIgnoreCase)) ||
                                consecutiveErrors >= 10;
                if (portDead)
                {
                    ErrorOccurred?.Invoke(this, $"读取错误 (端口断开): {ex.Message}");
                    FileLogger.Error("Read loop terminating: port appears dead", ex);
                    break;
                }

                // Transient (frame/parity/overrun) — warn user but keep running.
                consecutiveErrors++;
                ErrorOccurred?.Invoke(this, $"读取警告 ({consecutiveErrors}): {ex.Message}");
                try { await Task.Delay(50, token).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
                continue;
            }

            if (bytesRead <= 0) continue;

            var data = new byte[bytesRead];
            Buffer.BlockCopy(buffer, 0, data, 0, bytesRead);
            var record = new DataRecord(DateTime.Now, DataDirection.Receive, data, RecordStatus.Success);
            _channel.Writer.TryWrite(record);
            DataReceived?.Invoke(this, record);
        }

        FileLogger.Info($"Read loop exited for {port.PortName} (cancellation requested: {token.IsCancellationRequested})");
    }

    private void OnErrorReceived(object sender, SerialErrorReceivedEventArgs e)
    {
        ErrorOccurred?.Invoke(this, $"串口错误: {e.EventType}");
    }

    public async Task SendAsync(byte[] data)
    {
        if (_port == null || !_port.IsOpen)
            throw new InvalidOperationException("串口未打开");

        // SerialPort.WriteTimeout doesn't reliably apply to BaseStream.WriteAsync on Windows.
        // Without an external timeout, a flaky driver / unresponsive device can hang the
        // write forever, leaving AsyncRelayCommand stuck in IsExecuting=true (button disabled).
        var timeoutMs = Math.Max(500, _port.WriteTimeout);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(timeoutMs));

        try
        {
            await _port.BaseStream.WriteAsync(data.AsMemory(0, data.Length), cts.Token)
                .ConfigureAwait(false);

            var record = new DataRecord(DateTime.Now, DataDirection.Send, data, RecordStatus.Success);
            _channel.Writer.TryWrite(record);
            DataReceived?.Invoke(this, record);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            var record = new DataRecord(DateTime.Now, DataDirection.Send, data, RecordStatus.Timeout);
            _channel.Writer.TryWrite(record);
            DataReceived?.Invoke(this, record);
            ErrorOccurred?.Invoke(this, $"写超时 ({timeoutMs}ms) — 设备未响应");
            throw new TimeoutException($"写超时 ({timeoutMs}ms) — 设备未响应");
        }
        catch (Exception ex)
        {
            var record = new DataRecord(DateTime.Now, DataDirection.Send, data, RecordStatus.Failed);
            _channel.Writer.TryWrite(record);
            DataReceived?.Invoke(this, record);
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
        if (_port == null) return;

        _readCts?.Cancel();
        _port.ErrorReceived -= OnErrorReceived;

        bool wasOpen = _port.IsOpen;
        try
        {
            if (wasOpen) _port.Close();
        }
        catch { }

        // Do NOT block on _readLoop here — Cancel + Close already trigger the loop
        // to unwind on its own. Waiting on the UI thread caused multi-port disconnect
        // to stack up. The loop will observe cancellation and exit independently.
        _port.Dispose();
        _port = null;
        _readCts?.Dispose();
        _readCts = null;
        _readLoop = null;

        if (wasOpen)
            ConnectionStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _channel.Writer.TryComplete();
        Close();
    }
}

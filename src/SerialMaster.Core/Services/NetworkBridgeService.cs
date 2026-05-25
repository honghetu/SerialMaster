using System.Net;
using System.Net.Sockets;

namespace SerialMaster.Core.Services;

public enum BridgeMode
{
    TcpServer,
    TcpClient,
    Udp
}

public sealed class BridgeStats
{
    public long ReceivedFromSerial { get; set; }
    public long ForwardedToNetwork { get; set; }
    public long ReceivedFromNetwork { get; set; }
    public long ForwardedToSerial { get; set; }
    public DateTime StartedAt { get; set; }
}

/// <summary>
/// Bidirectional bridge: every byte from the serial port is forwarded to the network,
/// every byte from the network is forwarded back to the serial port.
/// The bridge owns the serial service it's given — caller should not share it.
/// </summary>
public sealed class NetworkBridgeService : IDisposable
{
    private readonly ISerialPortService _serial;
    private CancellationTokenSource? _cts;
    private Task? _serialPumpTask;
    private Task? _netPumpTask;
    private TcpListener? _listener;
    private TcpClient? _tcpClient;
    private UdpClient? _udp;
    private IPEndPoint? _udpPeer;
    private NetworkStream? _stream;
    private bool _disposed;

    public BridgeStats Stats { get; } = new();
    public bool IsRunning => _cts is { IsCancellationRequested: false };

    public event EventHandler<string>? Log;

    public NetworkBridgeService(ISerialPortService serial)
    {
        _serial = serial;
    }

    public async Task StartAsync(BridgeMode mode, string host, int port)
    {
        if (IsRunning) throw new InvalidOperationException("Bridge already running");
        if (!_serial.IsOpen) throw new InvalidOperationException("Serial port must be open before bridging");

        _cts = new CancellationTokenSource();
        Stats.StartedAt = DateTime.Now;

        switch (mode)
        {
            case BridgeMode.TcpServer:
                _listener = new TcpListener(IPAddress.Any, port);
                _listener.Start();
                Log?.Invoke(this, $"TCP server listening on :{port}, awaiting client...");
                _tcpClient = await _listener.AcceptTcpClientAsync(_cts.Token).ConfigureAwait(false);
                _stream = _tcpClient.GetStream();
                Log?.Invoke(this, $"Client connected: {_tcpClient.Client.RemoteEndPoint}");
                break;

            case BridgeMode.TcpClient:
                _tcpClient = new TcpClient();
                await _tcpClient.ConnectAsync(host, port, _cts.Token).ConfigureAwait(false);
                _stream = _tcpClient.GetStream();
                Log?.Invoke(this, $"Connected to {host}:{port}");
                break;

            case BridgeMode.Udp:
                _udp = new UdpClient(port);
                _udpPeer = string.IsNullOrEmpty(host)
                    ? null
                    : new IPEndPoint(IPAddress.Parse(host), port);
                Log?.Invoke(this, $"UDP bound :{port}, peer={(_udpPeer?.ToString() ?? "(first sender)")}");
                break;
        }

        _serialPumpTask = Task.Run(() => SerialToNetworkLoop(mode, _cts.Token));
        _netPumpTask = Task.Run(() => NetworkToSerialLoop(mode, _cts.Token));
    }

    public void Stop()
    {
        _cts?.Cancel();
        try { _stream?.Close(); } catch { }
        try { _tcpClient?.Close(); } catch { }
        try { _listener?.Stop(); } catch { }
        try { _udp?.Close(); } catch { }

        try { _serialPumpTask?.Wait(TimeSpan.FromSeconds(1)); } catch { }
        try { _netPumpTask?.Wait(TimeSpan.FromSeconds(1)); } catch { }

        _stream = null;
        _tcpClient = null;
        _listener = null;
        _udp = null;
        _udpPeer = null;
        _cts?.Dispose();
        _cts = null;

        Log?.Invoke(this, "Bridge stopped");
    }

    private async Task SerialToNetworkLoop(BridgeMode mode, CancellationToken token)
    {
        try
        {
            await foreach (var record in _serial.ReceivedData.ReadAllAsync(token))
            {
                if (record.Data == null || record.Data.Length == 0) continue;
                if (record.Direction != Models.DataDirection.Receive) continue;  // only forward inbound bytes

                Stats.ReceivedFromSerial += record.Data.Length;

                switch (mode)
                {
                    case BridgeMode.TcpServer:
                    case BridgeMode.TcpClient:
                        if (_stream != null)
                        {
                            await _stream.WriteAsync(record.Data, token).ConfigureAwait(false);
                            Stats.ForwardedToNetwork += record.Data.Length;
                        }
                        break;
                    case BridgeMode.Udp:
                        if (_udp != null && _udpPeer != null)
                        {
                            await _udp.SendAsync(record.Data, record.Data.Length, _udpPeer).ConfigureAwait(false);
                            Stats.ForwardedToNetwork += record.Data.Length;
                        }
                        break;
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (!token.IsCancellationRequested)
                Log?.Invoke(this, $"Serial→Net error: {ex.Message}");
        }
    }

    private async Task NetworkToSerialLoop(BridgeMode mode, CancellationToken token)
    {
        var buffer = new byte[4096];
        try
        {
            while (!token.IsCancellationRequested)
            {
                int bytesRead = 0;
                switch (mode)
                {
                    case BridgeMode.TcpServer:
                    case BridgeMode.TcpClient:
                        if (_stream == null) return;
                        bytesRead = await _stream.ReadAsync(buffer.AsMemory(0, buffer.Length), token)
                            .ConfigureAwait(false);
                        if (bytesRead == 0) return;
                        break;
                    case BridgeMode.Udp:
                        if (_udp == null) return;
                        var udpResult = await _udp.ReceiveAsync(token).ConfigureAwait(false);
                        _udpPeer ??= udpResult.RemoteEndPoint;
                        Buffer.BlockCopy(udpResult.Buffer, 0, buffer, 0, udpResult.Buffer.Length);
                        bytesRead = udpResult.Buffer.Length;
                        break;
                }

                if (bytesRead <= 0) continue;

                Stats.ReceivedFromNetwork += bytesRead;
                var slice = new byte[bytesRead];
                Buffer.BlockCopy(buffer, 0, slice, 0, bytesRead);

                try
                {
                    await _serial.SendAsync(slice).ConfigureAwait(false);
                    Stats.ForwardedToSerial += bytesRead;
                }
                catch (Exception ex)
                {
                    Log?.Invoke(this, $"Net→Serial write error: {ex.Message}");
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (!token.IsCancellationRequested)
                Log?.Invoke(this, $"Net→Serial error: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }
}

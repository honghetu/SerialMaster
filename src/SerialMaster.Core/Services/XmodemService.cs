using SerialMaster.Core.Models;
using System.Collections.Concurrent;

namespace SerialMaster.Core.Services;

/// <summary>
/// XMODEM-CRC / XMODEM-Checksum file transfer.
/// Uses ISerialPortService.DataReceived event (not Channel) so it can coexist
/// with other consumers of the same port.
/// </summary>
public class XmodemService
{
    private readonly ISerialPortService _serial;
    private readonly Action<string> _log;

    private const byte SOH = 0x01;  // 128 byte block
    private const byte STX = 0x02;  // 1024 byte block (not used in sender)
    private const byte EOT = 0x04;
    private const byte ACK = 0x06;
    private const byte NAK = 0x15;
    private const byte CAN = 0x18;
    private const byte SUB = 0x1A;  // padding
    private const byte CRC_REQ = (byte)'C';
    private const int BlockSize = 128;

    public event Action<int, int>? ProgressChanged;

    public XmodemService(ISerialPortService serial, Action<string> log)
    {
        _serial = serial;
        _log = log;
    }

    /// <summary>Send a file to a remote XMODEM receiver. Returns true on success.</summary>
    public async Task<bool> SendAsync(string filePath, CancellationToken token)
    {
        var data = await File.ReadAllBytesAsync(filePath, token);
        int totalBlocks = (data.Length + BlockSize - 1) / BlockSize;

        using var rx = new RxQueue(_serial);
        try
        {
            // Receiver starts by sending 'C' (CRC mode) or NAK (checksum mode).
            byte starter = await rx.WaitForAnyAsync(new byte[] { CRC_REQ, NAK }, 30000, token);
            bool useCrc = starter == CRC_REQ;
            _log(useCrc ? "接收方请求 CRC 模式" : "接收方请求 Checksum 模式");

            int blockNum = 1;
            for (int offset = 0; offset < data.Length; offset += BlockSize)
            {
                token.ThrowIfCancellationRequested();

                int remaining = Math.Min(BlockSize, data.Length - offset);
                byte[] packet = useCrc
                    ? new byte[3 + BlockSize + 2]
                    : new byte[3 + BlockSize + 1];

                packet[0] = SOH;
                packet[1] = (byte)(blockNum & 0xFF);
                packet[2] = (byte)(~blockNum & 0xFF);
                Array.Copy(data, offset, packet, 3, remaining);
                for (int i = remaining; i < BlockSize; i++) packet[3 + i] = SUB;

                if (useCrc)
                {
                    ushort crc = ComputeCrcXmodem(packet, 3, BlockSize);
                    packet[3 + BlockSize] = (byte)(crc >> 8);
                    packet[3 + BlockSize + 1] = (byte)(crc & 0xFF);
                }
                else
                {
                    byte sum = 0;
                    for (int i = 3; i < 3 + BlockSize; i++) sum += packet[i];
                    packet[3 + BlockSize] = sum;
                }

                int retries = 10;
                while (retries-- > 0)
                {
                    token.ThrowIfCancellationRequested();
                    await _serial.SendAsync(packet);
                    byte resp = await rx.WaitForAnyAsync(new byte[] { ACK, NAK, CAN }, 5000, token);
                    if (resp == ACK) break;
                    if (resp == CAN) { _log("传输被接收方取消"); return false; }
                    _log($"块 {blockNum} 重试 ({retries} 次剩余)");
                }
                if (retries <= 0)
                {
                    _log($"块 {blockNum} 超过最大重试，放弃");
                    return false;
                }

                _log($"发送块 {blockNum}/{totalBlocks}");
                ProgressChanged?.Invoke(blockNum, totalBlocks);
                blockNum++;
            }

            await _serial.SendAsync(new byte[] { EOT });
            byte final = await rx.WaitForAnyAsync(new byte[] { ACK }, 5000, token);
            _log(final == ACK ? "传输完成" : "传输完成 (未收到最终确认)");
            return true;
        }
        catch (OperationCanceledException)
        {
            _log("传输已取消");
            try { await _serial.SendAsync(new byte[] { CAN, CAN }); } catch { }
            return false;
        }
        catch (TimeoutException ex)
        {
            _log($"超时: {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            _log($"传输错误: {ex.Message}");
            return false;
        }
    }

    /// <summary>Receive a file via XMODEM-CRC and write to filePath.</summary>
    public async Task<bool> ReceiveAsync(string filePath, CancellationToken token)
    {
        using var rx = new RxQueue(_serial);
        using var fs = File.Create(filePath);

        try
        {
            // Send 'C' periodically to request CRC mode.
            for (int attempt = 0; attempt < 10; attempt++)
            {
                token.ThrowIfCancellationRequested();
                await _serial.SendAsync(new byte[] { CRC_REQ });
                if (await rx.PeekAsync(3000, token)) break;
                _log("等待发送方握手...");
            }

            int expectedBlock = 1;
            byte[] payload = new byte[BlockSize];

            while (true)
            {
                token.ThrowIfCancellationRequested();
                byte head = await rx.WaitForAnyAsync(new byte[] { SOH, STX, EOT, CAN }, 10000, token);
                if (head == EOT)
                {
                    await _serial.SendAsync(new byte[] { ACK });
                    _log("传输完成");
                    return true;
                }
                if (head == CAN) { _log("发送方取消"); return false; }
                if (head == STX) { _log("不支持 1K 块"); return false; }

                byte blockNum = await rx.NextAsync(2000, token);
                byte blockComp = await rx.NextAsync(2000, token);
                if ((byte)~blockNum != blockComp)
                {
                    await _serial.SendAsync(new byte[] { NAK });
                    _log("块号校验失败，请求重发");
                    continue;
                }

                for (int i = 0; i < BlockSize; i++)
                    payload[i] = await rx.NextAsync(2000, token);

                byte crcHi = await rx.NextAsync(2000, token);
                byte crcLo = await rx.NextAsync(2000, token);
                ushort recvCrc = (ushort)((crcHi << 8) | crcLo);
                ushort calcCrc = ComputeCrcXmodem(payload, 0, BlockSize);

                if (recvCrc != calcCrc)
                {
                    await _serial.SendAsync(new byte[] { NAK });
                    _log($"块 {blockNum} CRC 错误，请求重发");
                    continue;
                }

                if (blockNum == (byte)expectedBlock)
                {
                    await fs.WriteAsync(payload.AsMemory(0, BlockSize), token);
                    expectedBlock = (expectedBlock + 1) & 0xFF;
                    if (expectedBlock == 0) expectedBlock = 1;  // wrap
                    ProgressChanged?.Invoke(expectedBlock - 1, -1);
                    _log($"已接收块 {blockNum}");
                }
                // For duplicates we still ACK (sender's previous ACK was lost).
                await _serial.SendAsync(new byte[] { ACK });
            }
        }
        catch (OperationCanceledException)
        {
            _log("接收已取消");
            try { await _serial.SendAsync(new byte[] { CAN, CAN }); } catch { }
            return false;
        }
        catch (Exception ex)
        {
            _log($"接收错误: {ex.Message}");
            return false;
        }
    }

    private static ushort ComputeCrcXmodem(byte[] buffer, int offset, int length)
    {
        ushort crc = 0;
        for (int i = offset; i < offset + length; i++)
        {
            crc ^= (ushort)(buffer[i] << 8);
            for (int b = 0; b < 8; b++)
                crc = (crc & 0x8000) != 0 ? (ushort)((crc << 1) ^ 0x1021) : (ushort)(crc << 1);
        }
        return crc;
    }

    /// <summary>
    /// Buffers bytes from ISerialPortService.DataReceived (Receive direction only)
    /// without consuming the primary Channel. Disposable via using.
    /// </summary>
    private sealed class RxQueue : IDisposable
    {
        private readonly ISerialPortService _serial;
        private readonly ConcurrentQueue<byte> _queue = new();
        private readonly SemaphoreSlim _signal = new(0);
        private bool _disposed;

        public RxQueue(ISerialPortService serial)
        {
            _serial = serial;
            _serial.DataReceived += OnDataReceived;
        }

        private void OnDataReceived(object? sender, DataRecord record)
        {
            if (record.Direction != DataDirection.Receive) return;
            foreach (var b in record.Data)
            {
                _queue.Enqueue(b);
                _signal.Release();
            }
        }

        public async Task<byte> NextAsync(int timeoutMs, CancellationToken token)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            cts.CancelAfter(timeoutMs);
            try
            {
                while (true)
                {
                    if (_queue.TryDequeue(out byte b)) return b;
                    await _signal.WaitAsync(cts.Token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested)
            {
                throw new TimeoutException($"等待字节超时 ({timeoutMs}ms)");
            }
        }

        public async Task<byte> WaitForAnyAsync(byte[] expected, int timeoutMs, CancellationToken token)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            cts.CancelAfter(timeoutMs);
            try
            {
                while (true)
                {
                    while (_queue.TryDequeue(out byte b))
                    {
                        if (expected.Contains(b)) return b;
                    }
                    await _signal.WaitAsync(cts.Token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested)
            {
                throw new TimeoutException($"等待 {string.Join(',', expected.Select(b => b.ToString("X2")))} 超时");
            }
        }

        public async Task<bool> PeekAsync(int timeoutMs, CancellationToken token)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            cts.CancelAfter(timeoutMs);
            try
            {
                if (!_queue.IsEmpty) return true;
                await _signal.WaitAsync(cts.Token).ConfigureAwait(false);
                _signal.Release();  // give the byte back to NextAsync/WaitForAnyAsync
                return true;
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested)
            {
                return false;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _serial.DataReceived -= OnDataReceived;
            _signal.Dispose();
        }
    }
}

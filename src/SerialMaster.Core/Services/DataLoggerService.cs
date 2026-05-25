using SerialMaster.Core.Models;
using System.Text;

namespace SerialMaster.Core.Services;

public enum LogFormat
{
    /// <summary>"[timestamp] →/← HEX" — human readable, MainViewModel.OpenLog can re-parse.</summary>
    TextWithTimestamps,
    /// <summary>Raw concatenated bytes only — for binary capture / re-injection.</summary>
    RawBinary,
    /// <summary>One JSON line per record.</summary>
    Jsonl
}

/// <summary>
/// Background-friendly logger that writes serial records to disk.
/// Default path: %LocalAppData%/SerialMaster/data/{port}_{yyyyMMdd}.log
/// Rolls to a new file when the current file exceeds <see cref="MaxFileBytes"/>.
/// </summary>
public sealed class DataLoggerService : IDisposable
{
    private readonly object _lock = new();
    private FileStream? _stream;
    private string? _currentPath;
    private long _bytesWritten;
    private bool _disposed;

    public string Directory { get; }
    public string PortName { get; }
    public LogFormat Format { get; set; } = LogFormat.TextWithTimestamps;
    public long MaxFileBytes { get; set; } = 100 * 1024 * 1024;  // 100 MB roll size
    public long TotalBytesWritten { get; private set; }
    public string? CurrentFilePath => _currentPath;

    public DataLoggerService(string portName, string? customDir = null)
    {
        PortName = portName;
        Directory = customDir ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SerialMaster", "data");
        System.IO.Directory.CreateDirectory(Directory);
        OpenNewFile();
    }

    private void OpenNewFile()
    {
        Close();
        _bytesWritten = 0;
        var safe = string.Concat(PortName.Where(c => char.IsLetterOrDigit(c)));
        var ext = Format == LogFormat.RawBinary ? "bin" : (Format == LogFormat.Jsonl ? "jsonl" : "log");
        var fileName = $"{safe}_{DateTime.Now:yyyyMMdd-HHmmss}.{ext}";
        _currentPath = Path.Combine(Directory, fileName);
        _stream = new FileStream(_currentPath, FileMode.Create, FileAccess.Write, FileShare.Read, 8192);
    }

    public void Write(DataRecord record)
    {
        if (_disposed) return;

        byte[] payload;
        switch (Format)
        {
            case LogFormat.RawBinary:
                payload = record.Data;
                break;
            case LogFormat.Jsonl:
                var jsonObj = new
                {
                    t = record.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffK"),
                    dir = record.Direction == DataDirection.Send ? "tx" : "rx",
                    status = record.Status.ToString(),
                    hex = Convert.ToHexString(record.Data)
                };
                payload = Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(jsonObj) + "\n");
                break;
            default:
                var dir = record.Direction == DataDirection.Send ? "→" : "←";
                var hex = BitConverter.ToString(record.Data).Replace("-", " ");
                var status = record.Status == RecordStatus.Failed ? " FAILED" : "";
                payload = Encoding.UTF8.GetBytes(
                    $"[{record.Timestamp:yyyy-MM-dd HH:mm:ss.fff}] {dir} {hex}{status}\n");
                break;
        }

        lock (_lock)
        {
            if (_stream == null) return;
            _stream.Write(payload, 0, payload.Length);
            _bytesWritten += payload.Length;
            TotalBytesWritten += payload.Length;

            if (_bytesWritten >= MaxFileBytes)
            {
                _stream.Flush();
                OpenNewFile();
            }
        }
    }

    public void Flush()
    {
        lock (_lock) { _stream?.Flush(); }
    }

    public void Close()
    {
        lock (_lock)
        {
            try
            {
                _stream?.Flush();
                _stream?.Dispose();
            }
            catch { }
            _stream = null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Close();
    }
}

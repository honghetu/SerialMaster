using SerialMaster.Core.Models;
using System.Threading.Channels;

namespace SerialMaster.Core.Services;

public interface ISerialPortService : IDisposable
{
    SerialConfig Config { get; }
    bool IsOpen { get; }
    ChannelReader<DataRecord> ReceivedData { get; }

    event EventHandler<string>? ErrorOccurred;
    event EventHandler? ConnectionStateChanged;

    /// <summary>
    /// Fired (in addition to the Channel) whenever bytes are received or sent.
    /// Use this for transient consumers like file-transfer protocols (XMODEM) that
    /// need to peek bytes without consuming the primary Channel.
    /// </summary>
    event EventHandler<DataRecord>? DataReceived;

    void Open(SerialConfig config);
    void Close();
    Task SendAsync(byte[] data);
    void SetPinState(string pin, bool state);
    bool GetPinState(string pin);
}

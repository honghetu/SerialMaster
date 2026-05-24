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

    void Open(SerialConfig config);
    void Close();
    Task SendAsync(byte[] data);
    void SetPinState(string pin, bool state);
    bool GetPinState(string pin);
}

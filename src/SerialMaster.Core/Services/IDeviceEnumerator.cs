using SerialMaster.Core.Models;

namespace SerialMaster.Core.Services;

public interface IDeviceEnumerator
{
    IReadOnlyList<DeviceInfo> GetAvailablePorts();
    void StartWatching(Action<IReadOnlyList<DeviceInfo>> onChanged, int intervalMs = 2000);
    void StopWatching();
}

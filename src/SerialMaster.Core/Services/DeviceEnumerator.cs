using SerialMaster.Core.Models;
using System.IO.Ports;
using System.Management;
using Microsoft.Win32;

namespace SerialMaster.Core.Services;

[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed class DeviceEnumerator : IDeviceEnumerator, IDisposable
{
    private CancellationTokenSource? _watchCts;

    public IReadOnlyList<DeviceInfo> GetAvailablePorts()
    {
        var ports = new List<DeviceInfo>();
        var descriptions = GetPortDescriptions();

        foreach (string portName in SerialPort.GetPortNames())
        {
            descriptions.TryGetValue(portName, out string? desc);
            ports.Add(new DeviceInfo
            {
                PortName = portName,
                Description = desc ?? "(未知设备)",
                IsConnected = false,
                StatusText = "未连接"
            });
        }

        return ports.OrderBy(p => p.PortName).ToList();
    }

    private static Dictionary<string, string> GetPortDescriptions()
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"SELECT Name, DeviceID FROM Win32_PnPEntity WHERE Name IS NOT NULL");

            foreach (var obj in searcher.Get())
            {
                string? name = obj["Name"]?.ToString();
                string? deviceId = obj["DeviceID"]?.ToString();

                if (name == null || deviceId == null) continue;

                var match = System.Text.RegularExpressions.Regex.Match(name, @"\(COM(\d+)\)");
                if (match.Success)
                {
                    string portName = "COM" + match.Groups[1].Value;
                    if (!result.ContainsKey(portName))
                        result[portName] = name;
                }
            }
        }
        catch { }

        if (result.Count == 0)
        {
            try
            {
                using var regKey = Registry.LocalMachine.OpenSubKey(
                    @"HARDWARE\DEVICEMAP\SERIALCOMM");
                if (regKey != null)
                {
                    foreach (string valueName in regKey.GetValueNames())
                    {
                        string? portName = regKey.GetValue(valueName)?.ToString();
                        if (portName != null && !result.ContainsKey(portName))
                            result[portName] = valueName;
                    }
                }
            }
            catch { }
        }

        return result;
    }

    public void StartWatching(Action<IReadOnlyList<DeviceInfo>> onChanged, int intervalMs = 2000)
    {
        StopWatching();
        _watchCts = new CancellationTokenSource();
        var token = _watchCts.Token;

        Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var ports = GetAvailablePorts();
                    onChanged(ports);
                    await Task.Delay(intervalMs, token);
                }
                catch (OperationCanceledException) { break; }
                catch { }
            }
        }, token);
    }

    public void StopWatching()
    {
        _watchCts?.Cancel();
        _watchCts?.Dispose();
        _watchCts = null;
    }

    public void Dispose() => StopWatching();
}

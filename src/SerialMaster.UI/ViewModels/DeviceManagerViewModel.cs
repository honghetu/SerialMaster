using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SerialMaster.Core.Models;
using SerialMaster.Core.Services;
using System.Collections.ObjectModel;
using System.Windows;

namespace SerialMaster.UI.ViewModels;

public partial class DeviceManagerViewModel : ObservableObject
{
    private readonly IDeviceEnumerator _enumerator;
    private readonly ISettingsService _settingsService;

    [ObservableProperty]
    private ObservableCollection<DeviceInfo> _devices = new();

    [ObservableProperty]
    private DeviceInfo? _selectedDevice;

    public event EventHandler<DeviceInfo>? DeviceConnectRequested;

    public DeviceManagerViewModel(
        IDeviceEnumerator enumerator,
        ISettingsService settingsService)
    {
        _enumerator = enumerator;
        _settingsService = settingsService;

        RefreshDevices();

        _enumerator.StartWatching(ports =>
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                foreach (var newDevice in ports)
                {
                    var existing = Devices.FirstOrDefault(d => d.PortName == newDevice.PortName);
                    if (existing != null)
                    {
                        existing.Description = newDevice.Description;
                    }
                    else
                    {
                        Devices.Add(newDevice);
                    }
                }

                foreach (var existing in Devices)
                {
                    if (!ports.Any(p => p.PortName == existing.PortName))
                    {
                        if (existing.IsConnected)
                        {
                            existing.IsConnected = false;
                            existing.StatusText = "设备已断开";
                            existing.HasError = true;
                            existing.Config = null;
                        }
                    }
                }

                var toRemove = Devices
                    .Where(d => !ports.Any(p => p.PortName == d.PortName) && !d.IsConnected)
                    .ToList();
                foreach (var item in toRemove)
                    Devices.Remove(item);
            });
        });
    }

    [RelayCommand]
    private void RefreshDevices()
    {
        var ports = _enumerator.GetAvailablePorts();

        var connected = Devices.Where(d => d.IsConnected).ToDictionary(d => d.PortName);
        Devices.Clear();
        foreach (var port in ports)
        {
            if (connected.TryGetValue(port.PortName, out var existing))
            {
                port.IsConnected = existing.IsConnected;
                port.StatusText = existing.StatusText;
                port.Config = existing.Config;
            }
            Devices.Add(port);
        }
    }

    [RelayCommand]
    private void ConnectDevice(DeviceInfo? device)
    {
        if (device == null) return;

        if (device.IsConnected)
        {
            device.IsConnected = false;
            device.StatusText = "未连接";
            device.HasError = false;
            DeviceConnectRequested?.Invoke(this, device);
            return;
        }

        var settings = _settingsService.Load();
        int baudRate = device.PortName == settings.LastPort
            ? settings.LastBaudRate : 115200;

        try
        {
            var config = new SerialConfig(device.PortName, baudRate);
            device.Config = config;
            device.IsConnected = true;
            device.StatusText = $"已连接 ({baudRate})";
            device.HasError = false;

            DeviceConnectRequested?.Invoke(this, device);

            settings.LastPort = device.PortName;
            settings.LastBaudRate = baudRate;
            _settingsService.Save(settings);
        }
        catch (Exception ex)
        {
            device.HasError = true;
            device.StatusText = $"连接失败: {ex.Message}";
        }
    }
}

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using SerialMaster.Core.Models;
using SerialMaster.Core.Services;
using SerialMaster.UI.Themes;
using System.Collections.ObjectModel;

namespace SerialMaster.UI.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IDeviceEnumerator _deviceEnumerator;
    private readonly ISettingsService _settingsService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ThemeService _themeService;

    [ObservableProperty]
    private ObservableCollection<SessionViewModel> _sessions = new();

    [ObservableProperty]
    private SessionViewModel? _activeSession;

    public DeviceManagerViewModel DeviceManager { get; }

    public MainViewModel(
        IDeviceEnumerator deviceEnumerator,
        ISettingsService settingsService,
        IServiceProvider serviceProvider,
        ThemeService themeService,
        DeviceManagerViewModel deviceManager)
    {
        _deviceEnumerator = deviceEnumerator;
        _settingsService = settingsService;
        _serviceProvider = serviceProvider;
        _themeService = themeService;
        DeviceManager = deviceManager;

        DeviceManager.DeviceConnectRequested += OnDeviceConnectRequested;
    }

    private void OnDeviceConnectRequested(object? sender, DeviceInfo device)
    {
        if (device.IsConnected)
        {
            var serialService = _serviceProvider.GetRequiredService<ISerialPortService>();
            var session = new SessionViewModel(serialService, device);

            Sessions.Add(session);
            ActiveSession = session;
        }
        else
        {
            var session = Sessions.FirstOrDefault(s =>
                s.DeviceInfo?.PortName == device.PortName);

            if (session != null)
            {
                session.Dispose();
                Sessions.Remove(session);
            }
        }
    }

    [RelayCommand]
    private void ToggleTheme()
    {
        _themeService.ToggleTheme();

        var settings = _settingsService.Load();
        settings.Theme = _themeService.CurrentTheme;
        _settingsService.Save(settings);
    }

    [RelayCommand]
    private void CloseSession(SessionViewModel? session)
    {
        if (session == null) return;

        session.Dispose();
        Sessions.Remove(session);

        var device = DeviceManager.Devices
            .FirstOrDefault(d => d.PortName == session.DeviceInfo?.PortName);

        if (device != null)
        {
            device.IsConnected = false;
            device.StatusText = "未连接";
            device.HasError = false;
        }
    }
}

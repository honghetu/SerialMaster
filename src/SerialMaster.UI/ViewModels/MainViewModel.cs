using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using SerialMaster.Core.Models;
using SerialMaster.Core.Services;
using SerialMaster.UI.Themes;
using System.Collections.ObjectModel;
using System.Windows;

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

    [ObservableProperty]
    private bool _isLightTheme;

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

        IsLightTheme = _themeService.CurrentTheme == "Light";

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
        IsLightTheme = _themeService.CurrentTheme == "Light";

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

    [RelayCommand]
    private void NewConnection() => DeviceManager.ConnectDeviceCommand.Execute(DeviceManager.SelectedDevice);

    [RelayCommand]
    private void SaveLog() => MessageBox.Show("保存日志功能将在 Phase 2 实现", "提示",
        MessageBoxButton.OK, MessageBoxImage.Information);

    [RelayCommand]
    private void ExportData() => MessageBox.Show("导出数据功能将在 Phase 2 实现", "提示",
        MessageBoxButton.OK, MessageBoxImage.Information);

    [RelayCommand]
    private void Exit() => Application.Current.Shutdown();

    [RelayCommand]
    private void ClearCurrent()
    {
        ActiveSession?.ClearRecordsCommand.Execute(null);
    }

    [RelayCommand]
    private void ClearAll()
    {
        foreach (var s in Sessions.ToList())
            s.ClearRecordsCommand.Execute(null);
    }

    [RelayCommand]
    private void OpenFavorites() => MessageBox.Show("发送收藏夹将在 Phase 2 实现", "提示",
        MessageBoxButton.OK, MessageBoxImage.Information);

    [RelayCommand]
    private void ToggleWaveform() => MessageBox.Show("波形视图将在 Phase 3 实现", "提示",
        MessageBoxButton.OK, MessageBoxImage.Information);

    [RelayCommand]
    private void ToggleRadar() => MessageBox.Show("雷达视图将在 Phase 3 实现", "提示",
        MessageBoxButton.OK, MessageBoxImage.Information);

    [RelayCommand]
    private void ToggleView3D() => MessageBox.Show("3D 视图将在 Phase 3 实现", "提示",
        MessageBoxButton.OK, MessageBoxImage.Information);

    [RelayCommand]
    private void ToggleStatistics() => MessageBox.Show("统计面板将在 Phase 2 实现", "提示",
        MessageBoxButton.OK, MessageBoxImage.Information);

    [RelayCommand]
    private void ToggleTerminal() => MessageBox.Show("终端模式将在 Phase 2 实现", "提示",
        MessageBoxButton.OK, MessageBoxImage.Information);

    [RelayCommand]
    private void OpenChecksum() => MessageBox.Show("校验计算器将在 Phase 2 实现", "提示",
        MessageBoxButton.OK, MessageBoxImage.Information);

    [RelayCommand]
    private void OpenParser() => MessageBox.Show("协议解析器将在 Phase 4 实现", "提示",
        MessageBoxButton.OK, MessageBoxImage.Information);

    [RelayCommand]
    private void OpenMacro() => MessageBox.Show("宏编辑器将在 Phase 4 实现", "提示",
        MessageBoxButton.OK, MessageBoxImage.Information);

    [RelayCommand]
    private void OpenFileTransfer() => MessageBox.Show("文件传输将在 Phase 4 实现", "提示",
        MessageBoxButton.OK, MessageBoxImage.Information);

    [RelayCommand]
    private void ResetDtr() => MessageBox.Show("引脚控制将在 Phase 2 实现", "提示",
        MessageBoxButton.OK, MessageBoxImage.Information);

    [RelayCommand]
    private void BootRts() => MessageBox.Show("引脚控制将在 Phase 2 实现", "提示",
        MessageBoxButton.OK, MessageBoxImage.Information);

    [RelayCommand]
    private void About()
    {
        MessageBox.Show("SerialMaster 串口大师 v1.0.0\n\n" +
                        "面向嵌入式开发的串口调试工具\n" +
                        "WPF + .NET 8 | MVVM | AvalonDock\n\n" +
                        "Phase 1 MVP",
                        "关于 SerialMaster",
                        MessageBoxButton.OK, MessageBoxImage.Information);
    }
}

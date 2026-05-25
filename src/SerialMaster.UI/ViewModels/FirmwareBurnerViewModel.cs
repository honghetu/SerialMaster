using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SerialMaster.Core.Models;
using SerialMaster.Core.Services;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;

namespace SerialMaster.UI.ViewModels;

public partial class FirmwareBurnerViewModel : ObservableObject
{
    private readonly IDeviceEnumerator _enumerator;
    private readonly FirmwareBurnerService _service = new();
    private readonly Action _closeCallback;

    [ObservableProperty]
    private string _tabTitle = "🔥 烧录器";

    [ObservableProperty]
    private ChipFamily _selectedFamily = ChipFamily.Esp32;

    [ObservableProperty]
    private string _firmwarePath = string.Empty;

    [ObservableProperty]
    private string _selectedPort = string.Empty;

    [ObservableProperty]
    private int _baudRate = 921600;

    [ObservableProperty]
    private string _executablePath = string.Empty;

    [ObservableProperty]
    private int _flashOffset;

    [ObservableProperty]
    private ObservableCollection<string> _availablePorts = new();

    [ObservableProperty]
    private ObservableCollection<string> _logLines = new();

    [ObservableProperty]
    private bool _isBurning;

    public IReadOnlyList<ChipFamily> Families { get; } =
        new[] { ChipFamily.Esp32, ChipFamily.Esp8266, ChipFamily.Stm32Isp };

    public IReadOnlyList<int> BaudRates { get; } =
        new[] { 9600, 57600, 115200, 230400, 460800, 921600, 1500000 };

    public FirmwareBurnerViewModel(IDeviceEnumerator enumerator, Action closeCallback)
    {
        _enumerator = enumerator;
        _closeCallback = closeCallback;

        RefreshPorts();
        ExecutablePath = FirmwareBurnerService.DefaultExecutable(SelectedFamily);

        _service.OutputReceived += (_, line) =>
        {
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                LogLines.Add(line);
                while (LogLines.Count > 500) LogLines.RemoveAt(0);
            });
        };
        _service.Exited += (_, code) =>
        {
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                LogLines.Add(code == 0 ? "✓ 烧录完成 (exit 0)" : $"✗ 失败 (exit {code})");
                IsBurning = false;
            });
        };
    }

    partial void OnSelectedFamilyChanged(ChipFamily value)
    {
        ExecutablePath = FirmwareBurnerService.DefaultExecutable(value);
        FlashOffset = value switch
        {
            ChipFamily.Esp32   => 0x10000,
            ChipFamily.Esp8266 => 0x0,
            _ => 0
        };
    }

    [RelayCommand]
    private void RefreshPorts()
    {
        AvailablePorts.Clear();
        foreach (var p in _enumerator.GetAvailablePorts())
            AvailablePorts.Add(p.PortName);
        if (!string.IsNullOrEmpty(SelectedPort) && !AvailablePorts.Contains(SelectedPort))
            SelectedPort = string.Empty;
        if (string.IsNullOrEmpty(SelectedPort) && AvailablePorts.Count > 0)
            SelectedPort = AvailablePorts[0];
    }

    [RelayCommand]
    private void BrowseFirmware()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "选择固件文件",
            Filter = "固件文件 (*.bin;*.hex)|*.bin;*.hex|所有文件 (*.*)|*.*"
        };
        if (dlg.ShowDialog() == true) FirmwarePath = dlg.FileName;
    }

    [RelayCommand]
    private void BrowseExecutable()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "选择烧录工具可执行文件",
            Filter = "可执行文件 (*.exe)|*.exe|所有文件 (*.*)|*.*"
        };
        if (dlg.ShowDialog() == true) ExecutablePath = dlg.FileName;
    }

    [RelayCommand]
    private async Task Burn()
    {
        if (IsBurning) return;
        if (string.IsNullOrEmpty(FirmwarePath) || !File.Exists(FirmwarePath))
        {
            MessageBox.Show("请先选择有效的固件文件", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (string.IsNullOrEmpty(SelectedPort))
        {
            MessageBox.Show("请先选择端口", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        LogLines.Clear();
        IsBurning = true;

        var req = new BurnRequest
        {
            Family = SelectedFamily,
            FirmwarePath = FirmwarePath,
            PortName = SelectedPort,
            BaudRate = BaudRate,
            FlashOffset = FlashOffset,
            ExecutableOverride = ExecutablePath
        };

        try
        {
            await _service.StartAsync(req);
        }
        catch (Exception ex)
        {
            LogLines.Add($"启动失败: {ex.Message}");
            IsBurning = false;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        _service.Cancel();
        LogLines.Add("[已取消]");
        IsBurning = false;
    }

    public void Close()
    {
        Cancel();
        _closeCallback();
    }
}

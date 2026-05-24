using CommunityToolkit.Mvvm.ComponentModel;

namespace SerialMaster.Core.Models;

public partial class DeviceInfo : ObservableObject
{
    [ObservableProperty]
    private string _portName = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private SerialConfig? _config;

    [ObservableProperty]
    private string _statusText = "未连接";

    [ObservableProperty]
    private bool _hasError;
}

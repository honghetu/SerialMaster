using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SerialMaster.Core.Services;
using System.IO;
using System.Windows;

namespace SerialMaster.UI.ViewModels;

public partial class FileTransferViewModel : ObservableObject
{
    private readonly Func<ISerialPortService?> _getSerialService;
    private readonly Action _closeCallback;
    private CancellationTokenSource? _cts;

    [ObservableProperty]
    private string _filePath = string.Empty;

    [ObservableProperty]
    private string _statusText = "选择文件并开始传输...";

    [ObservableProperty]
    private int _progress;

    [ObservableProperty]
    private int _progressMax = 1;

    [ObservableProperty]
    private bool _isTransferring;

    [ObservableProperty]
    private string _transferLog = string.Empty;

    [ObservableProperty]
    private string _modeText = "待选择";

    [ObservableProperty]
    private string _tabTitle = "📁 文件传输";

    public FileTransferViewModel(Func<ISerialPortService?> getSerialService, Action closeCallback)
    {
        _getSerialService = getSerialService;
        _closeCallback = closeCallback;
    }

    public void Close()
    {
        _cts?.Cancel();
        _closeCallback();
    }

    [RelayCommand]
    private void BrowseFile()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "选择要发送的文件",
            Filter = "所有文件 (*.*)|*.*"
        };
        if (dlg.ShowDialog() == true)
        {
            FilePath = dlg.FileName;
            ModeText = $"发送: {Path.GetFileName(FilePath)} " +
                       $"(大小: {new FileInfo(FilePath).Length:N0} 字节)";
        }
    }

    [RelayCommand]
    private async Task SendFile()
    {
        if (string.IsNullOrEmpty(FilePath) || !File.Exists(FilePath))
        {
            MessageBox.Show("请先选择文件", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var serial = _getSerialService();
        if (serial == null || !serial.IsOpen)
        {
            MessageBox.Show("没有活动的串口连接", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        IsTransferring = true;
        StatusText = "准备发送...";
        Progress = 0;

        var xmodem = new XmodemService(serial, msg =>
            Application.Current.Dispatcher.Invoke(() =>
                AppendLog(msg)));

        xmodem.ProgressChanged += (block, total) =>
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Progress = block;
                ProgressMax = total;
                StatusText = $"发送中... 块 {block}/{total}";
            });
        };

        _cts = new CancellationTokenSource();
        AppendLog("=== XMODEM 发送开始 ===");
        var success = await xmodem.SendAsync(FilePath, _cts.Token);

        Application.Current.Dispatcher.Invoke(() =>
        {
            IsTransferring = false;
            StatusText = success ? "传输完成" : "传输失败";
        });
    }

    [RelayCommand]
    private async Task ReceiveFile()
    {
        var serial = _getSerialService();
        if (serial == null || !serial.IsOpen)
        {
            MessageBox.Show("没有活动的串口连接", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = "保存接收的文件为",
            Filter = "所有文件 (*.*)|*.*",
            FileName = $"xmodem-{DateTime.Now:yyyyMMdd-HHmmss}.bin"
        };
        if (dlg.ShowDialog() != true) return;

        FilePath = dlg.FileName;
        ModeText = $"接收: {Path.GetFileName(FilePath)}";
        IsTransferring = true;
        StatusText = "等待发送方...";
        Progress = 0;

        var xmodem = new XmodemService(serial, msg =>
            Application.Current.Dispatcher.Invoke(() => AppendLog(msg)));

        xmodem.ProgressChanged += (block, _) =>
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Progress = block;
                ProgressMax = Math.Max(block, ProgressMax);
                StatusText = $"接收中... 块 {block}";
            });
        };

        _cts = new CancellationTokenSource();
        AppendLog("=== XMODEM 接收开始 ===");
        var success = await xmodem.ReceiveAsync(FilePath, _cts.Token);

        Application.Current.Dispatcher.Invoke(() =>
        {
            IsTransferring = false;
            StatusText = success ? "接收完成" : "接收失败";
        });
    }

    [RelayCommand]
    private void CancelTransfer()
    {
        _cts?.Cancel();
        IsTransferring = false;
        StatusText = "已取消";
        AppendLog("传输已取消");
    }

    private void AppendLog(string msg)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        TransferLog += $"[{timestamp}] {msg}\n";
        if (TransferLog.Length > 5000) TransferLog = TransferLog[^3000..];
    }
}

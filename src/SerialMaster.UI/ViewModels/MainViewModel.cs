using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using SerialMaster.Core.Models;
using SerialMaster.Core.Services;
using SerialMaster.UI.Themes;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;

namespace SerialMaster.UI.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IDeviceEnumerator _deviceEnumerator;
    private readonly ISettingsService _settingsService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ThemeService _themeService;

    [ObservableProperty]
    private ObservableCollection<ObservableObject> _sessions = new();

    [ObservableProperty]
    private ObservableObject? _activeSession;

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
            var session = Sessions.OfType<SessionViewModel>()
                .FirstOrDefault(s => s.DeviceInfo?.PortName == device.PortName);

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
    private void CloseSession(ObservableObject? session)
    {
        if (session == null) return;

        switch (session)
        {
            case SessionViewModel svm:
                svm.Dispose();
                var device = DeviceManager.Devices
                    .FirstOrDefault(d => d.PortName == svm.DeviceInfo?.PortName);
                if (device != null)
                {
                    device.IsConnected = false;
                    device.StatusText = "未连接";
                    device.HasError = false;
                }
                break;
            case LogViewerSession lvs:
                lvs.Close();
                break;
        }

        Sessions.Remove(session);
    }

    [RelayCommand]
    private void OpenLog()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "打开日志文件",
            Filter = "日志文件 (*.log;*.txt)|*.log;*.txt|所有文件 (*.*)|*.*",
            DefaultExt = ".log"
        };

        if (dlg.ShowDialog() != true) return;

        try
        {
            var lines = File.ReadAllLines(dlg.FileName);
            var records = new ObservableCollection<DataRecord>();

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                // 解析格式: [yyyy-MM-dd HH:mm:ss.fff] →/← HEX_DATA STATUS
                var record = ParseLogLine(line);
                if (record != null)
                    records.Add(record);
            }

            // 创建一个只读的日志查看 Session
            var viewerSession = new LogViewerSession(
                Path.GetFileName(dlg.FileName),
                records,
                () => Sessions.Remove(Sessions.FirstOrDefault(s => s is LogViewerSession lvs2 && lvs2.TabTitle == Path.GetFileName(dlg.FileName))));

            Sessions.Add(viewerSession);
            ActiveSession = viewerSession;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"打开文件失败:\n{ex.Message}", "错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static DataRecord? ParseLogLine(string line)
    {
        try
        {
            // 格式: [2026-05-24 12:30:01.234] → AA BB 0C (或带 FAILED)
            var timeEnd = line.IndexOf(']');
            if (timeEnd < 0) return null;
            var timeStr = line.Substring(1, timeEnd - 1);
            if (!DateTime.TryParse(timeStr, out var timestamp)) return null;

            var rest = line.Substring(timeEnd + 2).Trim();
            var direction = rest.StartsWith("→") ? DataDirection.Send :
                            rest.StartsWith("←") ? DataDirection.Receive : DataDirection.Receive;

            var dataPart = rest.Substring(2).Trim();
            bool failed = dataPart.Contains("FAILED");

            // 提取 HEX 字节
            var hex = dataPart.Split(' ').TakeWhile(s =>
                s.Length == 2 && s.All(c => "0123456789ABCDEFabcdef".Contains(c))).ToArray();
            if (hex.Length == 0) return null;

            var bytes = hex.Select(h => Convert.ToByte(h, 16)).ToArray();

            return new DataRecord(timestamp, direction, bytes,
                failed ? RecordStatus.Failed : RecordStatus.Success);
        }
        catch { return null; }
    }

    [RelayCommand]
    private void SaveLog()
    {
        if (ActiveSession == null)
        {
            MessageBox.Show("没有活动的会话", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = "保存日志",
            Filter = "日志文件 (*.log)|*.log|文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*",
            DefaultExt = ".log",
            FileName = $"{GetSessionTitle(ActiveSession)}_{DateTime.Now:yyyy-MM-dd}.log"
                .Replace(" ", "_").Replace("●", "").Replace("○", "").Replace("__", "_")
        };

        if (dlg.ShowDialog() != true) return;

        try
        {
            var records = ActiveSession switch
            {
                SessionViewModel s => s.Records,
                LogViewerSession l => l.Records,
                _ => new ObservableCollection<DataRecord>()
            };

            using var writer = new StreamWriter(dlg.FileName, false, System.Text.Encoding.UTF8);
            foreach (var r in records)
            {
                var dir = r.Direction == DataDirection.Send ? "→" : "←";
                var hex = BitConverter.ToString(r.Data).Replace("-", " ");
                var status = r.Status == RecordStatus.Failed ? " FAILED" : "";
                writer.WriteLine($"[{r.Timestamp:yyyy-MM-dd HH:mm:ss.fff}] {dir} {hex}{status}");
            }

            MessageBox.Show($"已保存 {records.Count} 条记录", "保存完成",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"保存失败:\n{ex.Message}", "错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void ExportData()
    {
        if (ActiveSession == null)
        {
            MessageBox.Show("没有活动的会话", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = "导出数据",
            Filter = "CSV 文件 (*.csv)|*.csv|文本文件 (*.txt)|*.txt|JSON 文件 (*.json)|*.json",
            DefaultExt = ".csv",
            FileName = $"{GetSessionTitle(ActiveSession)}_{DateTime.Now:yyyy-MM-dd}.csv"
                .Replace(" ", "_").Replace("●", "").Replace("○", "")
        };

        if (dlg.ShowDialog() != true) return;

        try
        {
            var records = ActiveSession switch
            {
                SessionViewModel s => s.Records,
                LogViewerSession l => l.Records,
                _ => new ObservableCollection<DataRecord>()
            };

            using var writer = new StreamWriter(dlg.FileName, false, System.Text.Encoding.UTF8);

            if (dlg.FileName.EndsWith(".json"))
            {
                var json = System.Text.Json.JsonSerializer.Serialize(
                    records.Select(r => new {
                        Time = r.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                        Direction = r.Direction == DataDirection.Send ? "Send" : "Receive",
                        Hex = BitConverter.ToString(r.Data).Replace("-", " "),
                        Status = r.Status.ToString()
                    }),
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                writer.Write(json);
            }
            else
            {
                // CSV
                writer.WriteLine("时间,方向,数据(HEX),状态");
                foreach (var r in records)
                {
                    var dir = r.Direction == DataDirection.Send ? "Send" : "Receive";
                    var hex = BitConverter.ToString(r.Data).Replace("-", " ");
                    writer.WriteLine($"{r.Timestamp:yyyy-MM-dd HH:mm:ss.fff},{dir},{hex},{r.Status}");
                }
            }

            MessageBox.Show($"已导出 {records.Count} 条记录", "导出完成",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"导出失败:\n{ex.Message}", "错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void Exit() => Application.Current.Shutdown();

    [RelayCommand]
    private void ClearCurrent()
    {
        switch (ActiveSession)
        {
            case SessionViewModel svm: svm.ClearRecordsCommand.Execute(null); break;
            case LogViewerSession lvs: lvs.ClearRecordsCommand.Execute(null); break;
        }
    }

    [RelayCommand]
    private void ClearAll()
    {
        foreach (var s in Sessions.ToList())
        {
            switch (s)
            {
                case SessionViewModel svm: svm.ClearRecordsCommand.Execute(null); break;
                case LogViewerSession lvs: lvs.ClearRecordsCommand.Execute(null); break;
            }
        }
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

    private static string GetSessionTitle(ObservableObject? session)
    {
        return session switch
        {
            SessionViewModel svm => svm.TabTitle,
            LogViewerSession lvs => lvs.TabTitle,
            _ => "session"
        };
    }
}

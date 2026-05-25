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

        var favorites = _serviceProvider.GetRequiredService<SerialMaster.UI.Services.FavoritesService>();
        favorites.Changed += (_, _) =>
        {
            var items = favorites.Load();
            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                foreach (var s in Sessions.OfType<SessionViewModel>())
                    s.RefreshQuickSends(items);
            });
        };
    }

    private async void OnDeviceConnectRequested(object? sender, DeviceInfo device)
    {
        if (device.IsConnected)
        {
            var serialService = _serviceProvider.GetRequiredService<ISerialPortService>();
            var connectedStatus = device.StatusText;
            device.StatusText = "正在连接...";
            device.HasError = false;

            SessionViewModel? session = null;
            try
            {
                // Open is synchronous and may block several seconds on flaky USB-Serial drivers.
                // Push it to a background thread so the UI stays responsive.
                session = await Task.Run(() => new SessionViewModel(serialService, device));

                var favoritesService = _serviceProvider.GetRequiredService<SerialMaster.UI.Services.FavoritesService>();
                session.RefreshQuickSends(favoritesService.Load());

                device.StatusText = connectedStatus;
                Sessions.Add(session);
                ActiveSession = session;
            }
            catch (Exception ex)
            {
                device.IsConnected = false;
                device.HasError = true;
                device.StatusText = $"连接失败: {ShortenError(ex.Message)}";
                try { serialService.Dispose(); } catch { }
            }
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

    private static string ShortenError(string msg)
    {
        if (string.IsNullOrEmpty(msg)) return "未知错误";
        // Strip CR/LF, cap length so it fits the device list line
        msg = msg.Replace("\r", " ").Replace("\n", " ").Trim();
        return msg.Length > 80 ? msg[..80] + "..." : msg;
    }

    [RelayCommand]
    private void NewConnection()
    {
        // Focus device manager and refresh; selecting + connecting is then one click for the user.
        DeviceManager.RefreshDevicesCommand.Execute(null);
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
            case PlaceholderSession ps:
                ps.Close();
                break;
            case ChecksumViewModel cs:
                cs.Close();
                break;
            case FavoritesViewModel fv:
                fv.Close();
                break;
            case ProtocolParserViewModel pp:
                pp.Close();
                break;
            case MacroEditorViewModel me:
                me.Close();
                break;
            case FileTransferViewModel ft:
                ft.Close();
                break;
            case FirmwareBurnerViewModel fb:
                fb.Close();
                break;
            case CanBusViewModel cb:
                cb.Close();
                break;
            case NetworkBridgeViewModel nb:
                nb.Close();
                break;
            case StatisticsViewModel sv:
                sv.Close();
                break;
            case HelpViewModel hv:
                hv.Close();
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
    private void OpenFavorites()
    {
        var favoritesService = _serviceProvider.GetRequiredService<SerialMaster.UI.Services.FavoritesService>();
        var session = new FavoritesViewModel(
            favoritesService,
            async data =>
            {
                if (ActiveSession is SessionViewModel svm)
                    await svm.SendHexDataAsync(data);
                else
                    MessageBox.Show("没有活动的串口会话", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            },
            () => Sessions.Remove(Sessions.FirstOrDefault(s => s is FavoritesViewModel)));
        Sessions.Add(session);
        ActiveSession = session;
    }

    [RelayCommand]
    private void ToggleWaveform()
    {
        if (ActiveSession is SessionViewModel svm)
            svm.ToggleWaveformCommand.Execute(null);
    }

    [RelayCommand]
    private void ToggleRadar()
    {
        if (ActiveSession is SessionViewModel svm)
            svm.ToggleRadarCommand.Execute(null);
    }

    [RelayCommand]
    private void ToggleView3D()
    {
        if (ActiveSession is SessionViewModel svm)
            svm.ToggleView3DCommand.Execute(null);
    }

    [RelayCommand]
    private void ToggleStatistics()
    {
        var existing = Sessions.OfType<StatisticsViewModel>().FirstOrDefault();
        if (existing != null) { ActiveSession = existing; return; }

        var session = new StatisticsViewModel(
            () => ActiveSession as SessionViewModel
                  ?? Sessions.OfType<SessionViewModel>().FirstOrDefault(),
            () => Sessions.Remove(Sessions.FirstOrDefault(s => s is StatisticsViewModel)!));
        Sessions.Add(session);
        ActiveSession = session;
    }

    [RelayCommand]
    private void ToggleTerminal()
    {
        if (ActiveSession is SessionViewModel svm)
            svm.ToggleTerminalModeCommand.Execute(null);
    }

    [RelayCommand]
    private void OpenChecksum()
    {
        var session = new ChecksumViewModel(
            () => Sessions.Remove(Sessions.FirstOrDefault(s => s is ChecksumViewModel)));
        Sessions.Add(session);
        ActiveSession = session;
    }

    [RelayCommand]
    private void OpenParser()
    {
        var existing = Sessions.OfType<ProtocolParserViewModel>().FirstOrDefault();
        if (existing != null) { ActiveSession = existing; return; }

        var store = _serviceProvider.GetRequiredService<IProtocolDefinitionStore>();
        var session = new ProtocolParserViewModel(
            store,
            () => Sessions.OfType<SessionViewModel>().FirstOrDefault(s => s == ActiveSession)
                  ?? Sessions.OfType<SessionViewModel>().FirstOrDefault(),
            () => Sessions.Remove(Sessions.FirstOrDefault(s => s is ProtocolParserViewModel)!));
        Sessions.Add(session);
        ActiveSession = session;
    }

    [RelayCommand]
    private void OpenMacro()
    {
        var session = new MacroEditorViewModel(
            async data =>
            {
                if (ActiveSession is SessionViewModel svm)
                    await svm.SendHexDataAsync(data);
                else
                    MessageBox.Show("没有活动的串口会话", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            },
            () => Sessions.Remove(Sessions.FirstOrDefault(s => s is MacroEditorViewModel)));
        Sessions.Add(session);
        ActiveSession = session;
    }

    [RelayCommand]
    private void OpenFileTransfer()
    {
        var session = new FileTransferViewModel(
            () => ActiveSession is SessionViewModel svm ? svm.SerialService : null,
            () => Sessions.Remove(Sessions.FirstOrDefault(s => s is FileTransferViewModel)));
        Sessions.Add(session);
        ActiveSession = session;
    }

    [RelayCommand]
    private void OpenBurner()
    {
        var existing = Sessions.OfType<FirmwareBurnerViewModel>().FirstOrDefault();
        if (existing != null) { ActiveSession = existing; return; }

        var enumerator = _serviceProvider.GetRequiredService<IDeviceEnumerator>();
        var session = new FirmwareBurnerViewModel(
            enumerator,
            () => Sessions.Remove(Sessions.FirstOrDefault(s => s is FirmwareBurnerViewModel)!));
        Sessions.Add(session);
        ActiveSession = session;
    }

    [RelayCommand]
    private void OpenCanBus()
    {
        var existing = Sessions.OfType<CanBusViewModel>().FirstOrDefault();
        if (existing != null) { ActiveSession = existing; return; }

        var enumerator = _serviceProvider.GetRequiredService<IDeviceEnumerator>();
        var serial = _serviceProvider.GetRequiredService<ISerialPortService>();
        var session = new CanBusViewModel(
            enumerator, serial,
            () => Sessions.Remove(Sessions.FirstOrDefault(s => s is CanBusViewModel)!));
        Sessions.Add(session);
        ActiveSession = session;
    }

    [RelayCommand]
    private void OpenBridge()
    {
        var existing = Sessions.OfType<NetworkBridgeViewModel>().FirstOrDefault();
        if (existing != null) { ActiveSession = existing; return; }

        var enumerator = _serviceProvider.GetRequiredService<IDeviceEnumerator>();
        var serial = _serviceProvider.GetRequiredService<ISerialPortService>();
        var session = new NetworkBridgeViewModel(
            enumerator, serial,
            () => Sessions.Remove(Sessions.FirstOrDefault(s => s is NetworkBridgeViewModel)!));
        Sessions.Add(session);
        ActiveSession = session;
    }

    [RelayCommand]
    private void ResetDtr()
    {
        if (ActiveSession is SessionViewModel svm)
            svm.ToggleDtrCommand.Execute(null);
    }

    [RelayCommand]
    private void BootRts()
    {
        if (ActiveSession is SessionViewModel svm)
            svm.ToggleRtsCommand.Execute(null);
    }

    private void OpenPlaceholder(string feature, string icon, string phase, string description)
    {
        var session = new PlaceholderSession(feature, icon, phase, description,
            () => Sessions.Remove(Sessions.FirstOrDefault(s =>
                s is PlaceholderSession ps && ps.TabTitle == $"{icon} {feature}")));

        Sessions.Add(session);
        ActiveSession = session;
    }

    [RelayCommand]
    private async Task CheckForUpdates()
    {
        var svc = new UpdateCheckService();
        var current = AppVersion();
        var info = await svc.CheckAsync(current);

        if (!string.IsNullOrEmpty(info.Error))
        {
            MessageBox.Show($"检查更新失败:\n{info.Error}\n\n你也可以直接访问\n{UpdateCheckService.ReleasesPageUrl}",
                "SerialMaster — 检查更新", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (info.HasUpdate)
        {
            var msg = $"发现新版本 v{info.LatestVersion}\n当前版本 v{info.CurrentVersion?.ToString(3)}\n\n是否打开下载页?";
            if (MessageBox.Show(msg, "SerialMaster — 新版本可用",
                MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
            {
                OpenUrl(info.ReleaseUrl ?? UpdateCheckService.ReleasesPageUrl);
            }
        }
        else
        {
            MessageBox.Show($"已是最新版本 v{info.CurrentVersion?.ToString(3)}",
                "SerialMaster — 检查更新", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    public async Task CheckForUpdatesSilentAsync()
    {
        // Called from App on startup; only notifies if a new version exists, never on errors.
        try
        {
            await Task.Delay(2500);  // let UI settle first
            var svc = new UpdateCheckService();
            var current = AppVersion();
            var info = await svc.CheckAsync(current);
            if (!info.HasUpdate || info.LatestVersion == null) return;

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var ans = MessageBox.Show(
                    $"SerialMaster 有新版本 v{info.LatestVersion}（你当前 v{info.CurrentVersion?.ToString(3)}）\n\n是否打开下载页?",
                    "SerialMaster — 后台检查发现新版本",
                    MessageBoxButton.YesNo, MessageBoxImage.Information);
                if (ans == MessageBoxResult.Yes)
                    OpenUrl(info.ReleaseUrl ?? UpdateCheckService.ReleasesPageUrl);
            });
        }
        catch { /* silent — startup check must never crash */ }
    }

    private static void OpenUrl(string url)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url, UseShellExecute = true
            });
        }
        catch { }
    }

    [RelayCommand]
    private void OpenHelp()
    {
        var existing = Sessions.OfType<HelpViewModel>().FirstOrDefault();
        if (existing != null) { ActiveSession = existing; return; }

        var help = new HelpViewModel(
            () => Sessions.Remove(Sessions.FirstOrDefault(s => s is HelpViewModel)!));
        Sessions.Add(help);
        ActiveSession = help;
    }

    /// <summary>Always returns the entry .exe's version (i.e. SerialMaster.App).</summary>
    private static Version AppVersion()
        => System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version
           ?? typeof(MainViewModel).Assembly.GetName().Version
           ?? new Version(0, 0);

    [RelayCommand]
    private void About()
    {
        var version = AppVersion().ToString(3);
        MessageBox.Show(
            $"SerialMaster 串口大师 v{version}\n\n" +
            "面向嵌入式开发的多功能串口调试工具\n" +
            "WPF + .NET 8 | MVVM\n\n" +
            "https://github.com/honghetu/SerialMaster\n" +
            "联系邮箱: 13947617581@163.com",
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

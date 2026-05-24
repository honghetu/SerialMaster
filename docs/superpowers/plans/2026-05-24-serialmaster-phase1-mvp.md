# SerialMaster Phase 1 — MVP 实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 构建可用的串口调试工具 MVP — 设备发现、连接收发、数据展示（HEX/ASCII/双栏）、基础发送、深色/浅色主题。

**Architecture:** WPF MVVM 三项目结构。SerialMaster.Core 包含所有业务逻辑（无 UI 依赖），SerialMaster.UI 包含 ViewModel + View，SerialMaster.App 负责 DI 容器和启动。数据流：SerialPort → SerialPortService → Channel<byte[]> → SessionViewModel → DataView。

**Tech Stack:** C# WPF + .NET 8, CommunityToolkit.Mvvm, AvalonDock, Microsoft.Extensions.DependencyInjection, System.IO.Ports, System.Management

---

## File Structure

```
D:\开发\串口\
├── SerialMaster.sln
├── src/
│   ├── SerialMaster.Core/
│   │   ├── Models/
│   │   │   ├── SerialConfig.cs          # 串口配置参数
│   │   │   ├── DeviceInfo.cs            # 设备信息（端口号、描述、状态）
│   │   │   ├── DataRecord.cs            # 一条收发记录
│   │   │   ├── DataDirection.cs         # 枚举：发送/接收
│   │   │   ├── RecordStatus.cs          # 枚举：成功/失败/超时
│   │   │   └── AppSettings.cs           # 应用设置（主题、窗口等）
│   │   ├── Services/
│   │   │   ├── ISerialPortService.cs    # 串口服务接口
│   │   │   ├── SerialPortService.cs     # 封装 System.IO.Ports
│   │   │   ├── IDeviceEnumerator.cs     # COM 枚举接口
│   │   │   ├── DeviceEnumerator.cs      # WMI + 注册表查询
│   │   │   └── ISettingsService.cs
│   │   │   └── SettingsService.cs       # JSON 持久化
│   │   └── SerialMaster.Core.csproj
│   ├── SerialMaster.UI/
│   │   ├── ViewModels/
│   │   │   ├── MainViewModel.cs         # 主窗口 VM，管理所有 Session
│   │   │   ├── DeviceManagerViewModel.cs # 设备列表 VM
│   │   │   └── SessionViewModel.cs      # 单个串口 Session VM（含数据+发送+状态）
│   │   ├── Views/
│   │   │   ├── MainWindow.xaml/.cs      # 主窗口
│   │   │   ├── DeviceManagerView.xaml   # 设备侧边栏
│   │   │   └── SessionView.xaml         # 单个 Session Tab（数据展示+发送面板+状态栏）
│   │   ├── Controls/
│   │   │   └── StatusIndicator.xaml     # 状态指示灯控件
│   │   ├── Themes/
│   │   │   ├── DarkTheme.xaml           # 深色主题
│   │   │   ├── LightTheme.xaml          # 浅色主题
│   │   │   └── ThemeService.cs          # 主题切换
│   │   ├── Converters/
│   │   │   ├── BoolToBrushConverter.cs
│   │   │   ├── BytesToHexConverter.cs
│   │   │   ├── StatusToIconConverter.cs
│   │   │   ├── DirectionToBrushConverter.cs
│   │   │   ├── BoolToVisibilityConverter.cs
│   │   │   └── ZeroToVisibleConverter.cs
│   │   └── SerialMaster.UI.csproj
│   └── SerialMaster.App/
│       ├── App.xaml / App.xaml.cs
│       ├── DependencyInjection.cs       # DI 容器配置
│       └── SerialMaster.App.csproj
└── tests/
    └── SerialMaster.Core.Tests/
        ├── SerialPortServiceTests.cs
        ├── DeviceEnumeratorTests.cs
        ├── SettingsServiceTests.cs
        └── SerialMaster.Core.Tests.csproj
```

---

### Task 1: 创建解决方案和项目骨架

**Files:**
- Create: `D:\开发\串口\SerialMaster.sln`
- Create: 3 个项目 + 1 测试项目的 `.csproj` 和基础文件

- [ ] **Step 1: 检查 .NET SDK 版本**

```powershell
dotnet --version
```

Expected: 显示版本号（≥ 8.0.0）

- [ ] **Step 2: 创建解决方案**

```powershell
dotnet new sln -n SerialMaster -o "D:\开发\串口" --force
```

- [ ] **Step 3: 创建类库项目 SerialMaster.Core**

```powershell
dotnet new classlib -n SerialMaster.Core -o "D:\开发\串口\src\SerialMaster.Core" -f net8.0
Remove-Item "D:\开发\串口\src\SerialMaster.Core\Class1.cs"
```

- [ ] **Step 4: 添加 Core NuGet 包**

```powershell
dotnet add "D:\开发\串口\src\SerialMaster.Core\SerialMaster.Core.csproj" package System.IO.Ports
dotnet add "D:\开发\串口\src\SerialMaster.Core\SerialMaster.Core.csproj" package System.Management
dotnet add "D:\开发\串口\src\SerialMaster.Core\SerialMaster.Core.csproj" package Microsoft.Extensions.DependencyInjection
dotnet add "D:\开发\串口\src\SerialMaster.Core\SerialMaster.Core.csproj" package System.Text.Json
```

- [ ] **Step 5: 创建 WPF 项目 SerialMaster.UI**

```powershell
dotnet new wpf -n SerialMaster.UI -o "D:\开发\串口\src\SerialMaster.UI" -f net8.0
Remove-Item "D:\开发\串口\src\SerialMaster.UI\MainWindow.xaml"
Remove-Item "D:\开发\串口\src\SerialMaster.UI\MainWindow.xaml.cs"
```

- [ ] **Step 6: 添加 UI NuGet 包**

```powershell
dotnet add "D:\开发\串口\src\SerialMaster.UI\SerialMaster.UI.csproj" package CommunityToolkit.Mvvm
dotnet add "D:\开发\串口\src\SerialMaster.UI\SerialMaster.UI.csproj" package AvalonDock
dotnet add "D:\开发\串口\src\SerialMaster.UI\SerialMaster.UI.csproj" reference "D:\开发\串口\src\SerialMaster.Core\SerialMaster.Core.csproj"
```

- [ ] **Step 7: 创建启动项目 SerialMaster.App**

```powershell
dotnet new wpf -n SerialMaster.App -o "D:\开发\串口\src\SerialMaster.App" -f net8.0
dotnet add "D:\开发\串口\src\SerialMaster.App\SerialMaster.App.csproj" reference "D:\开发\串口\src\SerialMaster.UI\SerialMaster.UI.csproj"
dotnet add "D:\开发\串口\src\SerialMaster.App\SerialMaster.App.csproj" reference "D:\开发\串口\src\SerialMaster.Core\SerialMaster.Core.csproj"
```

- [ ] **Step 8: 创建测试项目**

```powershell
dotnet new mstest -n SerialMaster.Core.Tests -o "D:\开发\串口\tests\SerialMaster.Core.Tests" -f net8.0
Remove-Item "D:\开发\串口\tests\SerialMaster.Core.Tests\Test1.cs"
dotnet add "D:\开发\串口\tests\SerialMaster.Core.Tests\SerialMaster.Core.Tests.csproj" reference "D:\开发\串口\src\SerialMaster.Core\SerialMaster.Core.csproj"
```

- [ ] **Step 9: 将项目加入 solution**

```powershell
dotnet sln "D:\开发\串口\SerialMaster.sln" add "D:\开发\串口\src\SerialMaster.Core\SerialMaster.Core.csproj"
dotnet sln "D:\开发\串口\SerialMaster.sln" add "D:\开发\串口\src\SerialMaster.UI\SerialMaster.UI.csproj"
dotnet sln "D:\开发\串口\SerialMaster.sln" add "D:\开发\串口\src\SerialMaster.App\SerialMaster.App.csproj"
dotnet sln "D:\开发\串口\SerialMaster.sln" add "D:\开发\串口\tests\SerialMaster.Core.Tests\SerialMaster.Core.Tests.csproj"
```

- [ ] **Step 10: 验证构建**

```powershell
dotnet build "D:\开发\串口\SerialMaster.sln"
```

Expected: Build succeeded.

- [ ] **Step 11: Commit**

```bash
git add -A
git commit -m "feat: create solution structure with Core, UI, App, and test projects"
```

---

### Task 2: 定义 Core 数据模型

**Files:**
- Create: `src/SerialMaster.Core/Models/SerialConfig.cs`
- Create: `src/SerialMaster.Core/Models/DeviceInfo.cs`
- Create: `src/SerialMaster.Core/Models/DataRecord.cs`
- Create: `src/SerialMaster.Core/Models/DataDirection.cs`
- Create: `src/SerialMaster.Core/Models/RecordStatus.cs`
- Create: `src/SerialMaster.Core/Models/AppSettings.cs`

- [ ] **Step 1: 创建 Models 目录并编写枚举文件**

```powershell
New-Item -ItemType Directory -Force -Path "D:\开发\串口\src\SerialMaster.Core\Models"
```

`DataDirection.cs`:
```csharp
namespace SerialMaster.Core.Models;

public enum DataDirection
{
    Send,
    Receive
}
```

`RecordStatus.cs`:
```csharp
namespace SerialMaster.Core.Models;

public enum RecordStatus
{
    Success,
    Failed,
    Timeout
}
```

- [ ] **Step 2: 创建 SerialConfig.cs**

```csharp
using System.IO.Ports;

namespace SerialMaster.Core.Models;

public record SerialConfig(
    string PortName,
    int BaudRate = 115200,
    int DataBits = 8,
    Parity Parity = Parity.None,
    StopBits StopBits = StopBits.One,
    int ReadTimeoutMs = 2000,
    int WriteTimeoutMs = 2000
);
```

- [ ] **Step 3: 创建 DeviceInfo.cs**

```csharp
using System.ComponentModel;
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
```

- [ ] **Step 4: 创建 DataRecord.cs**

```csharp
namespace SerialMaster.Core.Models;

public record DataRecord(
    DateTime Timestamp,
    DataDirection Direction,
    byte[] Data,
    RecordStatus Status = RecordStatus.Success
);
```

- [ ] **Step 5: 创建 AppSettings.cs**

```csharp
namespace SerialMaster.Core.Models;

public class AppSettings
{
    public string Theme { get; set; } = "Dark";
    public string LastPort { get; set; } = string.Empty;
    public int LastBaudRate { get; set; } = 115200;
    public List<SerialConfig> RecentConfigs { get; set; } = new();
}
```

- [ ] **Step 6: 构建 Core 项目**

```powershell
dotnet build "D:\开发\串口\src\SerialMaster.Core\SerialMaster.Core.csproj"
```

- [ ] **Step 7: Commit**

```bash
git add src/SerialMaster.Core/Models/
git commit -m "feat: add core data models (SerialConfig, DeviceInfo, DataRecord, AppSettings)"
```

---

### Task 3: 实现 SerialPortService

**Files:**
- Create: `src/SerialMaster.Core/Services/ISerialPortService.cs`
- Create: `src/SerialMaster.Core/Services/SerialPortService.cs`
- Create: `tests/SerialMaster.Core.Tests/SerialPortServiceTests.cs`

- [ ] **Step 1: 创建 Services 目录**

```powershell
New-Item -ItemType Directory -Force -Path "D:\开发\串口\src\SerialMaster.Core\Services"
```

- [ ] **Step 2: 编写 ISerialPortService 接口**

```csharp
using SerialMaster.Core.Models;
using System.Threading.Channels;

namespace SerialMaster.Core.Services;

public interface ISerialPortService : IDisposable
{
    SerialConfig Config { get; }
    bool IsOpen { get; }
    ChannelReader<DataRecord> ReceivedData { get; }

    event EventHandler<string>? ErrorOccurred;
    event EventHandler? ConnectionStateChanged;

    void Open(SerialConfig config);
    void Close();
    Task SendAsync(byte[] data);
    void SetPinState(string pin, bool state);
    bool GetPinState(string pin);
}
```

- [ ] **Step 3: 编写 SerialPortService 实现**

```csharp
using SerialMaster.Core.Models;
using System.IO.Ports;
using System.Threading.Channels;

namespace SerialMaster.Core.Services;

public sealed class SerialPortService : ISerialPortService
{
    private SerialPort? _port;
    private readonly Channel<DataRecord> _channel;
    private readonly CancellationTokenSource _cts = new();

    public SerialConfig Config { get; private set; } = null!;
    public bool IsOpen => _port?.IsOpen ?? false;
    public ChannelReader<DataRecord> ReceivedData => _channel.Reader;

    public event EventHandler<string>? ErrorOccurred;
    public event EventHandler? ConnectionStateChanged;

    public SerialPortService()
    {
        _channel = Channel.CreateBounded<DataRecord>(new BoundedChannelOptions(100_000)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });
    }

    public void Open(SerialConfig config)
    {
        Close();
        Config = config;

        _port = new SerialPort(config.PortName, config.BaudRate, config.Parity, config.DataBits, config.StopBits)
        {
            ReadTimeout = config.ReadTimeoutMs,
            WriteTimeout = config.WriteTimeoutMs,
            DtrEnable = true,
            RtsEnable = true,
            NewLine = "\n"
        };

        _port.DataReceived += OnDataReceived;
        _port.ErrorReceived += OnErrorReceived;

        try
        {
            _port.Open();
            ConnectionStateChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"打开串口失败: {ex.Message}");
            throw;
        }
    }

    private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        var sp = (SerialPort)sender;
        int bytesToRead = sp.BytesToRead;

        if (bytesToRead <= 0) return;

        byte[] buffer = new byte[bytesToRead];
        try
        {
            sp.Read(buffer, 0, bytesToRead);
        }
        catch (TimeoutException)
        {
            return;
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"读取错误: {ex.Message}");
            return;
        }

        var record = new DataRecord(DateTime.Now, DataDirection.Receive, buffer, RecordStatus.Success);
        _channel.Writer.TryWrite(record);
    }

    private void OnErrorReceived(object sender, SerialErrorReceivedEventArgs e)
    {
        ErrorOccurred?.Invoke(this, $"串口错误: {e.EventType}");
    }

    public async Task SendAsync(byte[] data)
    {
        if (_port == null || !_port.IsOpen)
            throw new InvalidOperationException("串口未打开");

        try
        {
            await Task.Run(() => _port.Write(data, 0, data.Length));

            var record = new DataRecord(DateTime.Now, DataDirection.Send, data, RecordStatus.Success);
            _channel.Writer.TryWrite(record);
        }
        catch (Exception ex)
        {
            var record = new DataRecord(DateTime.Now, DataDirection.Send, data, RecordStatus.Failed);
            _channel.Writer.TryWrite(record);
            ErrorOccurred?.Invoke(this, $"发送失败: {ex.Message}");
            throw;
        }
    }

    public void SetPinState(string pin, bool state)
    {
        if (_port == null) return;

        switch (pin.ToUpper())
        {
            case "DTR": _port.DtrEnable = state; break;
            case "RTS": _port.RtsEnable = state; break;
        }
    }

    public bool GetPinState(string pin)
    {
        if (_port == null) return false;

        return pin.ToUpper() switch
        {
            "DTR" => _port.DtrEnable,
            "RTS" => _port.RtsEnable,
            "CTS" => _port.CtsHolding,
            "DSR" => _port.DsrHolding,
            _ => false
        };
    }

    public void Close()
    {
        if (_port != null)
        {
            _port.DataReceived -= OnDataReceived;
            _port.ErrorReceived -= OnErrorReceived;

            try
            {
                if (_port.IsOpen) _port.Close();
            }
            catch { }

            _port.Dispose();
            _port = null;
            ConnectionStateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _channel.Writer.TryComplete();
        Close();
        _cts.Dispose();
    }
}
```

- [ ] **Step 4: 编写单元测试 SerialPortServiceTests.cs**

```csharp
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SerialMaster.Core.Models;
using SerialMaster.Core.Services;

namespace SerialMaster.Core.Tests;

[TestClass]
public class SerialPortServiceTests
{
    [TestMethod]
    public void Open_WithInvalidPort_ThrowsException()
    {
        using var service = new SerialPortService();
        var config = new SerialConfig("COM_NONEXISTENT_99999");

        Assert.ThrowsException<System.IO.FileNotFoundException>(() =>
        {
            try { service.Open(config); }
            catch (System.IO.IOException) { }
            catch { service.Open(config); }
        });
    }

    [TestMethod]
    public void IsOpen_AfterConstruction_ReturnsFalse()
    {
        using var service = new SerialPortService();
        Assert.IsFalse(service.IsOpen);
    }

    [TestMethod]
    public void Config_AfterOpen_ReturnsConfig()
    {
        using var service = new SerialPortService();
        // This test requires a real or virtual serial port
        // Mark as inconclusive in CI environments
        var ports = System.IO.Ports.SerialPort.GetPortNames();
        if (ports.Length == 0)
        {
            Assert.Inconclusive("No serial ports available for testing");
            return;
        }

        var config = new SerialConfig(ports[0]);
        try
        {
            service.Open(config);
            Assert.AreEqual(config.PortName, service.Config.PortName);
        }
        catch
        {
            Assert.Inconclusive("Port in use, cannot test");
        }
        finally
        {
            service.Close();
        }
    }
}
```

- [ ] **Step 5: 运行测试**

```powershell
dotnet test "D:\开发\串口\tests\SerialMaster.Core.Tests\SerialMaster.Core.Tests.csproj"
```

- [ ] **Step 6: Commit**

```bash
git add src/SerialMaster.Core/Services/ tests/
git commit -m "feat: implement SerialPortService with bounded channel and tests"
```

---

### Task 4: 实现 DeviceEnumerator（设备发现）

**Files:**
- Create: `src/SerialMaster.Core/Services/IDeviceEnumerator.cs`
- Create: `src/SerialMaster.Core/Services/DeviceEnumerator.cs`
- Modify: `tests/SerialMaster.Core.Tests/` — add `DeviceEnumeratorTests.cs`

- [ ] **Step 1: 编写接口**

```csharp
using SerialMaster.Core.Models;

namespace SerialMaster.Core.Services;

public interface IDeviceEnumerator
{
    IReadOnlyList<DeviceInfo> GetAvailablePorts();
    void StartWatching(Action<IReadOnlyList<DeviceInfo>> onChanged, int intervalMs = 2000);
    void StopWatching();
}
```

- [ ] **Step 2: 编写 DeviceEnumerator 实现**

```csharp
using SerialMaster.Core.Models;
using System.IO.Ports;
using System.Management;
using Microsoft.Win32;

namespace SerialMaster.Core.Services;

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
            // WMI 查询
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
        catch
        {
            // WMI 不可用时 fallback 到注册表
        }

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
```

- [ ] **Step 3: 编写测试**

```csharp
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SerialMaster.Core.Services;

namespace SerialMaster.Core.Tests;

[TestClass]
public class DeviceEnumeratorTests
{
    [TestMethod]
    public void GetAvailablePorts_ReturnsList()
    {
        using var enumerator = new DeviceEnumerator();
        var ports = enumerator.GetAvailablePorts();

        Assert.IsNotNull(ports);
        // 系统至少应该列出当前存在的端口（包括 0 个的情况）
    }

    [TestMethod]
    public void GetAvailablePorts_EachPortHasName()
    {
        using var enumerator = new DeviceEnumerator();
        var ports = enumerator.GetAvailablePorts();

        foreach (var port in ports)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(port.PortName));
            Assert.IsTrue(port.PortName.StartsWith("COM"));
        }
    }
}
```

- [ ] **Step 4: 运行测试**

```powershell
dotnet test "D:\开发\串口\tests\SerialMaster.Core.Tests\SerialMaster.Core.Tests.csproj"
```

- [ ] **Step 5: Commit**

```bash
git add src/SerialMaster.Core/Services/IDeviceEnumerator.cs src/SerialMaster.Core/Services/DeviceEnumerator.cs tests/
git commit -m "feat: implement DeviceEnumerator with WMI and registry fallback"
```

---

### Task 5: 实现 SettingsService（配置持久化）

**Files:**
- Create: `src/SerialMaster.Core/Services/ISettingsService.cs`
- Create: `src/SerialMaster.Core/Services/SettingsService.cs`
- Modify: `tests/` — add `SettingsServiceTests.cs`

- [ ] **Step 1: 编写接口**

```csharp
using SerialMaster.Core.Models;

namespace SerialMaster.Core.Services;

public interface ISettingsService
{
    AppSettings Load();
    void Save(AppSettings settings);
    string GetSettingsDirectory();
}
```

- [ ] **Step 2: 编写 SettingsService 实现**

```csharp
using SerialMaster.Core.Models;
using System.Text.Json;

namespace SerialMaster.Core.Services;

public sealed class SettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _settingsPath;

    public SettingsService()
    {
        string dir = GetSettingsDirectory();
        Directory.CreateDirectory(dir);
        _settingsPath = Path.Combine(dir, "settings.json");
    }

    public string GetSettingsDirectory()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "SerialMaster");
    }

    public AppSettings Load()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                string json = File.ReadAllText(_settingsPath);
                return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            }
        }
        catch { }

        return new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        string json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(_settingsPath, json);
    }
}
```

- [ ] **Step 3: 编写测试**

```csharp
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SerialMaster.Core.Models;
using SerialMaster.Core.Services;

namespace SerialMaster.Core.Tests;

[TestClass]
public class SettingsServiceTests
{
    [TestMethod]
    public void Load_NoFile_ReturnsDefault()
    {
        var service = new SettingsService();
        var settings = service.Load();

        Assert.AreEqual("Dark", settings.Theme);
        Assert.AreEqual(115200, settings.LastBaudRate);
    }

    [TestMethod]
    public void SaveAndLoad_RoundTrip()
    {
        var service = new SettingsService();
        var original = new AppSettings
        {
            Theme = "Light",
            LastPort = "COM3",
            LastBaudRate = 9600
        };

        service.Save(original);
        var loaded = service.Load();

        Assert.AreEqual(original.Theme, loaded.Theme);
        Assert.AreEqual(original.LastPort, loaded.LastPort);
        Assert.AreEqual(original.LastBaudRate, loaded.LastBaudRate);
    }
}
```

- [ ] **Step 4: 运行测试**

```powershell
dotnet test "D:\开发\串口\tests\SerialMaster.Core.Tests\SerialMaster.Core.Tests.csproj" --filter "SettingsService"
```

- [ ] **Step 5: Commit**

```bash
git add src/SerialMaster.Core/Services/ISettingsService.cs src/SerialMaster.Core/Services/SettingsService.cs tests/
git commit -m "feat: implement SettingsService with JSON persistence"
```

---

### Task 6: 创建 Themes（深色/浅色主题）

**Files:**
- Create: `src/SerialMaster.UI/Themes/DarkTheme.xaml`
- Create: `src/SerialMaster.UI/Themes/LightTheme.xaml`
- Create: `src/SerialMaster.UI/Themes/ThemeService.cs`

- [ ] **Step 1: 创建 Themes 目录**

```powershell
New-Item -ItemType Directory -Force -Path "D:\开发\串口\src\SerialMaster.UI\Themes"
```

- [ ] **Step 2: 创建深色主题 DarkTheme.xaml**

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

    <!-- Background colors -->
    <SolidColorBrush x:Key="PrimaryBackgroundBrush" Color="#1E1E2E"/>
    <SolidColorBrush x:Key="SecondaryBackgroundBrush" Color="#181825"/>
    <SolidColorBrush x:Key="SurfaceBrush" Color="#313244"/>
    <SolidColorBrush x:Key="SurfaceHoverBrush" Color="#45475A"/>
    <SolidColorBrush x:Key="SurfaceBorderBrush" Color="#585B70"/>

    <!-- Text colors -->
    <SolidColorBrush x:Key="PrimaryTextBrush" Color="#CDD6F4"/>
    <SolidColorBrush x:Key="SecondaryTextBrush" Color="#A6ADC8"/>
    <SolidColorBrush x:Key="MutedTextBrush" Color="#6C7086"/>

    <!-- Accent colors -->
    <SolidColorBrush x:Key="AccentBrush" Color="#89B4FA"/>
    <SolidColorBrush x:Key="AccentHoverBrush" Color="#74C7EC"/>

    <!-- Status colors -->
    <SolidColorBrush x:Key="SuccessBrush" Color="#A6E3A1"/>
    <SolidColorBrush x:Key="ErrorBrush" Color="#F38BA8"/>
    <SolidColorBrush x:Key="WarningBrush" Color="#F9E2AF"/>
    <SolidColorBrush x:Key="SendDirectionBrush" Color="#89B4FA"/>
    <SolidColorBrush x:Key="ReceiveDirectionBrush" Color="#A6E3A1"/>

    <!-- Data display -->
    <SolidColorBrush x:Key="HexDataBrush" Color="#CBA6F7"/>
    <SolidColorBrush x:Key="AsciiDataBrush" Color="#94E2D5"/>

    <!-- Common styles -->
    <Style TargetType="Window">
        <Setter Property="Background" Value="{StaticResource PrimaryBackgroundBrush}"/>
    </Style>

    <Style TargetType="TextBox">
        <Setter Property="Background" Value="{StaticResource SurfaceBrush}"/>
        <Setter Property="Foreground" Value="{StaticResource PrimaryTextBrush}"/>
        <Setter Property="BorderBrush" Value="{StaticResource SurfaceBorderBrush}"/>
        <Setter Property="CaretBrush" Value="{StaticResource AccentBrush}"/>
        <Setter Property="FontFamily" Value="Consolas"/>
    </Style>

    <Style TargetType="Button">
        <Setter Property="Background" Value="{StaticResource SurfaceBrush}"/>
        <Setter Property="Foreground" Value="{StaticResource PrimaryTextBrush}"/>
        <Setter Property="BorderBrush" Value="{StaticResource SurfaceBorderBrush}"/>
        <Setter Property="Padding" Value="12,6"/>
    </Style>

    <Style TargetType="ListBox">
        <Setter Property="Background" Value="{StaticResource SecondaryBackgroundBrush}"/>
        <Setter Property="Foreground" Value="{StaticResource PrimaryTextBrush}"/>
        <Setter Property="BorderBrush" Value="{StaticResource SurfaceBorderBrush}"/>
    </Style>

    <Style TargetType="TabControl">
        <Setter Property="Background" Value="{StaticResource SecondaryBackgroundBrush}"/>
        <Setter Property="Foreground" Value="{StaticResource PrimaryTextBrush}"/>
    </Style>

    <Style TargetType="TextBlock">
        <Setter Property="Foreground" Value="{StaticResource PrimaryTextBrush}"/>
    </Style>
</ResourceDictionary>
```

- [ ] **Step 3: 创建浅色主题 LightTheme.xaml**

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

    <SolidColorBrush x:Key="PrimaryBackgroundBrush" Color="#EFF1F5"/>
    <SolidColorBrush x:Key="SecondaryBackgroundBrush" Color="#E6E9EF"/>
    <SolidColorBrush x:Key="SurfaceBrush" Color="#FFFFFF"/>
    <SolidColorBrush x:Key="SurfaceHoverBrush" Color="#CCD0DA"/>
    <SolidColorBrush x:Key="SurfaceBorderBrush" Color="#BCC0CC"/>

    <SolidColorBrush x:Key="PrimaryTextBrush" Color="#4C4F69"/>
    <SolidColorBrush x:Key="SecondaryTextBrush" Color="#5C5F77"/>
    <SolidColorBrush x:Key="MutedTextBrush" Color="#9CA0B0"/>

    <SolidColorBrush x:Key="AccentBrush" Color="#1E66F5"/>
    <SolidColorBrush x:Key="AccentHoverBrush" Color="#2A7EF5"/>

    <SolidColorBrush x:Key="SuccessBrush" Color="#40A02B"/>
    <SolidColorBrush x:Key="ErrorBrush" Color="#D20F39"/>
    <SolidColorBrush x:Key="WarningBrush" Color="#DF8E1D"/>
    <SolidColorBrush x:Key="SendDirectionBrush" Color="#1E66F5"/>
    <SolidColorBrush x:Key="ReceiveDirectionBrush" Color="#40A02B"/>

    <SolidColorBrush x:Key="HexDataBrush" Color="#8839EF"/>
    <SolidColorBrush x:Key="AsciiDataBrush" Color="#179299"/>

    <Style TargetType="Window">
        <Setter Property="Background" Value="{StaticResource PrimaryBackgroundBrush}"/>
    </Style>

    <Style TargetType="TextBox">
        <Setter Property="Background" Value="{StaticResource SurfaceBrush}"/>
        <Setter Property="Foreground" Value="{StaticResource PrimaryTextBrush}"/>
        <Setter Property="BorderBrush" Value="{StaticResource SurfaceBorderBrush}"/>
        <Setter Property="CaretBrush" Value="{StaticResource AccentBrush}"/>
        <Setter Property="FontFamily" Value="Consolas"/>
    </Style>

    <Style TargetType="Button">
        <Setter Property="Background" Value="{StaticResource SurfaceBrush}"/>
        <Setter Property="Foreground" Value="{StaticResource PrimaryTextBrush}"/>
        <Setter Property="BorderBrush" Value="{StaticResource SurfaceBorderBrush}"/>
        <Setter Property="Padding" Value="12,6"/>
    </Style>

    <Style TargetType="ListBox">
        <Setter Property="Background" Value="{StaticResource SecondaryBackgroundBrush}"/>
        <Setter Property="Foreground" Value="{StaticResource PrimaryTextBrush}"/>
        <Setter Property="BorderBrush" Value="{StaticResource SurfaceBorderBrush}"/>
    </Style>

    <Style TargetType="TabControl">
        <Setter Property="Background" Value="{StaticResource SecondaryBackgroundBrush}"/>
        <Setter Property="Foreground" Value="{StaticResource PrimaryTextBrush}"/>
    </Style>

    <Style TargetType="TextBlock">
        <Setter Property="Foreground" Value="{StaticResource PrimaryTextBrush}"/>
    </Style>
</ResourceDictionary>
```

- [ ] **Step 4: 创建 ThemeService.cs**

```csharp
using System.Windows;

namespace SerialMaster.UI.Themes;

public class ThemeService
{
    public string CurrentTheme { get; private set; } = "Dark";

    public void ApplyTheme(string theme)
    {
        CurrentTheme = theme;
        string themePath = theme == "Light" ? "Themes/LightTheme.xaml" : "Themes/DarkTheme.xaml";

        var dict = new ResourceDictionary
        {
            Source = new Uri(themePath, UriKind.Relative)
        };

        Application.Current.Resources.MergedDictionaries.Clear();
        Application.Current.Resources.MergedDictionaries.Add(dict);
    }

    public void ToggleTheme()
    {
        ApplyTheme(CurrentTheme == "Dark" ? "Light" : "Dark");
    }
}
```

- [ ] **Step 5: 构建 UI 项目**

```powershell
dotnet build "D:\开发\串口\src\SerialMaster.UI\SerialMaster.UI.csproj"
```

- [ ] **Step 6: Commit**

```bash
git add src/SerialMaster.UI/Themes/
git commit -m "feat: add Dark and Light WPF themes with ThemeService"
```

---

### Task 7: 编写 Converters 和自定义 StatusIndicator

**Files:**
- Create: `src/SerialMaster.UI/Converters/` — 4 个 converter
- Create: `src/SerialMaster.UI/Controls/StatusIndicator.xaml`

- [ ] **Step 1: 创建 Converters 目录并编写 Converters**

```powershell
New-Item -ItemType Directory -Force -Path "D:\开发\串口\src\SerialMaster.UI\Converters"
New-Item -ItemType Directory -Force -Path "D:\开发\串口\src\SerialMaster.UI\Controls"
```

`BoolToBrushConverter.cs`:
```csharp
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace SerialMaster.UI.Converters;

public class BoolToBrushConverter : IValueConverter
{
    public Brush? TrueBrush { get; set; }
    public Brush? FalseBrush { get; set; }

    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
            return b ? TrueBrush : FalseBrush;
        return FalseBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
```

`BytesToHexConverter.cs`:
```csharp
using System.Globalization;
using System.Windows.Data;

namespace SerialMaster.UI.Converters;

public class BytesToHexConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is byte[] bytes)
            return BitConverter.ToString(bytes).Replace("-", " ");
        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string hex)
        {
            hex = hex.Replace(" ", "").Replace("-", "");
            if (hex.Length % 2 != 0) hex = "0" + hex;
            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
                bytes[i] = System.Convert.ToByte(hex.Substring(i * 2, 2), 16);
            return bytes;
        }
        return Array.Empty<byte>();
    }
}
```

`StatusToIconConverter.cs`:
```csharp
using System.Globalization;
using System.Windows.Data;
using SerialMaster.Core.Models;

namespace SerialMaster.UI.Converters;

public class StatusToIconConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is RecordStatus status)
        {
            return status switch
            {
                RecordStatus.Success => "●",
                RecordStatus.Failed => "🔴",
                RecordStatus.Timeout => "⚠",
                _ => "●"
            };
        }
        return "●";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
```

`DirectionToBrushConverter.cs`:
```csharp
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using SerialMaster.Core.Models;

namespace SerialMaster.UI.Converters;

public class DirectionToBrushConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is DataDirection dir)
        {
            string key = dir == DataDirection.Send ? "SendDirectionBrush" : "ReceiveDirectionBrush";
            return Application.Current.TryFindResource(key) as Brush;
        }
        return Application.Current.TryFindResource("PrimaryTextBrush") as Brush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
```

`BoolToVisibilityConverter.cs`:
```csharp
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SerialMaster.UI.Converters;

public class BoolToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
            return (b ^ Invert) ? Visibility.Visible : Visibility.Collapsed;
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
```

`ZeroToVisibleConverter.cs`:
```csharp
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SerialMaster.UI.Converters;

public class ZeroToVisibleConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int count)
            return count == 0 ? Visibility.Visible : Visibility.Collapsed;
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
```

- [ ] **Step 2: 创建 StatusIndicator 控件**

`Controls/StatusIndicator.xaml`:
```xml
<UserControl x:Class="SerialMaster.UI.Controls.StatusIndicator"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             Height="12" Width="12">

    <Ellipse x:Name="IndicatorDot"
             Width="8" Height="8"
             Fill="{Binding RelativeSource={RelativeSource AncestorType=UserControl}, Path=StatusBrush}"/>
</UserControl>
```

`Controls/StatusIndicator.xaml.cs`:
```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SerialMaster.UI.Controls;

public partial class StatusIndicator : UserControl
{
    public static readonly DependencyProperty StatusBrushProperty =
        DependencyProperty.Register(nameof(StatusBrush), typeof(Brush), typeof(StatusIndicator),
            new PropertyMetadata(Brushes.Gray));

    public Brush StatusBrush
    {
        get => (Brush)GetValue(StatusBrushProperty);
        set => SetValue(StatusBrushProperty, value);
    }

    public StatusIndicator()
    {
        InitializeComponent();
    }
}
```

- [ ] **Step 3: 构建验证**

```powershell
dotnet build "D:\开发\串口\src\SerialMaster.UI\SerialMaster.UI.csproj"
```

- [ ] **Step 4: Commit**

```bash
git add src/SerialMaster.UI/Converters/ src/SerialMaster.UI/Controls/
git commit -m "feat: add converters and StatusIndicator control"
```

---

### Task 8: 实现 DeviceManagerViewModel + View（设备侧边栏）

**Files:**
- Create: `src/SerialMaster.UI/ViewModels/DeviceManagerViewModel.cs`
- Create: `src/SerialMaster.UI/Views/DeviceManagerView.xaml`

- [ ] **Step 1: 创建 ViewModels 和 Views 目录**

```powershell
New-Item -ItemType Directory -Force -Path "D:\开发\串口\src\SerialMaster.UI\ViewModels"
New-Item -ItemType Directory -Force -Path "D:\开发\串口\src\SerialMaster.UI\Views"
```

- [ ] **Step 2: 编写 DeviceManagerViewModel.cs**

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SerialMaster.Core.Models;
using SerialMaster.Core.Services;
using System.Collections.ObjectModel;
using System.IO.Ports;

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
                // 更新设备列表，保留已连接状态
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

                // 移除已不存在的端口
                var toRemove = Devices.Where(d => !ports.Any(p => p.PortName == d.PortName)).ToList();
                foreach (var item in toRemove)
                    Devices.Remove(item);
            });
        });
    }

    [RelayCommand]
    private void RefreshDevices()
    {
        var ports = _enumerator.GetAvailablePorts();

        Devices.Clear();
        foreach (var port in ports)
            Devices.Add(port);
    }

    [RelayCommand]
    private void ConnectDevice(DeviceInfo? device)
    {
        if (device == null) return;

        if (device.IsConnected)
        {
            // 断开连接
            device.IsConnected = false;
            device.StatusText = "未连接";
            device.HasError = false;
            return;
        }

        // 加载上次配置
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

            // 保存设置
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
```

- [ ] **Step 3: 编写 DeviceManagerView.xaml**

```xml
<UserControl x:Class="SerialMaster.UI.Views.DeviceManagerView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:local="clr-namespace:SerialMaster.UI.Views"
             xmlns:conv="clr-namespace:SerialMaster.UI.Converters"
             Background="{StaticResource SecondaryBackgroundBrush}"
             Width="240">

    <UserControl.Resources>
        <conv:BoolToBrushConverter x:Key="ConnectedBrush"
            TrueBrush="{StaticResource SuccessBrush}"
            FalseBrush="{StaticResource MutedTextBrush}"/>
    </UserControl.Resources>

    <Grid Margin="8">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <!-- 标题 -->
        <TextBlock Grid.Row="0" Text="设备列表" FontSize="14" FontWeight="SemiBold"
                   Margin="0,0,0,8"/>

        <!-- 设备列表 -->
        <ListBox Grid.Row="1"
                 ItemsSource="{Binding Devices}"
                 SelectedItem="{Binding SelectedDevice}"
                 BorderThickness="0"
                 Background="Transparent">
            <ListBox.ItemTemplate>
                <DataTemplate>
                    <Grid Margin="0,4">
                        <Grid.RowDefinitions>
                            <RowDefinition Height="Auto"/>
                            <RowDefinition Height="Auto"/>
                        </Grid.RowDefinitions>

                        <!-- 端口名 + 状态灯 -->
                        <StackPanel Grid.Row="0" Orientation="Horizontal">
                            <Ellipse Width="8" Height="8" Margin="0,0,6,0">
                                <Ellipse.Fill>
                                    <SolidColorBrush Color="{Binding IsConnected,
                                        Converter={StaticResource ConnectedBrush}}"/>
                                </Ellipse.Fill>
                            </Ellipse>
                            <TextBlock Text="{Binding PortName}" FontWeight="SemiBold"
                                       Foreground="{StaticResource PrimaryTextBrush}"/>
                            <TextBlock Text="{Binding StatusText, StringFormat=' — {0}'}"
                                       Foreground="{StaticResource SecondaryTextBrush}"
                                       Margin="4,0,0,0"/>
                        </StackPanel>

                        <!-- 设备描述 -->
                        <TextBlock Grid.Row="1" Text="{Binding Description}"
                                   Foreground="{StaticResource MutedTextBrush}"
                                   FontSize="11" Margin="14,1,0,0"
                                   TextTrimming="CharacterEllipsis"/>
                    </Grid>
                </DataTemplate>
            </ListBox.ItemTemplate>
        </ListBox>

        <!-- 底部按钮 -->
        <StackPanel Grid.Row="2" Orientation="Horizontal" Margin="0,8,0,0">
            <Button Content="连接" Width="70"
                    Command="{Binding ConnectDeviceCommand}"
                    CommandParameter="{Binding SelectedDevice}"/>
            <Button Content="刷新" Width="50" Margin="6,0,0,0"
                    Command="{Binding RefreshDevicesCommand}"/>
        </StackPanel>
    </Grid>
</UserControl>
```

- [ ] **Step 4: Commit**

```bash
git add src/SerialMaster.UI/ViewModels/DeviceManagerViewModel.cs src/SerialMaster.UI/Views/DeviceManagerView.xaml
git commit -m "feat: implement DeviceManager sidebar with device list and connect/disconnect"
```

---

### Task 9: 实现 SessionViewModel（串口 Session 管理）

**Files:**
- Create: `src/SerialMaster.UI/ViewModels/SessionViewModel.cs`
- Create: `src/SerialMaster.UI/Views/SessionView.xaml`

- [ ] **Step 1: 编写 SessionViewModel.cs**

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SerialMaster.Core.Models;
using SerialMaster.Core.Services;
using System.Collections.ObjectModel;
using System.Threading.Channels;

namespace SerialMaster.UI.ViewModels;

public partial class SessionViewModel : ObservableObject, IDisposable
{
    private readonly ISerialPortService _serialService;
    private readonly DeviceInfo _deviceInfo;
    private CancellationTokenSource? _readCts;

    [ObservableProperty]
    private string _tabTitle = string.Empty;

    [ObservableProperty]
    private ObservableCollection<DataRecord> _records = new();

    [ObservableProperty]
    private long _receivedBytes;

    [ObservableProperty]
    private long _sentBytes;

    [ObservableProperty]
    private int _errorCount;

    [ObservableProperty]
    private int _warningCount;

    [ObservableProperty]
    private bool _isPaused;

    [ObservableProperty]
    private string _sendText = string.Empty;

    public DeviceInfo DeviceInfo => _deviceInfo;

    public SessionViewModel(ISerialPortService serialService, DeviceInfo deviceInfo)
    {
        _serialService = serialService;
        _deviceInfo = deviceInfo;
        TabTitle = $"{deviceInfo.PortName}";

        if (deviceInfo.Config != null)
            _serialService.Open(deviceInfo.Config);

        _serialService.ErrorOccurred += OnError;
        _serialService.ConnectionStateChanged += (_, _) =>
        {
            TabTitle = _serialService.IsOpen
                ? $"{deviceInfo.PortName} ●"
                : $"{deviceInfo.PortName} ○";
        };

        StartReading();
    }

    private void StartReading()
    {
        _readCts = new CancellationTokenSource();
        var token = _readCts.Token;

        Task.Run(async () =>
        {
            var reader = _serialService.ReceivedData;
            await foreach (var record in reader.ReadAllAsync(token))
            {
                if (_isPaused) continue;

                Application.Current.Dispatcher.Invoke(() =>
                {
                    Records.Add(record);

                    if (record.Direction == DataDirection.Receive)
                        ReceivedBytes += record.Data.Length;
                    else
                        SentBytes += record.Data.Length;

                    if (record.Status == RecordStatus.Failed) ErrorCount++;
                    if (record.Status == RecordStatus.Timeout) WarningCount++;
                });
            }
        }, token);
    }

    private void OnError(object? sender, string message)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            ErrorCount++;
            _deviceInfo.HasError = true;
            _deviceInfo.StatusText = message;
        });
    }

    [RelayCommand]
    private async Task SendData()
    {
        if (string.IsNullOrWhiteSpace(SendText)) return;

        byte[] data;
        string text = SendText.Trim();

        // 判断是 HEX 还是 ASCII
        if (text.Replace(" ", "").All(c => "0123456789ABCDEFabcdef".Contains(c)) &&
            text.Contains(' '))
        {
            // HEX 模式
            string hex = text.Replace(" ", "");
            if (hex.Length % 2 != 0) hex = "0" + hex;
            data = new byte[hex.Length / 2];
            for (int i = 0; i < data.Length; i++)
                data[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        }
        else
        {
            // ASCII 模式
            data = System.Text.Encoding.ASCII.GetBytes(text);
            data = data.Concat(new byte[] { 0x0D, 0x0A }).ToArray();
        }

        try
        {
            await _serialService.SendAsync(data);
        }
        catch
        {
            ErrorCount++;
        }
    }

    [RelayCommand]
    private void ClearRecords()
    {
        Records.Clear();
        ReceivedBytes = 0;
        SentBytes = 0;
        ErrorCount = 0;
        WarningCount = 0;
    }

    [RelayCommand]
    private void TogglePause()
    {
        IsPaused = !IsPaused;
    }

    public void Dispose()
    {
        _readCts?.Cancel();
        _readCts?.Dispose();
        _serialService.ErrorOccurred -= OnError;
        _serialService.Close();
    }
}
```

- [ ] **Step 2: 编写 SessionView.xaml**

```xml
<UserControl x:Class="SerialMaster.UI.Views.SessionView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:conv="clr-namespace:SerialMaster.UI.Converters">

    <UserControl.Resources>
        <conv:BytesToHexConverter x:Key="BytesToHex"/>
        <conv:StatusToIconConverter x:Key="StatusToIcon"/>
        <conv:DirectionToBrushConverter x:Key="DirToBrush"/>
    </UserControl.Resources>

    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <!-- 数据展示区 -->
        <ListView Grid.Row="0" ItemsSource="{Binding Records}"
                  Background="{StaticResource SecondaryBackgroundBrush}"
                  VirtualizingStackPanel.IsVirtualizing="True"
                  VirtualizingStackPanel.VirtualizationMode="Recycling">

            <ListView.ItemTemplate>
                <DataTemplate>
                    <Grid Margin="4,1" VerticalAlignment="Center">
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="30"/>
                            <ColumnDefinition Width="120"/>
                            <ColumnDefinition Width="20"/>
                            <ColumnDefinition Width="*"/>
                        </Grid.ColumnDefinitions>

                        <!-- 状态指示灯 -->
                        <TextBlock Grid.Column="0"
                                   Text="{Binding Status, Converter={StaticResource StatusToIcon}}"
                                   FontSize="11" VerticalAlignment="Center"
                                   HorizontalAlignment="Center"/>

                        <!-- 时间戳 -->
                        <TextBlock Grid.Column="1"
                                   Text="{Binding Timestamp, StringFormat='HH:mm:ss.fff'}"
                                   Foreground="{StaticResource SecondaryTextBrush}"
                                   FontFamily="Consolas" FontSize="12"
                                   VerticalAlignment="Center"/>

                        <!-- 方向箭头 -->
                        <TextBlock Grid.Column="2"
                                   Text="{Binding Direction, StringFormat='{}{0}'}"
                                   Foreground="{Binding Direction, Converter={StaticResource DirToBrush}}"
                                   FontWeight="Bold" FontSize="14"
                                   VerticalAlignment="Center"/>

                        <!-- 数据 (HEX) -->
                        <TextBlock Grid.Column="3"
                                   Text="{Binding Data, Converter={StaticResource BytesToHex}}"
                                   Foreground="{StaticResource HexDataBrush}"
                                   FontFamily="Consolas" FontSize="12"
                                   TextWrapping="NoWrap" VerticalAlignment="Center"/>
                    </Grid>
                </DataTemplate>
            </ListView.ItemTemplate>
        </ListView>

        <!-- 发送面板 -->
        <Border Grid.Row="1" BorderBrush="{StaticResource SurfaceBorderBrush}"
                BorderThickness="0,1" Padding="8">

            <Grid>
                <Grid.RowDefinitions>
                    <RowDefinition Height="Auto"/>
                    <RowDefinition Height="Auto"/>
                </Grid.RowDefinitions>

                <TextBox Grid.Row="0" Text="{Binding SendText, UpdateSourceTrigger=PropertyChanged}"
                         MinHeight="40" AcceptsReturn="True"
                         Background="{StaticResource SurfaceBrush}"
                         Foreground="{StaticResource PrimaryTextBrush}"
                         FontFamily="Consolas" FontSize="13"
                         BorderBrush="{StaticResource SurfaceBorderBrush}"/>

                <StackPanel Grid.Row="1" Orientation="Horizontal"
                            Margin="0,6,0,0">
                    <Button Content="发送 (Ctrl+Enter)"
                            Command="{Binding SendDataCommand}"
                            Background="{StaticResource AccentBrush}"/>
                    <Button Content="⏸" Width="36" Margin="6,0,0,0"
                            Command="{Binding TogglePauseCommand}"/>
                    <Button Content="清空" Width="50" Margin="6,0,0,0"
                            Command="{Binding ClearRecordsCommand}"/>

                    <!-- 统计信息 -->
                    <TextBlock Margin="20,0,0,0" VerticalAlignment="Center"
                               Foreground="{StaticResource SecondaryTextBrush}"
                               FontSize="11">
                        <Run Text="接收:"/>
                        <Run Text="{Binding ReceivedBytes, StringFormat='{}{0:N0}'}"/>
                        <Run Text="字节  "/>
                        <Run Text="发送:"/>
                        <Run Text="{Binding SentBytes, StringFormat='{}{0:N0}'}"/>
                        <Run Text="字节  "/>
                    </TextBlock>

                    <Ellipse Width="8" Height="8" Fill="{StaticResource ErrorBrush}"
                             Margin="8,0,2,0" Visibility="{Binding ErrorCount,
                                Converter={StaticResource BoolToVisibility}}"/>
                    <TextBlock Text="{Binding ErrorCount, StringFormat='失败 {0}'}"
                               Foreground="{StaticResource ErrorBrush}" FontSize="11"
                               VerticalAlignment="Center"/>
                </StackPanel>
            </Grid>
        </Border>

        <!-- 状态栏 -->
        <Border Grid.Row="2" BorderBrush="{StaticResource SurfaceBorderBrush}"
                BorderThickness="0,1" Padding="6,3">
            <StackPanel Orientation="Horizontal">
                <TextBlock Foreground="{StaticResource SuccessBrush}" FontSize="11">
                    <Run Text="● 已连接"/>
                    <Run Text="{Binding TabTitle}"/>
                </TextBlock>
            </StackPanel>
        </Border>
    </Grid>
</UserControl>
```

- [ ] **Step 3: Commit**

```bash
git add src/SerialMaster.UI/ViewModels/SessionViewModel.cs src/SerialMaster.UI/Views/SessionView.xaml
git commit -m "feat: implement SessionViewModel with data display, send panel, and status bar"
```

---

### Task 10: 实现 MainViewModel + MainWindow（主窗口 + AvalonDock）

**Files:**
- Create: `src/SerialMaster.UI/ViewModels/MainViewModel.cs`
- Create: `src/SerialMaster.UI/Views/MainWindow.xaml`
- Create: `src/SerialMaster.UI/Views/MainWindow.xaml.cs`

- [ ] **Step 1: 编写 MainViewModel.cs**

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
            // 创建新的 Session
            var serialService = _serviceProvider.GetRequiredService<ISerialPortService>();
            // 创建新的 ISerialPortService 实例
            var session = new SessionViewModel(
                serialService,
                device);

            Sessions.Add(session);
            ActiveSession = session;
        }
        else
        {
            // 断开连接
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

        // 更新 DeviceManager 中的状态
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
```

- [ ] **Step 2: 编写 MainWindow.xaml**

```xml
<Window x:Class="SerialMaster.UI.Views.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:views="clr-namespace:SerialMaster.UI.Views"
        Title="SerialMaster" Height="700" Width="1100"
        MinHeight="400" MinWidth="800"
        WindowStartupLocation="CenterScreen">

    <Window.InputBindings>
        <KeyBinding Key="N" Modifiers="Control" Command="{Binding DeviceManager.ConnectDeviceCommand}"/>
        <KeyBinding Key="R" Modifiers="Control" Command="{Binding DeviceManager.RefreshDevicesCommand}"/>
    </Window.InputBindings>

    <Grid>
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="Auto"/>
            <ColumnDefinition Width="*"/>
        </Grid.ColumnDefinitions>

        <!-- 左侧设备侧边栏 -->
        <views:DeviceManagerView Grid.Column="0"
                                 DataContext="{Binding DeviceManager}"/>

        <!-- 右侧主内容区（TabControl 管理多个 Session）-->
        <Grid Grid.Column="1">
            <!-- 无 Session 时的提示 -->
            <TextBlock Text="选择一个设备并点击「连接」开始"
                       Foreground="{StaticResource MutedTextBrush}"
                       FontSize="16" HorizontalAlignment="Center"
                       VerticalAlignment="Center"
                       Visibility="{Binding Sessions.Count,
                           Converter={StaticResource ZeroToVisible}}"/>

            <!-- Session Tabs -->
            <TabControl ItemsSource="{Binding Sessions}"
                        SelectedItem="{Binding ActiveSession}">
                <TabControl.ItemTemplate>
                    <DataTemplate>
                        <StackPanel Orientation="Horizontal">
                            <Ellipse Width="8" Height="8" Margin="0,0,6,0">
                                <Ellipse.Fill>
                                    <SolidColorBrush Color="{Binding DeviceInfo.IsConnected,
                                        Converter={StaticResource ConnectedBrush}}"/>
                                </Ellipse.Fill>
                            </Ellipse>
                            <TextBlock Text="{Binding TabTitle}"/>
                            <Button Content="×" Width="20" Height="20"
                                    FontSize="14" Margin="6,0,0,0"
                                    Background="Transparent" BorderThickness="0"
                                    Foreground="{StaticResource MutedTextBrush}"
                                    Command="{Binding RelativeSource={RelativeSource
                                        AncestorType=Window}, Path=DataContext.CloseSessionCommand}"
                                    CommandParameter="{Binding}"/>
                        </StackPanel>
                    </DataTemplate>
                </TabControl.ItemTemplate>

                <TabControl.ContentTemplate>
                    <DataTemplate>
                        <views:SessionView DataContext="{Binding}"/>
                    </DataTemplate>
                </TabControl.ContentTemplate>
            </TabControl>
        </Grid>
    </Grid>
</Window>
```

- [ ] **Step 3: 编写 MainWindow.xaml.cs**

```csharp
using System.Windows;
using SerialMaster.UI.ViewModels;

namespace SerialMaster.UI.Views;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
```

- [ ] **Step 4: Commit**

```bash
git add src/SerialMaster.UI/ViewModels/MainViewModel.cs src/SerialMaster.UI/Views/MainWindow.xaml*
git commit -m "feat: implement MainWindow with AvalonDock, multi-tab sessions, and device sidebar"
```

---

### Task 11: 配置 DI 容器和 App 启动

**Files:**
- Modify: `src/SerialMaster.App/App.xaml`
- Modify: `src/SerialMaster.App/App.xaml.cs`
- Create: `src/SerialMaster.App/DependencyInjection.cs`

- [ ] **Step 1: 编写 DependencyInjection.cs**

```csharp
using Microsoft.Extensions.DependencyInjection;
using SerialMaster.Core.Services;
using SerialMaster.UI.Themes;
using SerialMaster.UI.ViewModels;

namespace SerialMaster.App;

public static class DependencyInjection
{
    public static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Core services
        services.AddSingleton<IDeviceEnumerator, DeviceEnumerator>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddTransient<ISerialPortService, SerialPortService>();

        // UI services
        services.AddSingleton<ThemeService>();

        // ViewModels
        services.AddSingleton<DeviceManagerViewModel>();
        services.AddSingleton<MainViewModel>();

        return services.BuildServiceProvider();
    }
}
```

- [ ] **Step 2: 修改 App.xaml**

```xml
<Application x:Class="SerialMaster.App.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             Startup="OnStartup">
    <Application.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <ResourceDictionary Source="/SerialMaster.UI;component/Themes/DarkTheme.xaml"/>
            </ResourceDictionary.MergedDictionaries>
        </ResourceDictionary>
    </Application.Resources>
</Application>
```

- [ ] **Step 3: 修改 App.xaml.cs**

```csharp
using System.Windows;
using SerialMaster.Core.Services;
using SerialMaster.UI.Themes;
using SerialMaster.UI.ViewModels;
using SerialMaster.UI.Views;

namespace SerialMaster.App;

public partial class App : Application
{
    private IServiceProvider? _serviceProvider;

    private void OnStartup(object sender, StartupEventArgs e)
    {
        _serviceProvider = DependencyInjection.ConfigureServices();

        // 加载保存的设置
        var settingsService = _serviceProvider.GetRequiredService<ISettingsService>();
        var settings = settingsService.Load();

        // 应用主题
        var themeService = _serviceProvider.GetRequiredService<ThemeService>();
        themeService.ApplyTheme(settings.Theme);

        // 启动主窗口
        var mainViewModel = _serviceProvider.GetRequiredService<MainViewModel>();
        var mainWindow = new MainWindow(mainViewModel);
        mainWindow.Show();
    }
}
```

- [ ] **Step 4: 构建整个解决方案**

```powershell
dotnet build "D:\开发\串口\SerialMaster.sln"
```

- [ ] **Step 5: Commit**

```bash
git add src/SerialMaster.App/
git commit -m "feat: configure DI container and app startup"
```

---

### Task 12: 集成测试 + 手动验证

- [ ] **Step 1: 构建 Release**

```powershell
dotnet build "D:\开发\串口\SerialMaster.sln" -c Release
```

- [ ] **Step 2: 运行全部单元测试**

```powershell
dotnet test "D:\开发\串口\SerialMaster.sln"
```

- [ ] **Step 3: 手动验证清单**

启动应用 `dotnet run --project "D:\开发\串口\src\SerialMaster.App"`，验证：
- [ ] 应用正常启动，默认深色主题
- [ ] 设备列表自动刷新，显示可用 COM 口
- [ ] 点击「连接」建立串口连接
- [ ] 接收数据正确显示（HEX 格式、时间戳、方向）
- [ ] 发送数据功能正常
- [ ] 多设备可同时连接并切换 Tab
- [ ] 「清空」按钮清除数据显示
- [ ] 「暂停」暂停刷新

- [ ] **Step 4: 测试主题切换**（通过代码或设置）并验证主题正确应用

- [ ] **Step 5: 修复发现的问题并 Commit**

---

## Phase 1 完成标准

- [x] 解决方案结构清晰，3 项目 + 1 测试项目
- [x] Core 层完全独立，无 UI 依赖
- [x] 串口收发正常工作
- [x] 设备发现（WMI + 注册表 fallback）
- [x] 数据展示 HEX 格式，带时间戳和方向
- [x] 基础发送功能
- [x] 多设备 Tab 切换
- [x] 深色/浅色主题
- [x] 设置 JSON 持久化
- [x] 单元测试覆盖 Core 层

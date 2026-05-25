# SerialMaster 串口大师

面向嵌入式开发的多功能串口调试工具。WPF + .NET 8，MVVM 架构。

**GitHub**: https://github.com/honghetu/SerialMaster
**联系邮箱**: 13947617581@163.com

## 功能概览

| 类别 | 功能 |
|---|---|
| **连接** | 多端口并行（每个 Tab 一个 Session）、WMI/注册表设备枚举、深/浅色主题切换 |
| **数据视图** | HEX 视图、VT 终端模式（ASCII 拼接，规划中升级 VT100/ANSI） |
| **发送** | HEX/ASCII/UTF-8 显式切换、行尾 CR/LF/CRLF/None、**定时发送**、**快捷发送按钮条** |
| **协议解析** | 帧头同步 + 定长截帧、12 种字段类型（含 Int16BE / Float32 / ASCII）、JSON 持久化、一键应用到会话 |
| **校验** | XOR / ADD / CRC8 / CRC16 / CRC32 / Modbus CRC |
| **可视化** | 波形 / 雷达 / 3D 浮动窗口（数据源当前为 ASCII 数值拆分） |
| **文件传输** | XMODEM-CRC / Checksum 模式，发送 + 接收双向（与 Session 共存，互不抢字节） |
| **宏 / 收藏** | 步骤编辑器、循环跳转、JSON 导入导出、收藏夹 |
| **日志** | 保存日志、打开日志查看器、CSV/JSON 导出 |
| **引脚控制** | DTR / RTS Toggle |
| **🔥 固件烧录** | 包装 esptool / stm32flash，UI 直接拉起；ESP32 / ESP8266 / STM32 ISP |
| **🚌 CAN 总线** | SLCAN (LAWICEL) 协议，标准/扩展帧、RTR；兼容 CANable / CANUSB / candleLight-SLCAN |
| **🌐 网络桥** | TCP Server / TCP Client / UDP ↔ Serial 双向透传 |

## 快速开始

```powershell
# 构建
dotnet build SerialMaster.sln

# 运行 UI
dotnet run --project src/SerialMaster.App

# 测试
dotnet test tests/SerialMaster.Core.Tests
```

要求：.NET 8 SDK；Windows 10/11（核心使用 `System.IO.Ports` + `System.Management`）。

## 关键功能用法

### 协议解析器（带帧同步）

1. **工具 → 协议解析器**
2. 左栏「已保存协议」可选预设（如 `示例: 6 字节传感器帧 (AA 55 + u16 LE...)`）
3. 中栏编辑：名称 / 帧头 HEX（留空 = 不做同步） / 帧长 / 字段表
4. **保存协议** 持久化到 `%LocalAppData%/SerialMaster/protocols.json`
5. **应用到会话** 将定义推送给当前 Session
6. 切回 Session Tab，点击发送区 **解析** 按钮显示解析面板
7. 流式收到的字节自动 Feed 到 `ProtocolParser`，按帧追加到 `ParsedFrames`

支持碎片到达（一次半帧 + 下次半帧也能正确出帧）；含垃圾字节时按帧头同步。

### 定时发送

发送区 **定时** ToggleButton + 间隔毫秒输入。打开后周期触发当前 `SendText` 的发送。串口断开自动停止。

### 快捷发送

从 **编辑 → 发送收藏夹** 添加条目（HEX 字符串 + 名称），返回 Session 即看到顶部按钮条。单击发送，按收藏的 `IsHex` 标志走 HEX 或 UTF-8 解码。

### 固件烧录器（外部进程包装）

**工具 → 🔥 固件烧录器**

| 字段 | 说明 |
|---|---|
| 芯片家族 | `Esp32` / `Esp8266` / `Stm32Isp` |
| 固件文件 | `.bin` / `.hex` |
| 端口 | 从枚举到的 COM 口选择 |
| 波特率 | 默认 921600；STM32 建议 115200 |
| 可执行路径 | `esptool.exe` / `stm32flash.exe`；若已在 PATH 中可只填文件名 |
| Flash 偏移 | ESP32 默认 `0x10000`，ESP8266 默认 `0x0` |

工具会以 `Process.Start` 调用对应可执行文件，捕获 stdout/stderr 流式打印到日志面板。**SerialMaster 不内嵌烧录器二进制**，需用户自备：

- esptool: https://github.com/espressif/esptool/releases
- stm32flash: https://sourceforge.net/projects/stm32flash/

### CAN 总线 (SLCAN)

**工具 → 🚌 CAN 总线**

1. 选 USB-CAN 适配器对应的 COM 口
2. 串口波特率（适配器协议层）选 115200 / 921600 等
3. CAN 总线波特率选 10kbps ~ 1Mbps
4. **打开** 按钮会依次发送 `C\r` (关闭) → `Snn\r` (设速率) → `O\r` (开启)
5. 发送区填 ID (HEX) + 数据 (HEX, ≤ 8 字节)，勾选 **扩展帧** 或 **RTR**
6. 接收帧实时显示在右侧表格（时间 / 类型 / ID / DLC / 数据）

适配器协议层：SLCAN (LAWICEL) — 适用于 CANable / CANUSB / candleLight 的 SLCAN 固件 / USBtin / 任何 LAWICEL 兼容设备。**不支持** PCAN / Kvaser / ZLG 等需要厂商 DLL 的设备（在路线图）。

### TCP/UDP ↔ Serial 桥

**工具 → 🌐 TCP/UDP 桥**

| 模式 | 行为 |
|---|---|
| `TcpServer` | 在指定端口监听，等待一个 TCP 客户端，建立后双向透传 |
| `TcpClient` | 主动连接 `Host:Port`，建立后双向透传 |
| `Udp` | 绑定本地端口；若填写 Host = 主动发到目标，留空 = 等待首个 UDP 来源 |

桥独立持有一个 `ISerialPortService` 实例（不影响主 Session）。流量统计实时刷新（Serial↔Net 字节数）。可用作：远程串口、ser2net 替代、虚拟 COM 网络中转。

### 输入格式

发送区下拉的格式控制 `InputParser` 行为：

| 模式 | 说明 |
|---|---|
| `Auto` | 全为 hex 字符（含空格/`-`/`,`/`:`）按 HEX 解析，否则按 UTF-8 |
| `Hex`  | 强制 HEX，支持 `0x` 前缀、空格/连字符/逗号分隔、奇数位自动补零 |
| `Ascii` / `Utf8` | 显式文本编码，按 `行尾` 设置追加 CR / LF / CRLF |

HEX 模式忽略行尾设置。

## 架构

```
SerialMaster.Core/                # 无 UI 依赖，可被测试覆盖
├── Models/                       # SerialConfig, DataRecord, ProtocolDefinition, ...
└── Services/
    ├── SerialPortService         # 后台异步读取循环 (BaseStream.ReadAsync)
    ├── DeviceEnumerator          # WMI + 注册表 fallback
    ├── SettingsService           # %AppData%/SerialMaster/settings.json
    ├── ProtocolParser            # 帧同步 + 字段解析（流式 Feed）
    ├── ProtocolDefinitionStore   # protocols.json 持久化
    ├── ChecksumService           # XOR / ADD / CRC* / Modbus
    ├── InputParser               # HEX/ASCII/UTF-8 + 行尾
    ├── SlcanCodec                # SLCAN 编解码 (LAWICEL)
    ├── NetworkBridgeService      # TCP/UDP ↔ Serial 双向桥
    ├── FirmwareBurnerService     # esptool / stm32flash 子进程
    └── XmodemService

SerialMaster.UI/                  # WPF 视图层
├── ViewModels/                   # MVVM (CommunityToolkit.Mvvm)
├── Views/                        # SessionView / DeviceManagerView / ProtocolParserView / ...
├── Controls/                     # WaveformControl / RadarControl / View3DControl
├── Services/                     # FavoritesService
├── Themes/                       # DarkTheme.xaml + LightTheme.xaml + ThemeService
└── Converters/                   # BytesToHex / DirectionToBrush / ...

SerialMaster.App/                 # 启动 + DI 容器
├── App.xaml(.cs)
└── DependencyInjection.cs        # Singleton / Transient 注册

tests/SerialMaster.Core.Tests/    # 41 个单元测试
```

### 依赖

| 包 | 用途 |
|---|---|
| `CommunityToolkit.Mvvm` | `[ObservableProperty]` / `[RelayCommand]` 源生成 |
| `Microsoft.Extensions.DependencyInjection` | DI 容器 |
| `System.IO.Ports` | 串口底层 |
| `System.Management` | WMI 查询设备名 |

### 数据流

```
SerialPort.BaseStream
    │ (后台 Task 循环 ReadAsync)
    ▼
Channel<DataRecord>(100k, DropOldest)
    │
    ▼
SessionViewModel.StartReading
    ├──▶ Records (ListView, virtualized)
    ├──▶ TerminalText
    ├──▶ ProcessVisualizationData ──▶ Waveform/Radar/3D 窗口
    └──▶ ProtocolParser.Feed ──▶ ParsedFrames
```

## 配置目录

| 路径 | 内容 |
|---|---|
| `%AppData%/SerialMaster/settings.json` | 主题、上次端口、波特率 |
| `%LocalAppData%/SerialMaster/protocols.json` | 协议定义列表 |
| `%LocalAppData%/SerialMaster/favorites.json` | 快捷发送条目 |

## 测试

```powershell
dotnet test tests/SerialMaster.Core.Tests
```

当前 69 通过 / 0 失败。覆盖：

- `InputParserTests` — HEX 各种格式、行尾追加、Auto 识别
- `ChecksumServiceTests` — Modbus / CRC16 / CRC8 / CRC32 标准向量
- `ProtocolParserTests` — 帧头同步、碎片重组、垃圾字节、Int16BE / Float32 字段
- `SlcanCodecTests` — 标准/扩展/RTR 帧、碎片化、垃圾字节同步、Encode/Decode 往返
- `FirmwareBurnerServiceTests` — 各芯片家族命令行拼接、带空格路径转义
- `NetworkBridgeServiceTests` — Stats 累加、必须先 Open 串口才能 Start
- `XmodemServiceTests` — CRC-XMODEM 标准向量、确定性
- `SerialPortServiceTests` / `DeviceEnumeratorTests` / `SettingsServiceTests`

## 路线图

**已完成**（v1.0 → v1.3）
- 多端口 Session、HEX/终端视图、深浅主题
- 校验工具、收藏夹、宏编辑器（步进 + 循环）
- 协议解析器（帧同步 + 应用到会话）
- 定时发送、快捷发送按钮条
- 异步读取循环、Modbus CRC 标准向量校验
- 固件烧录器（esptool / stm32flash 进程包装）
- CAN 总线（SLCAN/LAWICEL 协议）
- TCP/UDP ↔ Serial 桥
- 全局异常捕获 + FileLogger 日志落盘
- XMODEM 与活动 Session 解耦（DataReceived 事件，不抢 Channel）
- XMODEM 接收路径 + CRC 模式
- 菜单快捷键真正生效（Ctrl+N/O/S/E/L、Ctrl+Shift+F）
- LICENSE (MIT) + CHANGELOG + CI workflow + ISSUE/PR 模板

**短期**（A 组剩余）
- VT100/ANSI 终端解析器（替代当前 ASCII 拼接）
- 触发匹配：高亮 / 暂停 / 蜂鸣 / 触发发送 / 触发宏
- DataLogger 落盘（按天/按大小切片，超 50k 条不再丢弃）

**中期**（差异化）
- VOFA+ 兼容帧格式（FireWater / JustFloat）
- 波形 FFT / 光标测量 / 双 Y 轴
- Dashboard 双向控件（滑块 / 按钮 / LED）
- Lua / JS 脚本引擎
- Modbus RTU/TCP 主从内置
- ESP-IDF / Arduino CLI 集成（替代手动 esptool 调用）

**长期**
- CAN 厂商驱动：PCAN / Kvaser / ZLG / Vector（需引入各家 SDK）
- Avalonia 迁移跨平台
- 插件系统（自定义解析器 / 可视化 / 校验算法）

## 开发约定

- Core 层零 UI 依赖，所有业务逻辑放这里，便于单测
- ViewModel 用 `CommunityToolkit.Mvvm` 源生成（`[ObservableProperty]` / `[RelayCommand]`）
- 主题资源通过 `DynamicResource` 引用，保证运行时切换
- `ISerialPortService` 用 `Transient` 注册（每个 Session 独立实例）
- 单元测试：标准向量（CRC、协议字节序）必须有断言依据，不要靠"跑一遍记一下"

## 相关文档

- `docs/superpowers/specs/2026-05-24-serialmaster-design.md` — 详细设计文档
- `docs/superpowers/plans/2026-05-24-serialmaster-phase1-mvp.md` — Phase 1 实施计划

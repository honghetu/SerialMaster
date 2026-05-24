# SerialMaster 串口大师 — 设计文档

**日期**: 2026-05-24
**技术栈**: C# WPF + .NET 8
**模式**: MVVM (CommunityToolkit.Mvvm)

---

## 概述

面向嵌入式开发的串口调试工具。支持多设备同时连接、多格式数据展示、波形/雷达/3D 可视化、自定义协议解析、文件传输、脚本自动化、硬件引脚控制等功能。所有可视化面板均以浮动窗口呈现，支持拖拽停靠。

---

## 架构

```
SerialMaster/
├── SerialMaster.Core/              # 核心库，无 UI 依赖
│   ├── Models/                     # 数据模型
│   ├── Services/                   # 业务逻辑
│   │   ├── SerialPortService       # 串口封装
│   │   ├── DeviceEnumerator        # COM 枚举 + 设备名
│   │   ├── ProtocolParser          # 协议解析引擎
│   │   ├── ChecksumCalculator      # 校验计算器
│   │   ├── FileTransferService     # X/Y/ZMODEM
│   │   ├── TriggerEngine           # 触发匹配
│   │   ├── MacroEngine             # 脚本/宏执行
│   │   └── DataLogger              # 数据记录
│   └── Observables/                # 可观察对象
├── SerialMaster.UI/                # WPF 主界面
│   ├── ViewModels/
│   ├── Views/
│   ├── Controls/                   # 自定义控件
│   └── Themes/                     # 深色/浅色主题
└── SerialMaster.App/               # 启动项 + DI 容器
```

**依赖**: CommunityToolkit.Mvvm, AvalonDock, HelixToolkit, Microsoft.Extensions.DI, LiveCharts2

---

## 模块设计

### 1. 设备管理器 (DeviceManagerViewModel)

**职责**: 枚举可用 COM 口、获取设备描述、管理端口连接状态

- 通过 WMI (`Win32_PnPEntity`) 查询设备描述
- 注册表 fallback 查询 `HARDWARE\DEVICEMAP\SERIALCOMM`
- 定时刷新（可配置间隔，默认 2 秒）
- 每个设备显示：端口名、设备描述、连接状态、当前配置
- 预设模板：Arduino Uno / ESP32 / STM32 ISP / 通用

**关键接口**:
```
DeviceInfo { PortName, Description, IsConnected, BaudRate, Template }
DeviceManager {
    ObservableCollection<DeviceInfo> Devices
    Connect(DeviceInfo, SerialConfig)
    Disconnect(DeviceInfo)
    Refresh()
}
```

### 2. 端口 Session (SerialSession)

**职责**: 单端口完整生命周期管理

- 封装 `System.IO.Ports.SerialPort`
- 独立的收发缓冲队列 (`Channel<byte>`)
- 异步读写，不阻塞 UI 线程
- 自动重连（可配置，最大 3 次）
- 超时检测（可配置，默认 2000ms 无数据触发超时警告）
- 引脚状态变更事件

**关键接口**:
```
SerialSession {
    SerialConfig Config           // 端口号/波特率/数据位/停止位/校验
    PinState { Dtr, Rts, Cts, Dsr }
    IAsyncEnumerable<DataPacket> ReceiveStream
    Task Send(byte[])
    Task SetPin(string pin, bool state)
    event OnError, OnTimeout, OnStateChanged
}
```

### 3. 引脚控制 (PinControlViewModel)

**职责**: DTR/RTS/CTS/DSR 手动控制，预设时序

- 单个引脚高/低电平切换
- 预设序列：[复位] = DTR低→100ms→DTR高
- [Bootloader] = RTS低+DTR低→200ms→DTR高→50ms→RTS高
- 可自定义时序序列（保存为模板）

### 4. 数据视图 (DataViewViewModel)

**职责**: 数据展示核心，四种显示模式

- **HEX 模式**: 空格分隔十六进制，每行 16 字节
- **ASCII 模式**: 不可打印字符显示为 `.`
- **双栏模式**: 左侧 HEX 右侧 ASCII 对照
- **解析模式**: 按协议定义逐帧解析，显示字段列表

每条记录包含:
```
DataRecord {
    DateTime Timestamp     // 微秒精度
    Direction { Send, Receive }
    byte[] Data
    RecordStatus { Success, Failed, Timeout, Retry }
}
```

- VirtualizingStackPanel 虚拟化，支撑百万级记录
- 方向颜色: 发送=蓝色，接收=绿色
- 状态指示灯: 成功=绿圈，失败=红圈，超时=黄三角
- 底部状态栏: 收发字节数、错误/警告累计

### 5. 发送面板 (SendPanelViewModel)

**职责**: 数据发送 + 输入辅助

- 多编码: HEX / ASCII / UTF-8 / GB2312 / GBK
- 自动追加: `\r\n` / `\n` / `\r` / CRC16-Modbus / CRC32 / 自定义
- 定时发送: 可配置间隔 (10ms ~ 60s)
- 发送历史: 最近 200 条，显示状态，可复用/收藏
- 收藏夹: 命名、分组、排序
- 快捷键: `Ctrl+Enter` 发送

### 6. 终端模式 (TerminalViewModel)

**职责**: VT100/ANSI 终端模拟，交互式串口控制台

- VT100/ANSI 转义序列解析（SGR 颜色、光标控制、清屏等）
- 本地回显开关
- 输入行编辑（历史记录 ↑↓ 翻阅）
- 行缓冲 / 字符缓冲切换
- 可选择 HEX 发送还是纯文本发送

### 7. 波形视图 (WaveformViewModel)

**职责**: 浮动窗口，多通道实时曲线渲染

- 底层: LiveCharts2 高性能折线图
- 通道配置: 名称、颜色、数据源（字节位置+类型）、Y轴范围
- 数据源绑定: 从协议解析结果中提取数值
- 实时滚动: 显示最近 N 个采样点（可配 100~10000）
- X/Y 轴缩放、拖拽平移
- 游标/标记功能
- 历史缩略导航条
- 帧率: 目标 30fps，自适应降帧

### 8. 雷达视图 (RadarViewModel)

**职责**: 极坐标 360° 扫描显示

- 自定义 WPF 绘图 (SkiaSharp 或 DrawingVisual)
- 极坐标渲染: 角度-距离-强度
- 量程调节、余辉衰减
- 目标列表: 角度/距离/强度/时间
- 数据源绑定: 从解析器字段映射

### 9. 3D 视图 (View3DViewModel)

**职责**: 三维点云/网格渲染

- HelixToolkit.Wpf 渲染
- 点云、网格、线框三种渲染模式
- 颜色映射: 按高度/强度/自定义
- 鼠标旋转/缩放/平移
- 坐标轴参考线
- 数据源绑定: X/Y/Z 字段映射、颜色字段映射

### 10. 协议解析器 (ProtocolParserViewModel)

**职责**: 自定义数据帧结构定义与解析

帧定义模型:
```
ProtocolDefinition {
    string Name
    byte[] FrameHeader          // 可选帧头
    bool VariableLength
    List<FieldDefinition> Fields
}

FieldDefinition {
    string Name
    FieldPosition Position      // 起始字节 / 相对位置 / 动态位置
    int Length                  // 固定长度 / 从某个字段读取
    DataType { Fixed, uint8, uint16, int16, uint32, float32, BCD, ASCII, Raw }
    Endian { Little, Big }
    string DisplayFormat        // 可选格式化
    byte[] ExpectedValue        // 固定值校验（用于帧头/帧尾）
}
```

- 实时解析测试: 输入原始 HEX，即时查看解析结果
- 帧同步: 自动搜索帧头对齐
- 解析结果绑定到波形/雷达/3D 视图的数据源
- 协议定义可导入/导出 JSON

### 11. 校验工具 (ChecksumViewModel)

**职责**: 内建多种校验算法，独立工具窗口

支持算法:
- CRC8 (MAXIM, Dallas)
- CRC16 (Modbus, CCITT, XMODEM)
- CRC32 (IEEE, MPEG2)
- XOR
- SUM / SUM Complement
- LRC
- 自定义多项式 CRC

- 输入 HEX 字符串，实时计算结果显示
- 一键追加校验值到发送区

### 12. 触发匹配 (TriggerEngine)

**职责**: 数据条件匹配，触发预定义动作

```
TriggerRule {
    byte[] Pattern           // 匹配模式（支持通配符 ?）
    MatchPosition { Anywhere, Header, AtOffset(int) }
    TriggerAction { Highlight(HighlightColor), Pause, Beep, Send(byte[]), ExecuteMacro(name) }
}
```

- 多条规则可同时生效
- 高亮颜色可自定义
- 匹配统计计数

### 13. 文件传输 (FileTransferViewModel)

**职责**: XMODEM/YMODEM/ZMODEM + 原始 bin 发送

- 标准协议: XMODEM (128/1K), YMODEM, ZMODEM
- 原始二进制: 指定分包大小、间隔延时
- 进度条、传输速率显示
- 支持取消/重试
- 发送和接收双向
- 基于 `SerialSession` 的原始字节收发

### 14. 脚本/宏 (MacroEngine)

**职责**: 自动化指令序列

```
MacroStep {
    StepType { Send, Delay, WaitFor, SetPin, SetBaudRate, Loop, Goto }
    byte[] Data               // Send 时使用
    int TimeoutMs             // WaitFor 时使用
    byte[] ExpectedPattern    // WaitFor 的匹配模式
}
```

- 可视化步骤编辑器（列表 + 拖拽排序）
- 循环/跳转支持
- 执行时高亮当前步骤
- 可暂停/继续/停止
- 宏文件导入/导出 JSON

### 15. 数据记录 (DataLogger)

**职责**: 自动保存串口数据到文件

- 文件格式: 带时间戳文本 / 纯 HEX / JSON 结构化 / 原始二进制
- 文件分割: 按天 / 按小时 / 按文件大小 (MB)
- 路径模板: `{port}_{date}_{time}.log`
- 每个 Session 独立配置
- 手动导出: 导出当前显示范围或全部数据
- 滚动缓冲: 内存中只保留最近 N 条 (默认 10 万)，超出写入磁盘

### 16. 统计面板 (StatisticsViewModel)

**职责**: 浮动窗口，实时数据统计

- 连接时长、收发字节/帧数
- 实时速率（1s/10s/60s 平均）
- 峰值速率
- 错误/超时/断连计数
- 速率迷你走势图

### 17. 预设模板 (TemplateManager)

**职责**: 常用设备配置一键加载

预设:
- Arduino Uno: 115200-8N1, DTR 复位
- ESP32: 115200-8N1, RTS Bootloader
- STM32 ISP: 115200-8E1, 特定握手
- HC-05 蓝牙模块: 38400-8N1, AT 指令模式

用户可新增/编辑/删除自定义模板。
模板存储为 JSON: `%AppData%/SerialMaster/templates/`

### 18. 主题引擎 (ThemeService)

**职责**: 深浅色切换 + 自定义配色

- 深色主题: 藏蓝底 (#1E1E2E) + 亮色文字 + 绿色数据 / 蓝色高亮
- 浅色主题: 白底 (#FFFFFF) + 深灰文字 + 蓝色数据
- 实时切换无需重启
- 配色方案存储在 `%AppData%/SerialMaster/themes/`
- 所有自定义控件通过 DynamicResource 绑定颜色

---

## 数据流

```
SerialPort ──▶ SerialSession ──▶ Channel<byte[]> ──▶ DataPacketParser
                                                        │
                                          ┌─────────────┼─────────────┐
                                          ▼             ▼             ▼
                                    DataView      TriggerEngine   DataLogger
                                          │             │
                                    ProtocolParser     │
                                          │             │
                              ┌───────────┼───────────┐ │
                              ▼           ▼           ▼ │
                         Waveform     Radar        View3D
```

---

## 错误处理策略

- 串口异常 (`UnauthorizedAccessException`, `IOException` 等) → 统一捕获，UI 状态灯 + 状态栏消息
- 断连自动检测 (SerialPort.ErrorReceived 事件) → 通知用户，可选自动重连
- 发送超时 → 标记失败状态灯，记录到发送历史
- 文件 I/O 错误 → 提示用户检查权限/磁盘空间
- 所有异常记录到日志文件 `%AppData%/SerialMaster/logs/`

---

## 测试策略

- **单元测试**: Core 项目全量覆盖 (Services, Models, Engine)
- **模拟串口**: 使用 `com0com` 虚拟串口对进行集成测试
- **手动测试**: 真实硬件设备 (Arduino / ESP32 / USB-TTL)
- **UI 自动化**: 暂不实施，MVP 后评估

---

## 配置持久化

所有用户设置存储为 JSON:
```
%AppData%/SerialMaster/
├── settings.json          # 全局设置（主题、语言、窗口布局）
├── templates/             # 设备模板
├── themes/                # 自定义主题
├── protocols/             # 协议定义
├── macros/                # 宏脚本
└── logs/                  # 错误日志
```

---

## 项目结构

```
D:\开发\串口\
├── SerialMaster.sln
├── src/
│   ├── SerialMaster.Core/
│   │   ├── Models/
│   │   ├── Services/
│   │   └── SerialMaster.Core.csproj
│   ├── SerialMaster.UI/
│   │   ├── ViewModels/
│   │   ├── Views/
│   │   ├── Controls/
│   │   ├── Themes/
│   │   ├── Converters/
│   │   └── SerialMaster.UI.csproj
│   └── SerialMaster.App/
│       ├── App.xaml
│       ├── App.xaml.cs
│       ├── DependencyInjection.cs
│       └── SerialMaster.App.csproj
├── tests/
│   └── SerialMaster.Core.Tests/
│       └── SerialMaster.Core.Tests.csproj
└── docs/
    └── superpowers/
        └── specs/
            └── 2026-05-24-serialmaster-design.md
```

---

## 开发阶段

### 阶段 1: 核心框架 + 基础串口 (MVP)
- 项目骨架搭建 (DI, MVVM 基础设施)
- 设备管理器 + 端口 Session + 基础数据收发
- 数据视图 (HEX/ASCII/双栏)
- 发送面板 (基础)
- 深色/浅色主题
- 设置持久化

### 阶段 2: 进阶功能
- 引脚控制
- 终端模式
- 校验工具
- 数据记录
- 发送历史 + 收藏
- 统计面板
- 预设模板

### 阶段 3: 可视化
- 波形视图
- 雷达视图
- 3D 视图

### 阶段 4: 高级功能
- 协议解析器
- 触发匹配
- 文件传输 (X/Y/ZMODEM)
- 脚本/宏引擎

---

## 兼容性

- **Windows 10/11** (x64)
- **.NET 8 Runtime**
- **串口驱动**: 标准 CDC ACM / FTDI / CH340 / CP210x / PL2303
- **虚拟串口**: com0com, VSPD 等

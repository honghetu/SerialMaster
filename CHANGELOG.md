# Changelog

格式遵循 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)，版本号遵循 [SemVer](https://semver.org/lang/zh-CN/)。

## [Unreleased]

## [1.6.1] - 2026-05-25

### Fixed
- About 和"检查更新"弹窗错误地显示 v1.0：之前用 `typeof(MainViewModel).Assembly`
  取的是 SerialMaster.UI 工程集（默认版本 1.0），改用 `Assembly.GetEntryAssembly()`
  正确读取 SerialMaster.App.exe 的版本号

### Changed
- 新增 `Directory.Build.props` 作为版本号唯一来源（3 个项目自动继承）

## [1.6.0] - 2026-05-25

### Added
- 应用图标（自动生成，多分辨率）
- 实时统计面板（1s/10s/60s 速率、峰值、错误计数、协议帧数）
- 波形 X/Y 轴 + 数值刻度 + 网格
- DataLogger 落盘（toggle 录制到 %LocalAppData%/SerialMaster/data/，按 100MB 切片）
- 协议字段绑波形通道（ProtocolField.WaveformChannel）
- 顶部 toolbar 加波特率下拉（实时切换不断开） + 显示模式下拉（HEX/ASCII/Dual/Terminal）
- 帮助菜单 + 使用说明面板（F1 / 菜单触发）
- GitHub Releases 检查更新（菜单 + 启动后台静默检查）
- Send 反馈可见化（成功/失败/超时即时显示在输入框下方）
- DTR/RTS ToggleButton 文字显示 ON/OFF（不再只换底色）

### Changed
- Direction 列从 "Send"/"Receive" 改为 "Send:"/"Receive:" 视觉更清晰
- HEX 长帧自动折行，禁用水平滚动（不再左右拉）

### Fixed
- SendAsync 在坏驱动下永挂起（按钮变白色不动）→ 加 WriteAsync 超时
- ReadLoop 遇到瞬时错误（帧错/校验/溢出）直接退出 → 改为容错重试，连续 10 次或端口真死才断
- DTR/RTS ToggleButton 双重 toggle bug（IsChecked + Command 同时绑导致点击没效果）
- HelpView 因 Version 只读属性 + WPF Binding 默认 TwoWay 引发 InvalidOperationException 崩溃



### Added
- 全局未处理异常捕获 + 用户错误提示对话框
- `FileLogger`：写入 `%LocalAppData%/SerialMaster/logs/serialmaster-yyyyMMdd.log`
- `ISerialPortService.DataReceived` 事件 — 让 XMODEM 等瞬时消费者无需消费 Channel
- XMODEM 接收端 (`XmodemService.ReceiveAsync`) 和 UI 按钮
- XMODEM-CRC 模式（之前只有 Checksum 模式）
- Modal 主窗口快捷键真正生效（Ctrl+N/O/S/E/L、Ctrl+Shift+F）
- `NewConnectionCommand` 实现（刷新设备列表）
- `FavoritesService.Changed` 事件，已开 Session 收藏夹更新会自动刷新快捷条

### Changed
- About 对话框：版本号从 Assembly 动态读取，移除 AvalonDock 虚假描述
- WaveformControl: 复用 PointCollection，移除每帧 GC 压力
- View3DControl: 点上限从 5000 收紧到 1500（sphere mesh 性能限制）
- MacroEditor 步骤 HEX 解析改用 `InputParser`，支持 `0x` 前缀/连字符/混合大小写
- QuickSend 语义明确：HEX 收藏不附加行尾、文本收藏默认 CRLF（不再受 Session 的 SendMode 影响）
- SerialPortService.Close 不再在 UI 线程同步 Wait 1 秒

### Fixed
- CanBusViewModel 把串口错误事件当 CAN 帧追加到列表（污染数据）
- ProtocolDefinitionStore：用户删光所有协议后重启又见预设
- DeviceEnumerator.Dispose 不清回调引用（潜在内存泄漏）
- XMODEM 与 SessionViewModel 抢消费同一 Channel 导致 ACK/NAK 被吞 → 改用事件订阅
- 菜单 `Ctrl+N` 等快捷键只是装饰文本无实际效果
- `NewConnectionCommand` 菜单引用但 ViewModel 没实现（绑定失败）

### Removed
- `AvalonDock` NuGet 依赖（从未使用，节省 ~5MB）
- `Controls/StatusIndicator.xaml(.cs)`（零引用死代码）

## [1.2.0] - 2026-05-25

### Added
- 固件烧录器（包装 esptool / stm32flash 子进程）
- CAN 总线 (SLCAN/LAWICEL) 协议层 + 视图
- TCP/UDP ↔ Serial 桥
- 12 条 SLCAN 编解码单元测试

## [1.1.0] - 2026-05-25

### Added
- 协议解析器（帧头同步 + 12 字段类型 + JSON 持久化）
- 定时发送、快捷发送按钮条
- 10 条 ProtocolParser 单元测试

### Changed
- `SerialPortService` 同步事件改异步读取循环
- `InputParser`：HEX/ASCII/UTF-8 + 行尾，替代脆弱的"含空格才认 HEX"启发式

## [1.0.0] - 2026-05-24

### Added
- 多端口 Session、HEX/终端视图、深浅主题
- 校验工具、收藏夹、宏编辑器
- 设备枚举（WMI + 注册表 fallback）
- XMODEM 文件传输（基础）

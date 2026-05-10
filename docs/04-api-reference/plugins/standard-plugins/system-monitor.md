# SystemMonitor 插件

本页只描述当前仓库里实际存在的 SystemMonitor 插件实现，不再继续维护“版本信息 + 调优手册 + 理想化架构图”式旧稿。

## 先看这个插件现在是什么

按当前源码状态，SystemMonitor 是一个偏轻量的系统监控插件，核心不是独立应用外壳，而是一组围绕单例监控服务展开的集成点：

- `SystemMonitors`：监控数据和命令的中心单例。
- `SystemMonitorProvider`：把插件接入设置页和工具菜单。
- `SystemMonitorIStatusBarProvider`：把可选监控项接入主程序状态栏。
- `SystemMonitorControl`：实际显示监控数据的 WPF 控件。

因此它更接近“系统监控服务 + UI 接入层”，而不是一个体量很重的独立窗口程序。

## 当前最重要的文件

- `Plugins/SystemMonitor/manifest.json`
- `Plugins/SystemMonitor/SystemMonitors.cs`
- `Plugins/SystemMonitor/SystemMonitorControl.xaml(.cs)`
- `Plugins/SystemMonitor/SystemMonitorIStatusBarProvider.cs`

其中 `SystemMonitors.cs` 承担了绝大多数真正的运行时逻辑；另外两个类型主要负责把它接到宿主 UI。

## 当前功能面实际包括什么

从 `SystemMonitors` 的实现看，这个插件当前覆盖的监控面明显比旧文档里“时间 + RAM”更广：

### 性能计数器

插件会异步初始化 Windows 性能计数器，并定时更新：

- 系统 CPU 使用率
- 当前进程 CPU 使用率
- 系统 RAM 使用率
- 当前进程私有工作集

如果性能计数器初始化失败，当前实现会降级为不刷新这些数值，而不是中止整个插件。

### 磁盘与网络

插件当前会主动加载并维护：

- 所有已就绪磁盘的容量、已用空间、空闲空间、占用比例
- 非 loopback / tunnel 的网络接口信息
- 网络接口的 IPv4 地址、MAC 地址、链路速率和状态

这部分数据并不依赖状态栏开关，状态栏只是决定是否把其中一部分投影到主窗口底部。

### 进程与运行时环境

当前还会收集：

- 前 10 个高内存占用进程
- 当前进程线程数和句柄数
- 系统启动时间、应用运行时长、系统运行时长
- CPU 名称、主机名、.NET 运行时、系统架构、用户名
- 主屏幕分辨率

### GPU 与缓存

插件还会读取 `ConfigCuda.Instance`，在可用时展示 CUDA 设备名称和显存信息；同时提供缓存大小统计和清理命令。

## 当前接入宿主的三条链

### 设置页

`SystemMonitorProvider` 实现了 `IConfigSettingProvider`，会把 `SystemMonitors.GetInstance()` 作为设置页数据源，并用 `SystemMonitorControl` 作为显示控件。

这意味着设置页和单独弹窗看的其实是同一份单例数据，而不是两套监控实例。

### 工具菜单

同一个 `SystemMonitorProvider` 还实现了 `IMenuItemProvider`，当前会往 `Tool` 菜单下注入“性能监控”入口，并打开一个承载 `SystemMonitorControl` 的普通 WPF 窗口。

### 状态栏

`SystemMonitorIStatusBarProvider` 实现的是 `IStatusBarProviderUpdatable`，会根据配置开关动态决定状态栏项是否存在。当前可投影到状态栏的项包括：

- 时间
- 应用运行时长
- CPU 文本
- RAM 文本
- 磁盘图标与剩余空间

因此它不是旧文档里那种固定两项的静态状态栏提供器。

## 当前配置模型

`SystemMonitorSetting` 目前至少包含这些开关和参数：

- `UpdateSpeed`
- `DefaultTimeFormat`
- `IsShowTime`
- `IsShowRAM`
- `IsShowCPU`
- `IsShowUptime`
- `IsShowDisk`

旧文档里只写时间与 RAM，已经覆盖不全。

## 当前命令面

`SystemMonitors` 当前暴露的用户命令主要有：

- `ClearCacheCommand`
- `RefreshDrivesCommand`
- `RefreshNetworkCommand`
- `RefreshProcessesCommand`

这些命令对应的真实行为分别是清理应用数据与日志目录、重载磁盘列表、重载网络接口列表、重载高占用进程列表。

## 当前几个最容易写错的点

### 它不是独立窗口程序为中心的插件

虽然菜单会打开一个窗口，但窗口里只是挂载 `SystemMonitorControl`。真正持续运行的核心对象是 `SystemMonitors` 单例。

### 它不只是状态栏时间插件

当前状态栏只是三条集成链之一。大量数据其实服务于完整监控控件，包括磁盘、网络、GPU、进程列表和缓存统计。

### `IStatusBarProviderUpdatable` 很关键

状态栏显示项的刷新当前依赖 `SystemMonitorIStatusBarProvider` 监听配置变更后触发 `StatusBarItemsChanged`。如果把它误写成普通静态 provider，会把现在这条动态刷新链写偏。

### 类型命名和命名空间不要想当然

`SystemMonitors` 和 `SystemMonitorSetting` 当前位于 `ColorVision.UI.Configs` 命名空间，而不是插件自己的 `SystemMonitor` 命名空间。这是现状代码的一部分，不要擅自按“插件内部自成体系”去重述。

## 推荐阅读顺序

1. `Plugins/SystemMonitor/SystemMonitors.cs`
2. `Plugins/SystemMonitor/SystemMonitorControl.xaml.cs`
3. `Plugins/SystemMonitor/SystemMonitorIStatusBarProvider.cs`
4. `Plugins/SystemMonitor/manifest.json`

这样能先抓住真实控制面，再回到菜单、状态栏和装载信息。

## 继续阅读

- [Plugins/README.md](../../../../Plugins/README.md)
- [docs/02-developer-guide/plugin-development/overview.md](../../../02-developer-guide/plugin-development/overview.md)
- [docs/04-api-reference/plugins/standard-plugins/pattern.md](./pattern.md)
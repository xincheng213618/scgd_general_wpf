---
knowledge_id: "plugins.system-monitor"
knowledge_type: "reference"
status: "current"
summary: "SystemMonitor 的性能采样、状态栏、窗口生命周期与停止采样边界。"
aliases: ["状态栏性能监控在哪里","关闭监控窗口后还采样吗","SystemMonitorIStatusBarProvider","SystemMonitorControl"]
code_paths: ["Plugins/SystemMonitor/SystemMonitorControl.xaml.cs","Plugins/SystemMonitor/SystemMonitors.cs","Plugins/SystemMonitor/SystemMonitorIStatusBarProvider.cs"]
test_paths: ["Test/ColorVision.UI.Tests/SystemMonitorLifecycleTests.cs"]
related: ["plugins.index","plugins.capabilities","ui.status-bar"]
---

# SystemMonitor 插件

SystemMonitor 是 `Plugins/SystemMonitor/` 下的轻量系统监控插件，核心是 `SystemMonitors` 单例，再通过设置页、工具菜单和状态栏接入主程序。

## 先查什么

| 现象 | 第一检查点 |
| --- | --- |
| 状态栏不刷新 | `SystemMonitorIStatusBarProvider` 是否监听配置变更，`StatusBarItemsChanged` 是否触发 |
| CPU/RAM 为空 | Windows 性能计数器是否初始化失败；这是可降级项 |
| 磁盘列表为空 | drive 是否 ready、权限是否可读，可用刷新命令重载 |
| 网络信息缺失 | 是否被 loopback/tunnel 过滤，是否有 IPv4 地址 |
| 清理缓存失败 | 目标目录权限、文件占用，不要扩大到任意目录删除 |
| Tool 菜单无入口 | 插件目录、`manifest.json`、`SystemMonitor.dll`、菜单 Provider |

## 当前能力

| 能力 | 当前入口 | 说明 |
| --- | --- | --- |
| 监控单例 | `SystemMonitors.cs` | 数据刷新、命令、配置引用和运行时状态 |
| 设置页 | `SystemMonitorProvider` | 实现 `IConfigSettingProvider`，用 `SystemMonitorControl` 展示同一份单例数据 |
| 工具菜单 | `SystemMonitorProvider` | 向 Tool 菜单注入“性能监控”窗口 |
| 状态栏 | `SystemMonitorIStatusBarProvider` | 根据配置动态投影时间、运行时长、CPU、RAM、磁盘项 |
| 显示控件 | `SystemMonitorControl.xaml(.cs)` | 展示 CPU/RAM、磁盘、网络、进程、GPU、缓存等信息 |

## 监控内容

| 类别 | 当前数据 |
| --- | --- |
| 性能 | 系统 CPU、当前进程 CPU、系统 RAM、当前进程私有工作集 |
| 磁盘 | ready drive 的容量、已用、空闲、占用比例 |
| 网络 | 非 loopback/tunnel 网卡、IPv4、MAC、链路速率、状态 |
| 进程 | 前 10 个高内存进程、当前进程线程数和句柄数 |
| 运行时 | 系统启动时间、应用运行时长、系统运行时长、CPU 名称、主机名、.NET、架构、用户名、主屏幕分辨率 |
| GPU/缓存 | CUDA 设备名和显存信息、缓存大小和清理命令 |

## 配置与命令

| 类型 | 当前项 |
| --- | --- |
| 配置 | `UpdateSpeed`、`DefaultTimeFormat`、`IsShowTime`、`IsShowRAM`、`IsShowCPU`、`IsShowUptime`、`IsShowDisk` |
| 命令 | `ClearCacheCommand`、`RefreshDrivesCommand`、`RefreshNetworkCommand`、`RefreshProcessesCommand` |

`SystemMonitors.UpdateTimerState()` 只在详情控件已 Loaded，或时间/RAM/CPU/运行时长状态项至少一项启用时启动 `Timer`；所有详情控件 Unloaded 且这些状态项关闭时释放定时器。仅显示磁盘状态不要求周期采样。刷新间隔实际取 `Math.Max(100, UpdateSpeed)`；只有详情视图或 CPU/RAM 项需要性能计数器，才按需在后台初始化，失败时降级。

该插件所有 `IsShow*` 配置都为 false 时，`SystemMonitorIStatusBarProvider` 不应为了生成空状态栏项先创建监控单例。这些边界由 `Test/ColorVision.UI.Tests/SystemMonitorLifecycleTests.cs` 的配置关闭、详情活跃、磁盘单项与配置重载测试约束。它不包括右键隐藏某项、Hide Status Bar 或主窗口容器折叠；这些动作不改变插件配置，详见[状态栏隐藏与生命周期](../../ui-components/status-bar.md)。

## 刷新模型

| 数据 | 刷新方式 | 维护重点 |
| --- | --- | --- |
| CPU/RAM/时间/运行时长 | 定时器刷新 | 性能计数器失败时保持插件可用 |
| 磁盘列表 | 初始化和 `RefreshDrivesCommand` | 只展示 ready drive |
| 网络接口 | 初始化和 `RefreshNetworkCommand` | 排除 loopback/tunnel，只取 IPv4 |
| 高内存进程 | 初始化和 `RefreshProcessesCommand` | 权限失败项跳过 |
| CUDA 信息 | 构造时读取 `ConfigCuda.Instance` | 不可用时显示为空 |

## 验收

| 验收项 | 通过标准 |
| --- | --- |
| 设置页 | 设置中的系统监控项能显示 `SystemMonitorControl` |
| 工具菜单 | Tool 菜单能打开性能监控窗口 |
| 状态栏开关 | 时间、CPU、RAM、运行时长、磁盘开关能动态增删状态栏项 |
| 刷新命令 | 磁盘、网络、进程列表能重新加载，异常项不关闭窗口 |
| 缓存清理 | 只清理应用数据/日志相关目录，权限失败有提示 |
| 降级环境 | 性能计数器异常时插件仍能打开 |

## 边界

- 插件不是独立窗口程序；`SystemMonitors` 单例的存在不代表定时器始终运行，采样由视图活跃状态与动态状态项配置开关共同决定，不直接取决于容器 Visibility。
- 它不只是状态栏时间/RAM 插件；完整控件还展示磁盘、网络、GPU、进程和缓存。
- 状态栏动态刷新依赖 `IStatusBarProviderUpdatable`，不要写成静态 Provider。
- `SystemMonitors` 和 `SystemMonitorSetting` 当前位于 `ColorVision.UI.Configs` 命名空间，这是现状代码。

## 关键文件

| 任务 | 先看 |
| --- | --- |
| 监控数据和命令 | `SystemMonitors.cs` |
| 设置页/菜单 | `SystemMonitorControl.xaml.cs` |
| 状态栏 | `SystemMonitorIStatusBarProvider.cs` |
| 装载信息 | `manifest.json` |

## 本地构建与测试

以下命令只写本地产物，不上传、不执行缓存清理；缓存清理是删除操作，必须单独确认目标目录和授权。

```powershell
dotnet build .\Plugins\SystemMonitor\SystemMonitor.csproj -c Release -p:Platform=x64
dotnet test .\Test\ColorVision.UI.Tests\ColorVision.UI.Tests.csproj -c Release -p:Platform=x64 --filter FullyQualifiedName~SystemMonitorLifecycleTests
```

## 打包上传（需明确发布授权）

只有用户明确要求发布 SystemMonitor 时运行。wrapper 会构建、上传并清理本地 `.cvxp`，不支持 `--no-upload`。

```powershell
.\Scripts\package_plugin.bat SystemMonitor
```

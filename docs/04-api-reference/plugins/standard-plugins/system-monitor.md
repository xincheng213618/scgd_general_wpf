---
knowledge_id: "plugins.system-monitor"
knowledge_type: "reference"
status: "current"
summary: "系统监控的 CPU/RAM 采样、手动刷新与状态栏生命周期；缓存大小包含子目录，清理只删顶层文件，逐文件失败不会单独提示。"
aliases: ["系统监控","SystemMonitor","SystemMonitors","SystemMonitorSetting","清理缓存","缓存大小","更新速度","状态栏性能监控在哪里","关闭监控窗口后还采样吗","SystemMonitorIStatusBarProvider","SystemMonitorControl"]
code_paths: ["Plugins/SystemMonitor/SystemMonitorControl.xaml.cs","Plugins/SystemMonitor/SystemMonitors.cs","Plugins/SystemMonitor/SystemMonitorIStatusBarProvider.cs","Plugins/SystemMonitor/SystemMonitorControl.xaml","Plugins/SystemMonitor/manifest.json","UI/ColorVision.UI/Environments.cs","UI/ColorVision.Common/Utilities/MemorySize.cs"]
test_paths: ["Test/ColorVision.UI.Tests/SystemMonitorLifecycleTests.cs"]
related: ["plugins.index","plugins.capabilities","ui.status-bar"]
---

# 系统监控（SystemMonitor）

SystemMonitor 提供系统资源信息、应用运行时长和可选状态栏项目。插件加载后，从“工具”菜单打开“系统监控”，也可在设置页的同名标签中查看。两处使用同一个 `SystemMonitors` 单例；它不是独立运行程序。

## 查看和设置监控

1. 打开“系统监控”，查看 CPU、内存、磁盘、网络与进程信息。
2. 需要重新读取磁盘、网络或高内存进程列表时，点击对应区域的刷新图标；这些列表不随每次定时采样更新。
3. 在底部“设置”调整“更新速度”和日期格式，或打开需要的状态栏开关。关闭详情窗口后，启用的时间、运行时长、CPU、RAM 项仍可维持采样。

| 配置 | 默认值与限制 |
| --- | --- |
| `UpdateSpeed`（更新速度） | 1000毫秒；设置器忽略小于100的值，定时器另以100毫秒为最小间隔 |
| `DefaultTimeFormat`（日期格式） | `yyyy/MM/dd HH:mm:ss`；格式无效时格式化异常被忽略，时间文本可能保留旧值 |
| `IsShowTime` / `IsShowUptime` / `IsShowCPU` / `IsShowRAM` / `IsShowDisk` | 默认均为 false；控制本插件提供哪些状态栏项 |

## 监控内容与刷新方式

| 数据 | 内容与刷新范围 |
| --- | --- |
| CPU / RAM | 系统与进程性能计数器；详情活跃或对应状态栏项启用时周期采样。当前进程内存使用私有工作集计数器 |
| 磁盘 | 已就绪 drive 的容量、可用空间、已用量及比例；初始化和磁盘刷新命令重读。状态栏磁盘图标按生成元数据时的最大占用比例选择，不是磁盘故障检测 |
| 网络 | 非 loopback/tunnel 网卡的首个 IPv4、MAC、链路速率和状态；初始化和网络刷新命令重读。无 IPv4 的网卡仍可列出，IP 显示不可用；链路速率不是实时流量 |
| 高内存进程 | 按 `WorkingSet64` 排序取前10项，初始化和进程刷新命令在后台重读；无法读取的单个进程被跳过。它与私有工作集不是同一内存口径 |
| 时间与运行信息 | 当前时间、应用运行时长；详情活跃时另更新系统运行时长、当前进程线程数与句柄数。CPU 名称、主机名、.NET、架构、用户名、主屏幕尺寸和启动时间在构造时读取 |
| GPU | 构造时读取 `ConfigCuda.Instance`，名称可含多个设备，显存取首项；未检测到时显示相应文本和空显存，不周期刷新 |
| 缓存大小 | 构造及清理后在后台统计，统计范围与删除范围不同，见下节 |

进程 CPU/私有工作集计数器按进程名创建，没有按当前 PID 再匹配实例；比较同名多进程时须核对计数器来源，不能仅凭窗口显示的 PID 判断数值归属。

## 缓存大小与清理范围

“清理缓存”直接执行删除，没有事前确认对话框。运行前应核对下面两个实际目标和需要保留的文件，取得相应删除授权；它没有按文件扩展名或缓存用途做白名单筛选。

| 目标 | 路径来源 |
| --- | --- |
| 应用数据目录 | `Environments.DirAppData`，默认由当前用户 Roaming AppData 与入口程序集的 Company 组成；不是系统临时目录 |
| 日志文件的父目录 | `Path.GetDirectoryName(Environments.DirLog)`；默认日志路径来自 log4net 根配置的首个 `RollingFileAppender.File`，没有路径则跳过 |

缓存大小使用 `MemorySize.GetDirectoryLength` 递归统计两个目标及其子目录，没有重叠路径去重；日志目录位于应用数据树内时，该部分可能重复计入。

清理只枚举各目标的顶层文件，不递归删除子目录。逐文件删除异常被忽略，最终“清除成功，删除了 N 个文件”只表示成功删除的计数，不能证明所有文件已删除或没有权限/占用失败。外层目录枚举等异常才显示清除失败。清理后重新计算大小是后台任务，因此显示值不一定立即变化，也不应预期归零。

## 采样与状态栏生命周期

`UpdateTimerState()` 在详情控件 Loaded，或时间/RAM/CPU/运行时长状态项至少一项启用时启动定时器；所有详情控件 Unloaded 且这些项关闭时释放定时器。只启用磁盘状态不要求周期采样。详情窗口卸载不会调用监控单例的 `Dispose()`，已经初始化的性能计数器不因定时器停止而自动释放。

`SystemMonitorIStatusBarProvider` 实现 `IStatusBarProviderUpdatable`：配置开关变化时发出 `StatusBarItemsChanged`，配置重载时重新绑定当前配置。仅为全部关闭的状态栏生成空元数据时不会创建监控单例；设置页或详情控件仍可独立创建它。

右键隐藏状态项、Hide Status Bar 或折叠主窗口容器不会修改上述配置，不能据此认定采样已停止。显示隐藏的独立契约见[状态栏生命周期](../../ui-components/status-bar.md)。

## 异常与排查

| 现象 | 检查顺序与限制 |
| --- | --- |
| 菜单或设置页无入口 | 检查插件目录、manifest 和 `SystemMonitor.dll`，再核对 `SystemMonitorProvider`；菜单/设置项显示名是“系统监控”，manifest 名称是“性能监控” |
| CPU/RAM 为0、空白或不再变化 | 计数器只有一套初始化成功标志；任一初始化失败会使整组未就绪，没有逐计数器独立初始化或自动重试。异常通过 `Debug.WriteLine` 记录，不保证进入应用文件日志；0或旧值不是有效采样证明 |
| 磁盘/网卡刷新中断或只显示部分项 | 读取前先清空列表，枚举与逐项属性读取没有异常隔离；不能承诺坏项均被跳过。高内存进程列表才单独跳过读取失败的进程 |
| 状态栏项不出现 | 先核对对应 `IsShow*` 配置和 provider 更新，再检查宿主状态栏隐藏设置 |
| 缓存大小未清零 | 核对递归统计与顶层删除差异、占用/权限以及后台统计是否结束；不要扩大删除范围来追求显示归零 |

性能计数器采样时的异常也被捕获并保留现有数据；这个容错不等于磁盘、网卡和构造阶段的所有系统信息源都独立降级。

## 验证范围

`SystemMonitors.cs` 持有配置、数据采样和清理；`SystemMonitorControl.xaml(.cs)` 负责设置页/菜单窗口与 Loaded/Unloaded；`SystemMonitorIStatusBarProvider.cs` 负责状态栏元数据。监控和配置类型位于 `ColorVision.UI.Configs` 命名空间。

`SystemMonitorLifecycleTests` 通过纯决策方法和替身验证配置关闭、详情活跃、磁盘单项、元数据、单例来源复用与配置重载。它不创建真实性能计数器，也不验证 WPF 窗口事件、定时器释放、磁盘/网卡失败或缓存删除。

环境验收应分别确认实际入口、采样来源、状态栏增删和关闭详情后的采样条件。清理属于另行授权的文件删除，不能为验证文档执行；部分删除结果应按上节解释。

## 本地构建与测试

以下命令构建本地产物或运行隔离生命周期测试，不上传，也不执行缓存清理。若经 solution/MSBuild 构建并提供有效 `SolutionDir`，`PluginProject.HostCopy.targets` 还会将 DLL 和元数据复制到宿主的 Debug/Release 插件输出目录。

```powershell
dotnet build .\Plugins\SystemMonitor\SystemMonitor.csproj -c Release -p:Platform=x64
dotnet test .\Test\ColorVision.UI.Tests\ColorVision.UI.Tests.csproj -c Release -p:Platform=x64 --filter FullyQualifiedName~SystemMonitorLifecycleTests
```

## 打包上传（需明确发布授权）

只有用户明确要求发布 SystemMonitor 时运行。wrapper 会构建、上传并清理本地 `.cvxp`，不支持 `--no-upload`。

```powershell
.\Scripts\package_plugin.bat SystemMonitor
```

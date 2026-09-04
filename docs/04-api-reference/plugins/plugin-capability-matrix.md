---
knowledge_id: "plugins.capabilities"
knowledge_type: "reference"
status: "current"
summary: "横向定位现存插件的菜单、状态、数据库、设备与管理员权限边界。"
aliases: ["插件依赖","插件扩展点","插件权限","哪些插件会操作设备或服务","插件状态栏比较"]
code_paths: ["Plugins/Conoscope/","Plugins/Spectrum/","Plugins/SystemMonitor/","Plugins/WindowsServicePlugin/","Plugins/Pattern/","Plugins/ImageProjector/"]
test_paths: []
related: ["plugins.index","plugins.model","plugins.getting-started","plugins.conoscope","plugins.spectrum","plugins.system-monitor","plugins.windows-service","plugins.pattern"]
---

# 插件依赖与接入矩阵

本页用于按功能、宿主扩展点和外部影响选择插件。范围为 `Plugins/` 中的六个插件项目；每行插件名连接其操作、实现和验证主题。客户专用判定、MES 和项目流程见[项目知识入口](../projects/README.md)。

## 当前源码插件总表

| 插件与源码目录 | 主要用途 | 界面入口 | 外部依赖与操作影响 |
| --- | --- | --- | --- |
| [Pattern](./standard-plugins/pattern.md) · `Plugins/Pattern/` | 测试图卡生成、模板管理与文件导出 | 工具 → 图卡生成工具；功能启动器 | OpenCV、私有 ImageProjector 依赖；导入 ZIP 会替换模板目录，清空操作会删除对应目录内容 |
| [ImageProjector](./standard-plugins/pattern.md#图片投影) · `Plugins/ImageProjector/` | 图片列表、多屏投影、缩放与全屏显示 | 工具 → 图片投影工具；Pattern 窗口内入口 | Windows 显示器及 DPI；投影会改变目标屏幕的显示内容 |
| [Conoscope](./standard-plugins/conoscope.md) · `Plugins/Conoscope/` | VAM/锥镜图像、关注点、参考轴、预处理、色域与对比度分析 | 工具 → VAM；符合条件的 ImageEditor 右键入口；视图 → MVSVideo | 本地 CVCIE、Engine 测量采集和 MVS 观察相机是三种来源；MVS 另需海康驱动及 `MvCameraControl.dll`，采集可能操作设备与数据库 |
| [Spectrum](./standard-plugins/spectrum.md) · `Plugins/Spectrum/` | 光谱测量、标定分组、EQE、CIE 与结果导出 | 工具中的光谱窗口；窗口菜单与状态栏 | 光谱仪、快门/滤光轮/SMU、串口、native DLL 和许可证；测量及校零会操作设备，结果保存到 SQLite |
| [SystemMonitor](./standard-plugins/system-monitor.md) · `Plugins/SystemMonitor/` | CPU/RAM、磁盘、网络、进程、GPU 和缓存信息 | 工具 → 系统监控；同名设置页；可选状态栏项 | Windows 性能计数器、CUDA 信息和网卡；缓存统计与清理范围不同，清理会删除文件 |
| [WindowsServicePlugin](./standard-plugins/windows-service.md) · `Plugins/WindowsServicePlugin/` | 本机服务管理、在线选包、安装、数据库与配置迁移 | 帮助 → 服务管理器；应用与工具 → 内部工具 → 服务管理器；安装向导 | Windows 服务、MySQL、MQTT、服务包及权限代理；安装/恢复会改文件、数据库、服务与进程 |

应用内角色、Windows 权限和设备条件是不同前提。`ServiceManagerAppProvider` 声明应用内 `Administrator` 权限，但服务管理器还存在向导及旧工具入口；实际服务操作需要兼容的权限代理和目标资源权限。其余插件也不能仅凭窗口可打开就推断设备、路径或许可证已就绪。

## 按宿主扩展点定位

| 接入点 | 插件与实现 | 范围 |
| --- | --- | --- |
| 主菜单 | Pattern `ExportTestPatternWpf`、ImageProjector `MenuImageProjector`、Conoscope `MenuConoscopeWindow`、Spectrum `MenuSpectrumWindow`；SystemMonitor 通过 `SystemMonitorProvider` 提供元数据 | 前四者继承 `MenuItemBase`，SystemMonitor 实现 `IMenuItemProvider` |
| 窗口菜单 | Spectrum `LoadMenuForWindow("Spectrum", menu)` | 菜单按目标窗口加载；Conoscope 的 Ribbon、View 菜单和配置控件由其模块主题说明 |
| 宿主状态栏 | `SystemMonitorIStatusBarProvider : IStatusBarProviderUpdatable`；`SpectrumStatusBarProvider : IStatusBarProvider` | 前者按配置开关增删监控项；后者的目标为 `Spectrum`。其余四个项目没有声明这两类状态栏提供器 |
| 宿主设置页 | `SystemMonitorProvider : IConfigSettingProvider` | 设置页与菜单窗口共用 `SystemMonitorControl`；插件持有 `IConfig` 对象不等于注册了独立设置页 |
| ImageEditor 右键 | `ConoscopeImageViewContextMenu : IIEditorToolContextMenu` | 由 `ConoscopeModuleService` 检查当前文件与通道条件后显示入口 |
| 图卡扩展与启动 | Pattern 的 `IPattern` 发现、`PatternFeatureLauncher` | 从已装载程序集发现图卡；窗口可调用 `OpenImageProjectorCommand` 打开投影工具 |
| 帮助菜单、应用工具与向导 | `MenuServiceManager`、`ServiceManagerAppProvider`、`InstallServiceManager` | 前两者打开同一非模态服务管理窗口并要求应用内 Administrator 权限，后者是模态向导步骤；旧 `InstallTool` 仍声明 `ServiceLog` 菜单位置并实现主窗口初始化器 |
| Socket 与调度 | Spectrum 的五个 `ISocketJsonHandler` 和 `Job/` 测量/校零任务 | 复用测量 Manager；传输服务与业务指令见 [Spectrum Socket](./standard-plugins/spectrum-socket.md)，需要服务启用与相应设备条件 |

Conoscope 的 `ConoscopeViewState` / `ConoscopeDocument` 属于标签页和文档状态；全局配置、参考存储与窗口工作副本见[配置与持久化](./standard-plugins/conoscope.md#配置-working-copy、参考与持久化)。Pattern、ImageProjector、Spectrum 和服务管理器的配置也由各模块维护，不能把配置类型列表当作宿主设置页列表。

## 构建与交付差异

版本、manifest 身份、共享依赖剥离、安装和回退的共用规则见[插件产物与交付](../../02-developer-guide/plugin-development/getting-started.md)。发布版本来自编译产物；manifest 是发布时同步的元数据，不单独证明版本或 ABI 一致。

| 插件 | 本地复制与交付差异 | 命令及资源清单 |
| --- | --- | --- |
| Pattern / ImageProjector | HostCopy 默认关闭，需各自启用开关及有效 `SolutionDir`，只写当前配置；Pattern 包私有携带 ImageProjector。完整独立运行输出与剥离共享依赖的 `.cvxp` 用途不同 | [构建、独立运行与交付](./standard-plugins/pattern.md#构建、独立运行与交付) |
| Conoscope | 有效 `SolutionDir` 下，通用 HostCopy 写两套宿主插件目录；额外 target 还向 Debug/Release 宿主根目录复制顶层项目引用 DLL 及存在的 PDB | [本地构建、宿主复制与发布](./standard-plugins/conoscope.md#本地构建、宿主复制与发布) |
| SystemMonitor / WindowsServicePlugin | 使用通用 HostCopy 条件和 Debug/Release 双目录复制；WindowsServicePlugin 的插件 `.cvxp` 与业务服务 ZIP 分别发布 | [SystemMonitor 构建](./standard-plugins/system-monitor.md#本地构建与测试)、[服务插件交付](./standard-plugins/windows-service.md#构建、发布与验证) |
| Spectrum | 没有项目 HostCopy；正式发布同时维护独立 ZIP 和插件 `.cvxp` 两个更新源，使用专用脚本 | [本地构建](./standard-plugins/spectrum.md#本地构建与测试)、[双通道发布](./standard-plugins/spectrum.md#双通道发布-需明确发布授权) |

本地构建会写输出，也可能触发上述宿主复制；发布命令还会上传。运行对应发布命令前需明确发布对象和授权。文件齐全、复制成功或发布成功都不等于新进程已正确装载，运行发现和依赖诊断见[插件装载契约](../../02-developer-guide/plugin-development/overview.md)。

## 按问题进入验证主题

| 问题 | 对应检查与完成条件 |
| --- | --- |
| 图卡像素、颜色、模板或投影不符合预期 | [Pattern 图卡与配置](./standard-plugins/pattern.md#图卡入口与生成)、[图片投影](./standard-plugins/pattern.md#图片投影)及[验证范围](./standard-plugins/pattern.md#验证范围)；区分生成像素、多屏 DPI 和实际光学效果 |
| Conoscope 采集后没有图，或分析点不匹配 | [采集完成](./standard-plugins/conoscope.md#采集完成、文件发现与打开不是一个信号)、[关注点与分析](./standard-plugins/conoscope.md#关注点模板与分析快照)；文件出现、通道就绪与分析对齐各有判据 |
| Spectrum 已连接但不能测量，或远程结果不完整 | [设备与标定](./standard-plugins/spectrum.md#设备、标定和测量)、[模块验收](./standard-plugins/spectrum.md#验收)、[Socket 指令](./standard-plugins/spectrum-socket.md)；区分连接、标定 readiness、测量、落库与协议响应 |
| 系统监控为空，关闭窗口后仍采样，或清理未归零 | [监控排障](./standard-plugins/system-monitor.md#异常与排查)和[验证范围](./standard-plugins/system-monitor.md#验证范围)；检查数据源、状态栏配置和实际清理范围 |
| 服务安装、数据库迁移或恢复部分失败 | [安装顺序](./standard-plugins/windows-service.md#安装、数据库与配置顺序)和[恢复限制](./standard-plugins/windows-service.md#备份和恢复不等于自动回滚)；按文件、配置、数据库、服务逐层确认，不能以安装方法返回代替整体成功 |

各模块主题维护对应测试的实际覆盖范围。真机采集、校零、投影、缓存删除及服务/数据库变更需要在获准环境验证；本矩阵不声明一套覆盖所有插件的统一运行测试。

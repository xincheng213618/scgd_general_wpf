# 现有插件现场验收与交接清单

这页用于插件发版、现场替换、交接给维护人员时逐项验收。它和 [插件能力与交接矩阵](./plugin-capability-matrix.md) 的区别是：矩阵回答插件有什么能力和风险，本页回答拿到一个插件包后怎样证明它能交付、怎样记录回退边界。版本、manifest、`.cvxp` 文件名、native 文件和回退包证据按 [插件发布证据与版本核查表](./plugin-release-evidence.md) 留档。

当前只覆盖 `Plugins/` 目录里真实存在的插件：Conoscope、Spectrum、SystemMonitor、EventVWR、WindowsServicePlugin。Pattern、ImageProjector、ScreenRecorder 不在当前插件清单里，不参与当前验收。文档覆盖关系见 [当前插件文档覆盖清单](./current-plugin-coverage.md)。

## 验收分层

每个插件都按 6 层验收，不要只看到菜单出现就算通过。

| 层级 | 必查内容 | 证明方式 |
| --- | --- | --- |
| 包结构 | 插件目录、主 DLL、`manifest.json`、`README.md`、`CHANGELOG.md` | 展开 `.cvxp` 或检查主程序 `Plugins/<Name>/` |
| 版本 | `manifest.version`、`.csproj VersionPrefix`、输出 DLL `FileVersion`、`.cvxp` 文件名 | 对比 manifest、项目文件、DLL 属性和包名 |
| 宿主依赖 | `ColorVision.*.dll`、`.deps.json`、native DLL、数据文件 | 主程序根目录和插件目录文件检查 |
| 入口 | 主菜单、窗口级菜单、状态栏、设置页、Socket handler、向导 | 启动主程序后逐项打开 |
| 业务烟测 | 插件最小可用流程 | 生成结果、导出文件、状态栏刷新、Socket 返回或服务状态可读 |
| 回退 | 上一版包、插件目录备份、宿主 DLL 版本、外部配置恢复 | 交接记录中写清恢复步骤 |

## 统一准备

验收前先准备这些信息：

| 项 | 要记录什么 |
| --- | --- |
| 主程序版本 | 主程序构建配置、目标框架、输出目录 |
| 插件来源 | `.cvxp` 文件、插件目录或构建产物路径 |
| 构建命令 | `dotnet build Plugins/<Name>/<Name>.csproj -c Release -p:Platform=x64` |
| 打包命令 | `Scripts\package_plugin.bat <Name> --no-upload` |
| 插件目录 | `ColorVision/bin/x64/<Config>/net10.0-windows/Plugins/<Name>/` |
| 运行权限 | 普通用户或管理员模式 |
| 外设环境 | 光谱仪、MVS 相机、MySQL、MQTT、Windows 服务、许可证是否可用 |

快速检查插件目录：

```powershell
$name = "Spectrum"
$root = "ColorVision/bin/x64/Release/net10.0-windows"
$plugin = Join-Path $root "Plugins/$name"
Get-ChildItem $plugin
Get-Content (Join-Path $plugin "manifest.json")
Get-ChildItem $plugin -Filter "*.deps.json"
```

## 当前插件验收总表

| 插件 | 最小入口验收 | 最小业务验收 | 外部边界 | 必须记录的回退点 |
| --- | --- | --- | --- | --- |
| Conoscope | Tool -> `VAM`，ImageEditor 右键 `OpenByConoscope` | 打开一张图、添加关注点、计算色域或对比度、导出 CSV | MVS 相机、`MvCameraControl.dll`、图像资源 | 关注点/参考轴配置、导出字段、上一版插件目录 |
| Spectrum | Tool -> Spectrum，窗口菜单，状态栏，Socket handler | 无设备状态查询；有设备时连接、校零、测量、落库、导出；Socket `SpectrumStatus` | 光谱仪 native DLL、串口、SMU/Shutter/CFW、许可证、SQLite | 许可证目录、标定分组、SQLite 结果库、上一版 native DLL |
| SystemMonitor | Tool -> 性能监控，设置页，状态栏项 | CPU/RAM/磁盘/网络/进程刷新，状态栏开关生效，缓存统计不报错 | Windows 性能计数器、CUDA 信息、磁盘/日志目录权限 | 设置开关、缓存清理范围、状态栏显示配置 |
| EventVWR | Help -> 事件窗口，Help -> Dump 子菜单 | 读取 Application Error，切换 DumpType，保存当前进程 Dump | HKLM LocalDumps 注册表、WER、管理员权限 | 原注册表项、DumpFolder、DumpType、DumpCount |
| WindowsServicePlugin | Help -> 服务管理器，安装向导入口 | 刷新服务状态、读取 BaseLocation、打开 CFG；测试环境执行完整安装 | Windows 服务、MySQL、MQTT、服务包 ZIP、管理员权限 | 服务目录备份、MySQL/MQTT 配置、服务状态、上一版服务包 |

## Conoscope 验收

### 入口和版本

| 项 | 检查方式 |
| --- | --- |
| manifest | `Id=Conoscope`，`version=1.4.6.1`，`dllpath=Conoscope.dll` |
| DLL 版本 | `.csproj VersionPrefix=1.4.6.9`，交付时要和实际 DLL 文件版本一起记录 |
| 主菜单 | Tool 菜单下 `VAM` 入口，来自 `MenuConoscopeWindow` |
| ImageEditor 右键 | `ConoscopeImageViewContextMenu` 提供 `OpenByConoscope` |
| 窗口级入口 | `ConoscopeWindow` Ribbon、首页当前视图快捷区、MVS View 菜单 |

### 最小业务烟测

1. 打开 Tool -> `VAM`。
2. 导入一张可用的 CVCIE 或普通测试图。
3. 新建或拖动一个关注点圆，确认 overlay 跟随缩放和拖动。
4. 切换显示通道和参考图形模式。
5. 执行一次预处理，例如滤波、伪彩或 XYZ clamp。
6. 按 R/G/B 记录关注点并计算综合色域，或按白/黑记录并计算对比度。
7. 打开结果窗口并导出 CSV。

通过标准：

| 项 | 标准 |
| --- | --- |
| 视图 | 多标签视图、当前视图快捷区、参考轴和关注点显示正常 |
| 分析 | `ColorGamutResultWindow` 或 `ContrastResultWindow` 能显示关注点结果 |
| 导出 | CSV 生成，字段和关注点数量与当前结果一致 |
| MVS | 如果现场有相机，`MVSViewWindow` 能打开并显示相机链路；没有相机时记录未验证 |

### 失败分流

| 现象 | 先查 |
| --- | --- |
| Tool 菜单没有 `VAM` | 插件是否加载、`manifest.json`、`MenuConoscopeWindow` 是否被扫描 |
| 图片打不开 | 文件格式、`ColorVision.ImageEditor`、CVCIE/CVRAW 支持链 |
| 关注点不见或坐标错 | `ConoscopeView.FocusPoint.cs`、缩放状态、当前活动 View |
| 色域/对比度结果为空 | R/G/B 或白/黑是否都已记录、关注点快照是否完整 |
| MVS 相机不可用 | MVS SDK、`MvCameraControl.dll`、相机驱动、相机占用 |

## Spectrum 验收

### 入口和版本

| 项 | 检查方式 |
| --- | --- |
| manifest | `Id=Spectrum`，`version=1.0`，`dllpath=Spectrum.dll`，`requires=1.3.15.8` |
| DLL 版本 | `.csproj VersionPrefix=2.3.3.1`，交付时不要只记录 manifest 版本 |
| 主菜单 | Tool 菜单下 Spectrum 入口，来自 `MenuSpectrumWindow` |
| 窗口菜单 | `LoadMenuForWindow("Spectrum", menu)` 下的帮助、布局、许可证、原生日志等 |
| 状态栏 | `SpectrumStatusBarProvider` 显示连接、SN、标定组和运行状态 |
| Socket | `Plugins/Spectrum/Socket/` 下 5 个 `ISocketJsonHandler` |

### 最小业务烟测

无设备也要完成基础验收：

1. 打开 Spectrum 窗口。
2. 确认帮助窗口、许可证窗口、布局菜单、原生日志面板能打开。
3. 打开状态查询或无设备状态路径，确认窗口不会崩溃。
4. 启用 SocketProtocol 后发送 `SpectrumStatus`，确认能返回插件状态或明确的未连接错误。

有设备时继续：

1. 同步许可证，确认 `LicenseDatabase.Instance.SyncToLocal()` 所需目录就绪。
2. 连接光谱仪，确认设备 SN 和状态栏更新。
3. 选择或创建标定分组。
4. 执行暗校正或自适应积分。
5. 执行一次测量，确认曲线、CIE、结果列表刷新。
6. 导出测量结果，确认 SQLite 和导出文件一致。
7. 通过 Socket 执行连接、暗校正、测量和状态查询。

通过标准：

| 项 | 标准 |
| --- | --- |
| 设备 | 连接日志、SN、标定组和状态栏一致 |
| 结果 | `Spectrum.db` 或结果库中有新记录，曲线和导出文件对应 |
| Socket | JSON 模式下 handler 能返回 Code/Msg，设备未就绪时返回明确错误 |
| 资源 | `Magiude.dat`、`WavaLength.dat`、CIE 图片和 native DLL 在运行目录可用 |

### 失败分流

| 现象 | 先查 |
| --- | --- |
| 插件加载失败 | `.deps.json`、主程序根目录 `ColorVision.*.dll` 版本 |
| 设备连接失败 | 光谱仪 native DLL、驱动、串口、许可证、设备占用 |
| Socket 无返回 | SocketProtocol 是否启用、端口、JSON 模式、Spectrum 窗口和设备状态 |
| 测量无数据 | 标定组、积分时间、暗校正状态、ViewResultManager 配置 |
| CIE 或曲线资源缺失 | CIE 图片、数据文件、`CopyToOutputDirectory` |

## SystemMonitor 验收

### 入口和版本

| 项 | 检查方式 |
| --- | --- |
| manifest | `Id=SystemMonitor`，`version=1.0.1`，`requires=1.3.12.23` |
| DLL 版本 | `.csproj VersionPrefix=1.4.3.3` |
| 工具菜单 | Tool -> 性能监控，来自 `SystemMonitorProvider` |
| 设置页 | `SystemMonitorProvider` 同时实现 `IConfigSettingProvider` |
| 状态栏 | `SystemMonitorIStatusBarProvider` 动态输出时间、运行时长、CPU、RAM、磁盘 |

### 最小业务烟测

1. 打开性能监控窗口。
2. 确认 CPU/RAM、磁盘、网络、进程、运行时信息能刷新。
3. 打开设置页，切换 `IsShowTime`、`IsShowRAM`、`IsShowCPU`、`IsShowUptime`、`IsShowDisk`。
4. 确认主程序状态栏项能动态增删。
5. 执行刷新磁盘、刷新网络、刷新进程命令。
6. 执行缓存统计，清理缓存前确认目标目录。

通过标准：

| 项 | 标准 |
| --- | --- |
| 控件 | `SystemMonitorControl` 打开后不会因某个计数器失败整体崩溃 |
| 状态栏 | 配置变化后 `StatusBarItemsChanged` 生效 |
| 性能 | 更新间隔符合 `UpdateSpeed`，不明显拖慢主程序 |
| 权限 | 缓存清理只处理预期应用数据和日志目录 |

### 失败分流

| 现象 | 先查 |
| --- | --- |
| CPU/RAM 不刷新 | Windows 性能计数器初始化失败，当前实现应降级 |
| 状态栏不变化 | 配置开关、`IStatusBarProviderUpdatable`、状态栏刷新 |
| GPU 信息缺失 | CUDA 环境和 `ConfigCuda.Instance` |
| 清理缓存失败 | 目标目录权限、文件占用 |

## EventVWR 验收

### 入口和版本

| 项 | 检查方式 |
| --- | --- |
| manifest | `Id=EventVWR`，`version=1.0`，`requires=1.3.15.10` |
| DLL 版本 | `.csproj VersionPrefix=1.1.8.1` |
| 事件窗口 | Help -> `EventWindow`，来自 `ExportEventWindow` |
| Dump 菜单 | Help -> `MenuDump` 以及 DumpType、清空、保存子菜单 |
| 权限 | 事件窗口入口和 Dump 写入都按管理员权限处理 |

### 最小业务烟测

1. 以管理员模式启动主程序。
2. 打开 Help -> 事件窗口。
3. 确认能读取 Windows Application 日志中的 Error 项。
4. 选择一条错误，确认详情区域显示 Message。
5. 打开 Dump 子菜单，切换 DumpType。
6. 设置 DumpFolder、DumpCount、DumpType 后保存。
7. 保存当前进程 Dump，确认文件生成。
8. 清空 Dump 配置，确认注册表项符合预期。

通过标准：

| 项 | 标准 |
| --- | --- |
| 事件 | EventLog 读取失败有明确提示，不误写成插件未加载 |
| 注册表 | `HKLM\SOFTWARE\Microsoft\Windows\Windows Error Reporting\LocalDumps` 写入和清理可确认 |
| Dump | Dump 文件在目标目录生成，名称和时间可追踪 |
| 权限 | 普通权限失败被记录为权限限制，不作为功能通过 |

### 失败分流

| 现象 | 先查 |
| --- | --- |
| 菜单被禁用或打不开 | `RequiresPermission(PermissionMode.Administrator)`、主程序权限模式 |
| 事件为空 | Windows Application 日志是否存在 Error 项、EventLog 访问权限 |
| Dump 保存失败 | DumpFolder 权限、HKLM 写权限、目标文件占用 |
| 清理后仍生效 | 注册表默认 LocalDumps 与进程级 LocalDumps 是否混淆 |

## WindowsServicePlugin 验收

### 入口和版本

| 项 | 检查方式 |
| --- | --- |
| manifest | `Id=WindowsServicePlugin`，`version=1.0`，`requires=1.3.12.34` |
| DLL 版本 | `.csproj VersionPrefix=1.4.3.17` |
| Help 菜单 | Help -> 服务管理器，来自 `MenuServiceManager` |
| 向导 | `InstallServiceManager` 作为向导步骤入口 |
| 权限 | 安装、注册、启动停止服务、MySQL/MQTT 操作都按管理员模式验收 |

### 最小业务烟测

只读验收：

1. 以管理员模式启动主程序。
2. 打开 Help -> 服务管理器。
3. 刷新 `RegistrationCenterService`、`CVMainService_x64`、`CVMainService_dev` 状态。
4. 确认 `BaseLocation` 可读。
5. 打开服务目录和 `cfg/*.config`。
6. 刷新 MySQL 和 MQTT 状态。

测试环境完整安装验收：

1. 准备完整 `CVWindowsService` ZIP，不使用增量包。
2. 选择服务根目录，例如 `D:\CVService`。
3. 按需选择 MySQL ZIP 和 MQTT 安装程序。
4. 执行安装，确认安装前会停止受管服务。
5. 确认服务包解压、`CommonDll` 复制、旧服务注销、新服务注册。
6. 确认 MySQL/MQTT/WinService 配置同步成功。
7. 可选执行 `SQL/color_vision_all.sql`。
8. 启动服务并刷新状态。

通过标准：

| 项 | 标准 |
| --- | --- |
| 状态 | 服务安装状态、运行状态、BaseLocation 和窗口显示一致 |
| 配置 | `cfg/MySql.config`、`cfg/MQTT.config`、`cfg/WinService.config` 同步成功 |
| 安装 | 完整服务包能注册服务，失败时不会带旧配置继续启动 |
| 数据库 | MySQL 用户、端口、数据库和 SQL 导入记录清楚 |

### 失败分流

| 现象 | 先查 |
| --- | --- |
| 服务状态读不到 | 是否管理员、服务名是否存在、Windows 服务管理器 |
| 安装包校验失败 | ZIP 根目录结构、`RegWindowsService`、`CVMainWindowsService_x64/dev` |
| MySQL 安装失败 | ZIP 路径、端口占用、数据目录权限、SQL 编码 |
| MQTT 启动失败 | 安装程序路径、端口、服务/进程状态 |
| 配置同步失败 | `BaseLocation`、`cfg` 目录、文件权限、窗口日志 |

## 统一回退要求

插件交接时必须能回答回退到哪里。建议每次交付保留以下材料：

| 材料 | 说明 |
| --- | --- |
| 上一版 `.cvxp` | 能通过插件市场或手工安装回退 |
| 旧插件目录备份 | 直接回退 DLL、manifest、README、CHANGELOG 和 native 文件 |
| 主程序 DLL 版本表 | 避免插件回退后仍被新版/旧版 `ColorVision.*.dll` 卡住 |
| 外部配置备份 | 许可证、标定分组、注册表、服务目录、MySQL/MQTT 配置 |
| 操作日志 | 记录谁在什么机器、什么权限、执行了哪些安装或服务操作 |

回退不是简单替换插件 DLL。Spectrum 和 Conoscope 可能受 native DLL、许可证或外设驱动影响；EventVWR 和 WindowsServicePlugin 还涉及注册表、服务和本机目录。

## 插件现场交接记录模板

| 项 | 内容 |
| --- | --- |
| 插件名称 |  |
| 交付包 | `.cvxp` 文件名、生成时间、来源路径 |
| 版本 | `manifest.version`、DLL `FileVersion`、`.csproj VersionPrefix` |
| 主程序 | 主程序版本、输出目录、`ColorVision.*.dll` 版本是否满足 |
| 权限 | 普通用户/管理员，是否需要重启为管理员 |
| 入口验收 | 主菜单、窗口菜单、状态栏、设置页、Socket/向导 |
| 业务烟测 | 按本页对应插件最小流程填写通过/失败 |
| 外部依赖 | 设备、驱动、许可证、数据库、注册表、服务、native DLL |
| 生成数据 | CSV、SQLite 记录、Dump 文件、服务配置、日志路径 |
| 未验证项 | 没有设备、没有管理员、没有测试服务包等原因 |
| 回退材料 | 上一版包、目录备份、配置备份、服务备份 |
| 负责人 | 开发、测试、现场、客户确认人 |

## 继续阅读

- [插件能力与交接矩阵](./plugin-capability-matrix.md)
- [插件运行与交接场景手册](./plugin-handoff-playbook.md)
- [Conoscope 插件](./standard-plugins/conoscope.md)
- [Spectrum 插件](./standard-plugins/spectrum.md)
- [SystemMonitor 插件](./standard-plugins/system-monitor.md)
- [EventVWR 插件](./standard-plugins/eventvwr.md)
- [WindowsServicePlugin 插件](./standard-plugins/windows-service.md)

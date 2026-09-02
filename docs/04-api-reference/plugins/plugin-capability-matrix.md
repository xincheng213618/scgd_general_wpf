---
knowledge_id: "plugins.capabilities"
knowledge_type: "reference"
status: "current"
summary: "横向定位现存插件的菜单、状态、数据库、设备与管理员权限边界。"
aliases: ["哪个插件负责系统监控","哪些插件会操作设备或服务","Conoscope","Spectrum","SystemMonitor","WindowsServicePlugin"]
code_paths: ["Plugins/Conoscope/","Plugins/Spectrum/","Plugins/SystemMonitor/","Plugins/WindowsServicePlugin/"]
test_paths: ["Test/Conoscope.Tests/Conoscope.Tests.csproj","Test/Spectrum.Tests/Spectrum.Tests.csproj","Test/ColorVision.UI.Tests/SystemMonitorLifecycleTests.cs"]
related: ["plugins.index","plugins.model","plugins.getting-started","plugins.conoscope","plugins.spectrum","plugins.system-monitor","plugins.windows-service"]
---

# 插件依赖与接入矩阵

本页比较当前 `Plugins/` 的接入点、依赖和外部边界。先用下表确定模块，再点击插件名读取其完整业务契约；按菜单、状态栏、设置、Socket 或管理员权限查找时，使用后续矩阵。

程序集装载、依赖检查和产物安装问题统一见[插件装载与扩展发现](../../02-developer-guide/plugin-development/overview.md)及[插件产物与交付](../../02-developer-guide/plugin-development/getting-started.md)。本页不把 DLL 存在、端口连通或打包成功当作运行验证。

## 当前源码插件总表

| 插件 | 源码目录 | 版本事实源 | 宿主入口 | 主要能力 | 关键风险 |
| --- | --- | --- | --- | --- | --- |
| [Conoscope](./standard-plugins/conoscope.md) | `Plugins/Conoscope/` | DLL `FileVersion` / [csproj](https://github.com/xincheng213618/scgd_general_wpf/blob/master/Plugins/Conoscope/Conoscope.csproj)；[manifest](https://github.com/xincheng213618/scgd_general_wpf/blob/master/Plugins/Conoscope/manifest.json) 为同步副本 | Tool 菜单 `VAM`，ImageEditor 右键打开 | 锥镜/VAM 图像观察、关注点、参考轴、预处理、色域和对比度分析、MVS 观察相机 | MVS 依赖海康 `MvCameraControl.dll`；关注点逻辑是插件本地实现 |
| [Spectrum](./standard-plugins/spectrum.md) | `Plugins/Spectrum/` | DLL `FileVersion` / [csproj](https://github.com/xincheng213618/scgd_general_wpf/blob/master/Plugins/Spectrum/Spectrum.csproj)；[manifest](https://github.com/xincheng213618/scgd_general_wpf/blob/master/Plugins/Spectrum/manifest.json) 为同步副本 | Tool 菜单光谱窗口，Spectrum 窗口级菜单/状态栏，Socket JSON 指令 | 光谱仪连接、标定分组、测量、EQE、CIE、SQLite 结果、许可证、Socket 远程控制 | 依赖光谱仪 native DLL、OpenCV、串口、许可证；连接状态与标定可测量状态必须分开判断 |
| [SystemMonitor](./standard-plugins/system-monitor.md) | `Plugins/SystemMonitor/` | [manifest](https://github.com/xincheng213618/scgd_general_wpf/blob/master/Plugins/SystemMonitor/manifest.json) / [csproj](https://github.com/xincheng213618/scgd_general_wpf/blob/master/Plugins/SystemMonitor/SystemMonitor.csproj) | Tool 菜单，设置页，主程序状态栏 | CPU/RAM/磁盘/网络/进程/GPU/缓存监控和状态栏投影 | 性能计数器可能初始化失败并降级；监控单例位于 `ColorVision.UI.Configs` 命名空间 |
| [WindowsServicePlugin](./standard-plugins/windows-service.md) | `Plugins/WindowsServicePlugin/` | [manifest](https://github.com/xincheng213618/scgd_general_wpf/blob/master/Plugins/WindowsServicePlugin/manifest.json) / [csproj](https://github.com/xincheng213618/scgd_general_wpf/blob/master/Plugins/WindowsServicePlugin/WindowsServicePlugin.csproj) | 管理员应用提供器、安装向导；另有旧 `InstallTool` 菜单类型和主窗初始化器 | 本机服务管理、在线选包、完整包安装与数据库迁移；旧工具链另行存在 | 会改服务、数据库、进程和文件；新完整安装链不支持增量，下载与完成边界见[插件主题](./standard-plugins/windows-service.md) |

表中的版本来源用于定位，不代表已经验证交付一致性。DLL `FileVersion`、包名、manifest 同步与安装门禁见[插件产物与交付](../../02-developer-guide/plugin-development/getting-started.md)；启动装载的实际依赖检查见[装载与扩展发现](../../02-developer-guide/plugin-development/overview.md)。不能从 manifest 的 `requires` 字段推断启动加载器已经执行该检查。

## 入口和扩展点矩阵

| 插件 | 主菜单入口 | 窗口级菜单 | 状态栏 | 设置页 | Socket | 其他扩展点 |
| --- | --- | --- | --- | --- | --- | --- |
| Conoscope | `MenuConoscopeWindow` -> Tool / `VAM` | `ConoscopeWindow` Ribbon、View 菜单、`MenuMVSVideo` | 每标签页 `ConoscopeViewState` + `ConoscopeDocument`；全局 `ConoscopeConfig` / ReferenceStore | `ConoscopeConfigWindow`（含预处理页） | 无 | `ConoscopeImageViewContextMenu` 接入 ImageEditor 右键菜单 |
| Spectrum | `MenuSpectrumWindow` -> Tool | `LoadMenuForWindow("Spectrum", menu)`，包含帮助、布局、许可证、原生日志等菜单 | `SpectrumStatusBarProvider`，目标窗口 `Spectrum` | 多个 `ConfigService` 配置对象 | 5 个 `ISocketJsonHandler` | Quartz 任务、SQLite 结果、许可证同步 |
| SystemMonitor | `SystemMonitorProvider` -> Tool | 无独立复杂菜单 | `SystemMonitorIStatusBarProvider` | `IConfigSettingProvider` | 无 | `SystemMonitorControl` 同时用于设置页和窗口 |
| WindowsServicePlugin | `ServiceManagerAppProvider` -> 应用与工具 / 内部工具（管理员）；旧 `InstallTool` 声明 `OwnerGuid=ServiceLog` | 服务管理窗口内部命令 | 无 | `ServiceManagerConfig`、`MySqlServiceConfig`、`MqttServiceConfig`、`CVWinSMSConfig` | 无 | `InstallServiceManager` 向导入口；`InstallTool : IMainWindowInitialized` |

## 外部依赖和运行时边界

| 插件 | 外部设备/服务 | 文件和数据库 | 系统权限 | 现场排查先看 |
| --- | --- | --- | --- | --- |
| Conoscope | MVS 观察相机、`MvCameraControl.dll` | 关注点/参考轴/预处理配置，CSV 导出 | 普通图像分析通常不需要管理员；相机驱动由系统环境决定 | MVS SDK 是否安装、图像是否可打开、关注点是否记录、导出文件是否生成 |
| Spectrum | SP100/SP10/高利通光谱仪、Shutter、CFW、SMU、串口、native 光谱仪 DLL | `%APPDATA%\Spectromer\Config\Spectrum.db`、标定分组、许可证目录；CIE 图片来自宿主共享 ImageEditor 资源 | 通常不需要管理员；设备驱动和许可证要就绪 | 设备连接、标定 readiness、共享 native 会话、Socket 服务、SQLite 结果库 |
| SystemMonitor | Windows 性能计数器、CUDA 信息、网络接口 | 应用数据/日志的递归统计与顶层文件清理，详见[统计和清理范围](./standard-plugins/system-monitor.md#缓存大小与清理范围) | 删除取决于目标目录权限，逐文件失败会被忽略 | 性能计数器整组初始化、配置开关、状态栏 provider 是否刷新 |
| WindowsServicePlugin | Windows 服务、MySQL、MQTT、服务包 ZIP | `BaseLocation`、`cfg/*.config`、MySQL ZIP、MQTT installer、服务数据库 SQL、备份目录 | 大部分操作需要管理员模式 | 服务状态、BaseLocation、安装包结构、MySQL/MQTT 状态、CFG 同步日志 |

## 本地构建与授权发布矩阵

“构建”列只编译并可能按 HostCopy 条件同步本地产物；“发布上传”列会更新远端包并按脚本规则清理本地包，仅在用户明确要求发布该插件时运行。普通 wrapper 不支持 `--no-upload`，不可作为只读或本地-only 校验步骤。

| 插件 | 构建命令 | 发布上传命令（需授权） | 宿主镜像（条件） | 必带文件 |
| --- | --- | --- | --- | --- |
| Conoscope | `dotnet build Plugins/Conoscope/Conoscope.csproj -c Release -p:Platform=x64` | `Scripts\package_plugin.bat Conoscope` | solution/MSBuild 且 `SolutionDir` 有效时复制到宿主 `Plugins/Conoscope/` | `Conoscope.dll`、manifest、README、CHANGELOG；使用观察相机时再带 MVS/native 依赖 |
| Spectrum | `dotnet build Plugins/Spectrum/Spectrum.csproj -c Release -p:Platform=x64` | 正式发布：`Scripts\Spectrum.bat --release-notes "..."` | 无 HostCopy；专用脚本从项目输出打包 | `Spectrum.dll`、manifest、README、CHANGELOG、`Magiude.dat`、`WavaLength.dat`、光谱仪 native DLL；另生成独立 ZIP |
| SystemMonitor | `dotnet build Plugins/SystemMonitor/SystemMonitor.csproj -c Release -p:Platform=x64` | `Scripts\package_plugin.bat SystemMonitor` | solution/MSBuild 且 `SolutionDir` 有效时复制到宿主 `Plugins/SystemMonitor/` | `SystemMonitor.dll`、manifest、README、CHANGELOG |
| WindowsServicePlugin | `dotnet build Plugins/WindowsServicePlugin/WindowsServicePlugin.csproj -c Release -p:Platform=x64` | `Scripts\package_plugin.bat WindowsServicePlugin` | solution/MSBuild 且 `SolutionDir` 有效时复制到宿主 `Plugins/WindowsServicePlugin/` | `WindowsServicePlugin.dll`、manifest、README、CHANGELOG |

通用打包、HostCopy 条件、共享依赖剔除和安装替换统一维护在[插件产物契约](../../02-developer-guide/plugin-development/getting-started.md)。本表只保留各插件的差异，不复制另一份打包流程；具体文件是否必需还要按启用能力和对应模块页核验。

## 发布后烟测矩阵

| 插件 | 最小烟测 | 通过标准 |
| --- | --- | --- |
| Conoscope | 打开 Tool -> VAM；导入大 CVCIE；观察 Y 首屏与 XYZ 后台就绪；切通道；新增/移动关注点；执行色域或对比度分析；导出 CSV | readiness 与分阶段加载一致；新文档清关注点、同文档换通道保留关注点；结果窗口和 CSV 正常 |
| Spectrum | 打开 Spectrum；检查状态栏；执行无设备状态查询；有设备时连接、确认标定就绪、校零、测量、导出；启用 Socket 后发送 `SpectrumStatus`/`SpectrumConnect` | 状态栏区分连接和标定 readiness；测量结果与画像同事务落库；Socket 返回正确 Code/Msg 和连接标定状态 |
| SystemMonitor | 打开“系统监控”；切换状态栏开关；刷新磁盘/网络/进程；清理另按授权范围验证 | 核对采样来源、状态栏增删和停止条件；各数据源的失败边界见[监控排障](./standard-plugins/system-monitor.md#异常与排查) |
| WindowsServicePlugin | 以管理员模式打开服务管理器；刷新服务状态；选择测试服务根目录；验证配置文件打开；在测试环境执行安装流程 | 服务状态可读，配置同步日志明确，失败时不会带旧配置继续启动 |

## 维护风险清单

| 风险 | 影响 | 处理方式 |
| --- | --- | --- |
| manifest 版本和 DLL 文件版本不一致 | 插件管理器、市场包、现场 DLL 版本互相对不上 | 发版前同时核对 `manifest.json`、`.csproj VersionPrefix`、输出 DLL `FileVersion`、`.cvxp` 文件名 |
| PostBuild 复制 README/CHANGELOG 大小写不一致 | 构建成功但插件目录缺帮助文件 | 检查项目文件里的 `README.md`、`CHANGELOG.md` 与复制脚本大小写 |
| 插件依赖宿主共享 DLL | 单独复制插件 DLL 后运行失败 | 打包时使用 `shared_files.json`，现场检查主程序目录 `ColorVision.*.dll` 版本 |
| native DLL 未进入插件包 | Spectrum 或 Conoscope 设备链路运行时失败 | 抽检 `.cvxp` 内容，确认光谱仪 DLL、MVS DLL、OpenCV runtime 是否在正确位置 |
| 需要管理员的插件按普通权限测试 | WindowsServicePlugin 操作失败 | 文档和烟测明确标注管理员模式，不把权限失败当成功能缺陷 |
| Socket handler 已编译但服务未启用 | 外部客户端连不上 Spectrum 指令 | 检查 `ColorVision.SocketProtocol` 配置、端口、协议模式、插件程序集是否加载 |

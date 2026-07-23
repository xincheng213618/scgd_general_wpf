# 插件横向速查

本页只给维护人员做横向排查，不作为普通读者的入口。第一次找插件请先看 [现有插件能力说明](./README.md)，再进入具体插件页；只有需要比较菜单、状态栏、设置页、Socket、数据库、注册表、管理员权限或 native 依赖时，再回到本页。

本页按“日常维护、现场排查、发版验收”的视角整理当前 `Plugins/` 下真实存在的插件。单插件的业务细节仍放在各自页面；这里重点回答：

- 插件从哪里进入宿主。
- 它依赖哪些 UI/Engine 模块。
- 它有没有设备、Socket、数据库、注册表、Windows 服务等外部边界。
- 发布时应该验收什么。
- 哪些地方最容易被误判。

如果你已经遇到具体问题，例如插件没有加载、菜单没出现、`.deps.json` 版本不满足、`.cvxp` 包缺文件、管理员权限或 Socket 指令异常，先回到 [现有插件能力说明](./README.md) 找对应插件页。发版或现场替换时，按本文的构建、打包和烟测矩阵核对，并保存 manifest、DLL 版本、`.cvxp` 和回退材料。

## 当前源码插件总表

| 插件 | 源码目录 | manifest 版本 | `.csproj` 版本 | 宿主入口 | 主要能力 | 关键风险 |
| --- | --- | --- | --- | --- | --- | --- |
| Conoscope | `Plugins/Conoscope/` | `1.4.6.1` | `1.4.6.9` | Tool 菜单 `VAM`，ImageEditor 右键打开 | 锥镜/VAM 图像观察、关注点、参考轴、预处理、色域和对比度分析、MVS 观察相机 | manifest 版本与程序集版本不同；MVS 依赖海康 `MvCameraControl.dll`；关注点逻辑是插件本地实现 |
| Spectrum | `Plugins/Spectrum/` | `1.0` | `2.3.3.1` | Tool 菜单光谱窗口，Spectrum 窗口级菜单/状态栏，Socket JSON 指令 | 光谱仪连接、标定分组、测量、EQE、CIE、SQLite 结果、许可证、Socket 远程控制 | manifest 版本与程序集版本不同；依赖光谱仪 native DLL、OpenCV、串口、许可证；Socket 指令需要窗口和设备状态配合 |
| SystemMonitor | `Plugins/SystemMonitor/` | `1.0.1` | `1.4.3.3` | Tool 菜单，设置页，主程序状态栏 | CPU/RAM/磁盘/网络/进程/GPU/缓存监控和状态栏投影 | 性能计数器可能初始化失败并降级；监控单例位于 `ColorVision.UI.Configs` 命名空间 |
| WindowsServicePlugin | `Plugins/WindowsServicePlugin/` | `1.0` | `1.4.3.17` | Help 菜单服务管理器，向导入口 | CVWindowsService 安装/注册/启动停止、MySQL/MQTT 安装配置、服务目录和配置同步 | 会改 Windows 服务、MySQL、MQTT 和本机文件；需要管理员权限；不支持增量服务包 |

`manifest.version` 和 `.csproj VersionPrefix` 当前并不总是相同。交付时要同时确认：

- 插件管理器或市场展示用的 manifest 版本。
- DLL 的文件版本和程序集版本。
- `.cvxp` 文件名中由 `Scripts/package_cvxp.py` 读取到的 DLL `FileVersion`。
- `CHANGELOG.md` 是否对应这次实际 DLL。

## 入口和扩展点矩阵

| 插件 | 主菜单入口 | 窗口级菜单 | 状态栏 | 设置页 | Socket | 其他扩展点 |
| --- | --- | --- | --- | --- | --- | --- |
| Conoscope | `MenuConoscopeWindow` -> Tool / `VAM` | `ConoscopeWindow` Ribbon、View 菜单、`MenuMVSVideo` | 插件窗口内部状态 | `ConoscopeConfig` 和预处理设置窗口 | 无 | `ConoscopeImageViewContextMenu` 接入 ImageEditor 右键菜单 |
| Spectrum | `MenuSpectrumWindow` -> Tool | `LoadMenuForWindow("Spectrum", menu)`，包含帮助、布局、许可证、原生日志等菜单 | `SpectrumStatusBarProvider`，目标窗口 `Spectrum` | 多个 `ConfigService` 配置对象 | 5 个 `ISocketJsonHandler` | Quartz 任务、SQLite 结果、许可证同步 |
| SystemMonitor | `SystemMonitorProvider` -> Tool | 无独立复杂菜单 | `SystemMonitorIStatusBarProvider` | `IConfigSettingProvider` | 无 | `SystemMonitorControl` 同时用于设置页和窗口 |
| WindowsServicePlugin | `MenuServiceManager` -> Help | 服务管理窗口内部命令 | 无 | `ServiceManagerConfig`、`MySqlServiceConfig`、`MqttServiceConfig` | 无 | `InstallServiceManager` 作为向导步骤入口 |

## 外部依赖和运行时边界

| 插件 | 外部设备/服务 | 文件和数据库 | 系统权限 | 现场排查先看 |
| --- | --- | --- | --- | --- |
| Conoscope | MVS 观察相机、`MvCameraControl.dll` | 关注点/参考轴/预处理配置，CSV 导出 | 普通图像分析通常不需要管理员；相机驱动由系统环境决定 | MVS SDK 是否安装、图像是否可打开、关注点是否记录、导出文件是否生成 |
| Spectrum | SP100/SP10 光谱仪、Shutter、CFW、SMU、串口、native 光谱仪 DLL | `AppData\Spectromer\Config\Spectrum.db`、标定分组、许可证目录、CIE 图片资源 | 通常不需要管理员；设备驱动和许可证要就绪 | 设备连接日志、许可证同步、标定分组、Socket 服务、SQLite 结果库 |
| SystemMonitor | Windows 性能计数器、CUDA 信息、网络接口 | 应用缓存和日志目录统计 | 清理缓存取决于目标目录权限 | 性能计数器初始化、配置开关、状态栏 provider 是否刷新 |
| WindowsServicePlugin | Windows 服务、MySQL、MQTT、服务包 ZIP | `BaseLocation`、`cfg/*.config`、MySQL ZIP、MQTT installer、服务数据库 SQL、备份目录 | 大部分操作需要管理员模式 | 服务状态、BaseLocation、安装包结构、MySQL/MQTT 状态、CFG 同步日志 |

## 构建和打包矩阵

| 插件 | 构建命令 | 打包命令 | PostBuild 复制到 | 必带文件 |
| --- | --- | --- | --- | --- |
| Conoscope | `dotnet build Plugins/Conoscope/Conoscope.csproj -c Release -p:Platform=x64` | `Scripts\package_plugin.bat Conoscope` | `ColorVision/bin/x64/<Config>/net10.0-windows/Plugins/Conoscope/` | `Conoscope.dll`、`manifest.json`、`README.md`、`CHANGELOG.md`、MVS/native 依赖 |
| Spectrum | `dotnet build Plugins/Spectrum/Spectrum.csproj -c Release -p:Platform=x64` | `Scripts\package_plugin.bat Spectrum` 或专用 `Scripts\build_spectrum.py` | `ColorVision/bin/x64/<Config>/net10.0-windows/Plugins/Spectrum/` | `Spectrum.dll`、`manifest.json`、`README.md`、`CHANGELOG.md`、`Magiude.dat`、`WavaLength.dat`、CIE 图片、光谱仪 native DLL |
| SystemMonitor | `dotnet build Plugins/SystemMonitor/SystemMonitor.csproj -c Release -p:Platform=x64` | `Scripts\package_plugin.bat SystemMonitor` | `ColorVision/bin/x64/<Config>/net10.0-windows/Plugins/SystemMonitor/` | `SystemMonitor.dll`、`manifest.json`、`README.md`、`CHANGELOG.md` |
| WindowsServicePlugin | `dotnet build Plugins/WindowsServicePlugin/WindowsServicePlugin.csproj -c Release -p:Platform=x64` | `Scripts\package_plugin.bat WindowsServicePlugin` | `ColorVision/bin/x64/<Config>/net10.0-windows/Plugins/WindowsServicePlugin/` | `WindowsServicePlugin.dll`、`manifest.json`、`README.md`、`CHANGELOG.md` |

通用 `.cvxp` 打包逻辑在 `Scripts/package_cvxp.py`：

1. 可选执行 `dotnet build`。
2. 从插件输出目录收集文件。
3. 用 `Scripts/shared_files.json` 剔除宿主已共享文件。
4. 把插件根目录的 `README.md`、`CHANGELOG.md`、`manifest.json`、`PackageIcon.png` 放进包。
5. 读取主 DLL 的 `FileVersion`，生成 `<PluginName>-<FileVersion>.cvxp`。

因此只改 manifest 不会改变 `.cvxp` 文件名；只改 `.csproj VersionPrefix` 但不检查输出 DLL，也不能证明市场包版本正确。

## 发布后烟测矩阵

| 插件 | 最小烟测 | 通过标准 |
| --- | --- | --- |
| Conoscope | 打开 Tool -> VAM；导入一张 CVCIE 或示例图；新增/移动关注点；执行色域或对比度分析；导出 CSV | 窗口、Ribbon、当前视图快捷区、关注点 overlay、结果窗口和 CSV 均正常 |
| Spectrum | 打开 Spectrum；检查状态栏；执行无设备状态查询；有设备时连接、校零、测量、导出；启用 Socket 后发送 `SpectrumStatus` | 状态栏显示连接/SN/标定组；测量结果落库；Socket 返回正确 Code/Msg |
| SystemMonitor | 打开性能监控窗口；切换状态栏开关；刷新磁盘/网络/进程；执行清理缓存前确认目录 | 监控数据刷新，状态栏能动态增删，失败项不会拖垮整个插件 |
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

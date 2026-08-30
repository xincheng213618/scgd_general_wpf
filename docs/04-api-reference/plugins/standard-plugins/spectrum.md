---
knowledge_id: "plugins.spectrum"
knowledge_type: "topic"
status: "current"
summary: "Spectrum 的测量校正链、SQLite 结果和独立 ZIP 与 cvxp 双通道发布契约。"
aliases: ["Spectrum 如何校准和发布","光谱测量结果不一致","Spectrum","Spectrum.bat","SpectrometerManager","SpectrumMeasurementResult","ViewResultManagerConfig","ViewResultSpectrum","SpectrumMeasurementProfile"]
code_paths: ["Plugins/Spectrum/README.md","Plugins/Spectrum/Spectrum.csproj","Plugins/Spectrum/manifest.json","Plugins/Spectrum/App.xaml.cs","Plugins/Spectrum/MainWindow.xaml.cs","Plugins/Spectrum/SpectrometerManager.cs","Plugins/Spectrum/Calibration/","Plugins/Spectrum/Configs/","Plugins/Spectrum/Data/","Plugins/Spectrum/Models/ViewResultSpectrum.cs","Plugins/Spectrum/SpectrumCsvExporter.cs","Plugins/Spectrum/DirectSpectrometer/","Plugins/Spectrum/Job/","Plugins/Spectrum/License/","Plugins/Spectrum/Update/","Scripts/Spectrum.bat"]
test_paths: ["Test/Spectrum.Tests/Spectrum.Tests.csproj","Test/Spectrum.Tests/ViewResultSpectrumTests.cs","Test/Spectrum.Tests/SpectrumArchitectureBoundaryTests.cs","Scripts/tests/test_build_spectrum.py"]
related: ["plugins.index","plugins.capabilities","plugins.spectrum-socket"]
---

# Spectrum 插件

`Plugins/Spectrum/` 是光谱仪测量工作台插件。程序集版本以 `Spectrum.csproj` 为事实源，最低宿主版本读取同目录 `manifest.json`；发布脚本会校验并同步两者，不在说明页复制易漂移的版本号。

## 先查什么

| 现场问题 | 第一检查点 |
| --- | --- |
| Tool 菜单没有 Spectrum | 插件目录、`manifest.json`、`Spectrum.dll`、宿主版本要求 |
| 窗口状态栏为空 | `LoadMenuForWindow("Spectrum", ...)`、`StatusBarManager.Init(..., "Spectrum")` |
| 连接失败 | 许可证同步、USB/COM 配置、native DLL、设备占用、驱动 |
| 已连接但测量按钮不可用 | `IsCalibrationReady`、`CalibrationStatus`、配置路径与已加载文件指纹 |
| SN 为空或标定失败 | 设备序列号、当前 SN 的标定分组、`WavaLength.dat` / `Magiude.dat`，以及失败后的旧标定恢复状态 |
| 自动校零失败 | `ShutterController` 连接、严格开关确认和暗场流程；Socket/Job 必须有可用快门 |
| 测量超时或曲线不刷新 | 积分时间、同步频率模式、SDK 返回码、重试结果 |
| 结果列表有数据但数据库没有 | `ViewResultManager`、SQLite 路径、写入异常 |
| EQE 字段为 0 | SMU 配置、测量模式、EQE 回写 |
| Socket 无响应或请求结果难以判断 | [Spectrum Socket 业务契约](./spectrum-socket.md)及其公共传输层入口 |

## 运行链路

宿主按 `manifest.json` 加载 `Spectrum.dll`，`MenuSpectrumWindow` 在 Tool 菜单提供入口。`MainWindow` 是 WPF 组合点，负责生命周期、提示、结果列表和绘图；`SpectrometerManager` 是无窗口依赖的设备入口，管理光谱仪句柄、标定状态和测量流程。Shutter、滤光轮和 SMU 各自在控制器内串行化设备访问。

`IsConnected` 只表示通信已经建立。连接后按设备 SN 加载标定分组，只有配置路径、文件指纹和 native 已加载快照一致，且没有加载或持久化请求在途时，`IsCalibrationReady` 才为 `true`。测量入口会再次执行同一门禁，不能仅凭按钮状态或连接状态判断可测量。

测量按配置执行暗场、自动积分、采集和 EQE 派生，然后把结果与测量画像放进同一数据库事务。Manager 返回 `SpectrumMeasurementResult`；MainWindow 只做异步 UI 投影，历史曲线在第一次查看时延迟生成。

独立 WPF 入口是 `App.Application_Startup`，仍初始化配置、许可证和公共 Socket 模块，依赖匹配的 ColorVision 库、原生 DLL/驱动及标定资源；不是只复制一个可执行文件就能测量。独立更新在 `Update/SpectrumUpdateService.cs` 内维持自身责任边界，不应依赖主程序 ServiceHost 才能交付独立版。

`MainWindow.PrepareForShutdownCoreAsync` 停止接收新测量、取消连续测量，等待在途测量结束（包括其保存路径），随后尝试断开光谱仪并关闭辅助设备。`CloseAuxiliaryDevicesAsync` 的 `12` 秒仅限制等待 `IsBusy` 消退的轮询，超时仍忙的设备会警告并跳过强制释放；后续非忙设备的关闭及前面的在途等待不受这个计时器限制。它不是窗口关闭的总截止时间，也不保证所有设备已经安全关闭。

## 设备、标定和测量

| 环节 | 要确认 |
| --- | --- |
| 标定分组 | 当前设备 SN 能找到活动分组 |
| 标定文件 | 两个文件通过预校验，加载快照与当前配置的路径、分组和 SHA-256 一致 |
| 自动校零 | UI 手动流程允许人工遮光；Socket/Job 无人值守流程要求 Shutter 严格完成关闭和恢复 |
| EQE | SMU 已连接，电压/电流结果能写入结果对象 |

`CalibrationGroupWindow` 使用独立 working copy。保存时冻结候选配置，native 两个文件加载成功且该请求仍是当前请求后，才原子写入配置并发布为可测量状态；失败或取消不会把候选路径冒充成已加载标定。关闭窗口不会自动保存未提交改动。

主测量使用 `CM_*` API，`DirectSpectrometer/` 诊断工具使用 `SA_*` API；两者共享原生驱动并具有全会话互斥关系，不能同时连接。

## 数据和文件

| 类别 | 入口 | 说明 |
| --- | --- | --- |
| 插件元数据 | [manifest.json](https://github.com/xincheng213618/scgd_general_wpf/blob/master/Plugins/Spectrum/manifest.json) | 插件身份、DLL 路径和最低宿主要求；版本由发布脚本按 DLL 同步 |
| 程序集版本 | [Spectrum.csproj](https://github.com/xincheng213618/scgd_general_wpf/blob/master/Plugins/Spectrum/Spectrum.csproj) | `VersionPrefix` 生成发布 DLL `FileVersion`，是版本事实源 |
| 窗口 | `MainWindow.xaml(.cs)` | 生命周期、连接/测量按钮、EQE、结果列表、绘图和用户提示 |
| 设备状态 | `SpectrometerManager.cs` | 原生句柄、标定快照、设备操作门禁和一次完整测量；不创建窗口或文件对话框 |
| 标定 | `Calibration/` | 按光谱仪 SN 管理标定分组 |
| 辅助设备 | `Configs/*Controller.cs` | Shutter、滤光轮、SMU 各自的连接、命令和释放门禁 |
| 许可证 | `License/` | 许可证导入、同步和原生日志入口 |
| SQLite | `%APPDATA%\Spectromer\Config\Spectrum.db` | 本地结果库 |
| 光谱结果 | `SprectrumModel` | 光谱测量结果 |
| 测量画像 | `SpectrumMeasurementProfile` | 测量上下文和配置快照 |
| CSV | `SpectrumCsvExporter.cs` | 无 UI 的不可变快照、实际波长对齐和流式写入 |

CSV 按调用时选中结果建立不可变快照，Normal/EQE 模式使用各自固定字段；波长列取实际网格并集，先输出全部绝对值列，再输出对应 `sp` 相对值列。排查“导出为空”时先确认已选中结果及其有效 `fSpect1/fSpect2/fInterval/fPL`，不要再按旧的可见列模型排查。

保存前 `ViewResultSpectrum.NormalizeColorParam` 会规范化波长元数据并裁剪 `fPL`。有效起止波长和间隔下，点数按 `round((fSpect2 - fSpect1) / fInterval) + 1` 计算并限制在实际数组容量内；旧数据元信息无效时另有起点、间隔和最多 `4001` 点的初始回退规则，不能一律保存 native 数组全部容量。历史明细和曲线按需生成；`ViewResultManagerConfig.Count` 只限制内存显示集合，不删除 SQLite 历史记录，且 `Count <= 0` 不做该集合裁剪。

两种耗时终点不同：结果列表的“结果就绪耗时”累计到结果构造和 EQE 计算完成；`SpectrumMeasurementProfile.TotalDurationMs` 再累计到结果行插入完成，不包含测量画像行插入、事务提交和异步 UI 投影。不要用任一数值代替请求端到端耗时。

## Socket 和调度

五个 Socket handler 复用 Manager；准确的指令名、`Params`、返回字段、设备锁和 `30/60` 秒合作式取消边界仅在 [Spectrum Socket 业务契约](./spectrum-socket.md)维护。Spectrum 没有独立于 `ColorVision.SocketProtocol` 的另一套传输服务。

调度入口在 `Job/`：`SpectrumMeasureJob` 定时执行光谱测量，`SpectrumDarkCalibrationJob` 定时执行暗场/校零。调度任务失败时先看设备、标定 readiness、快门和 Scheduler 执行历史，不要把窗口是否打开作为业务前置条件。

## 验收

| 验收项 | 通过标准 |
| --- | --- |
| 插件装载 | Tool 菜单出现 Spectrum，能打开 `MainWindow` |
| 窗口扩展 | Spectrum 窗口菜单和状态栏出现，连接、型号、SN、标定组、模式可读 |
| 交付资源 | 包含 `Spectrum.dll`、manifest、README、CHANGELOG、标定文件和 native DLL |
| 许可证 | 连接前能同步许可证，异常能打开许可证管理或原生日志 |
| 设备连接 | 已知设备能读出型号和 SN；标定损坏时仍保持连接并明确显示不可测量状态，修复后可重新加载 |
| 单次测量 | 曲线刷新，结果列表新增记录，测量画像写入数据库 |
| EQE 测量 | SMU 数据、EQE 字段和导出结果一致 |
| 数据落库 | `Spectrum.db` 在同一事务中写入结果和测量画像；重置/删除不会发布数据库中已不存在的结果 |
| 标定切换 | 快速切组、无效候选和取消不会让声明快照与 native 状态错配；失败能恢复上一组或明确锁住测量 |
| Socket | 在宿主进程仍存活、服务启用且设备前提满足时验证无 Spectrum 窗口操作；区分连接与 readiness、忙与取消，校零要求快门 |
| 调度 | 在进程及 Scheduler 仍运行且设备前提满足时验证无窗口调用和执行历史；窗口关闭导致的断开不能视为保持可测量 |
| 双通道发布 | 插件 latest、独立 latest/latest-version、签名、下载大小和 SHA-256 全部通过专用脚本验收 |

## 本地构建与测试

以下命令只写本地编译/测试产物，不发布更新源。设备连接、暗场、快门和测量烟测会影响真实设备，须另外确认现场授权。

```powershell
dotnet build .\Plugins\Spectrum\Spectrum.csproj -c Release -p:Platform=x64
dotnet test .\Test\Spectrum.Tests\Spectrum.Tests.csproj -c Release -p:Platform=x64
```

`ViewResultSpectrumTests` 覆盖有效点数、真实端点和旧元信息回退；`SpectrumArchitectureBoundaryTests` 检查 Manager 不反向引用指定窗口、对话框或同步 Application Dispatcher 的源码模式。程序集内另有标定、CSV 等测试；引用测试文件不表示测试已经通过，也不能代替原生设备、窗口关闭或 Socket 时序验证。

## 双通道发布（需明确发布授权）

Spectrum 同时维护独立 ZIP 和 ColorVision `.cvxp` 更新源。只有用户明确要求发布 Spectrum 时运行下列命令；它会更新远端两个发布源，不是本地打包或测试命令。

```powershell
.\Scripts\Spectrum.bat --release-notes "本次变更说明"
```

不要用通用 `package_plugin.bat Spectrum` 代替正式发布。专用脚本会构建两种包、签名独立清单、按顺序提交两个更新源，并验证插件 latest、独立 latest/latest-version、下载大小和 SHA-256；全部远程验收通过后才删除本地 `.cvxp`。

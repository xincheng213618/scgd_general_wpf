---
knowledge_id: "plugins.spectrum"
knowledge_type: "topic"
status: "current"
summary: "光谱仪软件 Spectrum 的连接、标定、单次测量和 CSV 导出；标定状态与测量前文件复核、EQE 输入及独立 ZIP/cvxp 发布版本来源。"
aliases: ["Spectrum 如何校准和发布","光谱测量结果不一致","Spectrum","Spectrum.bat","SpectrometerManager","SpectrumMeasurementResult","ViewResultManagerConfig","ViewResultSpectrum","SpectrumMeasurementProfile","光谱仪软件","连接光谱仪","单次测试","IsCalibrationReady","设备序列号未知，无法保存标定配置"]
code_paths: ["Plugins/Spectrum/README.md","Plugins/Spectrum/Spectrum.csproj","Plugins/Spectrum/manifest.json","Plugins/Spectrum/App.xaml.cs","Plugins/Spectrum/MainWindow.xaml.cs","Plugins/Spectrum/MainWindow.xaml","Plugins/Spectrum/Properties/Resources.resx","Plugins/Spectrum/SpectrometerManager.cs","Plugins/Spectrum/Calibration/","Plugins/Spectrum/Configs/","Plugins/Spectrum/Data/","Plugins/Spectrum/Models/ViewResultSpectrum.cs","Plugins/Spectrum/SpectrumCsvExporter.cs","Plugins/Spectrum/DirectSpectrometer/","Plugins/Spectrum/Job/","Plugins/Spectrum/License/","Plugins/Spectrum/Update/","Scripts/Spectrum.bat","Scripts/build_spectrum.py"]
test_paths: ["Test/Spectrum.Tests/Spectrum.Tests.csproj","Test/Spectrum.Tests/ViewResultSpectrumTests.cs","Test/Spectrum.Tests/SpectrumArchitectureBoundaryTests.cs","Test/Spectrum.Tests/SpectrumCalibrationStateTests.cs","Test/Spectrum.Tests/SpectrumCsvExporterTests.cs","Scripts/tests/test_build_spectrum.py"]
related: ["plugins.index","plugins.capabilities","plugins.spectrum-socket"]
---

# Spectrum 插件

Spectrum 提供光谱仪连接、标定分组、光谱测量、EQE 计算和结果导出。在 ColorVision 中从 **工具 → 光谱仪软件** 打开，也可运行完整独立包中的 `Spectrum.exe`；再次点击宿主菜单会激活已有窗口。

运行需要匹配的 Windows/x64 环境、ColorVision 公共库、原生 DLL、设备驱动、许可证和标定文件。项目与依赖以 `Spectrum.csproj`、`Plugins/Directory.Build.props` 为准，最低宿主要求见 `manifest.json`。连接、校零、快门、滤光轮、源表和测量会操作真实设备，以下步骤适用于已获授权的现场环境。

## 连接、测量与导出

1. 在 **光谱仪连接** 区域选择型号和连接方式；使用串口时配置串口与波特率，再点 **连接光谱仪**。确认型号、SN 和连接状态，连接失败先按下表排查。
2. 查看 **标定文件** 区域的当前分组、波长文件、幅值文件和状态。需要配置时点 **管理...**，保存后确认加载结果；已有配置可用 **加载分组** 或文件旁的 **加载** 重载。标定未就绪时先修复配置，不继续测量。
3. 设置 **积分时间 (ms)**、**平均次数** 及所需自动校零、自动积分等选项。自动暗场要求可用快门；启用 EQE 时核实电压、电流的实际来源，见下文。
4. 点 **单次测试**，成功后检查结果列表与曲线。后台测量与界面投影异步衔接，返回成功和界面刷新不是同一时刻。
5. 在结果列表选中需要导出的记录，点击列表右上方的保存图标，选择 CSV 路径。没有选中记录会提示先选择数据；导出使用当前 Normal/EQE 模式的固定字段。

连续测试在同一区域设置间隔与次数；远程调用见 [Spectrum Socket](./spectrum-socket.md)，定时调用见下文“Socket 和调度”。

## 先查什么

| 现场问题 | 第一检查点 |
| --- | --- |
| 工具菜单没有“光谱仪软件” | 插件目录、`manifest.json`、`Spectrum.dll`、宿主版本要求 |
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

宿主按 `manifest.json` 加载 `Spectrum.dll`，`MenuSpectrumWindow` 在工具菜单提供“光谱仪软件”入口。`MainWindow` 是 WPF 组合点，负责生命周期、提示、结果列表和绘图；`SpectrometerManager` 是无窗口依赖的设备入口，管理光谱仪句柄、标定状态和测量流程。Shutter、滤光轮和 SMU 各自在控制器内串行化设备访问。

`IsConnected` 只表示通信已经建立。连接后按设备 SN 加载标定分组；`IsCalibrationReady` 检查已连接、没有加载或配置提交在途，以及已加载快照的分组名和路径与当前配置一致。这个属性不会重新读取文件内容。

测量入口的 `TryGetCalibrationNotReadyReason` 还会重算两个文件的 SHA-256。文件在加载后被替换，即使按钮先前可用，本次测量也会拒绝并要求重新加载；文件不存在或不可读也会使快照失效。排查时同时看 `CalibrationStatus` 和本次测量结果。

测量按配置执行暗场、自动积分、采集和 EQE 派生，然后把结果与测量画像放进同一数据库事务。Manager 返回 `SpectrumMeasurementResult`；MainWindow 只做异步 UI 投影，历史曲线在第一次查看时延迟生成。

独立 WPF 入口是 `App.Application_Startup`，仍初始化配置、许可证和公共 Socket 模块，依赖匹配的 ColorVision 库、原生 DLL/驱动及标定资源；不是只复制一个可执行文件就能测量。独立更新在 `Update/SpectrumUpdateService.cs` 内维持自身责任边界，不应依赖主程序 ServiceHost 才能交付独立版。

`MainWindow.PrepareForShutdownCoreAsync` 停止接收新测量、取消连续测量，等待在途测量结束（包括其保存路径），随后尝试断开光谱仪并关闭辅助设备。`CloseAuxiliaryDevicesAsync` 的 `12` 秒仅限制等待 `IsBusy` 消退的轮询，超时仍忙的设备会警告并跳过强制释放；后续非忙设备的关闭及前面的在途等待不受这个计时器限制。它不是窗口关闭的总截止时间，也不保证所有设备已经安全关闭。

## 设备、标定和测量

| 环节 | 要确认 |
| --- | --- |
| 标定分组 | 当前设备 SN 能找到活动分组 |
| 标定文件 | 两个文件通过预校验，加载快照与当前配置的路径、分组和 SHA-256 一致 |
| 自动校零 | UI 手动流程允许人工遮光；Socket/Job 无人值守流程要求 Shutter 严格完成关闭和恢复 |
| EQE | 已启用 EQE；确认本次电压、电流来自手工配置还是源表采集，且与被测样品一致 |

`CalibrationGroupWindow` 编辑独立副本。保存要求已知设备 SN；已连接时先加载两个候选文件，加载成功且请求仍有效才提交配置。未连接分支不加载 native，但 SN 缺失仍会拒绝保存。配置保存在 Windows 文档目录的 `Spectrometer/<SN>/CalibrationGroups.json`，以临时文件替换写入；保存失败会尝试恢复原标定，恢复失败时保持不可测量状态。

关闭有未保存修改的窗口会询问保存、不保存或取消；保存失败保持窗口打开，保存进行中也不允许关闭。

EQE 测量先取 `MainWindowConfig.EqeVoltage` / `EqeCurrentMA`。源表已打开且本次成功取得采样时，以实测电压、电流覆盖；未连接或没有取得本次采样时仍使用配置值。因此有 EQE 结果不等于本次读取过源表，配置值的来源必须与实验条件一致。

主测量使用 `CM_*` API，`DirectSpectrometer/` 诊断工具使用 `SA_*` API；两者共享原生驱动并具有全会话互斥关系，不能同时连接。

## 数据和文件

| 类别 | 入口 | 说明 |
| --- | --- | --- |
| 插件元数据 | [manifest.json](https://github.com/xincheng213618/scgd_general_wpf/blob/master/Plugins/Spectrum/manifest.json) | 插件身份、DLL 路径和最低宿主要求；版本由专用脚本按编译后的 `Spectrum.exe` 同步 |
| 构建与发布版本 | `Spectrum.csproj`、`Scripts/build_spectrum.py` | 工程的 `VersionPrefix` 生成版本；专用发布脚本读取编译后 `Spectrum.exe` 的四段 `FileVersion` 并同步源目录和输出目录的 manifest |
| 窗口 | `MainWindow.xaml(.cs)` | 生命周期、连接/测量按钮、EQE、结果列表、绘图和用户提示 |
| 设备状态 | `SpectrometerManager.cs` | 原生句柄、标定快照、设备操作门禁和一次完整测量；不创建窗口或文件对话框 |
| 标定 | `Calibration/` | 按光谱仪 SN 管理标定分组 |
| 辅助设备 | `Configs/*Controller.cs` | Shutter、滤光轮、SMU 各自的连接、命令和释放门禁 |
| 许可证 | `License/` | 许可证导入、同步和原生日志入口 |
| SQLite | `%APPDATA%\Spectromer\Config\Spectrum.db` | 本地结果库 |
| 光谱结果 | `SprectrumModel` | 光谱测量结果 |
| 测量画像 | `SpectrumMeasurementProfile` | 测量上下文和配置快照 |
| CSV | `SpectrumCsvExporter.cs` | 无 UI 的不可变快照、实际波长对齐和流式写入 |

CSV 按调用时选中结果建立不可变快照，Normal/EQE 模式使用各自固定字段，不按界面可见列生成。波长取各结果 `SpectralDatas` 的网格并集，先输出全部绝对值列，再输出对应 `sp` 相对值列；某条记录没有的波长留空。

`SpectralDatas` 按 `max(1, round(1 / fInterval))` 的步长取点并补上实际末端。例如 380–780 nm、0.1 nm 间隔的 4001 个原始点会形成 401 个导出采样点；不能把 CSV 列数当作原始 `fPL` 长度。排查导出为空时先确认已选中结果及有效的 `fSpect1/fSpect2/fInterval/fPL`。

保存前 `ViewResultSpectrum.NormalizeColorParam` 会规范化波长元数据并裁剪 `fPL`。有效起止波长和间隔下，点数按 `round((fSpect2 - fSpect1) / fInterval) + 1` 计算并限制在实际数组容量内；旧数据元信息无效时另有起点、间隔和最多 `4001` 点的初始回退规则，不能一律保存 native 数组全部容量。历史明细和曲线按需生成；`ViewResultManagerConfig.Count` 只限制内存显示集合，不删除 SQLite 历史记录，且 `Count <= 0` 不做该集合裁剪。

两种耗时终点不同：结果列表的“结果就绪耗时”累计到结果构造和 EQE 计算完成；`SpectrumMeasurementProfile.TotalDurationMs` 再累计到结果行插入完成，不包含测量画像行插入、事务提交和异步 UI 投影。不要用任一数值代替请求端到端耗时。

## Socket 和调度

五个 Socket handler 复用 Manager；准确的指令名、`Params`、返回字段、设备锁和 `30/60` 秒合作式取消边界仅在 [Spectrum Socket 业务契约](./spectrum-socket.md)维护。Spectrum 没有独立于 `ColorVision.SocketProtocol` 的另一套传输服务。

调度入口在 `Job/`：`SpectrumMeasureJob` 定时执行光谱测量，`SpectrumDarkCalibrationJob` 定时执行暗场/校零。调度任务失败时先看设备、标定 readiness、快门和 Scheduler 执行历史，不要把窗口是否打开作为业务前置条件。

## 验收

| 验收项 | 通过标准 |
| --- | --- |
| 插件装载 | 工具菜单出现“光谱仪软件”，能打开 `MainWindow` |
| 窗口扩展 | Spectrum 窗口菜单和状态栏出现，连接、型号、SN、标定组、模式可读 |
| 交付资源 | 包含 `Spectrum.dll`、manifest、README、CHANGELOG、标定文件和 native DLL |
| 许可证 | 连接前能同步许可证，异常能打开许可证管理或原生日志 |
| 设备连接 | 已知设备能读出型号和 SN；标定损坏时仍保持连接并明确显示不可测量状态，修复后可重新加载 |
| 单次测量 | 曲线刷新，结果列表新增记录，测量画像写入数据库 |
| EQE 测量 | 确认电压、电流来源，结果对象中的值、EQE 字段与导出结果一致 |
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

`ViewResultSpectrumTests` 覆盖有效点数、采样端点和旧元信息回退；`SpectrumCalibrationStateTests` 检查标定快照、路径及文件哈希；`SpectrumCsvExporterTests` 检查字段、采样网格、格式和调用时快照。`SpectrumArchitectureBoundaryTests` 检查 Manager 不反向引用指定窗口、对话框或同步 Application Dispatcher 的源码模式。这些用例不能代替原生设备、窗口关闭或 Socket 时序验证；引用测试文件也不表示测试已经运行。

## 双通道发布（需明确发布授权）

Spectrum 同时维护独立 ZIP 和 ColorVision `.cvxp` 更新源。只有用户明确要求发布 Spectrum 时运行下列命令；它会更新远端两个发布源，不是本地打包或测试命令。

```powershell
.\Scripts\Spectrum.bat --release-notes "本次变更说明"
```

不要用通用 `package_plugin.bat Spectrum` 代替正式发布。专用脚本会构建两种包、签名独立清单、按顺序提交两个更新源，并验证插件 latest、独立 latest/latest-version、下载大小和 SHA-256；全部远程验收通过后才删除本地 `.cvxp`。

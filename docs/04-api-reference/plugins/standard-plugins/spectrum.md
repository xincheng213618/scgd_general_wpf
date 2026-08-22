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
| Socket 无响应 | Socket 服务、端口、JSON 模式、请求 framing 和 Manager 设备状态 |

## 运行链路

宿主按 `manifest.json` 加载 `Spectrum.dll`，`MenuSpectrumWindow` 在 Tool 菜单提供入口。`MainWindow` 是 WPF 组合点，负责生命周期、提示、结果列表和绘图；`SpectrometerManager` 是无窗口依赖的设备入口，管理光谱仪句柄、标定状态和测量流程。Shutter、滤光轮和 SMU 各自在控制器内串行化设备访问。

`IsConnected` 只表示通信已经建立。连接后按设备 SN 加载标定分组，只有配置路径、文件指纹和 native 已加载快照一致，且没有加载或持久化请求在途时，`IsCalibrationReady` 才为 `true`。测量入口会再次执行同一门禁，不能仅凭按钮状态或连接状态判断可测量。

测量按配置执行暗场、自动积分、采集和 EQE 派生，然后把结果与测量画像放进同一数据库事务。Manager 返回 `SpectrumMeasurementResult`；MainWindow 只做异步 UI 投影，历史曲线在第一次查看时延迟生成。

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

## Socket 和调度

Spectrum 提供 `SpectrumStatus`、`SpectrumConnect`、`SpectrumAutoIntTime`、`SpectrumDarkCalibration` 和 `SpectrumMeasure` 五个 JSON 入口。

这些 handler 直接调用 `SpectrometerManager`，不要求 `MainWindow` 已打开；`SpectrumStatus` 仍会返回 `WindowOpen` 供诊断。handler 被编译出来不代表外部客户端一定能连上；还要确认 `ColorVision.SocketProtocol` 已启用、端口正确、协议模式是 JSON。连接响应会同时返回 `IsConnected`、`IsCalibrationReady` 和 `CalibrationStatus`；“连接成功但标定未就绪”不等于可以测量。

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
| Socket | 主窗口关闭时仍可查询、连接和测量；连接响应区分 `IsConnected` 与 `IsCalibrationReady`，校零强制要求快门 |
| 调度 | 窗口关闭时测量或暗场任务仍能执行并留下历史；失败原因指向设备、标定或快门 |
| 双通道发布 | 插件 latest、独立 latest/latest-version、签名、下载大小和 SHA-256 全部通过专用脚本验收 |

## 构建、测试与发布

Spectrum 同时维护独立 ZIP 和 ColorVision `.cvxp` 更新源，构建、测试和正式发布分别使用：

```powershell
dotnet build .\Plugins\Spectrum\Spectrum.csproj -c Release -p:Platform=x64
dotnet test .\Test\Spectrum.Tests\Spectrum.Tests.csproj -c Release -p:Platform=x64
.\Scripts\Spectrum.bat --release-notes "本次变更说明"
```

不要用通用 `package_plugin.bat Spectrum` 代替正式发布。专用脚本会构建两种包、签名独立清单、按顺序提交两个更新源，并验证插件 latest、独立 latest/latest-version、下载大小和 SHA-256；全部远程验收通过后才删除本地 `.cvxp`。

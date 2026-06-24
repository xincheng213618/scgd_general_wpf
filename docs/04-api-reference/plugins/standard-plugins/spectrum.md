# Spectrum 插件

`Plugins/Spectrum/` 是光谱仪测量工作台插件。维护时按五段看：插件装载、设备连接、标定、测量落库、Socket/调度自动化。

## 先查什么

| 现场问题 | 第一检查点 |
| --- | --- |
| Tool 菜单没有 Spectrum | 插件目录、`manifest.json`、`Spectrum.dll`、宿主版本要求 |
| 窗口状态栏为空 | `LoadMenuForWindow("Spectrum", ...)`、`StatusBarManager.Init(..., "Spectrum")` |
| 连接失败 | 许可证同步、USB/COM 配置、native DLL、设备占用、驱动 |
| SN 为空或标定失败 | 设备序列号、当前 SN 的标定分组、`WavaLength.dat` / `Magiude.dat` |
| 自动校零失败 | `ShutterController` 连接和暗场流程 |
| 测量超时或曲线不刷新 | 积分时间、同步频率模式、SDK 返回码、重试结果 |
| 结果列表有数据但数据库没有 | `ViewResultManager`、SQLite 路径、写入异常 |
| EQE 字段为 0 | SMU 配置、测量模式、EQE 回写 |
| Socket 无响应 | Socket 服务、JSON 模式、窗口和设备状态 |

## 运行链路

宿主按 `manifest.json` 加载 `Spectrum.dll`，`MenuSpectrumWindow` 在 Tool 菜单提供入口。`MainWindow` 打开后加载目标名为 `Spectrum` 的菜单和状态栏，`SpectrometerManager` 管理设备、Shutter、滤光轮、SMU、标定组和测量状态。连接前同步许可证，连接后按设备 SN 加载标定分组；测量时执行校零、自动积分、采集、渲染和持久化。

## 设备、标定和测量

| 环节 | 要确认 |
| --- | --- |
| 许可证 | 全局许可证目录和本地 `license` 目录已同步 |
| 设备连接 | USB/COM 配置正确，native 光谱仪 DLL 已在插件目录 |
| 标定分组 | 当前设备 SN 能找到活动分组 |
| 标定文件 | `WavaLength.dat` 和 `Magiude.dat` 存在且能加载 |
| 自动校零 | Shutter 可用，暗场流程完成 |
| 自动积分 | 积分时间能回写到窗口配置 |
| EQE | SMU 已连接，电压/电流结果能写入结果对象 |

`CalibrationGroupWindow` 会先把修改暂存在内存里，保存后才写回；关闭窗口不会自动保存未提交改动。

## 数据和文件

| 类别 | 入口 | 说明 |
| --- | --- | --- |
| 插件元数据 | `manifest.json` | `version=1.0`，`requires=1.3.15.8` |
| 程序集版本 | `Spectrum.csproj` | `VersionPrefix=2.3.3.1`，发包前确认是否和 manifest 同步 |
| 窗口 | `MainWindow.*.cs` | 连接、测量、EQE 和导出 |
| 设备状态 | `SpectrometerManager.cs` | 设备、Shutter、滤光轮、SMU、标定组和测量状态 |
| 标定 | `Calibration/` | 按光谱仪 SN 管理标定分组 |
| 许可证 | `License/` | 许可证导入、同步和原生日志入口 |
| SQLite | `AppData\Spectromer\Config\Spectrum.db` | 本地结果库 |
| 光谱结果 | `SprectrumModel` | 光谱测量结果 |
| 测量画像 | `SpectrumMeasurementProfile` | 测量上下文和配置快照 |
| 步骤明细 | 测量步骤 JSON | 自动化步骤记录 |

排查“导出为空”时先看列表筛选、相对/绝对光谱切换、可见列和导出模型。

## Socket 和调度

Spectrum 提供 5 个 JSON Socket 入口：

| EventName | 作用 |
| --- | --- |
| `SpectrumStatus` | 查询连接、设备和测量状态 |
| `SpectrumConnect` | 连接或断开光谱仪 |
| `SpectrumAutoIntTime` | 获取自动积分时间 |
| `SpectrumDarkCalibration` | 执行暗场/校零 |
| `SpectrumMeasure` | 执行测量 |

这些 handler 依赖当前窗口、`SpectrometerManager` 和设备状态。handler 被编译出来不代表外部客户端一定能连上；还要确认 `ColorVision.SocketProtocol` 已启用、端口正确、协议模式是 JSON。

调度入口在 `Job/`：`SpectrumMeasureJob` 定时执行光谱测量，`SpectrumDarkCalibrationJob` 定时执行暗场/校零。调度任务失败时先看窗口是否打开、设备是否连接，再看 Scheduler 执行历史。

## 验收

| 验收项 | 通过标准 |
| --- | --- |
| 插件装载 | Tool 菜单出现 Spectrum，能打开 `MainWindow` |
| 窗口扩展 | Spectrum 窗口菜单和状态栏出现，连接、型号、SN、标定组、模式可读 |
| 交付资源 | 包含 `Spectrum.dll`、manifest、README、CHANGELOG、标定文件和 native DLL |
| 许可证 | 连接前能同步许可证，异常能打开许可证管理或原生日志 |
| 设备连接 | 已知设备能读出型号、SN，并加载当前 SN 标定组 |
| 单次测量 | 曲线刷新，结果列表新增记录，测量画像写入数据库 |
| EQE 测量 | SMU 数据、EQE 字段和导出结果一致 |
| 数据落库 | `Spectrum.db` 同时有结果和测量画像 |
| Socket | `SpectrumStatus`、连接、校零、测量返回可判读 Code/Msg |
| 调度 | 测量任务或暗场任务能执行并留下历史 |

## 构建

```powershell
dotnet build Plugins/Spectrum/Spectrum.csproj -c Release -p:Platform=x64
Scripts\package_plugin.bat Spectrum --no-upload
```

Spectrum 也有专用脚本 `Scripts\build_spectrum.py`；使用前先确认当前发布流程是否要求它。

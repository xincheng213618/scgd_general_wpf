# Spectrum 光谱测量插件

Spectrum 是 ColorVision 的光谱仪测量插件，也可以作为独立 WPF 程序运行。它负责设备连接、光谱采集、校零、自动积分、结果保存、曲线显示、EQE 和校准文件管理。

项目目标框架为 `net10.0-windows`。版本事实源是 `Spectrum.csproj` 编译出的 DLL `FileVersion`；最低宿主版本读取 `manifest.json`，发布脚本会校验并同步两者。

## 先从哪里看

如果第一次接触这个项目，建议按下面顺序阅读：

1. `SpectrometerManager.cs`：设备句柄、标定状态、串行化设备操作和一次完整测量。
2. `MainWindow.xaml.cs`：窗口生命周期、按钮编排、结果列表和绘图交互。
3. `Data/ViewResultManager.cs`：SQLite 事务和当前显示的结果集合。
4. `Models/ViewResultSpectrum.cs`：一条测量结果及其延迟生成的曲线。
5. `SpectrumCsvExporter.cs`：无 UI 的 CSV 投影、波长对齐和写入。

不要从 Socket 或 Quartz Job 开始找测量算法。它们只是入口，最终都调用 `SpectrometerManager`。

## 运行结构

```text
MainWindow / Socket / Quartz Job
                |
                v
       SpectrometerManager
       - 唯一原生句柄
       - 唯一设备操作锁
       - Connect / Disconnect
       - Configured / Loaded calibration
       - Measure / Dark / AutoInt
                |
                v
     SpectrumMeasurementResult
                |
                v
       ViewResultManager
       - SQLite
       - 当前结果集合
```

各部分职责：

- `MainWindow` 只处理界面、进度和提示，不直接实现测量流程。
- `SpectrometerManager` 是设备操作的唯一入口。同一时刻只允许一个原生操作。
- `ViewResultManager` 在同一事务中保存结果与测量记录，并维护界面正在显示的有限数量结果。
- `ViewResultSpectrum` 自己持有曲线；没有额外的平行曲线集合。
- Socket 和 Quartz 不依赖主窗口是否打开。

## 最小代码示例

```csharp
SpectrometerManager manager = SpectrometerManager.Instance;

manager.Config.SpectrometerType = SpectrometerType.CMvSpectra;
manager.Config.IsComPort = true;
manager.Config.SzComName = "COM3";
manager.Config.BaudRate = 9600;

int connectResult = await manager.ConnectAsync();
if (connectResult != 1)
    throw new InvalidOperationException(manager.GetOperationErrorMessage(connectResult));
if (!manager.IsCalibrationReady)
    throw new InvalidOperationException(manager.CalibrationStatus);

manager.IntTime = 100;
SpectrumMeasurementResult measurement = await manager.MeasureAsync();
if (!measurement.IsSuccess)
    throw new InvalidOperationException(measurement.ErrorMessage);

ViewResultSpectrum result = measurement.Result!;
string luminance = result.Lv;

await manager.DisconnectAsync();
```

`MeasureAsync` 可能返回：

- `IsSuccess = true`：`Result` 是本次结果。
- `IsBusy = true`：设备正在执行其他原生操作，本次请求没有排队。
- 其他失败：查看 `ErrorCode`、`ErrorMessage` 和日志。

## 设备操作规则

- 新的光谱仪原生调用必须放进 `RunExclusiveAsync` 或 `TryRunExclusiveAsync`。
- UI、Socket 和 Job 不得自己创建、释放或缓存光谱仪 Handle。
- `IsConnected` 只表示通信已建立；开始测量前必须由 Manager 确认 `IsCalibrationReady`。
- 配置中的标定路径与设备实际加载的文件分开追踪，切换失败不得用新配置冒充已加载状态。
- 不要用布尔字段实现“正在测量”。检查和赋值不是原子操作。
- 停止连续测量时使用 `CancellationToken`；当前不可取消的原生调用完成后才释放设备。
- 关闭窗口会等待在途测量保存完成；辅助设备最多等待 12 秒，超时会提示并跳过强制释放，避免争抢仍在执行的设备操作。

## 结果与内存

原生 `COLOR_PARA.fPL` 的容量可能是 10000，但有效点数按下面公式计算：

```text
round((fSpect2 - fSpect1) / fInterval) + 1
```

保存前只保留有效切片。历史数据的明细和 ScottPlot 曲线在第一次查看时生成，避免启动时为每条记录创建多组大数组。`ViewResultManagerConfig.Count` 只限制内存中的显示结果，不删除 SQLite 历史记录。

## 主要目录

- `Calibration/`：校准组和幅值标定界面。
- `Configs/`：快门、滤光轮和 SMU 配置及控制器。
- `Data/`：SQLite 模型与结果管理。
- `DirectSpectrometer/`：使用另一套 `SA_*` API 的诊断工具，不并入主测量流程。
- `Job/`：Quartz 入口。
- `Socket/`：Socket 协议入口。
- `Models/`：结果与光谱明细模型。
- `Update/`：独立版更新、签名校验和回滚。
- `View/`：纯色度与显色性计算。

主测量的 `CM_*` API 与直连诊断的 `SA_*` API 共享同一原生驱动，因此两类连接具有全会话互斥关系；使用一方前必须先关闭另一方。

## 构建、测试与发布

在仓库根目录使用 PowerShell：

```powershell
dotnet build .\Plugins\Spectrum\Spectrum.csproj -p:Platform=x64
dotnet test .\Test\Spectrum.Tests\Spectrum.Tests.csproj -p:Platform=x64
```

专项测试覆盖结果有效点数、实际波长范围、CSV 对齐、标定文件快照、旧数据兼容，以及 Manager 不得反向依赖 MessageBox、窗口、文件对话框或同步 Dispatcher 的架构边界。原生设备 API 与窗口生命周期仍必须做真机检查。

正式发布同时维护独立 ZIP 和 ColorVision `.cvxp` 更新源：

```powershell
.\Scripts\Spectrum.bat --release-notes "本次变更说明"
```

不要改主程序 `Directory.Build.props`，也不要用主程序 `release.bat` 或通用 `package_plugin.bat Spectrum` 代替正式 Spectrum 发布。专用脚本会签名独立清单、提交两个更新源并完成远端版本、下载大小和 SHA-256 验收。

结果列表中的“结果就绪耗时”统计到结果生成和 EQE 计算完成；`SpectrumMeasurementProfile.TotalDurationMs` 累计到结果行插入完成，不包含测量记录行插入、事务提交与异步 UI 投影。

## 真机检查清单

每次修改连接、测量或生命周期后至少验证：

1. UI 连接、断开、关闭后重新打开。
2. 单次测量、连续测量、停止连续测量。
3. 校零、自动积分和自适应校零。
4. Socket 与 Quartz 在主窗口关闭时执行。
5. 测量过程中再次测量、断开或安装更新，确认不会并发进入原生 API。
6. 历史结果首次绘图、排序、删除和 CSV 导出。
7. 380–780 nm、0.1 nm 与 1 nm 数据的首尾波长及点数。

## 保留的独立边界

- `DirectSpectrometer` 是底层诊断工具，使用另一套 API 和独立日志。
- `Update/SpectrumUpdateService.cs` 是安全关键代码，必须保持独立版自包含，不依赖主程序 ServiceHost。
- Shutter、FilterWheel、SMU 分别对应真实设备，保留独立控制器是合理边界。
- `MainWindow` 保持一个 XAML code-behind 组合点，高频绘图和控件交互不强塞进命令；可测试的 CSV、设备状态和数据库事务留在各自真实边界中。

详细插件文档见 [Spectrum API 文档](../../docs/04-api-reference/plugins/standard-plugins/spectrum.md)，Socket 协议见 [Socket/README.md](Socket/README.md)。

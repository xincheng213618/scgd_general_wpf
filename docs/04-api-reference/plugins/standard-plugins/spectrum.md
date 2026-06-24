# Spectrum 插件

本页只描述当前仓库里实际存在的 Spectrum 插件实现，不再继续维护“版本表 + 功能宣传 + 理想化 API 手册”式旧稿。

## 先看这个插件现在是什么

按当前源码状态，Spectrum 不是一个零散的设备驱动示例，而是一个以独立光谱测试窗口为中心的插件工作台。它当前至少包含四条明确的运行链：

- Tool 菜单里的窗口入口。
- `Spectrum` 目标窗口自己的菜单和状态栏。
- 围绕 `SpectrometerManager` 的连接、标定和测量控制。
- 围绕 `ViewResultManager` 的结果展示、SQLite 持久化和测量画像记录。

因此它比旧文档里“光谱仪测试工具”这类泛化描述更具体，实际是一个完整但仍然以单窗口为中心的测量工作台。

## 当前最关键的文件

- `Plugins/Spectrum/manifest.json`
- `Plugins/Spectrum/MainWindow.xaml(.cs)`
- `Plugins/Spectrum/MainWindow.Connection.cs`
- `Plugins/Spectrum/MainWindow.Measurement.cs`
- `Plugins/Spectrum/SpectrometerManager.cs`
- `Plugins/Spectrum/Data/ViewResultManager.cs`
- `Plugins/Spectrum/SpectrumStatusBarProvider.cs`
- `Plugins/Spectrum/Calibration/CalibrationGroupWindow.xaml.cs`
- `Plugins/Spectrum/License/LicenseDatabase.cs`

如果只是想弄清插件怎么进入宿主、怎么连接设备、怎么保存结果，这几处代码已经覆盖了主体。

## 当前接入宿主的几条链

### 窗口入口

`MenuSpectrumWindow` 当前继承 `MenuItemBase`，挂在 `Tool` 菜单下，执行时直接打开 `MainWindow`。

这说明 Spectrum 现在最核心的宿主入口不是某个很厚的插件入口类，而是菜单项和随后打开的工作窗口。

### 窗口级菜单与状态栏

`MainWindow` 初始化时会调用：

- `MenuManager.GetInstance().LoadMenuForWindow("Spectrum", menu)`
- `StatusBarManager.GetInstance().Init(StatusBarGrid, "Spectrum")`

也就是说，这个插件并不只是在主程序菜单上挂一个入口。窗口打开后，还有一套以 `TargetName = "Spectrum"` 为目标的局部菜单和状态栏扩展面。

### 状态栏提供器

`SpectrumStatusBarProvider` 当前会把这些信息接到 `Spectrum` 窗口的状态栏：

- 连接状态
- 硬件型号
- SN 序列号
- 当前标定组
- 当前测量模式
- Shutter 连接状态
- CFW 滤光轮连接状态

其中 SN 文本当前还支持点击复制，因此它不是只读装饰项。

### manifest 信息

按当前 `manifest.json`，插件对宿主公开的装载信息是：

- `Id = "Spectrum"`
- `name = "Spectrum"`
- `version = "1.0"`
- `dllpath = "Spectrum.dll"`
- `requires = "1.3.15.8"`

这比旧文档里自定义的一长串版本和依赖表更接近当前真实装载模型。

## 当前运行时核心是谁

`SpectrometerManager` 是当前插件最重要的单例状态中心。它通过 `ConfigService` 取得并持有：

- `SpectrumConfig`
- `ShutterController`
- `FilterWheelController`
- `SmuController`
- 当前设备句柄
- 当前连接状态、硬件型号、序列号
- 当前标定组配置与活动分组
- 当前测量模式文本

因此 `MainWindow` 更多是在组织 UI 和调用链，真正跨文件共享的测量状态主要都收敛在 `SpectrometerManager`。

## 连接与标定当前怎么工作

`MainWindow.Connection.cs` 展示了当前连接链的真实顺序：

1. 连接前先调用 `LicenseDatabase.Instance.SyncToLocal()` 同步许可证。
2. 通过 `Spectrometer.CM_CreateEmission(...)` 创建句柄。
3. 根据配置决定走 USB 还是 COM 口初始化。
4. 连接成功后读取设备序列号。
5. 按序列号加载标定分组配置。
6. 加载当前波长标定文件和幅值标定文件。
7. 应用 SP100 参数。

如果连接失败，当前实现还会尝试读取设备列表；当检测到单个设备但连接失败时，会把问题引导到许可证管理，而不是只弹一个通用错误框。

### 标定分组不是简单文件选择框

`CalibrationGroupWindow` 当前按光谱仪 SN 管理分组配置。编辑时变更会先暂存在内存里，只有点击保存才真正写回；直接关闭窗口会放弃未保存改动。

这和旧文档里“选择一个标定文件继续测量”的说法相比，已经是更明确的一套按设备分组的配置模型。

## 测量链当前怎么展开

`MainWindow.Measurement.cs` 当前把单次测量拆成了几个清晰阶段：

- 自动校零前置检查
- 自动积分
- 自适应校零
- 采集数据
- 渲染结果
- 持久化结果

具体行为上，当前实现已经不只是“调用一次设备 SDK 然后画图”：

- 自动校零依赖 `ShutterController`。
- 同步频率模式会走 `CM_Emission_GetDataSyncfreq(...)`。
- 标准模式在超时时会做一次重试。
- EQE 模式下会接入 `SmuController`，并把电压电流结果回写到窗口配置和结果对象。

同时，测量过程会额外记录每个步骤的耗时、输入快照和成功状态。

## 结果与持久化当前怎么落地

`ViewResultManager` 当前不只是一个内存列表管理器，而是插件的数据落点。按实现看，它会：

- 在 `AppData\Spectromer\Config\Spectrum.db` 维护 SQLite 数据库。
- 保存 `SprectrumModel` 结果记录。
- 维护 `SpectrumMeasurementProfile` 测量画像。
- 保存测量步骤明细 JSON。
- 在需要时更新 EQE 字段和测量总耗时。

因此 Spectrum 当前不是“测完即丢”的临时工具，已经包含基本的数据追踪和回看能力。

### 导出与列表操作

当前主窗口还内置了：

- 相对光谱 / 绝对光谱切换
- CIE 图联动显示
- 可见列复制
- 普通光谱 CSV 导出
- EQE 模式 CSV 导出
- 结果删除与数据库清空

这些行为分散在 `MainWindow.Chart.cs`、`MainWindow.ListView.cs` 和 `MainWindow.Export.cs` 中。

## 当前还有哪些附加子系统

### 布局持久化

`MainWindow` 目前通过 `DockLayoutManager` 管理 AvalonDock 布局，并在窗口关闭时自动保存布局。这意味着它不是固定死板的单面板窗口。

### 许可证同步

`LicenseDatabase` 当前用 SQLite 跟踪已导入许可证文件的元数据，并在连接光谱仪前把全局许可证目录和本地 `license` 目录同步起来。

### 独立启动壳存在，但不是宿主扩展重点

仓库里确实还有 `App.xaml.cs`，它会在独立启动时初始化主题、日志、Socket 和主窗口。但在当前主程序插件装载模型里，文档更应该关注 manifest、菜单入口、provider 和窗口本体，而不是把这个 `Application` 类误写成日常宿主扩展入口。

## 当前几个最容易写错的点

### 它不是只有“连接设备 + 读一帧数据”

现在的 Spectrum 实现已经把许可证同步、标定分组、窗口布局、状态栏、SQLite 结果落库和测量画像都串起来了。继续把它写成轻量测试小工具，会明显低估当前复杂度。

### 结果持久化不只是一张表

除了光谱结果记录，当前还会单独落 `SpectrumMeasurementProfile` 和步骤明细 JSON。旧文档如果只写导出 CSV，会漏掉真正的追踪链。

### 标定是按 SN 组织的

当前标定配置不是简单的全局单文件路径，而是跟序列号和活动分组绑定。这个边界对理解现场设备切换很重要。

### 状态栏是窗口级扩展，不是全局主程序常驻项

`SpectrumStatusBarProvider` 的目标名是 `Spectrum`，它描述的是插件窗口内部状态栏，而不是主程序任意页面都可见的全局状态条。

## 推荐阅读顺序

1. `Plugins/Spectrum/MainWindow.xaml.cs`
2. `Plugins/Spectrum/SpectrometerManager.cs`
3. `Plugins/Spectrum/MainWindow.Connection.cs`
4. `Plugins/Spectrum/MainWindow.Measurement.cs`
5. `Plugins/Spectrum/Data/ViewResultManager.cs`
6. `Plugins/Spectrum/SpectrumStatusBarProvider.cs`
7. `Plugins/Spectrum/Calibration/CalibrationGroupWindow.xaml.cs`
8. `Plugins/Spectrum/manifest.json`

这样能先看到宿主入口，再看到状态中心、设备链和结果落点。

## 交接验收表

| 验收项 | 操作 | 通过标准 |
| --- | --- | --- |
| 插件装载 | 检查 `manifest.json`、`dllpath` 和 Tool 菜单 | Tool 菜单出现 Spectrum 入口，能打开 `MainWindow` |
| 窗口扩展 | 打开 Spectrum 窗口 | `TargetName = "Spectrum"` 的窗口菜单和状态栏加载，状态栏显示连接、型号、SN、标定组和测量模式 |
| 交付资源 | 检查构建或 `.cvxp` 包内容 | `Spectrum.dll`、`manifest.json`、`README.md`、`CHANGELOG.md`、标定文件和必要 native DLL 存在 |
| 许可证同步 | 连接前打开许可证管理或执行连接 | `LicenseDatabase` 能同步全局和本地许可证；许可证异常时能引导到许可证管理 |
| 设备连接 | 使用现场 USB/COM 配置连接已知光谱仪 | 成功创建句柄，读出型号、SN，并加载当前 SN 的标定分组 |
| 标定分组 | 编辑分组后保存，再关闭重开 | 保存的分组能按 SN 恢复；未保存关闭不会污染配置 |
| 单次测量 | 执行标准测量模式 | 曲线刷新，结果列表新增记录，步骤耗时和输入快照写入测量画像 |
| EQE 测量 | 配置 SMU 后执行 EQE 模式 | 电压、电流、EQE 字段写入结果对象并能导出 |
| 数据落库 | 检查 `AppData\Spectromer\Config\Spectrum.db` | 存在 `SprectrumModel`、`SpectrumMeasurementProfile` 和测量步骤 JSON |
| 导出与列表 | 执行 CSV/EQE CSV、复制可见列、删除/清空 | 文件列正确，列表和数据库操作符合窗口提示 |
| Socket 指令 | 在启用 Socket 的环境调用测量相关 handler | 连接、状态、测量、暗场和自动积分指令返回可判读状态 |

## 故障首查

| 现象 | 先查什么 |
| --- | --- |
| Tool 菜单没有 Spectrum | 插件目录、`manifest.json` 的 `Id/dllpath/requires`、`Spectrum.dll` 是否复制到主程序插件目录 |
| 窗口状态栏为空 | `SpectrumStatusBarProvider` 是否注册，`LoadMenuForWindow("Spectrum", ...)` 和 `StatusBarManager.Init(..., "Spectrum")` 是否执行 |
| 连接失败 | 许可证同步、USB/COM 配置、设备列表、native SDK DLL、设备占用和管理员/驱动状态 |
| SN 为空或标定加载失败 | 设备序列号读取、SN 对应标定分组、`WavaLength.dat` 和 `Magiude.dat` 路径 |
| 自动校零无法继续 | `ShutterController` 连接状态、暗场流程、自动校零前置检查结果 |
| 测量超时或曲线不刷新 | 积分时间、同步频率模式、SDK 返回码、重试结果和图表刷新链 |
| 结果列表有数据但数据库没有 | `ViewResultManager` 配置、SQLite 路径、写入异常和 `SpectrumMeasurementProfile` 保存 |
| EQE 字段为 0 | `SmuController` 配置、窗口测量模式、EQE 字段回写和二次计算更新 |
| Socket 无响应 | Socket handler 是否启用，命令是否落到当前 `MainWindow.ViewResultManager` 和 `SpectrometerManager` 状态 |
| 导出为空 | 当前列表筛选、相对/绝对光谱切换、可见列和导出模型 |

## 继续阅读

- [现有插件现场验收与交接清单](../README.md)
- [插件能力与交接矩阵](../plugin-capability-matrix.md)
- [Plugins/README.md](../../../../Plugins/README.md)
- [docs/02-developer-guide/plugin-development/overview.md](../../../02-developer-guide/plugin-development/overview.md)
- [docs/04-api-reference/plugins/standard-plugins/system-monitor.md](./system-monitor.md)

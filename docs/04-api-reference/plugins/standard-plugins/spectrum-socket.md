---
knowledge_id: "plugins.spectrum-socket"
knowledge_type: "topic"
status: "current"
summary: "Spectrum 五个 Socket 业务指令的参数、结果字段、设备门禁与合作式取消；30/60 秒不保证原生操作按时停止。"
aliases: ["光谱仪远程连接和测量","Spectrum Socket 指令","SpectrumStatus","SpectrumConnect","SpectrumDarkCalibration","SpectrumAutoIntTime","SpectrumMeasure","SpectrumStatusSocketHandler","SpectrumConnectSocketHandler","SpectrumDarkCalibrationSocketHandler","SpectrumAutoIntTimeSocketHandler","SpectrumMeasureSocketHandler","Spectrum.Socket"]
code_paths: ["Plugins/Spectrum/Socket/README.md","Plugins/Spectrum/Socket/SpectrumStatusSocketHandler.cs","Plugins/Spectrum/Socket/SpectrumConnectSocketHandler.cs","Plugins/Spectrum/Socket/SpectrumDarkCalibrationSocketHandler.cs","Plugins/Spectrum/Socket/SpectrumAutoIntTimeSocketHandler.cs","Plugins/Spectrum/Socket/SpectrumMeasureSocketHandler.cs","Plugins/Spectrum/SpectrometerManager.cs","Plugins/Spectrum/Configs/ShutterController.cs","Plugins/Spectrum/Data/ViewResultManager.cs","Plugins/Spectrum/Models/ViewResultSpectrum.cs","Plugins/Spectrum/App.xaml","Plugins/Spectrum/App.xaml.cs","Plugins/Spectrum/MainWindow.xaml.cs"]
test_paths: []
related: ["plugins.spectrum","ui.socket-protocol"]
---

# Spectrum Socket 业务指令与完成边界

`Plugins/Spectrum/Socket/` 是 `ColorVision.SocketProtocol` 上的五个 `ISocketJsonHandler` 实现，不是另一套 Socket 服务器。宿主装载 Spectrum 后使用公共分发器；独立程序的 `App.Application_Startup` 也调用公共 `SocketInitializer`。监听是否启用、JSON 模式、端口、handler 发现时机、报文分帧、发送记录和重发见[公共 Socket 契约](../../ui-components/ColorVision.SocketProtocol.md)，不能仅凭 handler 存在判断客户端可达。

本主题只定义 Spectrum 业务调用。设备、许可证、标定和原生驱动前提见 [Spectrum 测量与标定](./spectrum.md)。连接、断开、校零、自动积分和测量都会进入真实设备路径，必须另获现场授权；下面的合成报文只说明数据结构，不是设备联调授权。

## 请求与五个入口

```json
{"EventName":"SpectrumConnect","MsgID":"example-1","Params":"connect"}
```

实际路由名是 `SpectrumConnect` 等连续字符串，不是 `Spectrum.Connect`。操作参数在 `Params`，没有 `Action` 字段。五个 handler 自己构造的响应会设置对应 `EventName` 并回传请求的 `MsgID`；它们不验证 `Version`，也不按请求的 `SerialNumber` 选择设备。当前 handler 不设置响应的 `Version` / `SerialNumber`；不要把它们当作版本协商或设备选择结果。分发前失败的响应另遵守公共 Socket 契约，不能假定它们也具有这些关联字段。

| EventName | 参数、门禁与成功含义 | Data |
| --- | --- | --- |
| `SpectrumStatus` | 读取 Manager 当前属性；`200` 只表示状态读取成功 | 连接、采集配置及 `WindowOpen`，不含标定 readiness |
| `SpectrumConnect` | `Params` 去空白并转小写后，只有 `disconnect` 进入断开；其他值（包括空值、空串和拼错的值）都进入连接 | `IsConnected`、`IsCalibrationReady`、`CalibrationStatus` |
| `SpectrumDarkCalibration` | 已连接；`PerformDarkCalibrationAsync(requireShutter: true)` 要求可用快门并执行暗场；成功返回 `200` | 未设置 |
| `SpectrumAutoIntTime` | 已连接；`TryGetAutoIntegrationTimeAsync` 成功得到时间并写回 Manager 后返回 `200` | 数值 `IntTime` |
| `SpectrumMeasure` | 已连接，Manager 再检查标定和采集条件；直接等待本次 `MeasureAsync` 的结果，不从窗口列表找最新记录 | 本次结果的色度等字段，以及响应构造时 Manager 的 `IntTime` |

除 `SpectrumConnect` 外，当前四个 handler 不使用 `Params`。`SpectrumConnect` 不是严格的参数校验 API，不能用未知参数探测能力而假定不会连接设备。

`SpectrumStatus.Data` 完整字段为 `IsConnected`、`IntTime`、`Average`、`SerialNumber`、`EnableAutodark`、`EnableAutoIntegration`、`EnableAdaptiveAutoDark`、`MeasurementInterval`、`MeasurementNum`、`WindowOpen`。这些属性顺序读取，不持有设备操作锁，不是一次测量的冻结快照；`WindowOpen` 仅检查 `MainWindow.Instance != null`，不表示窗口可见、设备就绪或测量成功。

## 连接、标定与快门

连接响应 `Code = 200` 需要 `Connect()` 返回 `1` 且 Manager 为已连接，但不要求 `IsCalibrationReady`。因此必须把“通信已建立”和“可以测量”分开；连接时读到 readiness 也不能替代后续测量入口的再次校验。`SpectrumStatus` 没有 readiness 字段，标定门禁及修复依据见 [Spectrum 标定契约](./spectrum.md)。

断开响应按 `Disconnect()` 返回值决定 `200` 或 `-2`。Manager 的断开路径即使原生关闭/释放失败也会清空本地连接状态；释放失败还可能隔离原生会话。`Data.IsConnected = false` 不能单独证明驱动释放成功，必须结合 `Code` / `Msg` 和设备检查。

Socket 校零不回退到人工遮光。`CaptureDarkWithShutterCoreAsync` 先关闭快门，关闭确认失败时尝试恢复打开；正常进入暗场后在 `finally` 中尝试重新打开。`ShutterController` 分别识别 `turn off` / `turn on` 确认。快门缺失、关闭或恢复确认失败、native 返回失败通常映射为 `-3`，异常/取消另按下节处理；收到失败码不能据此认定光路已经恢复。

## 设备锁与合作式取消

`Connect` / `Disconnect` 使用 `RunExclusive` 等待设备门禁；当前连接 handler 没有传入取消令牌或设置操作超时。它不是“设备忙就立即拒绝”的路径，连接返回 `-4` 对应的是 `OperationBusy` 原生会话占用/隔离结果。

校零、自动积分和测量使用 `TryRunExclusiveAsync` 的 `WaitAsync(0, token)`，拿不到设备门禁就拒绝，不排队另一项原生操作。测量在服务停止接收请求时也可返回 `IsBusy`，handler 将其转成 `-4`。UI、Job 和 Socket 应复用这些 Manager 入口，不自行创建、释放或缓存光谱仪句柄。测量的设备锁覆盖采集阶段，捕获完成后才在锁外检查取消并保存结果；这不是覆盖数据库保存与响应发送的整请求锁。

三个耗时 handler 同步等待异步 Manager API，并分别建立校零/自动积分 `30` 秒、测量 `60` 秒的 `CancellationTokenSource`。**这是合作式取消触发时间，不是响应或设备停止的硬截止。** native 同步调用不接收该令牌，快门恢复也不会被它强制中断：

- 校零在 native 暗场调用前检查取消，调用后还等待快门恢复，但没有最终再次检查令牌。自动积分只在进入设备工作时检查取消，之后的 native 自动积分、可选同步频率调整和 `IntTime` 写回没有最终取消检查。两者都可能超过 `30` 秒后仍返回 `200`。
- 测量有多个取消检查点，成功捕获后、保存结果前还会检查一次。只有检测到取消并抛出 `OperationCanceledException` 才走取消响应；失败捕获也可能直接走业务失败。同步保存阶段不接收令牌，因此在该阶段触发 `60` 秒取消仍可能提交结果并返回成功。
- 请求超时、客户端断开或 `-4` 响应都不是设备安全停止证明。已经执行的暗场、快门或其他设备动作不会因令牌取消而自动回滚；设备锁等工作返回后才释放。

自动积分成功表示得到并写回一个时间，不表示可选同步频率调整也成功：`TryGetAutoIntegrationTimeAsync` 对后者失败只记录警告，仍可使用原自动积分值返回 `200`。

## 测量返回字段与持久化

`SpectrumMeasureSocketHandler` 使用 `SpectrumMeasurementResult.Result`，不会监听 UI 集合来推断完成。成功路径由 `ViewResultManager.SaveMeasurement` 在同一 SQLite 事务内插入光谱结果和 `SpectrumMeasurementProfile`，提交后发布 UI 投影，再返回结果；响应成功不保证界面已经刷新。WPF 跨线程投影通过 Dispatcher 排队，数据库代次变化也可能让该次投影被跳过。

| Data 字段 | 当前来源与 JSON 类型 |
| --- | --- |
| `Lv` | `ViewResultSpectrum.Lv`，字符串；由安全亮度值 `ToString()` 生成，不是 JSON 数值 |
| `x` / `y` / `u` / `v` | `fx` / `fy` / `fu` / `fv`，数值 |
| `CCT` / `Duv` | `fCCT` / `dC`，数值 |
| `DominantWavelength` / `PeakWavelength` / `HalfBandwidth` | `fLd` / `fLp` / `fHW`，数值 |
| `ColorPurity` / `Ra` | `fPur` / `fRa`，数值 |
| `IP` | 字符串，带 `%` 后缀 |
| `Blue` | 字符串，比例乘 `100` 后取两位小数的文本，**不带 `%`**；分母为零时为 `"0"` |
| `IntTime` | 响应构造时的 `SpectrometerManager.IntTime`，数值；不是从本次结果的不可变快照读取 |

以下是字段类型示例，数值为合成数据；字符串由当前实现的 `ToString()` 产生，不能把示例的小数格式当成跨区域设置的格式保证：

```json
{
  "EventName": "SpectrumMeasure",
  "MsgID": "example-2",
  "Code": 200,
  "Msg": "测量完成",
  "Data": {
    "Lv": "123.45",
    "x": 0.312, "y": 0.329, "u": 0.198, "v": 0.468,
    "CCT": 6504, "Duv": 0.003,
    "DominantWavelength": 530.2, "PeakWavelength": 531.5,
    "HalfBandwidth": 2.1, "ColorPurity": 0.85, "Ra": 95,
    "IP": "85%", "Blue": "12.5", "IntTime": 150.5
  }
}
```

测量失败或取消不等于数据库完全未写入：采集已开始但成功路径未完成时，Manager 的 `finally` 会尽力单独保存失败/取消的测量画像，保存失败仅记录日志。事务回滚的范围是那次数据库事务，不会撤销此前的设备动作；结果有效点数、显示集合上限和 CSV 见 [Spectrum 结果契约](./spectrum.md)。

## 业务错误与窗口边界

| Code | 当前 handler 的判据 |
| --- | --- |
| `200` | 本指令成功路径；状态查询和连接的成功都不代表可测量 |
| `-2` | 校零/自动积分/测量入口观察到未连接，或连接/断开返回失败 |
| `-3` | 校零非成功/非 Busy 结果、自动积分没有返回值，或测量 `IsSuccess` 为假/结果为空；具体失败原因看 `Msg` |
| `-4` | 三个耗时操作未进入设备门禁或捕获到取消异常；测量服务暂停也映射为忙；连接另用于原生会话占用/隔离 |
| `-99` | handler 的 `try/catch` 捕获到的其他异常；不能据此保证所有上游异常都转成此码 |

`MeasureAsync` 会把部分内部异常转成失败结果，因此数据库或采集异常可能表现为 `-3`，不能只检查 `-99`。公共分发/解析错误不属于上述业务码表，按[公共 Socket 契约](../../ui-components/ColorVision.SocketProtocol.md)处理。

业务 handler 不要求 `MainWindow` 已打开，不等于“关闭独立程序后仍提供后台服务”。Spectrum 窗口关闭会暂停接收新测量、等待在途路径并尝试断开设备；独立 WPF 程序也没有在 `App.xaml` 配置关闭窗口后常驻的模式。宿主仍存活且入口已启用时，可以在没有 Spectrum 窗口的情况下重新连接并操作，但仍须满足设备/标定/快门门禁。

## 源码入口与验证缺口

五个 handler 的 `Handle` 定义参数和返回码；设备互斥、标定检查和取消观察点在 `SpectrometerManager`，快门确认在 `Configs/ShutterController.cs`，结果事务在 `Data/ViewResultManager.cs`，字段格式在 `Models/ViewResultSpectrum.cs`。扩展指令时保留这些责任边界，传输注册只依公共 Socket 的当前发现规则。

当前没有发现针对这五个 handler 的专项自动化测试，因此 `test_paths` 为空。已有 Spectrum 数据/标定测试不等于 Socket 返回码、字段序列化、设备争用、超时或关闭窗口的协议覆盖；公共 Socket 的测试也不证明原生设备行为。源码契约说明不是端到端或真机验证结果；验证仍须分别覆盖 handler 字段/异常、忙与取消时序，以及经授权的快门恢复、原生长调用和进程生命周期。

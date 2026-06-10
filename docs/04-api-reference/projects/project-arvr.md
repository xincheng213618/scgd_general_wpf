# ProjectARVR

`Projects/ProjectARVR/` 是 AR/VR 显示设备综合光学测试项目包，运行时以 `ProjectARVR.dll` 加载。它是较早期的综合版本，核心是固定顺序的 PG 切图、FlowEngine 模板执行、ObjectiveTestResult 汇总和 Socket 返回。

## manifest 信息

| 字段 | 当前值 |
| --- | --- |
| `Id` | `ProjectARVR` |
| `version` | `1.0` |
| `dllpath` | `ProjectARVR.dll` |
| `requires` | `1.3.9.10` |

## 业务范围

ProjectARVR 面向 AR/VR 显示设备，覆盖白画面 FOV、亮度/颜色均匀性、中心色温/亮度、黑画面对比度、棋盘格对比度、水平/垂直 MTF、畸变和光轴偏角。

当前自动链路按源码实际执行到 `OpticCenter` 后汇总。枚举里虽然存在 `Ghost`、`DotMatrix`、白/黑画面瑕疵检测等类型，但当前 `SwitchPGCompleted()` 没有为这些后续类型执行模板，`IsTestTypeCompleted()` 会在下一个类型到 `Ghost` 时结束。

自动链路顺序：

```text
White2 -> White -> White1 -> Black -> Chessboard -> MTFH -> MTFV -> Distortion -> OpticCenter -> ProjectARVRResult
```

## 主要源码入口

| 文件/目录 | 作用 |
| --- | --- |
| `ARVRWindow.xaml(.cs)` | 主窗口、切图状态机、Flow 执行、结果解析和 Socket 回传 |
| `ProjectARVRConfig.cs` | 项目配置、模板编辑、初始化命令 |
| `ProjectARVRReuslt.cs` | 单流程结果实体，落库表 `ARVRReuslt` |
| `ObjectiveTestResult.cs` | 整机结果 DTO 和 CSV exporter |
| `ARVRRecipeConfig.cs` | 白/黑/棋盘格/MTF/畸变/光轴上下限 |
| `ObjectiveTestResultFix.cs` | 结果修正系数 |
| `ViewResultManager.cs` | 结果列表、落库、CSV 路径配置 |
| `Services/SocketControl.cs` | Socket 事件处理 |
| `PluginConfig/ProjectARVRPlugin.cs` | 功能启动器 |
| `PluginConfig/ProjectARVRMenu.cs` | 工具菜单入口 |

## 自动化业务链路

1. 外部系统发送 `ProjectARVRInit`。
2. `FlowInit.Handle()` 记录当前 `NetworkStream`，要求 ARVR 窗口已经打开；如果窗口不存在，返回 `Code=-3`。
3. `InitTest(SN)` 重置 `StepIndex`、`ObjectiveTestResult` 和 `CurrentTestType`；如果 SN 为空，生成 `SNxxxx`。
4. 项目返回 `SwitchPG`，要求外部系统切到第一个图案 `White2`。
5. 外部系统完成切图后发送 `SwitchPGCompleted`。
6. `SwitchPGCompleted()` 选择下一个 `ARVRTestType`，按关键字选择 Flow 模板并调用 `RunTemplate()`。
7. `RunTemplate()` 刷新 Flow，插入 `MeasureBatchModel`，用当前时间生成批次 code，并调用 `flowControl.Start(code)`。
8. Flow 完成后进入 `Processing()`，从批次结果中提取当前测试类型需要的算法结果和限值判定。
9. 若未到最后一个有效测试类型，项目再次发送 `SwitchPG`；到 `OpticCenter` 后调用 `TestCompleted()`。
10. `TestCompleted()` 生成 `ObjectiveTestResult`，导出 CSV，并通过 Socket 返回 `ProjectARVRResult`。

## Socket 事件

| 事件 | 方向 | 当前行为 |
| --- | --- | --- |
| `ProjectARVRInit` | 外部 -> 项目 | 初始化 SN 和状态；窗口不存在时返回 `ProjectARVR Wont Open` |
| `SwitchPG` | 项目 -> 外部 | 请求外部切换指定 `ARVRTestType` 图案 |
| `SwitchPGCompleted` | 外部 -> 项目 | 通知图案切换完成，项目启动对应 Flow |
| `ProjectARVRResult` | 项目 -> 外部 | 整机流程结束、超时或失败时返回 |
| `ProjectARVR` | 外部 -> 项目 | 校验 `request.Params` 是否存在于 `TemplateFlow.Params`，然后触发当前窗口 `RunTemplate()` |

维护 `ProjectARVR` 事件时要注意：当前源码只校验 `request.Params`，没有把 `FlowTemplate.SelectedValue` 切到这个 `flowParam`。如果现场希望“按请求参数运行指定模板”，需要补代码，不能只改协议文档。

## 测试类型和模板关键字

| 测试类型 | StepIndex | 模板关键字 | 主要结果 |
| --- | --- | --- | --- |
| `White2` | 1 | `WhiteFOV` | AA 区域、FOV |
| `White` | 2 | `White255` | POI、中心色温/亮度、亮度均匀性、颜色均匀性、FOV |
| `White1` | 3 | `White_calibrate` | 校准白画面中心色温/亮度 |
| `Black` | 4 | `Black` | 黑画面 POI、FOFO 对比度 |
| `Chessboard` | 5 | `Chessboard` | 棋盘格 POI、棋盘格对比度 |
| `MTFH` | 6 | `MTF_H` | 水平 MTF 中心、0.5F、0.8F 各点 |
| `MTFV` | 7 | `MTF_V` | 垂直 MTF 中心、0.5F、0.8F 各点 |
| `Distortion` | 8 | `Distortion` | 水平/垂直 TV 畸变 |
| `OpticCenter` | 9 | `OpticCenter` | X/Y 倾角、旋转角 |

这些关键字是运行时匹配依据，改模板命名会直接影响自动流程。

## 结果和报告

每个 Flow 会保存一个 `ProjectARVRReuslt`，整机结束时汇总成 `ObjectiveTestResult`。

| 结果对象 | 内容 |
| --- | --- |
| `ProjectARVRReuslt` | 单个测试类型的批次、Flow 状态、文件、结果 JSON、运行时间 |
| `ObjectiveTestResult` | 所有测试项的 `ObjectiveTestItem`，含测试值、上下限、PASS/FAIL |
| `Flow*TestReslut` | 标记各 Flow 是否成功解析 |
| `TotalResult` | 由各 Flow 标志和各测试项结果汇总 |

CSV 文件在 `TestCompleted()` 中生成：

```text
ObjectiveTestResults_{yyyyMMdd_HHmmss}.csv
```

保存目录来自 `ViewResultManager.Config.CsvSavePath`。注意当前 `TestCompleted()` 需要存在 Socket 客户端和 `SocketControl.Current.Stream`，否则会直接返回，不会走后续 CSV 和 Socket 返回。

## 关键配置

| 配置 | 作用 |
| --- | --- |
| `TemplateSelectedIndex` | 当前模板选择 |
| `StepIndex` | 当前自动流程步骤 |
| `TryCountMax` | Flow 超时重试次数 |
| `AllowTestFailures` | 失败后是否继续切图跑后续流程 |
| `ViewImageReadDelay` | 打开结果图前等待算法写图完成 |
| `TestTypeCompleted` | 配置里默认 `Ghost`，但当前实际完成判断来自 `IsTestTypeCompleted()` |
| `ResultSavePath`、`ResultSavePath1` | 配置保留字段；整机 CSV 实际用 `ViewResultManager.Config.CsvSavePath` |
| `ObjectiveTestResultFix` | 各测试项修正系数 |
| `ARVRRecipeConfig` | 亮度、颜色、FOV、对比度、MTF、畸变、光轴上下限 |

## 构建与交付

```powershell
dotnet build Projects/ProjectARVR/ProjectARVR.csproj -c Release -p:Platform=x64
Scripts\package_project.bat ProjectARVR --no-upload
```

## 交接验收表

| 验收项 | 操作 | 通过标准 |
| --- | --- | --- |
| 项目装载 | 检查 `manifest.json`、`ProjectARVR.dll` 和菜单入口 | 主程序能发现 `ProjectARVR`，工具菜单可打开 `ARVRWindow` |
| 初始化前置 | 先打开窗口，再发送 `ProjectARVRInit` | 不返回 `Code=-3`，项目记录 SN 并返回第一条 `SwitchPG` |
| 固定切图顺序 | 依次发送 `SwitchPGCompleted` | 顺序推进 `White2 -> White -> White1 -> Black -> Chessboard -> MTFH -> MTFV -> Distortion -> OpticCenter` |
| 模板匹配 | 检查每一步绑定的 Flow 模板名 | 模板名包含对应关键字，例如 `WhiteFOV`、`MTF_H`、`OpticCenter` |
| Flow 执行 | 每个图案完成切图后启动 Flow | 批次创建成功，Flow 完成后进入 `Processing()` |
| 结果汇总 | 跑完整机链路 | `ObjectiveTestResult` 中各测试项、上下限和 PASS/FAIL 被填充 |
| CSV 输出 | 检查 `ViewResultManager.Config.CsvSavePath` | 生成 `ObjectiveTestResults_{yyyyMMdd_HHmmss}.csv` |
| Socket 返回 | 完成到 `OpticCenter` | 外部收到 `ProjectARVRResult`，失败或超时时也有可解析返回 |
| 失败策略 | 分别设置 `AllowTestFailures=true/false` 做一次失败测试 | true 时尽量继续后续图案，false 时提前返回失败结果 |
| 交付包 | 执行 `Scripts\package_project.bat ProjectARVR --no-upload` | `.cvxp` 内含 DLL、manifest、README、CHANGELOG 和运行依赖 |

## 故障首查

| 现象 | 先查什么 |
| --- | --- |
| `ProjectARVRInit` 返回窗口未打开 | `ARVRWindow` 是否已打开，`FlowInit.Handle()` 是否拿到 `WindowInstance` |
| 初始化后没有 `SwitchPG` | SN 是否写入、`StepIndex` 是否重置、`CurrentTestType` 是否从 `White2` 开始 |
| `SwitchPGCompleted` 后不跑 Flow | 当前 `CurrentTestType`、模板关键字匹配、`FlowTemplate.SelectedValue` 和窗口实例 |
| 运行到 `Ghost` 前后行为不符合预期 | 当前自动链路只跑到 `OpticCenter`，`Ghost` 及后续枚举不是当前已交付自动能力 |
| Flow 成功但整机结果为空 | `Processing()` 是否从批次结果解析到对应算法 JSON，`Flow*TestReslut` 标志是否被设置 |
| CSV 没生成 | `TestCompleted()` 是否有有效 Socket stream，`CsvSavePath` 是否可写 |
| Socket 结果没有发出 | `SocketControl.Current.Stream`、客户端连接状态和 `ProjectARVRResult` 序列化 |
| `ProjectARVR` 事件按参数运行不符合预期 | 当前源码只校验 `request.Params` 是否存在，没有真正切换到该模板 |
| 失败后流程提前结束 | `AllowTestFailures`、`TryCountMax`、Flow 超时状态和当前测试项结果 |
| 现场结果和限值不一致 | `ARVRRecipeConfig`、`ObjectiveTestResultFix`、模板关键字和算法结果字段 |

## 交接注意事项

- 这个版本要求窗口已打开后 `ProjectARVRInit` 才能成功；如果要像 Lite 一样自动开窗，需要改 `FlowInit.Handle()`。
- 自动链路当前不跑 `Ghost` 及其后的枚举项，不要把枚举存在误写成现场能力已经完成。
- 模板选择依赖关键字，例如 `White255`、`MTF_H`、`OpticCenter`；改模板名时必须同步项目页和现场配置。
- `ProjectARVR` 事件当前没有真正切换到 `request.Params` 对应的模板，维护自动化对接时要优先确认这个行为是否满足客户侧预期。
- 如果 `AllowTestFailures=false`，失败会提前返回 `ProjectARVRResult`；如果为 true，会尽量继续后续图案并在结束时汇总。
- 新项目优先评估 `ProjectARVRPro` 或 `ProjectARVRLite`，不要把早期综合版本当成所有 AR/VR 流程的唯一标准。

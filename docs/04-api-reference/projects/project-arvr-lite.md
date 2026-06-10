# ProjectARVRLite

`Projects/ProjectARVRLite/` 是 AR/VR 轻量测试项目包，运行时以 `ProjectARVRLite.dll` 加载。它保留 AR/VR 核心测试和 Socket 切图协作，但比 ProjectARVR 更强调可配置测试类型、预处理和轻量交付。

## manifest 信息

| 字段 | 当前值 |
| --- | --- |
| `Id` | `ProjectARVRLite` |
| `version` | `1.0` |
| `dllpath` | `ProjectARVRLite.dll` |
| `requires` | `1.3.15.6` |

## 业务范围

ProjectARVRLite 用于快速 AR/VR 光学检测。它通过 `ProjectARVRLiteTestTypeConfig.json` 控制启用哪些测试类型，再由 Socket `SwitchPG` / `SwitchPGCompleted` 和 FlowEngine 模板完成切图、取图、算法、判定、CSV 和结果回传。

默认可见测试类型包括：

```text
W51, White, W25, Chessboard, MTFHV, Distortion, Ghost, OpticCenter, DotMatrix, WscreeenDefectDetection, BKscreeenDefectDetection
```

当前 `SwitchPGCompleted()` 只实现了 `W51`、`White`、`W25`、`Chessboard`、`MTFHV`、`Distortion`、`Ghost`、`OpticCenter` 的模板映射。`DotMatrix` 和白/黑画面瑕疵检测枚举存在，但自动链路没有对应分支；现场没有实现前应在测试类型配置里禁用。

## 主要源码入口

| 文件/目录 | 作用 |
| --- | --- |
| `ARVRWindow.xaml(.cs)` | 主窗口、测试类型状态机、Flow 执行、预处理、结果回传 |
| `ProjectARVRLiteConfig.cs` | 项目配置、模板编辑、初始化命令 |
| `TestTypeConfig.cs` | 测试类型启用/禁用配置，保存到 AppData |
| `ProjectARVRReuslt.cs` | 单流程结果实体，落库表 `ARVRReuslt` |
| `ObjectiveTestResult.cs` | 整机结果 DTO 和 CSV exporter |
| `ARVRRecipeConfig.cs` | W51、W255、W25、MTFHV、畸变、Ghost、光轴上下限 |
| `ObjectiveTestResultFix.cs` | 结果修正系数 |
| `Services/SocketControl.cs` | Socket 事件处理 |
| `EditTestTypeConfigWindow.xaml(.cs)` | 测试类型启用配置窗口 |
| `PluginConfig/ProjectARVRPlugin.cs` | 功能启动器 |
| `PluginConfig/ProjectARVRMenu.cs` | 工具菜单入口 |

## 自动化业务链路

1. 外部系统发送 `ProjectARVRInit`。
2. `FlowInit.Handle()` 记录当前 `NetworkStream`；如果窗口不存在，Lite 会自动创建并显示 `ARVRWindow`。
3. `InitTest(SN)` 重置 `StepIndex`、`ObjectiveTestResult` 和 `CurrentTestType`；如果 SN 为空，生成 `SNxxxxx`。
4. 项目读取 `TestTypeConfigManager.GetFirstEnabledTestType()`，返回第一条 `SwitchPG`。
5. 外部系统切图完成后发送 `SwitchPGCompleted`。
6. `SwitchPGCompleted()` 调用 `GetNextEnabledTestType(CurrentTestType)`，根据启用列表选择下一项，并按关键字选择 Flow 模板。
7. `RunTemplate()` 先执行 `PreProcessManager.ExecuteAsync(flowName, serialNumber, serverNodes)`；预处理失败时当前 Flow 直接失败。
8. 预处理通过后插入 `MeasureBatchModel`，调用 `flowControl.Start(code)`。
9. Flow 完成后解析批次算法结果，并在未完成所有启用测试时继续发送下一条 `SwitchPG`。
10. 最后 `TestCompleted()` 汇总 `ObjectiveTestResult`，按配置写 CSV，并返回 `ProjectARVRResult`。

## Socket 事件

| 事件 | 方向 | 当前行为 |
| --- | --- | --- |
| `ProjectARVRInit` | 外部 -> 项目 | 自动创建窗口、初始化 SN、返回第一条 `SwitchPG` |
| `SwitchPG` | 项目 -> 外部 | 请求切到指定 `ARVR1TestType` |
| `SwitchPGCompleted` | 外部 -> 项目 | 通知切图完成，项目执行下一条启用测试 |
| `ProjectARVRResult` | 项目 -> 外部 | 测试结束、失败或超时时返回 |
| 空白事件名 `"  "` | 外部 -> 项目 | 旧的直接运行入口；校验 `request.Params` 存在后调用当前窗口 `RunTemplate()` |

最后一条空白事件名是源码现状，不建议继续扩展；新对接优先使用 `ProjectARVRInit` / `SwitchPGCompleted`。

## 测试类型和模板关键字

| 测试类型 | StepIndex | 模板关键字 | 主要结果 |
| --- | --- | --- | --- |
| `W51` | 1 | `White51` | W51 FOV |
| `White` | 2 | `White255_Ghost_Test` | W255 中心亮度/色度、亮度均匀性、颜色均匀性、Ghost1 |
| `W25` | 3 | `White25` | W25 中心亮度/色度 |
| `Chessboard` | 4 | `Chessboard` | 棋盘格 POI 和对比度 |
| `MTFHV` | 5 | `MTF_HV` | 水平/垂直 MTF 中心、0.4F、0.8F 各点 |
| `Distortion` | 6 | `Distortion` | 水平/垂直 TV 畸变 |
| `Ghost` | 7 | `White_Ghost_Test` | 独立 Ghost 测试 |
| `OpticCenter` | 8 | `OpticCenter` | Opt center 与 Image center 的 X/Y 倾角和旋转角 |

`TestTypeConfigManager` 配置文件位置：

```text
%AppData%\ColorVision\Config\ProjectARVRLiteTestTypeConfig.json
```

如果启用了没有模板分支的测试类型，自动链路可能反复请求同一类图案，交付前必须检查该配置。

## 结果和报告

`ObjectiveTestResult` 汇总项比 ProjectARVR 更偏轻量和产品化：

| 测试组 | 结果字段 |
| --- | --- |
| W51 | `W51HorizontalFieldOfViewAngle`、`W51VerticalFieldOfViewAngle`、`W51DiagonalFieldOfViewAngle` |
| W255 | `W255CenterLunimance`、CIE 1931 x/y、CIE 1976 u/v、CCT、亮度/颜色均匀性、`Ghost1` |
| W25 | `W25CenterLunimance`、CIE 1931 x/y、CIE 1976 u/v |
| Chessboard | `ChessboardContrast` |
| MTFHV | 水平/垂直 MTF 的中心、0.4F、0.8F 各点 |
| Distortion | `HorizontalTVDistortion`、`VerticalTVDistortion` |
| Ghost | `Ghost` |
| OpticCenter | `OptCenterXTilt`、`OptCenterYTilt`、`OptCenterRotation`、`ImageCenterXTilt`、`ImageCenterYTilt`、`ImageCenterRotation` |

`TestCompleted()` 中的整机总结果会检查：

```text
FlowW51TestReslut
FlowWhiteTestReslut
FlowW25TestReslut
FlowChessboardTestReslut
FlowMTFHVTestReslut
FlowDistortionTestReslut
FlowOpticCenterTestReslut
```

源码里没有单独的 `FlowGhostTestReslut` 总结果标志；Ghost 数据可被解析，但整机 `TotalResult` 的 Flow 标志汇总没有单独把 Ghost 标志纳入。维护 Ghost 流程时要先确认客户是否要求它影响总结果。

CSV 输出受 `ViewResultManager.Config.IsSaveCsv` 控制；文件名格式：

```text
TestResults_{SN}_{yyyyMMdd_HHmmss}_.csv
```

如果 `SaveByDate` 打开，会先按 `yyyy-MM-dd` 建日期目录。结果图保存还会受 `SaveImageReusltDelay`、`CodeUseSN`、`CodeDateFormat` 等 `ViewResultManager.Config` 项影响。

## 关键配置

| 配置 | 作用 |
| --- | --- |
| `TryCountMax` | Flow 超时重试次数 |
| `AllowTestFailures` | 失败后是否继续后续测试 |
| `StepIndex` | 当前测试步骤显示 |
| `SNlocked` | 锁定 SN，防止流程中被改写 |
| `TemplateSelectedIndex` | 当前模板选择 |
| `TestTypeConfigManager` | 决定自动链路启用哪些测试类型 |
| `ARVRRecipeConfig.IsEnabled` | Recipe 是否启用 |
| `ObjectiveTestResultFix` | 各项结果修正系数 |
| `ViewResultManager.Config.IsSaveCsv` | 是否输出整机 CSV |
| `ViewResultManager.Config.SaveByDate` | CSV/图像是否按日期分目录 |

## 与 ProjectARVR 的区别

| 对比项 | ProjectARVR | ProjectARVRLite |
| --- | --- | --- |
| 窗口 | `ProjectARVRInit` 要求窗口已打开 | `ProjectARVRInit` 可自动创建窗口 |
| 测试顺序 | 固定枚举顺序，跑到 `OpticCenter` 后结束 | 由 `ProjectARVRLiteTestTypeConfig.json` 决定 |
| MTF | `MTFH`、`MTFV` 分开 | `MTFHV` 合并 |
| 预处理 | 主链路未见独立预处理 | `PreProcessManager.ExecuteAsync` |
| CSV | `ObjectiveTestResults_{time}.csv` | `TestResults_{SN}_{time}_.csv`，可按日期分目录 |
| 后续测试类型 | 枚举保留但自动链路未跑 | 配置里可能启用未实现分支，交付前必须校验 |

## 构建与交付

```powershell
dotnet build Projects/ProjectARVRLite/ProjectARVRLite.csproj -c Release -p:Platform=x64
Scripts\package_project.bat ProjectARVRLite --no-upload
```

## 交接验收表

| 验收项 | 操作 | 通过标准 |
| --- | --- | --- |
| 项目装载 | 检查 `manifest.json`、`ProjectARVRLite.dll` 和菜单入口 | 主程序能发现项目包，工具菜单可打开 `ARVRWindow` |
| 自动开窗 | 窗口未打开时发送 `ProjectARVRInit` | Lite 自动创建窗口、锁定 SN，并返回第一条 `SwitchPG` |
| 测试类型配置 | 打开 `ProjectARVRLiteTestTypeConfig.json` 或配置窗口 | 未实现的 `DotMatrix`、白/黑瑕疵检测保持禁用，启用项顺序符合现场方案 |
| 切图链路 | 按启用项依次发送 `SwitchPGCompleted` | 每个启用项只请求一次，流程能走到最后一个启用测试 |
| 预处理链路 | 运行一个需要预处理的 Flow | `PreProcessManager.ExecuteAsync(...)` 成功后才启动 Flow，失败时能给出失败结果 |
| 模板匹配 | 检查启用项对应模板名 | 模板包含 `White51`、`White255_Ghost_Test`、`MTF_HV` 等关键字 |
| 结果汇总 | 跑完整机测试 | `ObjectiveTestResult` 的 W51/W255/W25/MTF/Distortion/Ghost/OpticCenter 字段填充 |
| CSV/图像输出 | 打开 `IsSaveCsv` 和 `SaveByDate` 后运行 | 生成 `TestResults_{SN}_{yyyyMMdd_HHmmss}_.csv`，日期目录和图像保存符合配置 |
| 失败策略 | 模拟 Flow 失败或预处理失败 | `AllowTestFailures` 和 `TryCountMax` 行为符合交付预期 |
| 交付包 | 执行 `Scripts\package_project.bat ProjectARVRLite --no-upload` | `.cvxp` 内含 DLL、manifest、README、CHANGELOG 和默认配置说明 |

## 故障首查

| 现象 | 先查什么 |
| --- | --- |
| `ProjectARVRInit` 没有自动开窗 | 插件是否装载、`FlowInit.Handle()` 是否能创建 `ARVRWindow`、窗口单例是否异常 |
| 初始化后没有第一条 `SwitchPG` | 是否至少有一个启用测试类型，`ProjectARVRLiteTestTypeConfig.json` 是否损坏 |
| 反复请求同一图案 | 是否启用了没有实现分支的测试类型，`GetNextEnabledTestType()` 是否找不到下一个有效项 |
| 预处理失败 | `PreProcessManager`、当前 Flow 名称、`CVBaseServerNode` 和预处理服务返回 |
| Flow 找不到模板 | 模板名关键字、`TemplateFlow.Params`、当前 `TemplateSelectedIndex` |
| Ghost 数据有但总结果不受影响 | 当前没有单独 `FlowGhostTestReslut` 汇总标志，先确认客户是否要求 Ghost 参与 `TotalResult` |
| CSV 没生成 | `ViewResultManager.Config.IsSaveCsv`、`CsvSavePath`、`SaveByDate` 和目录权限 |
| SN 被改写或无法输入 | `SNlocked` 状态、初始化流程和空 SN 自动生成逻辑 |
| 旧空白事件名不稳定 | 新对接不要继续扩展空白事件名 handler，优先走 `ProjectARVRInit` / `SwitchPGCompleted` |
| 现场结果和限值不一致 | `ARVRRecipeConfig.IsEnabled`、`ObjectiveTestResultFix`、测试类型配置和模板输出字段 |

## 交接注意事项

- 交付前检查 `ProjectARVRLiteTestTypeConfig.json`，禁用没有模板分支的 `DotMatrix`、白画面瑕疵检测、黑画面瑕疵检测，除非已经补实现。
- 模板命名必须包含当前关键字，例如 `White51`、`White255_Ghost_Test`、`MTF_HV`；否则自动链路找不到 Flow。
- `PreProcessManager` 失败会在 Flow 启动前直接失败，排查时要看预处理节点和 `CVBaseServerNode`。
- `Ghost` 有独立解析逻辑，但没有单独的 `FlowGhostTestReslut` 汇总标志；客户若要求 Ghost 影响总判定，需要补业务逻辑。
- 旧的空白事件名 socket handler 不适合作为新协议扩展点。

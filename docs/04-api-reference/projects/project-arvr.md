# ProjectARVR

`Projects/ProjectARVR/` 是早期 AR/VR 光学测试项目包，运行时以 `ProjectARVR.dll` 加载。核心链路是固定 PG 切图顺序、FlowEngine 模板执行、`ObjectiveTestResult` 汇总和 Socket 返回。

## 先查什么

| 现象 | 第一检查点 |
| --- | --- |
| `ProjectARVRInit` 返回窗口未打开 | `ARVRWindow` 是否已打开，`FlowInit.Handle()` 是否拿到 `WindowInstance` |
| 初始化后没有 `SwitchPG` | SN、`StepIndex`、`CurrentTestType` 是否从 `White2` 重置 |
| `SwitchPGCompleted` 后不跑 Flow | 当前测试类型、模板关键字、`FlowTemplate.SelectedValue` |
| 到 Ghost 前后异常 | 当前自动链路只跑到 `OpticCenter`，Ghost 及后续枚举不是已交付自动链 |
| Flow 成功但整机结果为空 | `Processing()` 是否解析到算法 JSON，`Flow*TestReslut` 标志是否设置 |
| CSV 没生成 | `TestCompleted()` 是否有有效 Socket stream，`CsvSavePath` 是否可写 |
| `ProjectARVR` 事件没按参数跑 | 源码只校验 `request.Params` 存在，没有真正切换到该模板 |

## 自动链路

当前源码实际执行到 `OpticCenter` 后汇总：

```text
White2 -> White -> White1 -> Black -> Chessboard -> MTFH -> MTFV -> Distortion -> OpticCenter -> ProjectARVRResult
```

枚举里虽然存在 `Ghost`、`DotMatrix`、白/黑画面瑕疵检测等类型，但当前 `SwitchPGCompleted()` 没有为这些后续类型执行模板，`IsTestTypeCompleted()` 会在下一个类型到 `Ghost` 时结束。

## Socket 事件

| 事件 | 方向 | 行为 |
| --- | --- | --- |
| `ProjectARVRInit` | 外部 -> 项目 | 初始化 SN 和状态；窗口不存在返回 `Code=-3` |
| `SwitchPG` | 项目 -> 外部 | 请求外部切到指定 `ARVRTestType` |
| `SwitchPGCompleted` | 外部 -> 项目 | 通知切图完成，项目启动对应 Flow |
| `ProjectARVRResult` | 项目 -> 外部 | 整机结束、超时或失败时返回 |
| `ProjectARVR` | 外部 -> 项目 | 仅校验 `request.Params` 是否存在于 `TemplateFlow.Params`，再触发当前窗口 `RunTemplate()` |

## 测试类型和关键字

| 测试类型 | StepIndex | 模板关键字 | 主要结果 |
| --- | --- | --- | --- |
| `White2` | 1 | `WhiteFOV` | AA 区域、FOV |
| `White` | 2 | `White255` | POI、中心色温/亮度、均匀性、FOV |
| `White1` | 3 | `White_calibrate` | 校准白画面中心色温/亮度 |
| `Black` | 4 | `Black` | 黑画面 POI、FOFO 对比度 |
| `Chessboard` | 5 | `Chessboard` | 棋盘格 POI、棋盘格对比度 |
| `MTFH` | 6 | `MTF_H` | 水平 MTF |
| `MTFV` | 7 | `MTF_V` | 垂直 MTF |
| `Distortion` | 8 | `Distortion` | 水平/垂直 TV 畸变 |
| `OpticCenter` | 9 | `OpticCenter` | X/Y 倾角、旋转角 |

模板名关键字是运行时匹配依据，改名会直接影响自动流程。

## 结果、配置和文件

| 类别 | 入口 | 说明 |
| --- | --- | --- |
| 主流程 | `ARVRWindow.xaml.cs` | 固定切图、启动 Flow、汇总整机结果 |
| Socket | `Services/SocketControl.cs` | `ProjectARVRInit`、`SwitchPGCompleted`、`ProjectARVRResult` |
| 配置 | `ProjectARVRConfig.cs`、`ARVRRecipeConfig.cs` | SN、失败策略、亮度、颜色、FOV、对比度、MTF、畸变、光轴上下限 |
| 单项结果 | `ProjectARVRReuslt` | 单 Flow 结果实体，落库表 `ARVRReuslt` |
| 整机结果 | `ObjectiveTestResult` | 整机结果 DTO 和 CSV exporter |
| 成功标志 | `Flow*TestReslut`、`TotalResult` | 各 Flow 解析标志和测试项汇总 |
| 修正系数 | `ObjectiveTestResultFix` | 测试项修正系数 |
| CSV | `ViewResultManager.Config.CsvSavePath` | 整机 CSV 输出目录 |

CSV 文件名为 `ObjectiveTestResults_{yyyyMMdd_HHmmss}.csv`。注意当前 `TestCompleted()` 需要有效 Socket 客户端和 `SocketControl.Current.Stream`。

## 版本和构建

`manifest.json` 当前为 `Id=ProjectARVR`、`version=1.0`、`dllpath=ProjectARVR.dll`、`requires=1.3.9.10`。构建用 `dotnet build Projects/ProjectARVR/ProjectARVR.csproj -c Release -p:Platform=x64`，打包用 `Scripts\package_project.bat ProjectARVR`。

## 检查表

| 验收项 | 通过标准 |
| --- | --- |
| 项目装载 | 主程序发现 `ProjectARVR`，工具菜单能打开 `ARVRWindow` |
| 初始化 | 窗口已打开后发送 `ProjectARVRInit`，返回第一条 `SwitchPG` |
| 切图顺序 | 依次推进到 `OpticCenter` |
| 模板匹配 | 每步模板名包含对应关键字 |
| Flow 执行 | 每次切图完成后创建批次并进入 `Processing()` |
| 结果汇总 | `ObjectiveTestResult` 填充测试项、上下限、PASS/FAIL |
| CSV 输出 | 目标目录生成整机 CSV |
| Socket 返回 | 外部收到 `ProjectARVRResult` |
| 失败策略 | `AllowTestFailures=true/false` 行为符合配置 |

## 边界

- 这个版本要求 `ARVRWindow` 已打开后初始化才能成功。
- 自动链路不跑 `Ghost` 及其后续枚举项。
- `ProjectARVR` 事件当前没有真正切换到 `request.Params` 对应模板。
- 新项目优先评估 `ProjectARVRPro` 或 `ProjectARVRLite`，不要把早期综合版本当成唯一标准。

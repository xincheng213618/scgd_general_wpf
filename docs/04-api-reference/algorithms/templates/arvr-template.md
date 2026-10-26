---
knowledge_id: "algorithms.arvr"
knowledge_type: "reference"
status: "current"
summary: "ARVR 远端模板与请求对应关系、结果版本及 SFR 曲线与 CSV 范围；本地条纹 MTF 另有独立主题。"
aliases: ["CV_Ali_calcMtf", "ARVR算法", "MTF SFR FOV模板对应哪个结果", "SFR2.0", "MTF2.0", "FOV2.0", "Ghost2.0", "Ghost1.0", "畸变2.0", "StereoFusion", "SFR寻边", "ARVR屏幕缺陷检测", "SFR曲线", "SFR导出CSV", "MTF@Freq", "Freq@MTF", "AlgorithmARVRNode", "TemplateMTF2", "ViewHandleSFR2", "Sfr2PlotWindow"]
code_paths: ["Engine/ColorVision.Engine/Templates/Jsons/SFR2", "Engine/ColorVision.Engine/Templates/Jsons/Ghost2", "Engine/ColorVision.Engine/Templates/Jsons/MTF2", "Engine/ColorVision.Engine/Templates/Jsons/FOV2", "Engine/ColorVision.Engine/Templates/Jsons/Distortion2", "Engine/ColorVision.Engine/Templates/Jsons/BinocularFusion", "Engine/ColorVision.Engine/Templates/Jsons/SFRFindROI", "Engine/ColorVision.Engine/Templates/Jsons/FindCross", "Engine/ColorVision.Engine/Templates/Jsons/DetectScreenDefects", "Engine/ColorVision.Engine/Services/Devices/Algorithm/JsonDisplayAlgorithmBase.cs", "Engine/ColorVision.Engine/FlowProcessing/Nodes/Compatibility/Algorithm/AlgorithmARVRNode.cs", "Engine/FlowEngineLib/Base/CVBaseServerNode.cs", "Engine/ColorVision.Engine/FlowProcessing/Editor/NodeConfiguration/AlgorithmNodeConfigurators.cs", "Engine/ColorVision.Engine/FlowProcessing/Editor/NodeConfiguration/OLEDNodeConfigurators.cs", "Engine/ColorVision.Engine/Services/ResultHandleRegistry.cs", "Engine/ColorVision.Engine/Services/Devices/Algorithm/Views/AlgorithmView.xaml.cs", "Engine/ColorVision.Engine/Services/Results/AlgorithmResultDataSaver.cs", "Engine/cvColorVision/MQTTMessageLib/Algorithm/MQTTAlgorithmEventEnum.cs"]
test_paths: ["Test/ColorVision.UI.Tests/FindCrossResultOverlayTests.cs"]
related: ["algorithms.index", "algorithms.json-templates", "algorithms.template-menus", "algorithms.find-cross", "algorithms.grid-distortion", "engine.results", "algorithms.stripe-mtf"]
---

# ARVR 算法与模板

ARVR 远端入口通过算法服务计算，宿主负责选择模板、发送请求和展示结果。ImageView 与 Flow 另有共用自研算法的本地条纹 MTF 入口，直接编辑参数，不选择数据库算法模板。本页用于选择手动入口或流程算子、核对模板与结果版本，以及查看 SFR 曲线。各算法使用自己的参数模型；传统模板、JSON 模板和 POI 模板不能互换。

## 手动运行与模板对应关系

1. 在算法设备的通用手动面板中选择下表入口。大部分位于 **ARVR** 分组，**SFR寻边** 位于 **Json** 分组。
2. 选择对应参数模板；需要编辑时使用模板旁的编辑命令。保存与创建步骤见[模板编辑入口](./template-menu-entries.md)和 [JSON 模板](./json-templates.md)。
3. 按算法配置关注点或 ROI，并设置算法服务可读取的图像路径。界面的模板、非空路径检查不证明文件在服务端可读。
4. 点击 **计算**，结合本次请求、服务返回和历史结果判断完成状态。创建 `MsgRecord` 表示已建立请求记录并发起发送，不代表计算或落库成功。

下表中的编码和字典号属于模板身份，事件名属于请求协议；参数模板使用 JSON 模型。

| 手动入口 | 参数模板；编码 / 字典号 | 请求事件 | 结果处理器及额外版本条件 |
| --- | --- | --- | --- |
| ARVR → SFR2.0 | `TemplateSFR2`；`SFR` / `49` | `SFR` | `ViewHandleSFR2`；`Version == "2.0"` |
| ARVR → Ghost2.0 | `TemplateGhostQK`；`ghost` / `38` | `Ghost` | `ViewHandleGhostQK`；`Version == "2.0"` |
| ARVR → MTF2.0 | `TemplateMTF2`；`MTF` / `48` | `MTF` | `ViewHandleMTF2`；`Version == "2.0"` |
| ARVR → FOV2.0 | `TemplateDFOV`；`FOV` / `39` | `FOV` | `ViewHandleDFOV`；`Version == "2.0"` |
| ARVR → StereoFusion | `TemplateBinocularFusion`；`ARVR.BinocularFusion` / `35` | `ARVR.BinocularFusion` | `ViewHandleBinocularFusion` |
| Json → SFR寻边 | `TemplateSFRFindROI`；`ARVR.SFR.FindROI` / `36` | `ARVR.SFR.FindROI` | `ViewHandleSFRFindROI` |
| ARVR → FindCross | `TemplateFindCross`；`FindCross` / `45` | `FindCross` | `ViewHandleFindCross`；`Version == "1.0"` |
| ARVR → 屏幕缺陷检测 | `TemplateDetectScreenDefects`；`ARVR.DetectScreenDefects` / `58` | `ARVR.DetectScreenDefects` | `ViewHandleDetectScreenDefects` |

表中 FindCross 是远端算法入口；图像编辑器的原生定位及诊断模式见[本地十字定位](../detectors/find-cross.md)。SFR2.0 手动请求使用事件 `SFR`。

### POI 与版本字段

- **SFR2.0、MTF2.0、FindCross、SFR寻边**：配置界面有关注点模板，但 `JsonDisplayAlgorithmBase.Execute()` 只统一校验主模板与图像输入；各自仅在辅助模板选择有效时加入 `POITemplateParam`。不能把“有选择器”当作“发送前必填检查”，服务端要求需按对应协议确认。
- **屏幕缺陷检测**：辅助选择器标为 **ROI**，使用 POI 模板；未有效选择时仍发送 `POITemplateParam = { ID: -1, Name: null }`。请求还含 `OutputFileName`、`BufferLen`、`IsInversion = false`、`Color = 1`、`Channel = 1`；新配置输出文件名为 `result.json`，缓存大小为 `1024`。
- **SFR2.0、MTF2.0、FOV2.0、Ghost2.0**：手动请求明确带 `Params.Version = "2.0"`。Ghost2.0 还发送 `CIEFileName`。模板编码相同不代表请求参数可以互换。

这些请求通过 `TemplateParam` 引用模板身份，不在该字段中展开完整参数内容。修改模板后应确认保存成功，再核对服务实际读取的模板。

## Flow 接入

在流程编辑器中配置 **ARVR算法** 节点，通过 **算子** 选择算法、**参数模板** 选择参数。`AlgorithmARVRNode` 设置 `operatorCode`，`AlgorithmARVRNodeConfigurator` 随算法变化刷新模板面板。

| Flow 算子 | `operatorCode` | 参数模板 |
| --- | --- | --- |
| MTF | `MTF` | `TemplateMTF2` |
| SFR | `SFR` | `TemplateSFR2` |
| FOV | `FOV` | `TemplateDFOV` |
| 畸变 | `Distortion` | `TemplateDistortion2`（`distortion` / 字典 `40`）绑定 `TempName` |
| 双目融合 | `ARVR.BinocularFusion` | `TemplateBinocularFusion` |
| SFR_FindROI | `ARVR.SFR.FindROI` | `TemplateSFRFindROI` |
| 十字计算 | `FindCross` | `TemplateFindCross` |
| 屏幕缺陷检测 | `ARVR.DetectScreenDefects` | `TemplateDetectScreenDefects` |

**POI模板** 是节点的公共属性行，由 `PoiTemplatePropertiesEditor` 编辑，对所有算子都存在。

ARVR算法、通用算法和 AI算法2 的 SFR 参数选择器均使用 SFR2.0 JSON 模板。已有流程保存的旧 SFR 模板名称不会自动迁移，应重新选择 SFR2.0 模板并保存流程；字典号 `9` 的 Gamma 模板不能直接作为字典号 `49` 的 JSON 模板使用。

ARVR.Ghost算法与通用算法的鬼影参数选择器使用 `TemplateGhostQK`。为迁移已有流程，需要重新选择字典号 `38` 的 Ghost2.0 JSON 模板，并核对 CIE 输入；旧字典号 `7` 的 Ghost1.0 模板不会自动转换。历史列表不按 Ghost 版本筛选；明细展示和导出由 Ghost2.0 处理器按 `Version == "2.0"` 匹配。

### 本地条纹 MTF

本地 **MTF计算(V2)** 与 ImageView 共用原始灰度条纹对比度算法，不读取数据库算法模板。输入、取样公式、四部定位、输出单位和结果兼容集中在[本地条纹 MTF](../detectors/stripe-mtf.md)。

### 公共请求字段

新节点默认算子为 `MTF`、颜色为 `GREEN`、输出文件为 `result.json`、缓存大小为 `1024`。流程请求使用 `AlgorithmParam_ROI`，由节点及 `CVBaseServerNode` 填充：

| 字段 | 来源与限制 |
| --- | --- |
| `TemplateParam` | `BuildTemp()` 使用节点模板 ID、名称；未解析 ID 初值为 `-1` |
| `POITemplateParam` | 对所有算子创建 `{ ID: -1, Name: POITempName }`，不是手动界面解析后的 POI ID |
| `ImgFileName`、`FileType` | 非空节点图像路径才填充；空路径不自动补成上一步图像路径 |
| `Color`、`Channel` | 图像助手设置节点 `Color`，没有同步赋值 `Channel`；后者保留 DTO 初值 `GREEN` |
| `MasterId`、`MasterValue`、`MasterResultType` | 优先读取连接的上一步服务节点响应，否则尝试开始节点同名数据；有引用字段不证明对应结果有效 |
| `SMUData` | 仅从开始节点的 `SMUResult` 读取并转换，没有值则为 `null` |
| `OutputFileName`、`BufferLen`、`IsInversion` | 前两项取节点配置，反转固定为 `false`；文件名存在不代表文件已生成 |

这条 Flow 请求模型没有手动 2.0 入口的 `Params.Version`、`CIEFileName` 字段。手动成功而流程失败时，应比对完整请求及服务协议，不能仅比较事件名或模板名称。图像路径、上一步结果引用、SMU 数据也应分别核对。

## 结果版本与处理器选择

结果处理器先匹配 `ViewResultAlg.ResultType`，再执行各自的 `CanHandle1` 条件。上表标注的版本是**返回结果的 `Version`**；JSON 模板、请求版本和返回版本是不同层次，选择 JSON 模板不保证结果一定进入 V2 处理器。三个 `ARVR.*` 事件对应的结果枚举为 `ARVR_BinocularFusion`、`ARVR_SFR_FindROI`、`ARVR_DetectScreenDefects`。

`ViewHandleDistortion2` 仅接受 2.0 结果。旧模板、调试和结果读取入口均已淘汰；已有远端流程引用不会自动改写，应重新选择畸变2.0模板或改用本地节点。数据库旧记录不会删除，但不再提供专用明细显示、叠加和 CSV 导出。V2 无明细失败记录显示失败原因和参数；成功记录使用一条 JSON 文件明细。[本地点阵畸变 V2](../detectors/grid-distortion-v2.md) 也通过此结构交给 ProjectARVRPro，定位后可选择 TV、九点口径；有效的相对光学估计自动写入既有光学字段，并标明未经独立标定。公共装载、显示和保存链路见 [Engine 结果展示](../../engine-components/result-handoff-chain.md)。

## SFR 曲线与 CSV

`ViewHandleSFR2` 只接收版本 `2.0` 的 SFR 结果，明细通过一条 `DetailCommon` 的 `ResultFileName` 指向 JSON 结果文件。文件包含关注点 `name`、各曲线 `id`、`frequency` 和 `domainSamplingData`；文件必须在宿主可读。

历史结果右键菜单可打开结果集和 SFR 预览窗口 `Sfr2PlotWindow`。窗口支持关注点、四边及水平/垂直平均曲线选择，以及按曲线名称查询 **MTF@Freq**、**Freq@MTF**。曲线打不开时检查返回版本、明细数量和结果文件路径，再检查 JSON 数据。

**保存数据列** 导出所有关注点的曲线采样，列为 `name,id,frequency,value`，每条曲线按两数组较短长度配对，不截断为 48 点。文件名为 `{Batch}{ResultType}.csv`；当前实现追加到同名文件，重复导出会重复写入表头和数据。

旧版专用结果读取、曲线窗口和 CSV 导出已淘汰；数据库旧记录保留，但不能通过 2.0 处理器读取旧明细。SFR2.0 和自研本地 SFR 的参数、结果链路保持独立。

## 源码与验证边界

手动适配器和结果处理器与模板位于同一算法目录；SFR 曲线及查询位于 `Templates/Jsons/SFR2/Sfr2PlotWindow.xaml.cs` 和 `Sfr2PlotControl.xaml.cs`。流程的入口是 `Engine/ColorVision.Engine/FlowProcessing/Nodes/Compatibility/Algorithm/AlgorithmARVRNode.cs`，面板配置位于 `Engine/ColorVision.Engine/FlowProcessing/Editor/NodeConfiguration/AlgorithmNodeConfigurators.cs`。本页讨论的是宿主请求与显示契约，不定义算法服务内部计算公式或客户项目判定标准。

`FindCrossResultOverlayTests.cs` 验证本地诊断数据与旧结果的叠图中心坐标选择，不验证远端 ARVR 服务或真实绘制。算法请求、返回版本、SFR 曲线和导出仍需用对应结果样例验证；测试文件存在不表示这些链路已通过端到端测试。

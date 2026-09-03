---
knowledge_id: "algorithms.arvr"
knowledge_type: "reference"
status: "current"
summary: "ARVR 手动算法与流程节点的模板、POI 和请求对应关系；说明结果版本匹配及 SFR 曲线、查询和两种 CSV 导出的数据范围。"
aliases: ["ARVR算法","MTF SFR FOV模板对应哪个结果","SFR1.0","MTF2.0","FOV2.0","畸变评价","畸变2.0","StereoFusion","SFR寻边","ARVR屏幕缺陷检测","SFR曲线","SFR导出CSV","MTF@Freq","Freq@MTF","AlgorithmARVRNode","TemplateMTF2","ViewHandleSFR","WindowSFR"]
code_paths: ["Engine/ColorVision.Engine/Templates/ARVR/SFR","Engine/ColorVision.Engine/Templates/ARVR/Ghost","Engine/ColorVision.Engine/Templates/ARVR/Distortion","Engine/ColorVision.Engine/Templates/Jsons/MTF2","Engine/ColorVision.Engine/Templates/Jsons/FOV2","Engine/ColorVision.Engine/Templates/Jsons/Distortion2","Engine/ColorVision.Engine/Templates/Jsons/BinocularFusion","Engine/ColorVision.Engine/Templates/Jsons/SFRFindROI","Engine/ColorVision.Engine/Templates/Jsons/FindCross","Engine/ColorVision.Engine/Templates/Jsons/DetectScreenDefects","Engine/ColorVision.Engine/Services/Devices/Algorithm/JsonDisplayAlgorithmBase.cs","Engine/FlowEngineLib/Algorithm/AlgorithmARVRNode.cs","Engine/FlowEngineLib/Base/CVBaseServerNode.cs","Engine/ColorVision.Engine/FlowProcessing/Editor/NodeConfiguration/AlgorithmNodeConfigurators.cs","Engine/ColorVision.Engine/Services/ResultHandleRegistry.cs","Engine/ColorVision.Engine/Services/Devices/Algorithm/Views/AlgorithmView.xaml.cs","Engine/ColorVision.Engine/Services/Results/AlgorithmResultDataSaver.cs","Engine/cvColorVision/MQTTMessageLib/Algorithm/MQTTAlgorithmEventEnum.cs"]
test_paths: ["Test/ColorVision.UI.Tests/AlgorithmNodeTemplateMappingTests.cs","Test/ColorVision.UI.Tests/FindCrossResultOverlayTests.cs"]
related: ["algorithms.index","algorithms.ghost","algorithms.json-templates","algorithms.template-menus","algorithms.find-cross","engine.results"]
---

# ARVR 算法与模板

ARVR 算法通过算法服务计算，宿主负责选择模板、发送请求和展示结果。本页用于选择手动入口或流程算子、核对模板与结果版本，以及查看 SFR 曲线。各算法使用自己的参数模型；传统模板、JSON 模板和 POI 模板不能互换。

## 手动运行与模板对应关系

1. 在算法设备的通用手动面板中选择下表入口。大部分位于 **ARVR** 分组，**SFR寻边** 位于 **Json** 分组。
2. 选择对应参数模板；需要编辑时使用模板旁的编辑命令。保存与创建步骤见[模板编辑入口](./template-menu-entries.md)和 [JSON 模板](./json-templates.md)。
3. 按算法配置关注点或 ROI，并设置算法服务可读取的图像路径。界面的模板、非空路径检查不证明文件在服务端可读。
4. 点击 **计算**，结合本次请求、服务返回和历史结果判断完成状态。创建 `MsgRecord` 表示已建立请求记录并发起发送，不代表计算或落库成功。

下表中的编码和字典号属于模板身份，事件名属于请求协议。SFR1.0、Ghost1.0 和畸变评价使用传统参数模型，其余使用 JSON 模型。

| 手动入口 | 参数模板；编码 / 字典号 | 请求事件 | 结果处理器及额外版本条件 |
| --- | --- | --- | --- |
| ARVR → SFR1.0 | `TemplateSFR`；`SFR` / `9` | `SFR` | `ViewHandleSFR` |
| ARVR → Ghost1.0 | `TemplateGhost`；`ghost` / `7` | `Ghost` | `ViewHandleGhost`；参数与叠图见[鬼影检测](../detectors/ghost-detection.md) |
| ARVR → MTF2.0 | `TemplateMTF2`；`MTF` / `48` | `MTF` | `ViewHandleMTF2`；`Version == "2.0"` |
| ARVR → FOV2.0 | `TemplateDFOV`；`FOV` / `39` | `FOV` | `ViewHandleDFOV`；`Version == "2.0"` |
| ARVR → 畸变评价 | `TemplateDistortionParam`；`distortion` / `10` | `Distortion` | `ViewHandleDistortion`；不排除 2.0 结果 |
| ARVR → 畸变2.0 | `TemplateDistortion2`；`distortion` / `40` | `Distortion` | `ViewHandleDistortion2`；`Version == "2.0"` |
| ARVR → StereoFusion | `TemplateBinocularFusion`；`ARVR.BinocularFusion` / `35` | `ARVR.BinocularFusion` | `ViewHandleBinocularFusion` |
| Json → SFR寻边 | `TemplateSFRFindROI`；`ARVR.SFR.FindROI` / `36` | `ARVR.SFR.FindROI` | `ViewHandleSFRFindROI` |
| ARVR → FindCross | `TemplateFindCross`；`FindCross` / `45` | `FindCross` | `ViewHandleFindCross`；`Version == "1.0"` |
| ARVR → 屏幕缺陷检测 | `TemplateDetectScreenDefects`；`ARVR.DetectScreenDefects` / `58` | `ARVR.DetectScreenDefects` | `ViewHandleDetectScreenDefects` |

表中 FindCross 是远端算法入口；图像编辑器的原生定位及诊断模式见[本地十字定位](../detectors/find-cross.md)。SFR 手动请求使用消息库常量 `Event_SFR_GetData`，其值也是 `SFR`。

### POI 与版本字段

- **SFR1.0**：执行前必须选中有效的 SFR 模板和 **关注点模板**，请求带两者的 ID、名称。
- **MTF2.0、FindCross、SFR寻边**：配置界面有关注点模板，但 `JsonDisplayAlgorithmBase.Execute()` 只统一校验主模板与图像输入；各自仅在辅助模板选择有效时加入 `POITemplateParam`。不能把“有选择器”当作“发送前必填检查”，服务端要求需按对应协议确认。
- **屏幕缺陷检测**：辅助选择器标为 **ROI**，使用 POI 模板；未有效选择时仍发送 `POITemplateParam = { ID: -1, Name: null }`。请求还含 `OutputFileName`、`BufferLen`、`IsInversion = false`、`Color = 1`、`Channel = 1`；新配置输出文件名为 `result.json`，缓存大小为 `1024`。
- **MTF2.0、FOV2.0、畸变2.0**：手动请求明确带 `Params.Version = "2.0"`。畸变2.0 还发送 `CIEFileName`；畸变评价不附带这一版本字段。模板编码相同不代表请求参数可以互换。

这些请求通过 `TemplateParam` 引用模板身份，不在该字段中展开完整参数内容。修改模板后应确认保存成功，再核对服务实际读取的模板。

## Flow 接入

在流程编辑器中配置 **ARVR算法** 节点，通过 **算子** 选择算法、**参数模板** 选择参数。`AlgorithmARVRNode` 设置 `operatorCode`，`AlgorithmARVRNodeConfigurator` 随算法变化刷新模板面板。

| Flow 算子 | `operatorCode` | 参数模板 |
| --- | --- | --- |
| MTF | `MTF` | `TemplateMTF2` |
| SFR | `SFR` | `TemplateSFR` |
| FOV | `FOV` | `TemplateDFOV` |
| 畸变 | `Distortion` | `TemplateDistortion2` 和 `TemplateDistortionParam` 两个选择器绑定同一个 `TempName` |
| 双目融合 | `ARVR.BinocularFusion` | `TemplateBinocularFusion` |
| SFR_FindROI | `ARVR.SFR.FindROI` | `TemplateSFRFindROI` |
| 十字计算 | `FindCross` | `TemplateFindCross` |
| 屏幕缺陷检测 | `ARVR.DetectScreenDefects` | `TemplateDetectScreenDefects` |

**POI模板** 是节点的公共属性行，由 `FlowPoiTemplateEditor` 编辑，对所有算子都存在。畸变的两个参数选择器共享同一名称，不是同时发送两套模板；运行前确认最终 `TempName`。

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

畸变存在匹配重叠：传统 `ViewHandleDistortion` 不限制版本，`ViewHandleDistortion2` 接受 2.0，两者可能同时命中。算法结果界面与数据保存器均取注册集合中第一个 `CanHandle1` 成功项，注册器没有显式 V2 优先级。排查时确认实际选中的处理器及结果模型，不能假定畸变2.0 必然选中 `ViewHandleDistortion2`。公共装载、显示和保存链路见 [Engine 结果展示](../../engine-components/result-handoff-chain.md)。

## SFR 曲线与 CSV

SFR 明细包含 ROI 坐标、`Pdfrequency` 频率数组和 `PdomainSamplingData` 响应数组。`ViewHandleSFR.Load()` 仅在 `ViewResults == null` 时读取该结果 ID 的数据库明细并建立 **分析** 菜单；已有集合会复用，不是每次打开都重新查库。

1. 选中 SFR 历史结果，在该行的右键菜单中选择 **分析**，打开 `WindowSFR`。
2. 使用 **选择数据线** 切换 ROI 明细。窗口按两数组较短长度配对，最多使用前 `48` 点；空数组不会形成有效曲线。
3. 输入频率后点击 **MTF@Freq**，在相邻采样点间线性插值；输入 MTF 后点击 **Freq@MTF**，查找首个从高于或等于阈值向低于阈值穿越的区间并插值。查询范围受当前窗口采样点限制；找不到区间不能视为测量值为零。
4. 用 **保存图表** 导出当前曲线图，或按下表选择 CSV 入口。

| 导出入口 | 数据范围与写入方式 |
| --- | --- |
| 曲线窗口 → 导出 CSV | 当前数据线、窗口实际使用的最多 48 对点，列为 `Frequency,MTF`；写入所选文件，覆盖同名内容 |
| 历史结果 → 保存数据列 | `ViewHandleSFR.SideSave()` 导出当前 `ViewResults` 的所有 SFR 明细；每条 ROI 及成对采样列分别输出，按最长数组展开，短数组缺项留空；没有 48 点截断。写入所选目录的 `{ResultType}_{Batch}.csv`，覆盖同名内容 |

因此两种 CSV 不必有相同的行列数。曲线打不开时先检查该结果是否加载明细、两个采样字段是否为可解析数组；导出内容不符时确认入口与当前结果集合，再检查结果 ID 和数据库记录。

## 源码与验证边界

手动适配器和结果处理器与模板位于同一算法目录；SFR 曲线及查询位于 `Templates/ARVR/SFR/WindowSFR.xaml.cs`。流程的入口是 `Engine/FlowEngineLib/Algorithm/AlgorithmARVRNode.cs`，面板配置位于 `Engine/ColorVision.Engine/FlowProcessing/Editor/NodeConfiguration/AlgorithmNodeConfigurators.cs`。本页讨论的是宿主请求与显示契约，不定义算法服务内部计算公式或客户项目判定标准。

`AlgorithmNodeTemplateMappingTests.cs` 只断言 ARVR 的 `POITempName` 解析到 `FlowPoiTemplateEditor`，没有覆盖所有算子切换及请求发送。`FindCrossResultOverlayTests.cs` 验证本地诊断数据与旧结果的叠图中心坐标选择，不验证远端 ARVR 服务或真实绘制。算法请求、返回版本、SFR 曲线和导出仍需用对应结果样例验证；测试文件存在不表示这些链路已通过端到端测试。

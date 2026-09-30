---
knowledge_id: "algorithms.arvr"
knowledge_type: "reference"
status: "current"
summary: "ARVR 远端模板与请求对应关系；ImageView 与 Flow 共用的自研条纹 MTF、直接配参及兼容结果契约，以及 SFR 曲线与 CSV 范围。"
aliases: ["条纹MTF","StripeMtfAnalyzer","ImageView MTF","H/V条纹","本地MTF","LocalMtfNode","CV_Ali_calcMtf","ARVR算法","MTF SFR FOV模板对应哪个结果","SFR1.0","MTF2.0","FOV2.0","畸变评价","畸变2.0","StereoFusion","SFR寻边","ARVR屏幕缺陷检测","SFR曲线","SFR导出CSV","MTF@Freq","Freq@MTF","AlgorithmARVRNode","TemplateMTF2","ViewHandleSFR","WindowSFR"]
code_paths: ["UI/ColorVision.ImageEditor/Algorithms/Mtf","UI/ColorVision.ImageEditor/EditorTools/Algorithms/Calculate/Mtf","Engine/ColorVision.Engine/PropertyEditor/LocalMtfConfigurationEditor.cs","Engine/ColorVision.Engine/FlowProcessing/Nodes/LocalMtfNode.cs","Engine/ColorVision.Engine/Services/Devices/Algorithm/LocalMtf","Engine/ColorVision.Engine/Templates/ARVR/SFR","Engine/ColorVision.Engine/Templates/ARVR/Ghost","Engine/ColorVision.Engine/Templates/ARVR/Distortion","Engine/ColorVision.Engine/Templates/Jsons/MTF2","Engine/ColorVision.Engine/Templates/Jsons/FOV2","Engine/ColorVision.Engine/Templates/Jsons/Distortion2","Engine/ColorVision.Engine/Templates/Jsons/BinocularFusion","Engine/ColorVision.Engine/Templates/Jsons/SFRFindROI","Engine/ColorVision.Engine/Templates/Jsons/FindCross","Engine/ColorVision.Engine/Templates/Jsons/DetectScreenDefects","Engine/ColorVision.Engine/Services/Devices/Algorithm/JsonDisplayAlgorithmBase.cs","Engine/ColorVision.Engine/FlowProcessing/Nodes/Compatibility/Algorithm/AlgorithmARVRNode.cs","Engine/FlowEngineLib/Base/CVBaseServerNode.cs","Engine/ColorVision.Engine/FlowProcessing/Editor/NodeConfiguration/AlgorithmNodeConfigurators.cs","Engine/ColorVision.Engine/Services/ResultHandleRegistry.cs","Engine/ColorVision.Engine/Services/Devices/Algorithm/Views/AlgorithmView.xaml.cs","Engine/ColorVision.Engine/Services/Results/AlgorithmResultDataSaver.cs","Engine/cvColorVision/MQTTMessageLib/Algorithm/MQTTAlgorithmEventEnum.cs"]
test_paths: ["Test/ColorVision.UI.Tests/StripeMtfLocatorTests.cs","Test/ColorVision.UI.Tests/AlgorithmNodeTemplateMappingTests.cs","Test/ColorVision.UI.Tests/FindCrossResultOverlayTests.cs"]
related: ["algorithms.index","algorithms.ghost","algorithms.json-templates","algorithms.template-menus","algorithms.find-cross","algorithms.grid-distortion","engine.results"]
---

# ARVR 算法与模板

ARVR 远端入口通过算法服务计算，宿主负责选择模板、发送请求和展示结果。ImageView 与 Flow 另有共用自研算法的本地条纹 MTF 入口，直接编辑参数，不选择数据库算法模板。本页用于选择手动入口或流程算子、核对模板与结果版本，以及查看 SFR 曲线。各算法使用自己的参数模型；传统模板、JSON 模板和 POI 模板不能互换。

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
| ARVR → 畸变评价 | `TemplateDistortionParam`；`distortion` / `10` | `Distortion` | `ViewHandleDistortion`；`Version != "2.0"` |
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

**POI模板** 是节点的公共属性行，由 `PoiTemplatePropertiesEditor` 编辑，对所有算子都存在。畸变的两个参数选择器共享同一名称，不是同时发送两套模板；运行前确认最终 `TempName`。

**本地条纹 MTF** 使用共用的 `StripeMtfAnalyzer`，运行不依赖供应商 MTF DLL。ImageView 的 **分析测量 → 清晰度与频域 → 条纹 MTF（H / V / 四部）** 与矩形右键菜单都可打开参数编辑器；主菜单使用画面上的矩形，没有矩形时使用整图。单独 H、V 图像使用测量矩形，四部模式每个矩形圈住一整组图案。参数窗口默认只显示“图案”和“高级设置”开关，计算使用当前配置值；展开高级设置后才显示计算方式、去噪、取样及输出单位。四部模式额外显示“四部定位”组，单独 H/V 隐藏该组；极值法隐藏“两端取样比例”，均值法才显示。收起高级设置或切换图案均保留已填写的值；开关只控制展示，不写入算法配置。自动定位重试不修改已保存的参考阈值或小框尺寸。ImageView 与节点参数窗口遵循相同规则。显示坐标按 DPI 转换为原图像素，旋转矩形拒绝测量。结果窗列出各框数值，四部模式另列 H、V、整体均值；叠图属于临时算法图层，切图或新请求使旧结果失效。此入口不需要设备或数据库。

Flow 的 `LocalMtfNode` 位于自定义节点 **MTF计算(V2)**。图像连接 `IN_IMG`，运行时布点结果连接 `IN_POI`，两个输入须属于同一批次且均到达。直接编辑 **算法参数**；参数 JSON 随流程保存，**结果名称**仅用于结果主表命名。没有运行时布点时，把 Start 接到 `IN_POI`，配置 **测量区域**，`0,0,0,0` 表示整图。优先借用方向变换完成的上游 RAW 内存帧，无内存帧时才读取历史图像。接受 8/16 位、1/3 通道以及有行步长的 RAW，不把 CIE 或显示伪彩图当作测量输入。POI 中心矩形和左上角矩形按原服务的单精度坐标及 `Convert.ToInt32` 规则转换；名称须非空且唯一，矩形完整位于图内。

横条纹、竖条纹都在测量框内计算 `(亮−暗)/(亮+暗)`，方向决定图案标记，不改变对比度公式。彩色输入先按 BGR 转灰度。均值法按像素计数舍弃最暗/最亮比例，再各取指定比例求均值；极值法取去噪后的两端点。直方图保留边界灰度中所需的像素个数，不因同值像素过多而整段丢弃；只处理 ROI，并复用直方图缓冲。纯黑、越界或取样不足会失败，不补零；有亮度的常量图输出零。原模板的额外 `sensorRatio` 校正及启用的 `mathMaskRect` 不受支持，导入时明确拒绝。

`pattern=5` 为四部横竖条纹：定位平面进行 5×5 高斯平滑、CLAHE、阈值分割和 15×15 椭圆闭运算，仅在满足最小面积、不贴边且能容纳四个取样框的完整候选中选择最大轮廓，过滤邻近图案的边缘残片与孤立小目标。参考阈值未定位到完整目标时，自动回退到未增强定位平面的 Otsu 分割，适应信号变暗及背景增强导致的误判；8 位图使用超出 255 的参考值时直接自动估算，不要求换算默认阈值。两次定位都找不到完整目标时提示圈住整组图案并留出边距。正常定位继续使用原处理路径，不增加用户参数。按轮廓质心及偏移量，沿四个对角方向放置测量框，编号为左上、右上、右下、左下；`distanceToRect` 是小框中心到目标中心的距离。测量仍使用未增强的原始灰度。`firstIsHor` 决定左上/右下归入 H 或 V。定位阈值 5000 为优先尝试的默认参考值；默认运行可自动重试，阈值仅在高级设置中供调整。`PercentageDisplay` 决定输出比例或百分数，下游判定限须使用同一单位。此测量不是斜边 SFR 曲线，也不输出 MTF50/MTF10。

成功结果保持类型 `MTF`、版本 `2.0`、一条含 `ResultFileName` 的 `DetailCommon` 明细及既有 JSON 字段：`result` 保存各矩形，四部模式另有 `resultChild`、`childRects`、`Average`、`horizontalAverage`、`verticalAverage`。`ViewHandleMTF2` 和客户解析可沿用此结构。自研算法版本、实际参数、ROI 和内存/文件来源记录在主表参数中。**结构兼容不代表数值逐位相同**：定位预处理及直方图端点处理可能与旧 DLL 有差异；本地极值法也不能假定等同于未公开的供应商 `CalcMethod=1`。替换生产流程前，应使用相同原图和 ROI 对照数值并核对判定限。

结果目录默认位于当前用户 `LocalAppData/ColorVision/Results/MTF`。先写文件，再事务保存主表与明细；数据库失败回滚并清理本次文件，持久化完成后发布 `local-flow` 通知并向后续节点输出结果 ID。计算失败不产生成功记录；业务上下限仍由客户流程处理。没有与内存帧对应的已保存图片时，历史结果可能没有原图。原有远端模板算法入口仍按上表运行。

`StripeMtfLocatorTests` 使用合成图验证四部定位对背景增强、位深、亮度和不合适参考阈值的处理，并检查缺角图案仍会失败、输入像素不会被修改。套件还保留节点模板映射和结果叠图等相邻契约，不包含供应商 DLL 对照或现场样本宿主。替换生产流程前应在获授权环境使用相同原图、ROI、参数和判定限对照自研算法与目标 DLL，并把读图、数据库、UI 和整套流程耗时分别记录。

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

传统 `ViewHandleDistortion` 排除 2.0，`ViewHandleDistortion2` 接受 2.0，两者按版本分开。V2 无明细失败记录显示失败原因和参数；成功记录使用一条 JSON 文件明细。[本地点阵畸变 V2](../detectors/grid-distortion-v2.md) 也通过此结构交给 ProjectARVRPro，定位后可选择 TV、九点口径；相对光学估计默认不发布到既有光学字段。公共装载、显示和保存链路见 [Engine 结果展示](../../engine-components/result-handoff-chain.md)。

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

手动适配器和结果处理器与模板位于同一算法目录；SFR 曲线及查询位于 `Templates/ARVR/SFR/WindowSFR.xaml.cs`。流程的入口是 `Engine/ColorVision.Engine/FlowProcessing/Nodes/Compatibility/Algorithm/AlgorithmARVRNode.cs`，面板配置位于 `Engine/ColorVision.Engine/FlowProcessing/Editor/NodeConfiguration/AlgorithmNodeConfigurators.cs`。本页讨论的是宿主请求与显示契约，不定义算法服务内部计算公式或客户项目判定标准。

`AlgorithmNodeTemplateMappingTests.cs` 只断言 ARVR 的 `POITempName` 解析到 `PoiTemplatePropertiesEditor`，没有覆盖所有算子切换及请求发送。`FindCrossResultOverlayTests.cs` 验证本地诊断数据与旧结果的叠图中心坐标选择，不验证远端 ARVR 服务或真实绘制。算法请求、返回版本、SFR 曲线和导出仍需用对应结果样例验证；测试文件存在不表示这些链路已通过端到端测试。

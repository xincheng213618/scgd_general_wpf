# ARVR 模板

ARVR 不是一个统一 schema，而是一组传统模板、JSON 模板、POI 模板和 Flow 节点共同组成的算法家族。维护时先确认当前链路走的是手动算法还是 Flow 节点，再看模板类型。

## 先查什么

| 问题 | 第一检查点 |
| --- | --- |
| MTF/SFR 结果点位不对 | 请求是否带 `POITemplateParam` |
| Flow 节点模板下拉不对 | `AlgorithmNodeConfigurators.cs` 的 ARVR 分支 |
| 手动运行正常、流程运行异常 | 比对手动 `Algorithm*.cs` 和 `AlgorithmARVRNode.getBaseEventData()` |
| FOV/Distortion 模板混乱 | 传统模板和 JSON V2 是否同时挂到同一 `TempName` |
| SFR 曲线打不开或导出异常 | `WindowSFR`、结果表采样数据和 `ViewHandleSFR` |
| Ghost overlay 不对 | 请求 `Color`、点阵行列和结果 handler |
| 双目融合/SFR_FindROI 找不到传统模板 | 它们走 JSON 模板，不在 `Templates/ARVR/` 传统目录里 |

## 模板矩阵

| 家族 | 模板 | 代码/字典 | 事件 | 结果入口 |
| --- | --- | --- | --- | --- |
| FOV | `TemplateFOV` | `Code = FOV`，`TemplateDicId = 6` | `Event_FOV_GetData` | `ViewHandleFOV` |
| Ghost | `TemplateGhost` | `Code = ghost`，`TemplateDicId = 7` | `Ghost` | `ViewHandleGhost` |
| MTF | `TemplateMTF` | `Code = MTF`，`TemplateDicId = 8` | `Event_MTF_GetData` | `ViewHandleMTF` |
| SFR | `TemplateSFR` | `Code = SFR`，`TemplateDicId = 9` | `Event_SFR_GetData` | `ViewHandleSFR`、`WindowSFR` |
| Distortion | `TemplateDistortionParam` | `Code = distortion`，`TemplateDicId = 10` | `Distortion` | `ViewHandleDistortion` |
| AOI | `TemplateAOIParam` | `Code = AOI`，`TemplateDicId = 12` | 主要作为参数配置 | 项目/AOI 链路消费 |
| BinocularFusion | `TemplateBinocularFusion` | `Code = ARVR.BinocularFusion`，`TemplateDicId = 35` | `ARVR.BinocularFusion` | `ViewHandleBinocularFusion` |
| SFR_FindROI | `TemplateSFRFindROI` | `Code = ARVR.SFR.FindROI`，`TemplateDicId = 36` | `ARVR.SFR.FindROI` | `ViewHandleSFRFindROI` |
| FindCross | `TemplateFindCross` | `Code = FindCross`，`TemplateDicId = 45` | `FindCross` | `ViewHandleFindCross` |

## 传统算法链

| 算法 | 关键点 |
| --- | --- |
| MTF | `AlgorithmMTF` 选择 `TemplateMTF` 和 `TemplatePoi`，请求必须带 `POITemplateParam` |
| SFR | `AlgorithmSFR` 选择 `TemplateSFR` 和 `TemplatePoi`，结果曲线在 `WindowSFR` |
| FOV | `AlgorithmFOV` 发布 `Event_FOV_GetData`，显示侧还会处理批次、Raw 和本地图像输入 |
| Distortion | `AlgorithmDistortion` 发布 `Distortion`，结果映射看 `ViewResultDistortion` |
| Ghost | `AlgorithmGhost` 附带 `Color`，发布 `Ghost` |

这些 `Algorithm*.cs` 多数是宿主和 MQTT 请求适配器，不是本地数值算法实现。结果层代码同样重要。

## Flow 接入

`AlgorithmARVRNode` 决定 `operatorCode`，`AlgorithmARVRNodeConfigurator` 决定属性面板挂哪些模板：

| Flow 算子 | `operatorCode` | 模板选择 |
| --- | --- | --- |
| MTF | `MTF` | `TemplateMTF` + `TemplatePoi` |
| SFR | `SFR` | `TemplateSFR` + `TemplatePoi` |
| FOV | `FOV` | `TemplateDFOV` + `TemplateFOV` |
| 畸变 | `Distortion` | `TemplateDistortion2` + `TemplateDistortionParam` |
| SFR_FindROI | `ARVR.SFR.FindROI` | `TemplateSFRFindROI` + `TemplatePoi` |
| 双目融合 | `ARVR.BinocularFusion` | `TemplateBinocularFusion` |
| 十字计算 | `FindCross` | `TemplateFindCross` + `TemplatePoi` 作为 ROI |

Flow 请求还会统一带 `BufferLen`、颜色通道、上一步图像参数和 SMU 结果。流程里异常时不能只看模板名。

## 结果 handler

| 结果类型 | Handler | 排查重点 |
| --- | --- | --- |
| `FOV` | `ViewHandleFOV` | 图像输入、角度/距离字段 |
| `Ghost` | `ViewHandleGhost` | 颜色通道、点阵数量、overlay |
| `MTF` | `ViewHandleMTF` | POI 点值、CSV、统计值 |
| `SFR` | `ViewHandleSFR`、`WindowSFR` | `Pdfrequency`、`PdomainSamplingData` 曲线 |
| `Distortion` | `ViewHandleDistortion`、`ViewResultDistortion` | 枚举映射、最终点阵 |
| `ARVR_BinocularFusion` | `ViewHandleBinocularFusion` | JSON V2 结果 |
| `ARVR_SFR_FindROI` | `ViewHandleSFRFindROI` | ROI 检出结果 |
| `FindCross` | `ViewHandleFindCross` | 十字计算结果 |

## 验收

| 场景 | 通过标准 |
| --- | --- |
| 手动 MTF/SFR | 请求同时包含参数模板和 POI 模板，结果能被对应 handler 接住 |
| Flow ARVR 节点 | 切换算法类型后，模板选择器和 `operatorCode` 一起变化 |
| FOV/Distortion | 传统模板和 JSON V2 不串用，结果展示正确 |
| SFR 曲线 | `WindowSFR` 能打开曲线，CSV 字段和采样数据一致 |
| Ghost | 请求带 `Color`，点阵数量和 overlay 一致 |
| JSON 分支 | BinocularFusion、SFR_FindROI、FindCross 走对应 JSON 模板 |

## 关键文件

| 文件 | 作用 |
| --- | --- |
| `Templates/ARVR/MTF/AlgorithmMTF.cs` | MTF 手动请求 |
| `Templates/ARVR/SFR/AlgorithmSFR.cs` | SFR 手动请求 |
| `Templates/ARVR/SFR/WindowSFR.xaml.cs` | SFR 曲线和导出 |
| `Templates/ARVR/FOV/DisplayFOV.xaml.cs` | FOV 输入和显示 |
| `Templates/ARVR/Distortion/ViewResultDistortion.cs` | Distortion 结果映射 |
| `Templates/ARVR/Ghost/AlgorithmGhost.cs` | Ghost 请求 |
| `Templates/Flow/NodeConfigurator/AlgorithmNodeConfigurators.cs` | Flow 属性面板 |
| `Engine/FlowEngineLib/Algorithm/AlgorithmARVRNode.cs` | Flow `operatorCode` 和基础请求 |

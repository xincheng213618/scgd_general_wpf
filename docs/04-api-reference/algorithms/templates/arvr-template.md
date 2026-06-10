# ARVR 模板

本页只描述当前仓库里真实可见的 ARVR 模板族，不再维护“光学算法教材 + 统一参数手册”式旧稿。

## 这个模板族当前在做什么

按当前源码状态，ARVR 不是一个单模板，而是一组并行存在的模板和显示算法：

- `MTF`
- `SFR`
- `FOV`
- `Distortion`
- `Ghost`

这些实现共享同一种宿主框架，但参数模型、结果表现和是否依赖 POI 并不统一。再往前走到 Flow 节点时，还会混入 JSON 变体，例如 `SFR_FindROI` 这类模板。

所以这页更适合当成“ARVR 家族地图”，而不是试图维护一张万能参数表。

## 当前最关键的文件

- `Engine/ColorVision.Engine/Templates/ARVR/MTF/TemplateMTF.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/MTF/MTFParam.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/MTF/AlgorithmMTF.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/MTF/ViewHandleMTF.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/SFR/SFRParam.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/SFR/AlgorithmSFR.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/SFR/WindowSFR.xaml.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/FOV/FOVParam.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/FOV/AlgorithmFOV.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/FOV/DisplayFOV.xaml.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/Distortion/DistortionParam.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/Distortion/AlgorithmDistortion.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/Distortion/ViewResultDistortion.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/Ghost/GhostParam.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/Ghost/AlgorithmGhost.cs`
- `Engine/ColorVision.Engine/Templates/Flow/NodeConfigurator/AlgorithmNodeConfigurators.cs`
- `Engine/FlowEngineLib/Algorithm/AlgorithmARVRNode.cs`

## 当前模板矩阵

ARVR 目录下既有传统强类型模板，也会在 Flow 中接入 JSON V2 模板。交接时先用下面这张表确认当前链路，不要只按目录名判断。

| 模板族 | 传统模板 | 字典/代码 | 运行事件 | 关键请求参数 | 结果入口 |
| --- | --- | --- | --- | --- | --- |
| `FOV` | `TemplateFOV` | `TemplateDicId = 6`，`Code = FOV` | `Event_FOV_GetData` | `TemplateParam` | `ViewHandleFOV`，`ViewResultAlgType.FOV` |
| `Ghost` | `TemplateGhost` | `TemplateDicId = 7`，`Code = ghost` | `Ghost` | `TemplateParam`、`Color` | `ViewHandleGhost`，`ViewResultAlgType.Ghost` |
| `MTF` | `TemplateMTF` | `TemplateDicId = 8`，`Code = MTF` | `Event_MTF_GetData` | `TemplateParam`、`POITemplateParam` | `ViewHandleMTF`，`ViewResultAlgType.MTF` |
| `SFR` | `TemplateSFR` | `TemplateDicId = 9`，`Code = SFR` | `Event_SFR_GetData` | `TemplateParam`、`POITemplateParam` | `ViewHandleSFR`，`ViewResultAlgType.SFR` |
| `Distortion` | `TemplateDistortionParam` | `TemplateDicId = 10`，`Code = distortion` | `Distortion` | `TemplateParam` | `ViewHandleDistortion`，`ViewResultAlgType.Distortion` |
| `AOI` | `TemplateAOIParam` | `TemplateDicId = 12`，`Code = AOI` | 当前不是独立主运行入口 | 模板参数配置 | 主要作为 ARVR/AOI 参数配置，不要误写成完整结果链 |

这里的“运行事件”来自当前手动算法类。Flow 节点的 `operatorCode` 还会覆盖到 `ARVR.BinocularFusion`、`ARVR.SFR.FindROI`、`FindCross` 这些 JSON 模板分支，见下方 Flow 表。

## 当前主链怎么分

### MTF

`TemplateMTF` 是经典参数模板，当前：

- `Code = MTF`
- `TemplateDicId = 8`

`MTFParam` 里当前最直接可见的参数包括：

- `MTF_dRatio`
- `eEvaFunc`
- `dx`
- `dy`
- `ksize`

`AlgorithmMTF` 的实际行为不是本地算图，而是：

- 打开 `TemplateMTF`
- 打开 `TemplatePoi`
- 组装 `POITemplateParam`
- 发布 `Event_MTF_GetData`

这说明当前 MTF 运行链明确依赖 POI 模板，而不是独立于 POI 存在。

结果侧最值得看的不是参数类，而是 `ViewHandleMTF`。它会：

- 把结果导出成 CSV
- 统计最大值、最小值、均值、方差和均匀性
- 作为 `ViewResultAlgType.MTF` 的处理器接入 UI

### SFR

`SFRParam` 当前比旧文档里简单得多，直接可见的核心参数只有 `Gamma`。真正的显示和结果交互更多落在：

- `AlgorithmSFR`
- `WindowSFR`

`AlgorithmSFR` 和 MTF 一样，会额外要求 `TemplatePoi`，再发布 `Event_SFR_GetData`。`WindowSFR` 则负责把结果里的 `Pdfrequency`、`PdomainSamplingData` 反序列化成曲线，并提供阈值和频率换算。

因此当前 SFR 文档不能再只讲模板参数，也要把结果窗口算进去。

### FOV

`FOVParam` 当前是一个较完整的参数模型，直接包含：

- `Radio`
- `CameraDegrees`
- `ThresholdValus`
- `DFovDist`
- `FovPattern`
- `FovType`
- `Xc`、`Yc`、`Xp`、`Yp`

`AlgorithmFOV` 负责打包 `Event_FOV_GetData`，而 `DisplayFOV` 则承担了当前非常实际的一层工作：

- 从服务管理器取图像源设备
- 支持批次、原始文件和本地图像三种输入
- 拉取 Raw 文件列表并允许直接打开

这说明 FOV 当前并不是“只配参数然后跑算法”的极简模板。

### Distortion

`DistortionParam` 当前是真正的大参数对象，包含多组 blob 阈值、面积过滤、形状过滤和全局策略项，例如：

- `filterByColor`
- `minThreshold` / `maxThreshold`
- `minArea` / `maxArea`
- `filterByCircularity`
- `filterByConvexity`
- `filterByInertia`
- `CornerType`
- `SlopeType`
- `LayoutType`
- `DistortionType`

`AlgorithmDistortion` 负责发布 `Distortion` 事件，`ViewResultDistortion` 则把枚举值和最终点阵结果重新映射成可展示的描述对象。

### Ghost

`GhostParam` 当前可见的核心参数并不复杂，主要围绕检测点阵：

- `Ghost_radius`
- `Ghost_cols`
- `Ghost_rows`
- `Ghost_ratioH`
- `Ghost_ratioL`

`AlgorithmGhost` 额外附带了 `Color` 参数，再发布 `Ghost` 事件。也就是说，颜色通道当前是 Ghost 链上的一等输入，不是页面注释里的附加项。

## Flow 里怎么接进来

`AlgorithmARVRNode` 与 `AlgorithmNodeConfigurators` 共同揭示了当前 ARVR 家族在 Flow 里的真实用法：

- `MTF`、`SFR` 节点会同时要求参数模板和 `POI` 模板。
- `FOV`、`畸变` 节点既能接经典参数模板，也能接 JSON 变体。
- `SFR_FindROI` 这类分支会同时接 `TemplateSFRFindROI` 和 `TemplatePoi`。

因此当前 ARVR 族不是一条平坦目录，而是传统模板、JSON 模板、POI 模板和 Flow 节点共同拼出来的运行面。

| Flow 算子 | `operatorCode` | 配置器挂载 | 交接重点 |
| --- | --- | --- | --- |
| `MTF` | `MTF` | `TemplateMTF` + `TemplatePoi` | 缺 POI 时请求会少 `POITemplateParam`，结果点位解释不完整。 |
| `SFR` | `SFR` | `TemplateSFR` + `TemplatePoi` | SFR 结果曲线依赖 ROI/POI 的空间定义。 |
| `FOV` | `FOV` | `TemplateDFOV` + `TemplateFOV` | 同一属性名可挂 JSON V2 或传统模板，排查时要看实际模板来源。 |
| `畸变` | `Distortion` | `TemplateDistortion2` + `TemplateDistortionParam` | JSON V2 和传统强类型参数共存，结果展示还要看 `result.Version`。 |
| `SFR_FindROI` | `ARVR.SFR.FindROI` | `TemplateSFRFindROI` + `TemplatePoi` | 它不是传统 `TemplateSFR`，而是 JSON ROI 检出链。 |
| `双目融合` | `ARVR.BinocularFusion` | `TemplateBinocularFusion` | 走 JSON 模板，不要在 ARVR 传统目录里找参数类。 |
| `十字计算` | `FindCross` | `TemplateFindCross` + `TemplatePoi` 字段作为 ROI | 名称是 ROI，但底层仍用 `TemplatePoi` 模板选择器。 |

`AlgorithmARVRNode.getBaseEventData(...)` 还会把 `BufferLen`、颜色通道、上一步图像参数和 SMU 结果一起组进请求。现场如果看到 Flow 手动运行正常但流程运行异常，需要同时比对手动算法类和 Flow 节点生成的参数。

## 结果落库与展示

结果交接不能只看 `Algorithm*.cs`。当前 ARVR 传统结果至少有下面几条落库/展示链：

| 结果 | 结果表/字段线索 | 展示入口 | 排查重点 |
| --- | --- | --- | --- |
| `FOV` | `t_scgd_algorithm_result_detail_fov`，包含 `pattern`、`radio`、`camera_degrees`、`dist`、`threshold`、`degrees` | `ViewHandleFOV` | 图像输入、模板参数和结果表中的角度/距离字段要一起看。 |
| `Ghost` | `t_scgd_algorithm_result_detail_ghost`，包含 `rows`、`cols`、`radius`、`led_centers`、`ghost_pixels` | `ViewHandleGhost` | 颜色通道和点阵数量会影响最终 overlay。 |
| `SFR` | `t_scgd_algorithm_result_detail_sfr`，包含 ROI、`gamma`、`pdfrequency`、`pdomain_sampling_data` | `ViewHandleSFR`、`WindowSFR` | CSV/曲线展示来自采样数据反序列化，不只是单个数值。 |
| `Distortion` | `t_scgd_algorithm_result_detail_distortion`，包含 `layout_type`、`slope_type`、`corner_type`、`max_ratio`、`final_points` | `ViewHandleDistortion`、`ViewResultDistortion` | 枚举映射和最终点阵要一起校验。 |

## 当前几个最容易写错的点

### ARVR 不是统一 schema

各子目录共享的是模板宿主和显示算法风格，不是同一套参数字段。

### 多数算法类是宿主和命令适配器

`AlgorithmMTF`、`AlgorithmSFR`、`AlgorithmFOV`、`AlgorithmDistortion`、`AlgorithmGhost` 主要负责开窗口、取输入、打 MQTT 请求，而不是在本地直接完成数值计算。

### POI 在 ARVR 里不是边角料

至少 MTF、SFR、SFR_FindROI 当前都显式依赖 `TemplatePoi`。如果忽略 POI，这页就解释不通当前运行链。

### 结果处理代码同样重要

像 `ViewHandleMTF`、`WindowSFR`、`ViewResultDistortion` 这些结果层实现，是理解用户最终看到什么的重要入口，不该被旧文档省掉。

### 传统模板和 JSON V2 不是替代关系

FOV、Ghost、Distortion、SFR_FindROI 等链路在 Flow 中会同时暴露传统模板和 JSON 模板。不能简单写成“已经升级到 V2”，也不能只保留旧模板说明；要按实际 `operatorCode`、模板类型和结果版本确认。

## 验收建议

| 场景 | 必验项 |
| --- | --- |
| 手动 MTF/SFR | 请求参数同时包含 `TemplateParam` 和 `POITemplateParam`，结果能被对应 `ViewHandle*` 接住。 |
| Flow ARVR 节点 | 切换算法类型后，模板选择器随类型切换，并且 `operatorCode` 与算法类型一致。 |
| FOV/Distortion V2 | 同一节点能区分传统模板和 JSON 模板，结果展示不串 handler。 |
| SFR 曲线 | `WindowSFR` 能打开曲线，CSV 导出字段和结果表 `pdomain_sampling_data` 对得上。 |
| Ghost | 请求包含 `Color`，结果表里的点阵数量和 overlay 展示一致。 |

## 推荐阅读顺序

1. `Engine/ColorVision.Engine/Templates/ARVR/MTF/AlgorithmMTF.cs`
2. `Engine/ColorVision.Engine/Templates/ARVR/SFR/AlgorithmSFR.cs`
3. `Engine/ColorVision.Engine/Templates/ARVR/FOV/DisplayFOV.xaml.cs`
4. `Engine/ColorVision.Engine/Templates/ARVR/Distortion/ViewResultDistortion.cs`
5. `Engine/ColorVision.Engine/Templates/Flow/NodeConfigurator/AlgorithmNodeConfigurators.cs`

## 继续阅读

- [POI 模板](./poi-template.md)
- [JSON 模板](./json-templates.md)
- [流程引擎](./flow-engine.md)

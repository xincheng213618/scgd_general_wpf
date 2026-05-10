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

## 当前几个最容易写错的点

### ARVR 不是统一 schema

各子目录共享的是模板宿主和显示算法风格，不是同一套参数字段。

### 多数算法类是宿主和命令适配器

`AlgorithmMTF`、`AlgorithmSFR`、`AlgorithmFOV`、`AlgorithmDistortion`、`AlgorithmGhost` 主要负责开窗口、取输入、打 MQTT 请求，而不是在本地直接完成数值计算。

### POI 在 ARVR 里不是边角料

至少 MTF、SFR、SFR_FindROI 当前都显式依赖 `TemplatePoi`。如果忽略 POI，这页就解释不通当前运行链。

### 结果处理代码同样重要

像 `ViewHandleMTF`、`WindowSFR`、`ViewResultDistortion` 这些结果层实现，是理解用户最终看到什么的重要入口，不该被旧文档省掉。

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
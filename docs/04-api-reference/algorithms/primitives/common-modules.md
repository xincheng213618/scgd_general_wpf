# 通用算法模块

本页不再把当前仓库描述成一个独立的“通用算法平台”。按源码现状，它更适合作为共享算法构件的导航页。

## 现在这页真正应该覆盖什么

当前仓库里反复被多个算法、模板和 Flow 节点复用的公共构件，主要集中在这几组：

- ROI / 发光区定位
- POI 点集与伴生模板
- Matching 模板匹配
- JSON 形式的裁剪或寻边模板

这些构件的共同点不是“纯算法内核”，而是都同时带有：

- 模板编辑入口
- 显示算法宿主
- MQTT 命令打包
- 有时还带结果处理或 Flow 接入

所以本页的作用更像“去哪看哪一组共享构件”，而不是继续抽象一套并不存在的统一框架。

## 当前最值得优先看的几支

### ROI

经典 ROI 现在主要落在 `Templates/FindLightArea`：

- `TemplateRoi`
- `RoiParam`
- `AlgorithmRoi`
- `DisplayRoi`

它当前代表的是发光区定位模板链，而不是全局通用 ROI SDK。

除此之外，还有 JSON 版 ROI 入口：

- `TemplateImageROI`
- `TemplateSFRFindROI`

如果你要看的是裁剪或 ARVR 的找 ROI，这两支比经典 `TemplateRoi` 更贴近现状。

### POI

POI 是当前最典型的共享原语。它至少覆盖：

- `TemplatePoi`
- `PoiParam`
- `PoiPoint`
- `AlgorithmPOI`
- `AlgorithmBuildPoi`
- 过滤、修正、标定、输出伴生模板

而且它会被 JSON 算法和 Flow 节点继续引用，所以 POI 不是单一算法页，而是跨模块数据结构。

### Matching

Matching 当前也是一条完整但相对精简的共享链：

- `TemplateMatch`
- `MatchParam`
- `AlgorithmMatching`
- `DisplayMatching`
- `ViewHandleMatching`

`AlgorithmMatching` 当前会：

- 打开 `TemplateMatch`
- 可选打开 `TemplatePoi`
- 允许指定 `TemplateFile`
- 发布 `Event_MatchTemplate`

所以 Matching 也不是只剩一个计算函数，而是完整的模板与命令宿主。

## 当前这些共享模块怎样串到系统里

按当前实现，它们大体都走同一类运行模式：

1. 通过 `TemplateEditorWindow` 或自定义编辑控件维护模板。
2. 通过 `DisplayAlgorithmBase` 派生类暴露 UI 和命令。
3. 在算法类里组装 `CVTemplateParam` 和其它输入参数。
4. 通过 MQTT 事件把请求发给服务侧。
5. 视情况再由结果处理器或 Flow 节点继续消费。

这也是为什么把这些模块单纯写成“算法库”会失真，因为当前实现里 UI、模板和命令宿主是一体的。

## 如果现在要按需求读源码

### 想看区域选择或裁剪

优先读 [ROI](./roi.md)。

### 想看点集模板、点集构建或 POI 复用

优先读 [POI](./poi.md) 和 [POI 模板](../templates/poi-template.md)。

### 想看图像模板匹配

优先读 `Engine/ColorVision.Engine/Templates/Matching/AlgorithmMatching.cs` 和 `TemplateMatch.cs`。

### 想看这些构件怎样被编排进流程

优先读 [流程引擎](../templates/flow-engine.md) 以及 `Templates/Flow/NodeConfigurator`。

## 当前几个最容易写错的点

### 通用不等于独立框架

这些共享模块并没有组成一套单独发布的公共 SDK，而是散布在 `ColorVision.Engine/Templates` 下、由主程序宿主统一托管。

### 共享模块并不纯粹

它们通常混合了模板、UI、MQTT 消息和结果显示。继续按严格三层架构去写，很容易和现状错位。

### POI、ROI、Matching 之间有交叉引用

例如 Matching 可以继续打开 `TemplatePoi`，而 ARVR 的 `SFR_FindROI` 又会要求 POI 模板。这些模块不是完全彼此独立的岛。

## 继续阅读

- [ROI](./roi.md)
- [POI](./poi.md)
- [ARVR 模板](../templates/arvr-template.md)
# 算法与模板概览

本章现在收束成“模板系统与算法接入链”的导读，不再继续维护把所有图像处理方法平铺成百科目录的旧写法。

## 这一章在讲什么

这里的“算法”主要对应 `Engine/ColorVision.Engine/Templates/` 及其周边接入链，而不是仓库里所有底层图像处理代码的总表。当前重点包括：

- 模板如何被发现、加载、管理和编辑。
- Flow 模板如何接入 `FlowEngineLib`。
- JSON 模板如何通过专门编辑器进入系统。
- ARVR、POI 等业务模板族怎样和算法服务对接。

如果你要找的是 OpenCV 级别的底层处理函数，入口通常不在这一章，而更接近 `Engine/cvColorVision/`、`UI/ColorVision.Core/` 或原生 DLL 侧。

## 当前章节结构

### 入口页

- [算法系统概览](./overview.md)：当前实现链的整体说明，先看这页最省时间。
- [当前算法模板覆盖清单](./current-algorithm-template-coverage.md)：把 `Templates/` 实际目录逐项映射到文档入口和后续补齐重点。

### 专题目录

- `templates/`：模板管理、流程模板、JSON 模板、POI/ARVR、FindLightArea、JND、LED 检测、BuzProduct、Validate、Compliance、DataLoad、Matching、SysDictionary、FocusPoints、ImageCropping、模板菜单等专题页。
- `detectors/`：少量缺陷/检测类专题。
- `primitives/`：少量基础构件说明。

这几个目录下仍保留一些历史页面，但本章首页不再把它们全部当成稳定入口平铺出来。

## 当前最值得先认识的代码锚点

从现状看，模板与算法链路最值得先认识的是这几类文件：

- `Templates/TemplateContorl.cs`：模板发现与注册入口。
- `Templates/TemplateManagerWindow.xaml.cs`：模板管理窗口。
- `Templates/TemplateEditorWindow.xaml.cs`：模板编辑窗口。
- `Templates/Flow/TemplateFlow.cs`：流程模板与流程编辑器接入点。
- `Templates/Jsons/ITemplateJson.cs`：JSON 模板的公共装载/导入导出逻辑。
- `Templates/Jsons/EditTemplateJson.xaml(.cs)`：JSON 模板编辑控件，负责文本/属性两种编辑模式。
- `Templates/POI/AlgorithmImp/AlgorithmPOI.cs`、`Templates/ARVR/*/Algorithm*.cs`：典型业务算法 UI 与消息组装入口。

## 当前几个关键边界

- 很多 `Algorithm*` 类本身不是最终计算核心，它们当前更多负责收集模板参数、文件路径、设备信息，再通过 MQTT/服务链发出执行请求。
- `POI` 不是孤立专题，它在当前代码里仍然是多个算法族共享的上游模板和参数来源。
- `Flow` 模板虽然表现形态不同，但它仍属于同一个 Templates 系统的一部分，不应和普通模板链完全割裂。
- JSON 模板和传统强类型模板目前并存，阅读时不要默认系统只保留一种模板定义方式。

## 推荐阅读顺序

1. 先看 [算法系统概览](./overview.md)，建立运行时主链认知。
2. 再看 [当前算法模板覆盖清单](./current-algorithm-template-coverage.md)，确认每个 `Templates/` 子目录的文档入口。
3. 再对照 [Templates 模块分析](../../03-architecture/components/templates/analysis.md)，理解目录和注册入口。
4. 如果关注流程模板，再看 [FlowEngineLib 架构](../../03-architecture/components/engine/flow-engine.md)。
5. 最后按具体业务域进入 `templates/` 下的单页，例如 [FindLightArea 发光区定位模板](./templates/find-light-area.md)、[JND 模板](./templates/jnd-template.md)、[LED 检测模板](./templates/led-detection.md)、[BuzProduct 产品业务参数模板](./templates/buz-product-template.md)、[Validate 判定规则模板](./templates/validate-rules.md)、[Compliance 结果交接](./templates/compliance-results.md)、[DataLoad 数据加载模板](./templates/data-load-template.md)、[Matching 模板匹配](./templates/matching-template.md)、[SysDictionary 系统字典模板](./templates/sys-dictionary-template.md)、[FocusPoints 关注点模板](./templates/focus-points-template.md)、[ImageCropping 图像裁剪模板](./templates/image-cropping-template.md)、[模板菜单入口](./templates/template-menu-entries.md)，并始终与源码对照阅读。

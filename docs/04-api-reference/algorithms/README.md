---
knowledge_id: "algorithms.index"
knowledge_type: "index"
status: "current"
summary: "区分统一本地图像算法平台与 Engine 模板/MQTT 算法，并按任务定位专题。"
aliases: ["算法代码在哪里","ColorVision.Algorithms","TemplateControl","MQTTAlgorithm"]
code_paths: ["UI/ColorVision.Algorithms/AlgorithmCatalog.cs","Engine/ColorVision.Engine/Templates/TemplateControl.cs","Engine/ColorVision.Engine/Services/Devices/Algorithm/MQTTAlgorithm.cs"]
test_paths: []
related: ["algorithms.platform","algorithms.template-overview","engine.template-design","algorithms.roi-routes","algorithms.poi-routes","engine.results"]
---

# 算法与模板知识入口

算法相关问题先区分两个入口：`UI/ColorVision.Algorithms/` 的中立算法契约，与 `Engine/ColorVision.Engine/Templates/` 的模板及服务接入。部分算法在本地计算，部分 `Algorithm*` 类只组装外部服务请求；不能仅凭类名判断执行位置。

## 按问题检索

| 问题 | 主题 | 主要源码 |
| --- | --- | --- |
| 新算法如何定义输入、参数、结果、overlay 与执行入口 | [统一图像算法平台](../../02-developer-guide/core-concepts/image-algorithm-platform-v1.md) | `UI/ColorVision.Algorithms/` |
| ONNX 应接在哪里，是否已经实现 | [ONNX 接入方案](../../02-developer-guide/core-concepts/onnx-inference-future-design.md) | 以该页状态和实际源码为准 |
| 模板如何发现、持久化、编辑与搜索 | [注册与持久化](../../03-architecture/components/templates/design.md)、[编辑与创建宿主](./templates/template-management.md) | `Templates/TemplateControl.cs` |
| 模板体系和算法服务如何衔接 | [接入概览](./overview.md)、[通用构件](./primitives/common-modules.md) | `Engine/ColorVision.Engine/Templates/` |
| Flow 选择模板并执行 | [Flow 接入](./templates/flow-engine.md)、[Engine 模板链](../engine-components/template-flow-chain.md) | `FlowProcessing/`、`Engine/FlowEngineLib/` |
| 历史结果和统一 overlay 如何进入图像画布 | [结果链](../engine-components/result-handoff-chain.md) | `ResultHandleRegistry`、`AlgorithmOverlayManager` |
| 模板菜单、属性面板、JSON 编辑器从哪里进入 | [菜单入口](./templates/template-menu-entries.md)、[JSON 模板](./templates/json-templates.md)、[PropertyGrid](../ui-components/property-grid.md) | `DisplayAlgorithmConfiguration.cs`、`Templates/Jsons/` |

## 按模板或业务关键词检索

| 关键词 | 主题 |
| --- | --- |
| POI、关注点数据 | [POI 模板](./templates/poi-template.md)、[POI 构件](./primitives/poi.md) |
| ROI、发光区、FocusPoints | [ROI 路由](./primitives/roi.md)、[FindLightArea](./templates/find-light-area.md)、[FocusPoints](./templates/focus-points-template.md) |
| AR/VR、Ghost、LED、灯条 | [ARVR](./templates/arvr-template.md)、[Ghost](./detectors/ghost-detection.md)、[LED](./templates/led-detection.md) |
| 数据加载、Matching、四点裁剪 | [DataLoad](./templates/data-load-template.md)、[Matching](./templates/matching-template.md)、[ImageCropping](./templates/image-cropping-template.md) |
| 系统字典 | [SysDictionary](./templates/sys-dictionary-template.md) |

## 检索与判断边界

模板族页面回答的是当前适配层与参数契约，不承诺外部算法服务或私有 native DLL 的内部实现。按页面的 `code_paths` 定位精确符号，再用 `test_paths` 和验证缺口判断结论强度。标为 `planned` 的方案不能当作现有功能；旧链接兼容页只负责跳转，不是第二份事实源。

## 验证入口与缺口

本页为两条算法体系的路由索引；验证随选中平台或模板主题，不把本地 provider 测试用于证明远端算法服务。

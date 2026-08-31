---
knowledge_id: "algorithms.template-primitives"
knowledge_type: "index"
status: "current"
summary: "路由 Engine 模板中的 ROI、POI、Matching 共享构件并区分统一算法平台。"
aliases: ["ROI POI Matching如何复用","TemplateRoi","TemplatePoi","TemplateMatch"]
code_paths: ["Engine/ColorVision.Engine/Templates/FindLightArea","Engine/ColorVision.Engine/Templates/POI","Engine/ColorVision.Engine/Templates/Matching"]
test_paths: []
related: ["algorithms.index","algorithms.roi-routes","algorithms.poi-routes","algorithms.matching","algorithms.platform"]
---

# Engine 模板共享构件

本页路由 Engine 模板中的 ROI、POI、Matching 共享构件，不定义另一套算法接口。中立输入、参数、Result 和 overlay 的统一契约已在 [图像算法平台](../../../02-developer-guide/core-concepts/image-algorithm-platform-v1.md) 与 `UI/ColorVision.Algorithms/` 中实现；两层不能混称。

## 按任务定位

| 任务 | 模板与源码入口 | 边界 |
| --- | --- | --- |
| 发光区、区域定位与裁剪 | [ROI](./roi.md)、`TemplateRoi`、`TemplateImageROI`、`TemplateSFRFindROI` | 传统参数、JSON 与本地算法路径分开核对 |
| 点集构建、过滤、修正和复用 | [POI](./poi.md)、[POI 模板](../templates/poi-template.md) | `TemplatePoi`、`PoiParam`、`PoiPoint` 被多个算法族使用 |
| 模板图匹配和 AOI 四角结果 | [Matching](../templates/matching-template.md) | `MatchingDisplayAlgorithmConfig` 分别持有参数选择与 `TemplateFile`，当前不附带 POI 模板选择 |
| 编排进流程 | [Flow 接入](../templates/flow-engine.md)、[PropertyGrid](../../ui-components/property-grid.md) | 节点执行、参数编辑与模板持久化是不同契约 |
| 新增中立算法或自定义 overlay | [算法平台](../../../02-developer-guide/core-concepts/image-algorithm-platform-v1.md)、[结果链](../../engine-components/result-handoff-chain.md) | 不以旧模板目录替代中立算法接口 |

## 传统适配链的共同结构

1. `TemplateControl` 与 `TemplateEditorWindow` 负责发现、选择和编辑模板。
2. `DisplayAlgorithmBase<TConfig>` 暴露配置与 `Execute()`，`DisplayAlgorithmControl` 提供手动界面。
3. 服务型适配器打包 `CVTemplateParam`、图像和附加参数，经 MQTT 请求执行。
4. Flow 可从节点参数进入同一服务语义；结果再按实际类型进入历史 handler 或后续节点。

这个结构描述的是服务型模板适配器，不覆盖所有本地算法。具体类是否走 native、本地托管或外部服务，必须沿 `Execute` / `SendCommand` 继续追踪。

## 不可互换的模型

`TemplateRoi` 是传统参数模板，`TemplateImageROI` 和 `TemplateSFRFindROI` 是 JSON 模板；它们不能合并成一张“ROI 参数”表。ARVR 的 SFR 找 ROI 依赖 POI，并不意味着 Matching 也依赖 POI。公共术语相同只能作为检索线索，不能用来推导配置或运行时兼容性。

## 验证入口与缺口

此索引不宣称模板构件具有统一执行契约；各分支的测试、服务依赖和验证缺口以对应页为准。

---
knowledge_id: "algorithms.roi-routes"
knowledge_type: "index"
status: "current"
summary: "按用途定位本地发光区与中立算法 ROI 模型，并查阅各自的参数与坐标契约。"
aliases: ["ROI到底对应哪个模块","AlgorithmInvocation.Roi"]
code_paths: ["UI/ColorVision.Algorithms/AlgorithmInvocation.cs"]
related: ["algorithms.index","algorithms.find-light-area","algorithms.platform"]
---

# ROI 模型与模板入口

ROI 表示感兴趣区域，在 ColorVision 中由不同能力定义参数与坐标语义。本页用于选择入口；共享名称不表示各分支的模板、坐标和持久化格式可以互换。

## 按用途选择

| 用途 | 模型或模板 | 操作与契约 |
| --- | --- | --- |
| 本地发光区四角定位 | `RobustV2` 与 `LocalFindLuminousAreaNode`，不要求检测参数模板 | [本地 V2 的搜索区域、图像来源与 POI 回写](../templates/find-light-area.md#配置本地发光区定位-v2) |
| 中立算法平台的输入区域 | `AlgorithmInvocation.Roi` 与 `AlgorithmRoi` 的矩形、圆、多边形、折线类型 | [算法平台](../../../02-developer-guide/core-concepts/image-algorithm-platform-v1.md) |

中立 ROI 类型定义在 `UI/ColorVision.Algorithms/AlgorithmInvocation.cs`。本地 V2 的节点与原生实现见发光区主题。

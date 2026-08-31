---
knowledge_id: "algorithms.circle-fit"
knowledge_type: "reference"
status: "current"
summary: "CircleFit 保留实现的参数、结果与验证契约；默认运行时由 Experimental 门禁拒绝执行。"
aliases: ["如何拟合圆、为什么圆拟合默认不可执行","CircleFit","CircleFitAlgorithmProvider"]
code_paths: ["UI/ColorVision.ImageEditor/Algorithms/CircleFitAlgorithmProvider.cs","UI/ColorVision.ImageEditor/Algorithms/StandardAlgorithmCatalog.cs","UI/ColorVision.ImageEditor/Algorithms/ImageAlgorithmPlatform.cs"]
test_paths: ["Test/ColorVision.UI.Tests/CircleFitV1Tests.cs","Test/ColorVision.UI.Tests/AlgorithmReleaseGateTests.cs"]
related: ["algorithms.platform","algorithms.index"]
---

# 圆拟合 V1（M6.3）

## 当前发布边界

当前默认 `ImageAlgorithmPlatform.CreateDefaultProviders()` 把本页 provider 包装在 `ExperimentalAlgorithmProviderGate` 中：菜单和 Batch 可执行投影隐藏该能力，直接调用默认 Runner 也返回 `provider_unavailable`，详情包含 `algorithm_experimental`。本页后面的参数、结果与宿主接入描述属于保留实现及测试契约，不是产品已开放的承诺；不得在调用方另建执行旁路来绕过门禁。

本页 `status: current` 表示它记录当前源码事实，不代表算法已发布。M 编号是历史增量标识；其他增量是否可用以 [统一平台发布清单](./image-algorithm-platform-v1.md#当前发布清单) 为准。`Test/ColorVision.UI.Tests/AlgorithmReleaseGateTests.cs` 验证默认拒绝行为，专题测试覆盖实现细节；解除门禁还需完成对应数值、最坏资源与生产规模验证。


## 阶段边界与已有能力盘点

M6.3 提供稳定 ID `colorvision.measurement.circle-fit`。仓库既有 Hough 圆、最小包围圆和客户专用圆检测都包含图像检测或特定业务规则，不能替代可序列化、可组合的通用 point-set 圆拟合。本阶段不复制这些实现，也不把找点和拟合隐藏在同一个算法中。

算法只拟合显式点集：`Invocation.Roi` 必须是 `PolylineAlgorithmRoi`，每个顶点都是一个输入点。图像输入仅提供 document/revision、宽高和 DPI 上下文；provider 不读取、复制或修改像素。M6.1 的亚像素边缘点可由调用方投影为该 ROI，从而与圆拟合显式组合。

## 参数与数值规则

- `Mode`：代数初始化后的几何最小二乘，或默认的确定性三点共识加 Huber 几何 IRLS 稳健拟合。
- `ResidualThresholdPixels`：按点到拟合圆的径向距离判定有效点；未通过点保留在表中，原因是 `residual_above_threshold`。
- `MinimumInlierCount`、最小/最大半径和最小角覆盖分别约束可接受结果；不满足时返回带原因的结构化拒绝，而不是伪造圆。
- `MaximumPoints`、`MaximumConsensusCandidates`、`MaximumConsensusEvaluations`、`MaximumIterations` 和 `MaximumOverlayPoints` 限制计算、迭代和显示资源。

坐标统一采用 pixel-center。Physical ROI 先按输入 DPI 转为像素。圆由中心 `(cx,cy)` 和半径 `r` 表示；`SignedRadialResidual = distance(point, center) - r`，正值表示点在圆外。角覆盖为有效点极角排序后 `360° - 最大缺口`。

`Confidence = coverageFraction × inlierFraction / (1 + RMS/threshold)`，是 0..1 的确定性质量分数，不是统计概率或标定置信区间。共识采样固定、无随机状态；小点集穷举三点组合，大点集受候选和距离评估预算约束，因此相同输入可复现。

## Result artifacts

| Artifact | 内容 |
| --- | --- |
| `circle-fit-summary` | 接受状态、点/有效点/拒绝点数量、中心、半径、RMS、最大残差、角覆盖与质量分数 |
| `circle-fit-points` | 输入点、圆上投影、带符号/绝对径向残差、有效标志与拒绝原因 |
| `circle-fit-geometry` | 接受时的 `Circle`，以及所有带残差、质量和过滤原因的 `Point` |
| `circle-fit-overlay` | transient 拟合圆、有效点和拒绝点样式 |
| `circle-fit-provenance` | `colorvision.measurement.circle-fit/v1` 参数、坐标、拟合、预算与质量规则 |

共线或重复点使用 `degenerate_point_distribution`；有效点不足、半径或角覆盖未通过时分别使用稳定拒绝原因。拒绝结果仍包含 Measurement、逐点 Table、Geometry 和 provenance，供各宿主统一处理。

## 保留的宿主适配（默认禁用）

本节是保留代码和测试的接入契约；默认产品菜单与 Runner 仍由 Experimental 门禁拒绝执行。

- ImageView：“算法调用 → 圆拟合...”选择点集、编辑统一参数、显示表格与 transient overlay，并导出 CSV/JSON。关闭窗口、Clear、切图或 revision 改变均复用统一 analysis session 与 overlay 生命周期。
- Batch：`BatchAlgorithmAnalysisProcessor` 接受同一个 Invocation 并输出结构化 JSON；它不属于 Batch 图像格式转换菜单中的像素算法。
- Flow：`LocalFlowImageAlgorithmAdapter` 可复用本地 Invocation/Result；本阶段没有宣称新增生产 STNode，也不改变旧远端 MQTT execution plane。
- Copilot：该分析不输出图像，本阶段未进入显式白名单；稳定 alias 不会使其自动暴露。

## 验证范围与限制

`CircleFitV1Tests` 覆盖九种规范图像格式、带离群点的稳健数值 golden、Physical/DPI、最小二乘、共线/不足/半径/角覆盖拒绝、资源上限、取消、输入只读、所有权释放、Batch/Flow 一致性，以及 ImageView 表格、圆形 WPF Visual 和回收。计算上限由共识候选/距离评估预算限定，后续 IRLS 为 `O(iterations × points)`，结果内存为 `O(points)`。

V1 不从像素自动检测圆、不拟合椭圆或多个圆，也不输出统计协方差。仿射、透视和单应性属于 [几何变换](./geometric-transform-v1.md) 的独立契约，不由圆拟合隐式提供。

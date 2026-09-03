---
knowledge_id: "algorithms.line-fit"
knowledge_type: "reference"
status: "current"
summary: "LineFit 保留实现的参数、结果与验证契约；默认运行时由 Experimental 门禁拒绝执行。"
aliases: ["如何拟合直线、为什么直线拟合默认不可执行","LineFit","LineFitAlgorithmProvider"]
code_paths: ["UI/ColorVision.ImageEditor/Algorithms/LineFitAlgorithmProvider.cs","UI/ColorVision.ImageEditor/Algorithms/StandardAlgorithmCatalog.cs","UI/ColorVision.ImageEditor/Algorithms/ImageAlgorithmPlatform.cs"]
test_paths: ["Test/ColorVision.UI.Tests/LineFitV1Tests.cs","Test/ColorVision.UI.Tests/AlgorithmReleaseGateTests.cs"]
related: ["algorithms.platform","algorithms.index"]
---

# 直线拟合 V1

## 当前发布边界

此算法为 Experimental，默认不展示或执行；门禁和错误码见[统一平台发布清单](./image-algorithm-platform-v1.md#当前发布清单)。


## 适用范围

`colorvision.measurement.line-fit` 对显式点集拟合直线；图像线检测由 Hough、FindCross 或对应业务算法负责。

本算法只拟合显式点集：`Invocation.Roi` 必须是 `PolylineAlgorithmRoi`，其中每个顶点都是一个输入点。图像输入只提供文档/revision、宽高与 DPI 上下文；provider 不读取或复制像素。亚像素边缘结果 `subpixel-edge-geometry` 中接受的边缘点可由调用方投影成这个 ROI，从而显式组合“找点”和“拟合”，二者不会被隐藏在一个难以单测的步骤里。

## 参数与数值规则

- `Mode`：正交总最小二乘，或默认的确定性 Huber IRLS 稳健拟合。
- `ResidualThresholdPixels`：最终以垂直欧氏距离判定有效点；未通过点保留在表中，原因是 `residual_above_threshold`。
- `MinimumInlierCount`：有效点不足时返回成功的结构化拒绝结果，而不是伪造一条直线。
- `MaximumPoints`、`MaximumIterations` 和 `MaximumOverlayPoints` 分别限制计算、迭代和显示资源。
- `OutputExtent`：拟合线可限制在有效点投影范围，或裁剪到图像边界。

所有计算使用 pixel-center 坐标。Physical ROI 先通过输入 DPI 转成像素。拟合结果采用单位方向 `(dx,dy)` 和单位法向 `(nx,ny)`，直线规范式为 `nx*x + ny*y + c = 0`；表中的 `SignedResidual` 沿该法向为正。方向符号规范化为 `dx > 0`，垂直线为 `dy >= 0`，因此角度和有符号残差可复现。

`Confidence = linearity × inlierFraction / (1 + RMS/threshold)`，其中 `linearity` 是协方差主方向的归一化特征值差；它是 0..1 的确定性质量分数，不是统计概率或标定后的置信区间。`Residual` 是像素单位的正交拟合残差。

## Result artifacts

| Artifact | 内容 |
| --- | --- |
| `line-fit-summary` | 接受状态、点/有效点/拒绝点数量、角度、方向/法向、`c`、RMS、最大残差与质量分数 |
| `line-fit-points` | 每个输入点、投影点、带符号/绝对残差、有效标志与拒绝原因 |
| `line-fit-geometry` | 接受时的 `Line`，以及所有带残差、质量和过滤原因的 `Point` |
| `line-fit-overlay` | transient 拟合线、有效点和拒绝点样式 |
| `line-fit-provenance` | `colorvision.measurement.line-fit/v1` 参数、坐标、拟合与质量规则 |

重复点导致零方差时使用 `degenerate_point_distribution`；最终有效点不足使用 `insufficient_inliers`。两者仍保留逐点表、Geometry、Measurement 与 provenance，方便 ImageView、Batch、Flow 和测试统一处理。

## 保留的宿主适配（默认禁用）

以下是保留实现和测试的接入契约，不改变默认菜单与 Runner 的 Experimental 拒绝状态。

- ImageView：“算法调用 → 直线拟合...”选择点集、编辑统一参数、显示表格与 transient overlay，并可导出 CSV/JSON。关闭窗口、Clear、切图或 revision 改变均走统一 analysis session 与 overlay 生命周期。
- Batch：使用 `BatchAlgorithmAnalysisProcessor` 和同一个 Invocation，输出结构化 JSON；它不是 Batch 图像格式转换菜单中的像素算法。
- Flow：`LocalFlowImageAlgorithmAdapter` 可复用本地 Invocation/Result。当前没有宣称存在专用生产 STNode，也没有改变旧远端 MQTT execution plane。
- Copilot：分析结果没有图像输出，不在白名单内；反射或 alias 不会使其自动暴露。

## 验证范围与限制

`LineFitV1Tests` 覆盖九种规范图像格式、稳健离群点 golden、垂直线、Physical/DPI、TLS/Huber、结构化拒绝、资源上限、取消、输入只读、成功/失败/取消释放、Batch/Flow 一致性，以及 ImageView 表格和实际 WPF Visual 回收。算法复杂度为 `O(iterations × points)`，内存为 `O(points)`。

V1 不从图像自动提线、不隐式运行亚像素边缘、不拟合多条线，也不提供统计协方差。需要拟合圆时使用独立的[圆拟合](./circle-fit-v1.md)参数和结果契约。

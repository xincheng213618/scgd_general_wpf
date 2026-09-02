---
knowledge_id: "algorithms.contour-analysis"
knowledge_type: "reference"
status: "current"
summary: "ContourAnalysis 保留实现的参数、结果与验证契约；默认运行时由 Experimental 门禁拒绝执行。"
aliases: ["轮廓提取返回什么、为什么当前未开放","ContourAnalysis","ContourAnalysisAlgorithmProvider"]
code_paths: ["UI/ColorVision.ImageEditor/Algorithms/ContourAnalysisAlgorithmProvider.cs","UI/ColorVision.ImageEditor/Algorithms/StandardAlgorithmCatalog.cs","UI/ColorVision.ImageEditor/Algorithms/ImageAlgorithmPlatform.cs"]
test_paths: ["Test/ColorVision.UI.Tests/ContourAnalysisV1Tests.cs","Test/ColorVision.UI.Tests/AlgorithmReleaseGateTests.cs"]
related: ["algorithms.platform","algorithms.index"]
---

# 轮廓提取 V1（M5.2）

## 当前发布边界

此算法为 Experimental，默认不展示或执行；门禁和错误码见[统一平台发布清单](./image-algorithm-platform-v1.md#当前发布清单)。


轮廓提取使用 OpenCvSharp，提供独立的参数、ROI 和结构化结果契约。亚像素边缘、直线和圆拟合由各自算法提供。

## 稳定身份和宿主能力

- AlgorithmId：`colorvision.analysis.contours`
- 算法版本：`1.0.0`
- 参数 schema：`1`
- provider：`colorvision.contours.cpu`，CPU / Local / deterministic
- 输入：一个 Gray8、Gray16、Gray32Float、Bgr/Bgra 8/16/float 中立帧
- ROI：整图、矩形、圆或多边形；物理坐标按输入 DPI 统一换算
- 宿主：ImageView、结构化 Batch、本地 Flow/headless
- 输出：无图像输出；格式转换不是本算法职责

Copilot 当前获批的批处理工具只接收图像输出。轮廓 descriptor 因而不声明 `Copilot`，也不进入自动白名单；不能把结构化结果伪装成图像以绕过该边界。

## 参数与二值化规则

`ContourAnalysisParameters` 是 ImageView、Batch 和本地 Flow 共用的唯一参数模型：

- `Threshold` 使用 0..255 标称刻度，16 位映射到 0..65535，float 映射到 0..1；
- `ForegroundPolarity` 选择 `Bright`（大于等于阈值）或 `Dark`（小于等于阈值）；
- 彩色输入按 `0.114B + 0.587G + 0.299R` 计算亮度，alpha 不参与；NaN/Infinity 计为无效像素且不进入前景；
- Blob 与轮廓 provider 共用 `BinaryAnalysisMaskBuilder`，避免相同格式/阈值语义形成两套实现；
- `RetrievalMode` 支持 `External`、`List`、`Tree`；`ApproximationMode` 支持 `None`、`Simple`；
- `SimplificationEpsilon > 0` 时，在提取结果上执行闭合 Douglas–Peucker 简化，面积、周长和质心描述简化后实际输出的几何；
- 可按面积、周长、点数、圆度、实心度和是否接触图像边界筛选；上限为 `0` 的最大值参数表示不限。

`MaximumCandidates`、`MaximumTotalPoints` 和 `MaximumOverlayContours` 分别限制候选数、全部结构化几何点数和仅用于显示的 overlay 数。前两项超限返回 `contour_limit_exceeded` 或 `contour_point_limit_exceeded` 结构化失败，不返回截断后易误用的数据；overlay 超限只产生诊断，Table 和 Geometry 仍完整。

## Result artifact

成功结果包含：

- `contour-summary`：ROI/前景/无效像素、候选/接受/拒绝数、结构化点数、接受轮廓总面积和总周长；
- `contours`：每个候选的 next/previous/child/parent 层级、接受状态、稳定过滤原因、面积、有向面积、周长、点数、边界框、质心、圆度、实心度、填充率和图像边界状态；
- `contour-geometry`：ROI 与每条轮廓的 Point/Line/Polygon；坐标是全图像素坐标，轮廓闭合是隐含语义；
- `contour-overlay`：transient ROI 和通过筛选的轮廓；关闭结果窗口、切图、Clear 或提交新图时由统一 overlay 管理器移除实际 Visual；
- `contour-provenance`：schema `colorvision.analysis.contours/v1`、输入格式/DPI、ROI、参数、阈值规则、坐标和层级语义。

Geometry 的 `Confidence` 使用实心度（轮廓面积/凸包面积），它是确定性的几何质量量，不是分类概率。拒绝原因使用稳定代码，例如 `area_below_minimum`、`perimeter_above_maximum`、`point_count_below_minimum`、`circularity_below_minimum`、`solidity_below_minimum` 和 `touches_image_border`。

## 保留的 ImageView、Batch 与 Flow 适配（默认禁用）

下述入口和结果窗代码保留用于验证；默认产品运行时仍受 Experimental 门禁阻止，不表示当前菜单可执行。

ImageView 的“算法调用 → 轮廓提取”提供整图、矩形、圆和多边形入口。结果窗口显示完整 Table，可导出 CSV/JSON，并用统一 `ImageAlgorithmAnalysisSession` 和 invocation coordinator 遵守 document/revision/latest-wins；窗口关闭会释放 Result 和 transient Visual。

结构化 Batch 使用 `BatchAlgorithmAnalysisProcessor` 导出相同 Invocation 的 JSON/CSV；本地 Flow 使用 `LocalFlowImageAlgorithmAdapter` 返回同一 Result artifact。旧 MQTT/设备 `AlgorithmNode` 保持独立 execution plane，不受影响。

## 数值、性能与所有权门禁

测试覆盖已知矩形的面积/周长/质心/点集、孔洞 Tree 层级、External 检索、None/Simple 点压缩、Gray16/float/NaN、全图坐标 ROI、稳定过滤原因、候选/点数/overlay 上限、ImageView 实际 Visual、Batch 与本地 Flow 一致性，以及成功、失败、取消时 transferred input 释放。输入帧始终只读。

执行只分配一个 Gray8 二值 mask，OpenCV 直接借用该工作缓冲区提取轮廓；随后只保存最终点集和结构化 artifact。取消在 mask 扫描和候选测量循环中轮询，并在 native `findContours` 前后检查；正在进行的单次 native 调用不能被中途打断，这是当前明确的 provider 边界。

2026-08-27 在当前 x64 Debug 测试宿主对 3840×2160 空前景执行 opt-in probe：Gray16 为 629.6 ms、托管分配 8,376,056 B；Bgra32 为 1,163.2 ms、托管分配 9,819,600 B。两者的理论 mask 都是 8,294,400 B，满足“一个整帧 Gray8 mask + 16 MiB 固定余量”的门禁。Bgra32 单次观测到约 84 MiB private working-set 增量，而 Gray16 为约 0.5 MiB；private working set 受 OpenCV allocator/进程缓存影响，不把该单次差值当作稳定 SLA，但它是彩色 4K 路径需持续跟踪的真实风险。复杂前景的点集与层级还会按轮廓数量增长，因此必须保留候选和总点数上限。

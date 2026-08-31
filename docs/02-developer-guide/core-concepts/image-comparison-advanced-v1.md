---
knowledge_id: "algorithms.image-comparison-advanced"
knowledge_type: "reference"
status: "current"
summary: "ImageComparison 的输入、参数、结果、宿主接入与定向验证契约。"
aliases: ["SSIM、色差与高级图像比较如何解释","ImageComparison","ImageComparisonAlgorithmProvider"]
code_paths: ["UI/ColorVision.ImageEditor/Algorithms/ImageComparisonAlgorithmProvider.cs","UI/ColorVision.ImageEditor/Algorithms/StandardAlgorithmCatalog.cs","UI/ColorVision.ImageEditor/Algorithms/ImageAlgorithmPlatform.cs"]
test_paths: ["Test/ColorVision.UI.Tests/ImageComparisonAdvancedV1Tests.cs","Test/ColorVision.UI.Tests/ImageComparisonV1Tests.cs","Test/ColorVision.UI.Tests/ImageAlgorithmPerformanceGateTests.cs"]
related: ["algorithms.platform","algorithms.index"]
---

# 图像比较高级 V1（M4）

M4 在 M3 的同一稳定算法身份 `colorvision.analysis.image-comparison` 上增加 ROI 比较、SSIM 和对齐前质量诊断。行为版本升级为 `1.1.0`，参数 schema 升级为 2；M3 的 schema 1 Invocation 由显式 `1 → 2` migrator 迁移，原有参数值保持不变，新参数使用统一默认值。

## 设计原因与能力边界

引入通用 SSIM 和双图对齐前检查时，选择扩展既有图像比较 provider，保留严格双输入校验、精确差分、MSE/RMSE/PSNR、热力图、blink/split 和资源所有权，避免创建同名算法或第二套 UI 计算。ROI 统计与高级图像比较复用 `AlgorithmPixelRoi`，统一矩形、圆、多边形的像素中心、DPI 和裁剪规则。

本算法的“对齐”只是一项有界采样的平移预检：它报告候选图相对 reference 的整数偏移、相关性、重叠率和置信度，绝不变换输入或差分结果。实际图像变换属于独立的[图像配准](./image-registration-v1.md)和[镜头畸变校正](./lens-distortion-correction-v1.md)，不能用预检结果冒充校正后图像。

## 参数 schema 2

schema 2 保留 M3 的 `IncludeAlphaInMetrics`、`FloatPeakValue` 和 `HeatmapMaximum`，并增加：

| 参数 | 默认值 | 验证与含义 |
| --- | --- | --- |
| `EnableSsim` | true | 计算逐通道及聚合 SSIM |
| `SsimWindowSize` | 11 | 3..255 的奇数；使用边界裁剪的方形 box window |
| `SsimK1` / `SsimK2` | 0.01 / 0.03 | 各自 `(0,1]`，生成稳定常数 C1/C2 |
| `SsimMinimumValidFraction` | 0.5 | `(0,1]`；窗口内有限样本对不足时排除该窗口 |
| `EnableAlignmentPrecheck` | true | 启用只读的整数平移诊断 |
| `AlignmentSearchRadius` | 8 | 0..32 pixel，X/Y 各向搜索 |
| `AlignmentWarningThresholdPixels` | 0.5 | 非负；超过时产生偏移 warning |
| `AlignmentMinimumOverlapFraction` | 0.75 | `(0,1]`；拒绝重叠不足的候选偏移 |
| `AlignmentMaximumSamples` | 4096 | 256..100000；以规则网格限制每个偏移的样本量 |

Runner 在 provider 执行前完成 schema 迁移、默认值合并和校验。schema 1 JSON 往返与迁移有回归测试；未知的未来 schema 仍按平台规则结构化拒绝。

## ROI 与差分语义

- Invocation 可不带 ROI，或携带 Pixel/Physical 坐标的矩形、圆、多边形 ROI；整数像素中心、矩形半开区间、圆/多边形边界包含和 X/Y DPI 换算与 M1 完全相同。
- ROI 超界时与图像求交并返回 `comparison_roi_clipped`；没有像素中心时返回 `comparison_roi_empty`。
- MSE、RMSE、PSNR、SSIM 和对齐预检只使用 ROI 内样本。
- `absolute-difference`、`signed-difference` 及其普通显示图仍覆盖完整图像，保持 M3 精确 artifact 兼容；ROI 外不会被清零或裁剪。
- `difference-heatmap` 在 ROI 外明确为黑色，使展示边界不与数值 artifact 混淆。
- 成功结果包含 Pixel Geometry 和 transient Overlay；结果窗关闭时统一移除 overlay 并释放 Result。

## SSIM 数值规则

每个被比较通道独立计算局部 SSIM：

`((2 μx μy + C1) (2 σxy + C2)) / ((μx² + μy² + C1) (σx² + σy² + C2))`

均值、总体方差和协方差来自当前 box window 内、同时位于 ROI 且两输入均有限的样本对。图像边界处窗口裁剪到有效像素；ROI 外样本不泄漏进窗口。有限样本比例低于 `SsimMinimumValidFraction` 的窗口记为 invalid，不参与平均。8/16-bit 的峰值分别为 255/65535，float 使用显式 `FloatPeakValue`，因此等比例的 8/16/float 输入具有一致语义。结果限制在 `[-1,1]`，按有效窗口数聚合通道。

实现使用逐列滚动和水平滑窗，时间复杂度为 `O(width × height × compared channels)`，额外内存为 `O(width)`，不会为大图构造窗口积分立方体。每 16 行检查取消。没有有效窗口时不伪造 SSIM，Measurement 省略 `comparison.ssim`，保留 valid/invalid window count 并产生 `ssim_unavailable`。

## 对齐前检查

对齐预检先按 BGR 的 Rec.601 luma（灰度直接使用原值）采样 reference ROI，再在 `[-radius,+radius]²` 搜索 candidate。每个候选偏移使用有限样本对的归一化互相关；规则网格步长由 ROI 包围盒和 `AlignmentMaximumSamples` 决定，不做随机抽样。偏移 `(dx,dy)` 的定义是比较 `reference(x,y)` 与 `candidate(x+dx,y+dy)`。

结果表 `image-comparison-alignment` 返回 `Status`、估计偏移、幅值、best/zero correlation、峰值 margin、confidence、overlap、样本数和步长。`ok` 时还返回 `alignment-precheck` Transform Geometry，矩阵和 residual/confidence 只用于诊断。低纹理、样本不足或重叠不足分别以 `low_texture`、`insufficient_samples`、`insufficient_overlap` 表示，并产生结构化 warning；这些状态不会触发隐式配准。

## Result 与宿主接入

`image-comparison` Measurement 增加聚合/逐通道 SSIM、有效/无效窗口计数及对齐偏移/相关性；`image-comparison-channels` Table 增加 SSIM 列；StructuredData schema 更新为 `colorvision.analysis.image-comparison/v2`，记录 ROI、SSIM、对齐和请求的输出计划。M3 的五个 Image artifact 保持名称、格式和所有权不变；旧 Invocation 未指定计划时仍返回全部五张。ImageView 明确只请求三个 visualization，headless/API 可按名称选择精确差、显示差、heatmap 或 `metrics-only`。请求图像在分配前按 192 MiB retained-output 总预算检查，超限结构化失败。

ImageView 的“算法调用 → 图像比较”提供全图、矩形、圆和多边形入口。当前图与文件候选仍形成不可变快照并使用共享 analysis session；窗口显示差分/blink/split、通道质量和对齐预检，可导出 JSON/CSV bundle，PNG 和结构化导出默认拒绝覆盖。关闭、取消、切图、source revision 改变或较新 Invocation 都会使迟到结果失效。

Descriptor 仍只声明 `Interactive | Headless | Local | Deterministic | MultiInput`，并增加三种 ROI 支持。Batch、Flow 和 Copilot 尚无经审批的双输入配对契约，因此 M4 不为能力矩阵强行声明它们，也不把文件系统候选或远端设备算法暴露给 Copilot。

## 定向验证与验证缺口

`Test/ColorVision.UI.Tests/ImageComparisonAdvancedV1Tests.cs` 覆盖 schema 1→2 迁移与默认值、Catalog 版本/ROI 声明、Gray8/Gray16/Gray32Float/BGR 的 golden SSIM、常量闭式解、跨位深尺度一致性、非有限窗口、矩形/圆/多边形及 Physical/DPI 坐标、ROI 外精确差分与黑色热力图、空/裁剪 ROI、已知平移方向、低纹理诊断、取消和 transferred 双输入释放，以及 ImageView 菜单、质量表和 transient overlay 生命周期。

执行验证时记录当次代码版本、配置、真实依赖、实际执行的测试与跳过原因，不复用旧机器的通过数量、DLL 大小、构建警告数或分支合并状态。普通托管测试不能证明 native 项目已经重建；缺少 C++ workload 或真实 helper 时，应按[平台验收门禁](./image-algorithm-platform-v1.md#m0-验收门禁)保留缺口，不创建占位 DLL。

## 共享平台契约

图像文档变更与 revision、跨入口 latest-wins、overlay token、源帧租约及结果释放遵守[平台执行与所有权规则](./image-algorithm-platform-v1.md#m0-执行与所有权规则)。格式规范化、pin/只读 header、Catalog 菜单与 Batch 投影、native 依赖和 Copilot 审批边界由[平台兼容规范](./image-algorithm-platform-v1.md#执行平面与兼容层)统一维护；本页不保存跨算法的阶段完成清单。其它算法是否已开放，以[当前发布门禁](./image-algorithm-platform-v1.md#当前发布清单)为准。

## 性能与输出预算

图像比较按请求计划生成 artifact。兼容调用仍可请求全部五张图，但在分配前受 192 MiB retained-output 总预算约束；交互入口只请求三个显示 artifact，结果窗口只物化当前选中的一张差分视图。超预算应结构化拒绝，不能把“总是生成五张全尺寸图”当作必需成本。

`Test/ColorVision.UI.Tests/ImageComparisonV1Tests.cs` 的按需输出、`FourKLegacyFiveArtifactPlanIsRejectedBeforeLargeOutputAllocation`、`PreCancelledComparisonDoesNotEnterPreparationOrAllocateOutputImages` 和结果释放测试约束上述行为。WPF→lease 缓存、格式规范化 snapshot、并发取消安全的 run snapshot、最终 Result buffer、选中视图的 WPF 存储和 Batch 返回值仍是必要所有权边界；Bgr48/Bgra64 另需 RGB 通道交换，不能为减少复制破坏这些边界。

`Test/ColorVision.UI.Tests/ImageAlgorithmPerformanceGateTests.cs` 的 `ComparisonPipelineProbe` 在 4K/8K Gray16/Bgra32 上记录耗时、managed allocation、private bytes 变化和 retained artifact bytes；它关闭 SSIM/对齐预检且只请求 heatmap，因此不能证明开启全部分析后的生产性能。数值预算以测试源码为准，跨机器比较须保持输入、配置、开关和测量方法一致。

4K/8K probe 仅在环境变量 `COLORVISION_IMAGE_ALGORITHM_PERF` 为 `1` 时实际运行；未设置时测试方法直接返回，普通测试汇总中的成功不能当作已测大图性能。启用探针会消耗较多本机内存与 CPU，只在明确需要性能验证的环境运行，并如实记录未运行项。

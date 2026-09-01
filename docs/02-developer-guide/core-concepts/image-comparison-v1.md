---
knowledge_id: "algorithms.image-comparison"
knowledge_type: "reference"
status: "current"
summary: "ImageComparison 当前行为版本 1.1、schema 2 的双输入比较、ROI、SSIM、对齐预检、输出预算及 schema 1 迁移契约。"
aliases: ["如何比较两幅图像并读取误差与差异结果","SSIM、色差与高级图像比较如何解释","ImageComparison","ImageComparisonAlgorithmProvider"]
code_paths: ["UI/ColorVision.ImageEditor/Algorithms/ImageComparisonAlgorithmProvider.cs","UI/ColorVision.ImageEditor/Algorithms/StandardAlgorithmCatalog.cs","UI/ColorVision.ImageEditor/Algorithms/ImageAlgorithmPlatform.cs"]
test_paths: ["Test/ColorVision.UI.Tests/ImageComparisonV1Tests.cs","Test/ColorVision.UI.Tests/ImageComparisonAdvancedV1Tests.cs","Test/ColorVision.UI.Tests/ImageAlgorithmPerformanceGateTests.cs"]
related: ["algorithms.platform","algorithms.index","algorithms.image-registration","algorithms.lens-distortion-correction"]
---

# 图像比较 V1（M3–M4）

稳定算法 ID 为 `colorvision.analysis.image-comparison`。当前行为版本是 `1.1.0`，参数 schema 是 2；M3 的 schema 1 Invocation 通过显式 `1 → 2` migrator 迁移，原有参数保持不变，新参数使用统一默认值。本页是基础差分、ROI、SSIM 和对齐预检的单一当前契约。

## 输入与能力边界

算法比较编码后的设备样本值，不执行缩放、裁剪、位深/通道转换、ICC 转换或配准。两个输入必须：

- 分别命名为 `reference` 和 `candidate`，有符号差固定为 `reference - candidate`。
- 尺寸、位深、通道数完全一致。
- 都提供相同的显式 `ColorSpace` 标签；ImageView 使用 `encoded-device-values`。

违反约束时返回 `invalid_input_names`、`dimension_mismatch`、`format_mismatch`、`color_space_unspecified` 或 `color_space_mismatch`，不会隐式转换。DPI 不同不改变像素阵列比较，但产生 `dpi_mismatch` warning。

算法的“对齐”只是有界采样的整数平移预检：报告 candidate 相对 reference 的偏移、相关性、重叠率和置信度，不变换输入或差分结果。实际变换属于[图像配准](./image-registration-v1.md)和[镜头畸变校正](./lens-distortion-correction-v1.md)。

## 参数与基础数值语义

schema 2 保留三个基础参数：

- `IncludeAlphaInMetrics` 默认 `true`，只控制指标和热力图是否统计 alpha；精确差分保留所有通道。
- `FloatPeakValue` 默认 `1`，是 float 输入计算 PSNR 的显式峰值；8/16-bit 固定使用 255/65535。
- `HeatmapMaximum` 默认 `0`，表示使用峰值作显示归一化上限；正值用于固定显示量程。

对所有被统计通道的有限样本对，`MSE = mean((reference-candidate)^2)`，`RMSE = sqrt(MSE)`，`PSNR = 20 log10(peak/RMSE)`。完全相同时 PSNR 为 `Infinity`，JSON 使用命名浮点字符串。Float32 输入在 double 域累计；输入 NaN/Infinity 不进入指标并计入 invalid count，显示图以洋红标识。

schema 2 增加：

| 参数 | 默认值 | 验证与含义 |
| --- | --- | --- |
| `EnableSsim` | true | 计算逐通道及聚合 SSIM |
| `SsimWindowSize` | 11 | 3..255 的奇数；使用边界裁剪的方形 box window |
| `SsimK1` / `SsimK2` | 0.01 / 0.03 | 各自 `(0,1]`，生成稳定常数 C1/C2 |
| `SsimMinimumValidFraction` | 0.5 | `(0,1]`；有限样本不足时排除窗口 |
| `EnableAlignmentPrecheck` | true | 启用只读整数平移诊断 |
| `AlignmentSearchRadius` | 8 | 0..32 pixel，X/Y 各向搜索 |
| `AlignmentWarningThresholdPixels` | 0.5 | 非负；超过时产生偏移 warning |
| `AlignmentMinimumOverlapFraction` | 0.75 | `(0,1]`；拒绝重叠不足的候选偏移 |
| `AlignmentMaximumSamples` | 4096 | 256..100000；限制每个偏移的样本量 |

Runner 在执行 provider 前完成迁移、默认值合并和校验；未知未来 schema 结构化拒绝。

## ROI 与差分语义

- Invocation 可不带 ROI，或携带 Pixel/Physical 坐标的矩形、圆、多边形 ROI；整数像素中心、矩形半开区间、边界包含和 DPI 换算复用统一 ROI 规则。
- ROI 超界时与图像求交并返回 `comparison_roi_clipped`；没有像素中心时返回 `comparison_roi_empty`。
- MSE、RMSE、PSNR、SSIM 和对齐预检只使用 ROI 内样本。
- `absolute-difference`、`signed-difference` 及普通显示图仍覆盖完整图像；ROI 外不清零或裁剪。
- `difference-heatmap` 在 ROI 外为黑色，避免展示边界与数值 artifact 混淆。
- 成功结果包含 Pixel Geometry 和 transient Overlay，窗口关闭时统一移除并释放 Result。

## 输出与资源预算

图像输出包括：

- `absolute-difference`：保持输入尺寸、位深和通道的精确绝对差。
- `signed-difference`：同通道 32-bit float 有符号差。
- `absolute-difference-visualization`：BGR24 显示归一化。
- `signed-difference-visualization`：BGR24，中性灰表示零。
- `difference-heatmap`：BGR24 差值热力图。

结构化输出包括 `image-comparison` Measurement/StructuredData、`image-comparison-channels` Table、Geometry 和 Diagnostics。精确与显示 artifact 故意分离，不得从截图或热力图反推数值。

旧 Invocation 未指定计划时仍返回五张图。新调用在 metadata 的 `colorvision.image-comparison.requested-artifacts` 中按名称选择，或使用 `metrics-only`；未知名称返回 `comparison_output_plan_invalid`。图像分配前检查 192 MiB retained-output 总预算，超限返回 `comparison_output_budget_exceeded`；每次大数组分配前检查取消，Runner 负责释放 transferred 双输入。

## SSIM 规则

每个比较通道独立计算局部 SSIM：

`((2 μx μy + C1) (2 σxy + C2)) / ((μx² + μy² + C1) (σx² + σy² + C2))`

均值、总体方差和协方差只使用窗口内、ROI 内且两输入均有限的样本对。边缘窗口裁剪到有效像素；有限样本比例不足的窗口记为 invalid。峰值规则与 PSNR 相同，结果限制在 `[-1,1]` 并按有效窗口数聚合。实现使用逐列滚动和水平滑窗，时间复杂度为 `O(width × height × channels)`、额外内存为 `O(width)`，每 16 行检查取消。没有有效窗口时省略 `comparison.ssim` 并产生 `ssim_unavailable`。

## 对齐预检

预检按 BGR Rec.601 luma（灰度直接使用原值）采样 reference ROI，在 `[-radius,+radius]²` 搜索 candidate。候选偏移使用有限样本对的归一化互相关；规则网格步长由 ROI 包围盒和 `AlignmentMaximumSamples` 决定，不做随机抽样。`(dx,dy)` 表示比较 `reference(x,y)` 与 `candidate(x+dx,y+dy)`。

`image-comparison-alignment` Table 返回状态、偏移、幅值、best/zero correlation、峰值 margin、confidence、overlap、样本数和步长。`ok` 时还返回 `alignment-precheck` Transform Geometry。低纹理、样本不足或重叠不足分别使用 `low_texture`、`insufficient_samples`、`insufficient_overlap` 并产生 warning；不会触发隐式配准。

## ImageView 与宿主

ImageView 的“算法调用 → 图像比较”提供全图、矩形、圆和多边形入口。当前图与文件候选形成不可变快照并使用共享 analysis session；窗口显示差分、blink/split、通道质量和对齐预检，可导出 JSON/CSV bundle 和当前 PNG，默认拒绝覆盖已有文件。交互入口只请求三个 visualization；关闭、取消、切图、source revision 改变或较新 Invocation 会阻止迟到结果显示。

Descriptor 声明 `Interactive | Headless | Local | Deterministic | MultiInput` 和三种 ROI 支持。Batch、Flow 和 Copilot 尚无经审批的双输入配对契约，不得按文件名或目录顺序隐式配对，也不应强行声明对应 capability。

## 共享平台契约

图像 revision、latest-wins、overlay token、源帧租约和结果释放遵守[平台执行与所有权规则](./image-algorithm-platform-v1.md#m0-执行与所有权规则)。格式规范化、只读 header、Catalog 投影、native 依赖和 Copilot 审批由[平台兼容规范](./image-algorithm-platform-v1.md#执行平面与兼容层)维护。

## 验证范围与缺口

`ImageComparisonV1Tests` 覆盖输入校验、基础指标、精确差分、非有限值、输出计划、预算、取消、双输入和结果释放。`ImageComparisonAdvancedV1Tests` 覆盖 schema 迁移、ROI、SSIM、已知平移、低纹理诊断、ImageView 质量表与 overlay 生命周期。

`ImageAlgorithmPerformanceGateTests` 的 `ComparisonPipelineProbe` 只在 `COLORVISION_IMAGE_ALGORITHM_PERF=1` 时运行，并关闭 SSIM/对齐、只请求 heatmap；普通测试通过不能当作 4K/8K 或全分析性能证据。启用探针会消耗较多 CPU 和内存，跨机器比较须保持输入、配置和测量方法一致。

普通托管测试不证明 native 项目已重建；缺少 C++ workload 或真实 helper 时，应按[平台验收门禁](./image-algorithm-platform-v1.md#m0-验收门禁)保留缺口。

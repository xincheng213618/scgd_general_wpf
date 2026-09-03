---
knowledge_id: "algorithms.image-comparison"
knowledge_type: "reference"
status: "current"
summary: "图像比较的操作、参数范围、ROI、差分/SSIM/对齐结果和PNG/JSON/CSV导出；预检不校正图像，192MiB仅限制图像输出，采样数存在狭长区域上限缺口。"
aliases: ["图像比较", "ImageComparison", "CompareImage", "ImageDiff", "ImageComparisonAlgorithmProvider", "ImageComparisonParameters", "ImageComparisonEditorTool", "ImageComparisonResultWindow", "ImageComparisonQualityAnalyzer", "ImageComparisonOutputPlan", "差分热力图", "有符号差", "SSIM", "PSNR", "对齐预检", "IncludeAlphaInMetrics", "FloatPeakValue", "HeatmapMaximum", "EnableSsim", "SsimWindowSize", "SsimK1", "SsimK2", "SsimMinimumValidFraction", "EnableAlignmentPrecheck", "AlignmentSearchRadius", "AlignmentWarningThresholdPixels", "AlignmentMinimumOverlapFraction", "AlignmentMaximumSamples", "comparison_output_budget_exceeded", "float_difference_artifact_overflow", "alignment_precheck_inconclusive", "comparison_roi_empty", "comparison_roi_clipped", "comparison_output_plan_invalid", "colorvision.image-comparison.requested-artifacts"]
code_paths: ["UI/ColorVision.ImageEditor/Algorithms/ImageComparisonAlgorithmProvider.cs", "UI/ColorVision.ImageEditor/Algorithms/ImageComparisonQualityAnalyzer.cs", "UI/ColorVision.ImageEditor/Algorithms/ImageComparisonParameterMigrator.cs", "UI/ColorVision.ImageEditor/Algorithms/ImageComparisonOutputPlan.cs", "UI/ColorVision.ImageEditor/Algorithms/StandardAlgorithmParameters.cs", "UI/ColorVision.ImageEditor/Algorithms/StandardAlgorithmCatalog.cs", "UI/ColorVision.ImageEditor/Algorithms/ImageAlgorithmPlatform.cs", "UI/ColorVision.ImageEditor/Algorithms/ImageAlgorithmInputFactory.cs", "UI/ColorVision.ImageEditor/Algorithms/AlgorithmPixelRoi.cs", "UI/ColorVision.ImageEditor/Algorithms/AlgorithmResultExporter.cs", "UI/ColorVision.ImageEditor/EditorTools/Algorithms/Calculate/ImageComparison"]
test_paths: ["Test/ColorVision.UI.Tests/ImageComparisonV1Tests.cs","Test/ColorVision.UI.Tests/ImageComparisonAdvancedV1Tests.cs","Test/ColorVision.UI.Tests/ImageAlgorithmPerformanceGateTests.cs"]
related: ["algorithms.platform","algorithms.index","algorithms.image-registration","algorithms.lens-distortion-correction"]
---

# 图像比较：差分、SSIM 与对齐预检

图像比较用当前图像与一张候选图像计算逐像素误差、通道质量和可能的平移偏差，适用于检查同尺寸、同格式图像的差异。它比较编码后的设备样本值，不计算感知色差 ΔE，也不自动配准或给出产品合格判定。

稳定算法 ID 为 `colorvision.analysis.image-comparison`，兼容别名为 `ImageComparison`、`CompareImage`、`ImageDiff`。当前算法版本为 `1.1.0`、参数 schema 为 2；schema 1 Invocation 经 `ImageComparisonParametersV1ToV2Migrator` 迁移，保留已有参数并为缺失字段使用默认值。

## 在 ImageView 中比较图像

1. 打开作为参考的图像。在图像区域菜单中选择“算法调用 → 图像比较”，再选择“全图比较…”或“矩形 ROI…”“圆形 ROI…”“多边形 ROI…”。
2. 在“图像比较参数”窗口调整参数并提交。此入口每次创建一组默认参数；关闭而不提交则结束操作。
3. 在“选择要与当前图像比较的图像”中选择一张候选文件。候选加载器使用 WPF 解码器，保留像素格式并读取第一帧；文件筛选器列出某种扩展名不保证本机可解码，也不代表支持多帧逐帧比较。
4. 使用 ROI 入口时，在选择候选文件之后圈定当前参考图上的区域；全图入口直接执行。进度窗口可取消分析。
5. 完成后查看结果窗口。若快照期间参考图已改变，会提示重试；分析中的切图、revision 改变或新调用按共享 analysis session 规则使旧结果失效。

`ImageComparisonEditorTool` 取得当前图与候选图的独立快照，调用 Runner；此分析不把候选图或差分图应用到原图。结果窗口关闭时停止 Blink、移除本次 transient overlay 并释放 Result。通用的 revision、抢占和所有权规则见[平台执行与所有权](./image-algorithm-platform-v1.md#m0-执行与所有权规则)。

### 查看结果

| 区域 | 操作与含义 |
| --- | --- |
| 差分 | 默认“差分热力图”；可切换“绝对差（显示归一化）”或“有符号差（中性灰为零）”。这些都是显示图，数值含义见下文 |
| Blink | 点击“开始”交替显示当前图与候选图；间隔默认 500 ms，可调 100–2000 ms。切到其它视图时停止 |
| Split | 调整“候选图覆盖比例”，将候选图从左侧覆盖在参考图上；默认 0.5。这是显示裁剪，不改变图像或指标 |
| 通道质量 | 查看 Gray 或 B/G/R/A 各通道的 MSE、RMSE、PSNR、SSIM、最大绝对差及有效/无效样本数；顶部摘要显示聚合值 |
| 对齐预检 | 先看 Status，再看偏移、相关性、重叠比例和 Confidence；状态不是 `ok` 时不能把零偏移当作对齐成功 |

### 导出结果

选择保存位置后，三个按钮分别输出：

| 按钮 | 保存内容 |
| --- | --- |
| 保存当前差分 PNG | “差分”下拉框当前选中的 BGR24 显示图；即使正在看 Blink/Split，也不会保存闪烁帧、分割画面或整个窗口截图 |
| 导出 JSON | 算法 ID/版本、状态、诊断及结构化结果；图像 artifact 只写名称、角色和 metadata，不包含像素数据 |
| 导出 CSV | 主文件写 Measurement，Table、Geometry、StructuredData 各写相邻文件，例如选择 `comparison.csv` 会生成 `comparison_image-comparison-channels.csv`；文件数取决于是否有 ROI 和有效对齐 Geometry |

窗口导出拒绝覆盖已有文件，CSV 的任一伴随目标已存在也会拒绝。JSON/CSV 由 `AlgorithmResultExporter` 先写临时文件再提交；CSV 提交失败会尝试清理本次新建文件，清理失败仍可能留下文件。PNG 直接以 `CreateNew` 写目标，编码/写入失败可能留下不完整文件。遇到“保存失败”或“导出失败”，检查本次目标与伴随文件后换用新名称；不能只凭文件存在认定导出完整。当前比较窗口调用同步导出入口，没有独立导出进度或取消控件。

## 输入与 ROI

Provider 要求恰好两个输入，名称分别为 `reference` 和 `candidate`（名称不区分大小写）。有符号差固定为 `reference - candidate`，与集合顺序无关。两输入的宽高与 `AlgorithmImageFormat` 必须相同，并提供相同的非空 `ColorSpace` 标签（标签比较不区分大小写）；ImageView 使用 `encoded-device-values`。

比较阶段不缩放、裁剪、转换位深/通道、做 ICC 转换或配准。ImageView 进入平台前仍会按统一规则规范化 RGB/BGR、调色板和 Alpha；“同格式”指规范化后的算法输入，细节见[平台格式契约](./image-algorithm-platform-v1.md#执行平面与兼容层)。DPI 差异不改变逐像素比较，任一轴差异超过 0.01 时产生 `dpi_mismatch`。

Invocation 可不带 ROI，或携带 Pixel/Physical 坐标的矩形、圆、多边形 ROI。物理坐标以毫米表示，使用参考图 DPI 换算；整数坐标是像素中心，矩形使用半开区间，圆和多边形包含边界。

| 内容 | ROI 的作用 |
| --- | --- |
| MSE、RMSE、PSNR、SSIM | 只统计区域内的样本对；SSIM 窗口也只累计区域内样本 |
| 对齐预检 | 在参考 ROI 内选采样锚点；偏移后的候选位置必须在候选图内，但可以落在原 ROI 之外 |
| 绝对差、有符号差及两张普通显示图 | 仍输出完整图像，ROI 外不清零或裁剪 |
| 差分热力图 | ROI 外为黑色；区域内零差为深蓝色，黑色不能直接解读为“误差为零” |
| ROI Geometry / Overlay | 仅指定 ROI 时生成，保留请求区域转换后的 Pixel 几何；扫描范围与图像求交，不把几何本身改写为裁剪结果 |

裁剪使扫描边界改变时返回 `comparison_roi_clipped` warning。裁剪后的扫描范围为空时返回 `comparison_roi_empty`；范围非空但形状内没有有效像素样本时返回 `no_finite_samples`。两者应分别排查区域位置/大小和样本值。

## 参数

`ImageComparisonParameters` 的 schema 2 默认值如下。数值范围包含端点；浮点参数还必须是有限值。关闭 SSIM 或对齐预检也不会跳过相应参数的合法性校验。

| 参数 / 界面名称 | 默认值 | 范围与含义 |
| --- | --- | --- |
| `IncludeAlphaInMetrics` / 统计包含 Alpha 通道 | true | 控制基础指标、SSIM 与热力图是否统计 Alpha；不删除差分图中的 Alpha，对齐始终只用灰度/颜色亮度 |
| `FloatPeakValue` / 浮点图像峰值 | 1 | 正数；用于 Float32 的 PSNR、SSIM 稳定常数及自动显示量程。8/16-bit 固定用 255/65535 |
| `HeatmapMaximum` / 热力图最大差值 | 0 | 非负；0 使用标称峰值，正值固定显示量程。实际同时影响热力图、绝对差显示图和有符号差显示图，不改变数值指标 |
| `EnableSsim` / 计算 SSIM | true | 启用逐通道与聚合 SSIM |
| `SsimWindowSize` / SSIM 窗口大小 | 11 | 3–255 的奇数；边界裁剪的方形 box window |
| `SsimK1` / `SsimK2` | 0.01 / 0.03 | 各为 0.000001–1，生成稳定常数 C1/C2 |
| `SsimMinimumValidFraction` / SSIM 最小有效窗口比例 | 0.5 | 0.01–1；窗口内可用 ROI 位置中的有限样本比例 |
| `EnableAlignmentPrecheck` / 执行对齐预检 | true | 只报告整数平移诊断，不变换输入 |
| `AlignmentSearchRadius` / 对齐预检搜索半径 | 8 | 0–32 px；X/Y 各方向搜索 |
| `AlignmentWarningThresholdPixels` / 平移警告阈值 | 0.5 | 0–64 px；偏移幅值严格超过阈值才发出 warning，不是自动校正阈值 |
| `AlignmentMinimumOverlapFraction` / 最小重叠比例 | 0.75 | 0.1–1；有效匹配对占参考有效采样点的比例门槛 |
| `AlignmentMaximumSamples` / 对齐预检最大采样数 | 4096 | 256–100000；用于计算网格步长，当前不保证实际采样数的硬上限，见对齐预检 |

Runner 在 Provider 前完成参数迁移、默认值合并和校验；未知未来 schema 返回结构化失败。参数问题可包含 `invalid_float_peak`、`invalid_heatmap_maximum`、`invalid_odd_value` 或 `out_of_range`，应按失败字段调整。

## 指标与差分数值

对选中通道、区域内的有限样本对：`MSE = mean((reference-candidate)^2)`，`RMSE = sqrt(MSE)`，`PSNR = 20 log10(peak/RMSE)`。MSE/RMSE 越小表示样本误差越小；比较 PSNR 时须保持峰值、格式、通道与区域定义一致。

- 统计数量以“通道样本对”为单位，不是像素数；BGR 的一个有效像素可贡献三个样本。
- 有效样本完全相同时 PSNR 为 `Infinity`，JSON 以命名浮点字符串保留。被排除的样本不会破坏这个结论，因此还要检查 `InvalidCount`，不能仅凭 Infinity 判定整张图的每个通道相同。
- Float32 在 double 域计算差值与累计。NaN/Infinity 输入对不进入指标，计入 invalid count；没有任何有效对则失败。普通显示图的无效像素为洋红，热力图仅受所选统计通道影响，所以排除无效 Alpha 后两者颜色可能不同。
- 有限 Float32 输入相减也可能超出 Float32 输出范围：绝对/有符号差 artifact 存储 IEEE Infinity，但基础指标仍用 double 域有限差值。当请求的有符号图出现这种溢出时产生 `float_difference_artifact_overflow`；不能把数值 artifact 解释为无限精度结果。

### SSIM

每个比较通道独立计算局部 SSIM：

`((2 μx μy + C1) (2 σxy + C2)) / ((μx² + μy² + C1) (σx² + σy² + C2))`

其中 `C1=(K1×peak)²`、`C2=(K2×peak)²`。均值、总体方差和协方差仅使用窗口内、ROI 内且两输入均有限的样本对。窗口中心也须在 ROI 内；图像边缘裁剪窗口，有效样本不足或计算结果非有限的窗口计为 invalid。结果限制在 `[-1,1]`，按有效窗口数聚合。

关闭 SSIM 或没有有效窗口时不输出 `comparison.ssim` Measurement，摘要显示 `N/A`；启用但无有效窗口会产生 `ssim_unavailable`。算法采用逐列累计与水平滑窗，时间复杂度为 `O(width × height × channels)`、额外内存为 `O(width)`，每 16 行检查取消；使用小 ROI 不等于只扫描小范围图像。

### 对齐预检

预检使用灰度原值或 `0.114B + 0.587G + 0.299R` 亮度，在 `[-radius,+radius]²` 内比较 `reference(x,y)` 与 `candidate(x+dx,y+dy)`。`(dx,dy)` 是候选图相对参考图的采样偏移；它不会作用到输入、基础差分或 SSIM。

采样为确定性的规则网格，步长为 `max(1, ceil(sqrt(ROI包围盒面积 / AlignmentMaximumSamples)))`。**当前最大采样数存在上限缺口**：实现没有逐点计数截断；按该公式，10000×1 区域、目标 4096 会得到步长 2 和最多 5000 个网格点。狭长区域不能依赖该参数限制为恰好不超过指定数量。

候选偏移按有限样本对的归一化互相关排序，并要求重叠比例达标；相关性相同时保留搜索顺序中先遇到的项。Confidence 由最佳相关性和相对次佳峰的 margin 计算，`ok` 不等于高置信度，重复纹理出现多个同等峰值时尤其要检查该值。

`image-comparison-alignment` Table 包含 Status、EstimatedShiftX/Y、ShiftMagnitude、BestCorrelation、ZeroShiftCorrelation、PeakMargin、Confidence、OverlapFraction、SampleCount、SampleStep。状态解释如下：

| Status | 当前含义 |
| --- | --- |
| `disabled` | 参数关闭了预检；零偏移不是测量结论 |
| `insufficient_samples` | 参考 ROI 的有效网格点少于 4 |
| `insufficient_overlap` | 没有合格偏移且零偏移有效匹配少于 4 |
| `low_texture` | 没有合格偏移，但零偏移至少有 4 对样本；常见于低纹理，也可能受相关性/重叠门槛影响 |
| `ok` | 找到有限相关性且重叠达标的偏移；额外生成 `alignment-precheck` Transform Geometry |

非 `ok`/`disabled` 状态产生 `alignment_precheck_inconclusive`；`ok` 且幅值超过阈值时产生 `alignment_shift_suspected`。需要实际变换时使用[图像配准](./image-registration-v1.md)；镜头几何失真按[畸变校正](./lens-distortion-correction-v1.md)处理。

## 输出计划与预算

| 图像 artifact | 格式和用途 |
| --- | --- |
| `absolute-difference` | 同尺寸、同位深、同通道的未缩放绝对差；Float32 范围限制见上文 |
| `signed-difference` | 同尺寸、同通道的 32-bit float 有符号差 |
| `absolute-difference-visualization` | BGR24 归一化绝对差显示图 |
| `signed-difference-visualization` | BGR24，零差映射到中性灰，超出量程的有限值饱和 |
| `difference-heatmap` | BGR24，按每像素所选通道中的最大绝对差着色 |

成功结果还包含 `image-comparison` Measurement/StructuredData、`image-comparison-channels` 和 `image-comparison-alignment` Table，以及适用的 Geometry/Overlay 和 Diagnostics。结构化 schema 为 `colorvision.analysis.image-comparison/v2`。数值与显示输出分开使用，不从 PNG 颜色反推原始误差。

调用方可在 Invocation metadata 中指定输出，名称以逗号分隔、不区分大小写。例如：

```json
{
  "colorvision.image-comparison.requested-artifacts": "absolute-difference,signed-difference"
}
```

省略该键时保留兼容行为，请求全部五张图；`metrics-only` 不生成图像，但仍按参数计算 SSIM、对齐和其它结构化结果。C# 调用可使用 `ImageComparisonOutputPlan.CreateMetadata(...)`；空值或未知名称返回 `comparison_output_plan_invalid`。

ImageView 固定请求三张显示图，参数窗口没有输出计划开关。分配输出前检查 **192 MiB 图像输出总预算**及单数组大小，每次图像数组分配前检查取消；预算不包含输入快照、WPF 显示副本及分析工作内存，也不是进程内存上限。三张 BGR24 显示图需要 `宽×高×9` 字节，例如 7680×4320 约需 284.77 MiB，会返回 `comparison_output_budget_exceeded`。单用 ROI 或关闭 SSIM 不会缩小这些全幅输出。

需要保留原尺寸而减少图像输出时，调用方应显式选更少 artifact 或 `metrics-only`；不要把下采样后重新比较当作原分辨率结果。Descriptor 支持 `Interactive | Headless | Local | Deterministic | MultiInput | Roi`；Batch、Flow、Copilot 没有本算法的双输入配对入口，不按文件名或目录顺序隐式配对。

## 失败检查与验证范围

| 现象或代码 | 检查内容 |
| --- | --- |
| 候选文件加载失败、`Unsupported pixel format` | 文件是否可读、WPF 是否有解码器、首帧格式能否映射到平台格式；“所有文件”不会绕过解码限制 |
| `invalid_input_names` | 是否恰好提供 reference/candidate；不要把双输入仍命名为单输入 source |
| `dimension_mismatch` / `format_mismatch` | 两个算法输入的像素尺寸与规范格式；预检不会替你调整 |
| `color_space_unspecified` / `color_space_mismatch` | 是否有一致的显式编码标签；相同标签本身不证明已经做过色彩标定 |
| `comparison_roi_empty` / `no_finite_samples` | 区域是否与图像相交、是否含像素中心、所选通道是否只剩非有限样本 |
| `comparison_output_budget_exceeded` | 输出计划和全图尺寸；ROI 不减少分配，192 MiB 不包括输入内存 |
| SSIM 为 N/A、偏移为零 | 分别检查 EnableSsim/有效窗口数、对齐 Status/Confidence，不能直接判定图像相同 |

`ImageComparisonV1Tests` 覆盖基础指标、整数/浮点差分、非有限与溢出、输入验证、输出选择/预算、取消及输入/结果释放；`ImageComparisonAdvancedV1Tests` 覆盖 schema 迁移、ROI、SSIM、已知偏移、低纹理及结果窗口/overlay。界面测试验证控件存在和生命周期，不等于覆盖文件选择、全部解码器、PNG/CSV 操作或实际 Blink/Split 交互。

采样数量测试使用 257×257 方形输入，未覆盖上述狭长 ROI 上限缺口；参数测试也不能替代每个边界值的完整验证。`ImageAlgorithmPerformanceGateTests.ComparisonPipelineProbe` 仅在 `COLORVISION_IMAGE_ALGORITHM_PERF=1` 时执行，关闭 SSIM/对齐并只请求 heatmap；它的 4K/8K 结果不能证明默认三图窗口或全部分析满足同样预算。探针会消耗较多 CPU/内存，比较性能须保持输入与配置一致。

交付验证还应遵循[平台验收门禁](./image-algorithm-platform-v1.md#m0-验收门禁)。

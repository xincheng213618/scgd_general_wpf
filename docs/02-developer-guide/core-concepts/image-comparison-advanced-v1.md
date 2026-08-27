# 图像比较高级 V1（M4）

M4 在 M3 的同一稳定算法身份 `colorvision.analysis.image-comparison` 上增加 ROI 比较、SSIM 和对齐前质量诊断。行为版本升级为 `1.1.0`，参数 schema 升级为 2；M3 的 schema 1 Invocation 由显式 `1 → 2` migrator 迁移，原有参数值保持不变，新参数使用统一默认值。

## 盘点与阶段边界

仓库没有可复用的通用 SSIM 或双图对齐前检查。M1 已经实现矩形、圆、多边形的像素中心、DPI、裁剪规则，因此 M4 把该逻辑抽为共享 `AlgorithmPixelRoi`，M1 与 M4 使用同一实现。M3 已经提供严格双输入校验、精确差分、MSE/RMSE/PSNR、热力图、blink/split 和资源所有权，M4 直接扩展该 provider，没有创建同名算法或第二套 UI 计算。

M4 的“对齐”只是一项有界采样的平移预检：它报告候选图相对 reference 的整数偏移、相关性、重叠率和置信度，绝不变换输入或差分结果。真实相关性/特征配准和镜头畸变校正属于 M8；M4 不提前实现 M5–M12。

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

`image-comparison` Measurement 增加聚合/逐通道 SSIM、有效/无效窗口计数及对齐偏移/相关性；`image-comparison-channels` Table 增加 SSIM 列；StructuredData schema 更新为 `colorvision.analysis.image-comparison/v2`，记录 ROI、SSIM 和对齐全部复现信息。M3 的五个 Image artifact 保持名称、格式和所有权不变。

ImageView 的“算法调用 → 图像比较”提供全图、矩形、圆和多边形入口。当前图与文件候选仍形成不可变快照并使用共享 analysis session；窗口显示差分/blink/split、通道质量和对齐预检，可导出 JSON/CSV bundle，PNG 和结构化导出默认拒绝覆盖。关闭、取消、切图、source revision 改变或较新 Invocation 都会使迟到结果失效。

Descriptor 仍只声明 `Interactive | Headless | Local | Deterministic | MultiInput`，并增加三种 ROI 支持。Batch、Flow 和 Copilot 尚无经审批的双输入配对契约，因此 M4 不为能力矩阵强行声明它们，也不把文件系统候选或远端设备算法暴露给 Copilot。

## M4 门禁

定向回归覆盖 schema 1→2 迁移与默认值、Catalog 版本/ROI 声明、Gray8/Gray16/Gray32Float/BGR 的 golden SSIM、常量闭式解、跨位深尺度一致性、非有限窗口、矩形/圆/多边形及 Physical/DPI 坐标、ROI 外精确差分与黑色热力图、空/裁剪 ROI、已知平移方向、低纹理诊断、取消和 transferred 双输入释放，以及 ImageView 菜单、质量表和 transient overlay 生命周期。

无占位 DLL 状态下可复现的证据为：公共契约项目构建 0 警告/0 错误，M4 定向测试 39/39；UI 托管测试程序集使用 `--no-build` 运行 1550 项，其中 1547 项通过，另外 3 项因真实 `opencv_helper.dll` 缺失而失败。M0–M4 组合回归、Copilot 全量测试和文档校验此前也已运行，但主项目 x64 与 ImageEditor 构建在当前环境都会经 `Native/opencv_helper/opencv_helper.vcxproj:28` 以 MSB4278 失败（缺少 `Microsoft.Cpp.Default.props` / C++ workload）。临时零字节 DLL 已删除，依赖它所得的构建成功不计入门禁。

## M0.5 集成加固清单（M4 后、M5 前）

M4 的算法数值切片已经完成，但以下平台问题尚未闭环，因此不能把 latest-wins、格式兼容、overlay 生命周期或大图性能写成已完全满足：

- `ImageView.Clear()` 只清理显示状态，没有推进 frame-store revision，也没有使 algorithm preview/analysis session 失效。Clear 期间完成的旧结果仍可能重新显示、提交或打开结果窗口。
- `AcquireImageFrameCore -> WriteableBitmap.ToHImage -> ImageAlgorithmInputFactory.Copy(HImage)` 只携带 depth/channels。当前公共图像格式不能区分 RGB/BGR、Bgr32 的未用字节、直 alpha 与 premultiplied alpha；ImageView 接受的 Rgb24、Rgb48、Bgr32 和 Pbgra32 因而存在通道或 alpha 语义误解释风险。
- `AlgorithmOverlayStore.Clear*()` 只清逻辑 artifact；renderer 独立加入 DrawCanvas 的 visual 没有随切图/commit 统一移除，persistent visual 也没有统一回收路径，旧 overlay 可能残留到新图。
- 新 preview session 覆盖 host session ID 时不会取消或通知旧 session；`PreviewAsync` 忽略 `SetLatestAlgorithmPreviewInvocation` 返回的 `false`。PseudoColor 又只按 source revision 重建 session，因此同 revision 被其他预览抢占后可能持续得到 `Superseded`。
- Catalog 为 Gray32Float 声明 Threshold 支持，但默认阈值仍为 128、验证范围仍为 0..65535，而 provider 对 float 使用峰值 1；正常 `[0,1]` 图像使用默认值会全黑。
- AlgorithmsContextMenu 的展示/顺序仍硬编码，Batch 仍有 `CompatibilityOrder`；Catalog 已统一执行和默认值，但尚未完全消除展示定义重复。
- preview 数据路径仍有 lease 到 byte buffer、Mat、结果 buffer 和 WriteableBitmap 的多次全帧复制；lease/revision 所有权正确性得到复用，但大图内存峰值和延迟没有基准证明。

当前工作停在 M4/M5 阶段边界。工作树 `b13e6471d` 与当前 develop `59729d617` 的差异按短距离集成校验处理，不否定或重做已经完成的 M0–M4。后续应迁移/重放现有改动，重点复核 ImageAlgorithmPreviewSession、BatchImageAlgorithms、ImageView、Draw/Overlay 和相关测试的重叠，再重跑定向测试、UI/Copilot 全量和主项目 x64 构建。上述正确性问题及集成校验组成 M0.5 加固清单；在独立审查汇总并确认整改方案前，M0.5 与 M5 都保持 pending。

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

`image-comparison` Measurement 增加聚合/逐通道 SSIM、有效/无效窗口计数及对齐偏移/相关性；`image-comparison-channels` Table 增加 SSIM 列；StructuredData schema 更新为 `colorvision.analysis.image-comparison/v2`，记录 ROI、SSIM、对齐和请求的输出计划。M3 的五个 Image artifact 保持名称、格式和所有权不变；旧 Invocation 未指定计划时仍返回全部五张。ImageView 明确只请求三个 visualization，headless/API 可按名称选择精确差、显示差、heatmap 或 `metrics-only`。请求图像在分配前按 192 MiB retained-output 总预算检查，超限结构化失败。

ImageView 的“算法调用 → 图像比较”提供全图、矩形、圆和多边形入口。当前图与文件候选仍形成不可变快照并使用共享 analysis session；窗口显示差分/blink/split、通道质量和对齐预检，可导出 JSON/CSV bundle，PNG 和结构化导出默认拒绝覆盖。关闭、取消、切图、source revision 改变或较新 Invocation 都会使迟到结果失效。

Descriptor 仍只声明 `Interactive | Headless | Local | Deterministic | MultiInput`，并增加三种 ROI 支持。Batch、Flow 和 Copilot 尚无经审批的双输入配对契约，因此 M4 不为能力矩阵强行声明它们，也不把文件系统候选或远端设备算法暴露给 Copilot。

## M4 门禁

定向回归覆盖 schema 1→2 迁移与默认值、Catalog 版本/ROI 声明、Gray8/Gray16/Gray32Float/BGR 的 golden SSIM、常量闭式解、跨位深尺度一致性、非有限窗口、矩形/圆/多边形及 Physical/DPI 坐标、ROI 外精确差分与黑色热力图、空/裁剪 ROI、已知平移方向、低纹理诊断、取消和 transferred 双输入释放，以及 ImageView 菜单、质量表和 transient overlay 生命周期。

无占位 DLL 状态下的原 M4 证据为：公共契约项目构建 0 警告/0 错误，M4 定向测试 39/39；UI 托管测试程序集使用 `--no-build` 运行 1550 项，其中 1547 项通过，另外 3 项因当时缺少真实 `opencv_helper.dll` 而失败。当前复验显式引用用户现有的 1,592,832-byte 真实 Release DLL：UI 全量 2069 通过且 5 项按自身 native export 条件跳过，主项目 x64 构建 0 错误、316 个 analyzer/nullability 等警告。该 DLL 不是占位文件；本机 C++ workload 仍缺失，默认构建在 native vcxproj 以 MSB4278 阻塞，native 项目自身未获得重建验证。

## M0.5 集成加固清单（M4 后、M5 前）

M4 的算法数值切片已经完成。M0.5.1 至 M0.5.7 已闭环，后续条目仍未完成，因此不能把大图性能写成已完全满足：

- **已完成 M0.5.2**：Clear、SetImageSource 和原地像素提交统一推进一次 frame-store revision，取消/失效当前 preview 与 analysis，并拒绝迟到提交或结果窗口展示。
- **已完成 M0.5.1**：WPF 入口显式规范化 RGB/BGR、Bgr32 未用字节、直通/预乘 Alpha 与 Indexed8 palette；直接 HImage 适配必须声明中立格式，不再只按 depth/channels 推断。
- **已完成 M0.5.3**：preview 与 analysis 共享按 document/revision 分区的非 WPF latest-wins 协调器；同 scope 跨入口/owner 抢占会取消旧 run，旧完成或释放不能影响新 owner，不同文档/revision 不会误取消。被抢占的 preview session 可在同 revision 重新 claim，PseudoColor 不再永久 `Superseded`。
- **已完成 M0.5.4**：统一 overlay manager 同时拥有 artifact 与 DrawCanvas Visual，并记录 document/revision/token；Commit 清 transient、保留并 rebase persistent，换图、Clear 和 ImageView 释放清全部，窗口关闭按 lifetime 释放，同名替换不受旧 session 迟到 Dispose 影响。兼容的 `AlgorithmOverlays` façade 清理也会同步移除其受管 Visual。
- **已完成 M0.5.5**：Threshold 与 bilateral `SigmaColor` 升级为 schema 2 的 0..255 标称强度刻度，并按 8/16/float 峰值映射；ImageView 与 Batch 投影同一默认 Threshold=128。schema 1 通过 migrator 保留旧整数绝对 DN，旧 float 越界阈值结构化拒绝；Gray8/Gray16/Gray32Float golden 与双边滤波数值容差测试覆盖当前语义。Canny、Basic Adjustment、空间参数及无强度参数算法的位深审计未发现同类默认漂移。
- **已完成 M0.5.6**：Descriptor 的中立展示元数据拥有 ImageView 兼容菜单 ID/顺序和图像输出 Batch 顺序；两个宿主都按 Catalog capability + presentation 投影，`CompatibilityOrder` 和菜单成员清单已删除。现有专用 WPF 编辑器仅保留为命令兼容适配器；新增适用 descriptor 的自动出现、能力拒绝、旧顺序/alias 及 Canny 默认值同源均有回归测试。结构化分析 Batch 保留独立 processor，不被错误投影进只接受图像 artifact 的 Batch 窗口。阶段证据为投影测试 5/5、相关扩展回归 148/148、UI 全量 2064 通过/5 项按自身 native export 条件跳过、Copilot 全量 1863/1863、公共契约构建 0 错误，文档 180 文件与 31 个重定向通过；当时关于主项目零警告的记录不作为当前可复现证据。
- **已完成 M0.5.7**：`ColorVision.Algorithms` 可独立生成包含两个目标框架与 README 的 NuGet，ImageEditor 包声明同版本依赖，CI 固定先发布 Algorithms；包契约 3/3。RemoveMoire 在 provider 选择前验证真实 DLL/export，识别打包 runtime 路径并保留已验证模块，失败返回结构化诊断；定向测试 4/4。Copilot 明确算法意图可达受审批与目录/覆盖/数量策略保护的白名单工具，远端或反射发现能力仍不可见；定向 12/12、扩展 83/83、全量 1868/1868。Flow 文档明确本地路径当前是 adapter/API，并非生产节点。UI 全量 2069 通过/5 项条件跳过；显式真实 helper 的主项目 x64 构建 0 错误、316 个警告，默认 native 构建仍因缺 C++ workload 阻塞。
- **已完成 M0.5.8**：OpenCV/native 输入改为受 `AlgorithmImageBuffer` 生命周期约束的 pin/只读 header，Mat→Result 只分配最终 owned buffer，常用格式 Result→WPF 不再 `ToArray()`；比较结果窗口只物化当前选中的一张差分视图。4K/8K Gray16/Bgra32 的 opt-in 实测、managed allocation 和宽松延迟预算均成为自动门禁；lease、canonical normalization、可取消 run snapshot、Result 所有权与 Batch 返回值所需复制仍明确保留。阶段证据为性能 probe 8/8、相关回归 174/174、UI 全量 2079 通过/5 项条件跳过、显式真实 helper 的主项目 x64 构建 0 错误/316 警告，以及文档 180 文件/31 重定向通过。

## M0.5.8 性能门禁

修改前后以同一 x64 Debug、关闭 SSIM/对齐预检的 opt-in probe 复测；预览列为 managed allocated bytes，括号内为本机修改后延迟：

| 尺寸/格式 | 修改前 | 修改后 |
| --- | ---: | ---: |
| 4K Gray16 | 49,854,272 | 16,617,216 (52.1 ms) |
| 4K Bgra32 | 99,691,888 | 33,250,864 (70.5 ms) |
| 8K Gray16 | 199,253,344 | 66,383,680 (80.9 ms) |
| 8K Bgra32 | 399,047,968 | 133,491,856 (536.0 ms) |

比较性能门禁不再把“总是生成五张全尺寸图”当作既定功能成本。兼容调用仍可请求全部五张，但在分配前受 192 MiB retained-output 预算约束；交互入口只请求三个显示 artifact，性能 probe 只请求它实际检查的 heatmap。门禁同时验证预取消不进入 prepare、4K 超预算计划不产生大图分配、按需输出的 retained bytes 与 Result 释放。其余保留边界是 WPF→lease 缓存、带格式规范化 snapshot、并发取消安全的 run snapshot、最终 Result buffer、选中视图的 WPF 存储和 Batch 返回值；Bgr48/Bgra64 另需 RGB 通道交换，不以不安全借用换取数字变小。

M0–M4 与 M0.5.1 已安全集成到 develop 基线，M0.5.2 至 M0.5.8 在后续分支形成串行增量。下一阶段只推进 M5 的 Blob/连通域最小纵向切片；更高里程碑保持 pending，直到前一门禁通过。

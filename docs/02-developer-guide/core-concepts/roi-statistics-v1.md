---
knowledge_id: "algorithms.roi-statistics"
knowledge_type: "reference"
status: "current"
summary: "ROI统计的区域选择、百分位、直方图、坏点候选计数/返回上限及六文件CSV导出；说明Float32精确统计预算、列名精度限制和实际窗口操作。"
aliases: ["ROI 统计", "区域统计", "ROI直方图", "ROI百分位", "ROI坏点候选", "ROI 统计导出", "直方图分箱数", "最多返回坏点候选", "RoiStatistics", "ROIStatistics", "RoiStatisticsRectangle", "RoiStatisticsCircle", "RoiStatisticsPolygon", "RoiStatisticsParameters", "RoiStatisticsAlgorithmProvider", "RoiStatisticsEditorTool", "RoiStatisticsResultWindow", "HistogramBins", "Percentiles", "DetectBadPixelCandidates", "BadPixelNeighborhoodRadius", "BadPixelSigmaThreshold", "BadPixelMinimumDeviationFraction", "MaximumBadPixelCandidates", "StdDevPopulation", "roi.bad_pixel_candidate_count", "roi.bad_pixel_channel_candidate_count", "roi_required", "roi_empty_after_clip", "roi_exact_float_statistics_budget_exceeded", "bad_pixel_candidates_truncated"]
code_paths: ["UI/ColorVision.ImageEditor/Algorithms/RoiStatisticsAlgorithmProvider.cs", "UI/ColorVision.ImageEditor/Algorithms/RoiStatisticsParameters.cs", "UI/ColorVision.ImageEditor/Algorithms/StandardAlgorithmCatalog.cs", "UI/ColorVision.ImageEditor/Algorithms/ImageAlgorithmPlatform.cs", "UI/ColorVision.ImageEditor/Algorithms/AlgorithmPixelRoi.cs", "UI/ColorVision.ImageEditor/Algorithms/ImageAlgorithmInputFactory.cs", "UI/ColorVision.ImageEditor/Algorithms/AlgorithmResultExporter.cs", "UI/ColorVision.ImageEditor/EditorTools/Algorithms/Calculate/RoiStatistics", "UI/ColorVision.ImageEditor/TransientRoiSelectionSession.cs", "UI/ColorVision.Algorithms/AlgorithmInvocation.cs", "UI/ColorVision.Algorithms/AlgorithmResults.cs", "UI/ColorVision.Algorithms/AlgorithmExecution.cs", "UI/ColorVision.ImageEditor/BatchProcessing/BatchAlgorithmAnalysisProcessor.cs", "Engine/ColorVision.Engine/FlowProcessing/Algorithms/LocalFlowImageAlgorithmAdapter.cs", "UI/ColorVision.ImageEditor/EditorTools/Histogram/HistogramEditorTool.cs", "UI/ColorVision.Common/Utilities/ImageUtils.cs"]
test_paths: ["Test/ColorVision.UI.Tests/RoiStatisticsV1Tests.cs", "Test/ColorVision.UI.Tests/ImageAlgorithmPlatformTests.cs", "Test/ColorVision.UI.Tests/TransientRoiSelectionSessionTests.cs", "Test/ColorVision.UI.Tests/ImageAlgorithmPerformanceGateTests.cs"]
related: ["algorithms.platform", "algorithms.index", "algorithms.image-profile", "algorithms.image-comparison"]
---

# ROI 统计：区域、直方图与坏点候选

ROI 统计计算所选矩形、圆形或多边形内的通道统计、百分位、直方图、饱和/无效值及局部异常点候选，适合检查区域亮度分布和定位异常读数。坏点候选是局部数值规则的结果，不直接给出设备缺陷或产品合格判定。沿线读取变化使用[灰度与颜色剖面](./image-profile-v1.md)，两幅图的差异使用[图像比较](./image-comparison-v1.md)。

当前稳定算法 ID 为 `colorvision.analysis.roi-statistics`，算法版本 `1.0.0`、参数 schema 为 1。它接收一张图和 Rectangle/Circle/Polygon ROI，输出七个结构化 artifact，不生成新图像。核心格式支持 Gray、BGR、BGRA 的 8/16-bit 和 Float32；灰度输出 Gray，彩色按 B/G/R/A 分通道统计，包括 Alpha，不自动增加亮度通道。

## 在 ImageView 中选择区域

1. 打开图像，在图像区域菜单选择“算法调用 → ROI 统计”，选择“矩形 ROI…”“圆形 ROI…”或“多边形 ROI…”。
2. 在“ROI 统计参数”窗口设置直方图、百分位和坏点参数，提交后开始选择；关闭参数窗口而不提交则结束。
3. 按入口完成选择：

   | 入口 | 操作 |
   | --- | --- |
   | 矩形 | 从一个角拖到对角，松开完成；显示坐标的宽和高都须大于 1 DIP |
   | 圆形 | 按下位置是圆心，拖到半径端点后松开；直径须大于 1 DIP |
   | 多边形 | 逐点单击，按 Enter/Space 或右键结束；至少三点、非零面积、相邻点不重复且不自交 |

4. 等待分析；进度窗口可以取消。成功后打开“ROI 统计结果”，并在图像上标出 ROI 和本次返回的候选点。

无效选择会继续等待；Esc 取消当前选择。已有矩形、圆和多边形图元可右键选择“ROI 统计…”，省去重新绘制区域。现有多边形入口按点集统计闭合区域，不依据图元的 `IsComple` 再决定是否闭合。

分析不修改原图，结果窗口关闭后释放结果并移除该临时 overlay。路径和执行绑定当前 document/revision，切图、修改源像素或新分析会按[统一 session 规则](./image-algorithm-platform-v1.md#m0-执行与所有权规则)使旧请求失效。

工具栏 `Histogram` 是另一入口：它读取显示位图的整幅图，固定 256 桶，Gray16/Rgb48 按高 8 位分桶。需要区域、原位深数值和以下统计字段时，使用本页 ROI 统计入口。

## 读懂结果

### 统计摘要

每行对应一个通道；窗口顶部“ROI 像素”是选区内像素数，“坏点候选”是去重后的候选像素位置数。整数值单位为 DN，Float32 单位为原始值域的 value；Float32 不隐式归一化。

| 字段 | 口径 |
| --- | --- |
| `IncludedCount` | ROI 内像素数，包含该通道的非有限值 |
| `ValidCount` / `InvalidCount` | 有限 / 非有限值数量；两者之和等于 IncludedCount |
| `NaNCount`、`PositiveInfinityCount`、`NegativeInfinityCount` | InvalidCount 的分类 |
| `Minimum`、`Maximum`、`Mean` | 只计算有限值 |
| `StdDevPopulation` | 总体标准差，分母为有限样本数 N，不是 N−1 |
| `P1`、`P5`、`P50` 等 | 按参数顺序输出的百分位，使用排序后 `(N−1)×p/100` 位置的线性插值 |
| `LowSaturatedCount` / `HighSaturatedCount` | 有限值中 `<=0` / `>=标称最大值` 的数量；8-bit、16-bit、Float32 的最大值分别为 255、65535、1 |
| `SaturatedCount` | 低端与高端饱和数量之和，不等于坏点数 |
| `BadPixelCandidateCount` | 当前通道的全部候选数，不受返回表上限截断 |

例如有限值 `[1,3,5,9]` 的 P25、P50、P75 分别为 2.5、4、6；分箱数不改变这些百分位。全为非有限值的通道仍有计数行，但 min/max/mean/stddev/百分位为空；只要 ROI 包含像素，整个调用仍可成功，不能把空统计读成零。

窗口数值按 `G10` 显示。BGRA 的 A 通道也执行饱和和坏点规则；全部不透明的 Alpha 通道可能全部被计为高端饱和，这是通道数值口径，不表示颜色数据损坏。

### 直方图

横轴 `Pixel value` 使用桶中心，纵轴 `Count` 是桶内有限值数量。图中折线连接桶计数，不表示逐像素顺序，也不是归一化概率密度。下方表格给出 `LowerInclusive`、`Upper`、`UpperInclusive` 和 `Count`，以表格边界解释读数。

| 输入 | 分桶规则 |
| --- | --- |
| 8/16-bit | 覆盖完整位深范围；桶边界由 `2^位深 / HistogramBins` 计算，最末 Upper 为 256/65536，并标记闭区间。实际整数样本最大仍为 255/65535 |
| 非常量 Float32 | 覆盖当前 ROI 有限值的 min..max，最后一桶包含 max；不量化回 8 位 |
| 常量 Float32 | 只返回一个闭合桶 `[value,value]`，Count 为有限值总数，不强行返回 HistogramBins 个重复桶 |
| 没有有限值的 Float32 | 直方图无行，查统计摘要中的无效值分类 |

同一通道所有桶 Count 之和等于 ValidCount。常量 Float32 的单桶应结合表格读取；不同 ROI 的浮点桶范围可能不同，比较分布时不能只比较 BinIndex。

### 坏点候选与返回上限

检测对每个 ROI 像素、每个通道分别执行。邻域是半径 r 的 `(2r+1)×(2r+1)` 方形，排除中心像素、ROI 外和图像外位置，以及非有限邻居；有限邻居不足三个则跳过该通道。

设局部中位数为 m，`MAD = median(|邻居值−m|)`：

- `Threshold = max(最小坏点偏差比例×标称最大值, Sigma阈值×1.4826×MAD)`。
- `Deviation = |中心值−m|` 必须严格大于 Threshold 且大于 0；非有限中心值不成为候选。
- `Confidence = clamp(1−Threshold/Deviation,0,1)`，Threshold<=0 时为 1。这是强度评分，不是缺陷概率。

候选表每行是一组“X/Y 坐标 × 通道”，`Reason` 为 `local_median_outlier`。同一像素多个通道异常可占多行；顶部数量按坐标去重，Measurement `roi.bad_pixel_channel_candidate_count` 则累计全部通道候选。

`MaximumBadPixelCandidates` 是所有通道合计的返回上限。正数时仍扫描全部区域并统计总数，但 Table/Geometry/Overlay 只保留最强 K 项，依次比较 Confidence、Deviation/Threshold、Deviation，并按 Y、X、通道稳定打破并列。截断时诊断含 `bad_pixel_candidates_truncated`。

设置 `DetectBadPixelCandidates=false` 或上限为 0 会跳过检测，计数为零、候选表为空；这不证明图像没有异常。导出只能保存本次保留的候选坐标，不能恢复被上限截去的项；需要更多坐标时调大上限并重新运行。

## 参数参考

`RoiStatisticsParameters` 不保存 ROI 点；区域属于 `Invocation.Roi`。范围包含端点，浮点参数必须有限，关闭坏点检测后仍校验相关参数。

| 参数 / 界面名称 | 默认值 | 范围与作用 |
| --- | --- | --- |
| `HistogramBins` / 直方图分箱数 | 256 | 2–4096；改变直方图分桶，不改变均值和百分位 |
| `Percentiles` / 百分位 (%) | 1、5、50、95、99 | 1–32 个互异有限值，各在 0–100；见下方列名限制 |
| `DetectBadPixelCandidates` / 检测坏点候选 | true | 是否执行局部异常点检测 |
| `BadPixelNeighborhoodRadius` / 坏点邻域半径 | 1 | 1–5；扩大邻域会增加扫描工作量 |
| `BadPixelSigmaThreshold` / 坏点 Sigma 阈值 | 6 | 0.1–100 |
| `BadPixelMinimumDeviationFraction` / 最小坏点偏差比例 | 0.02 | 0–1；乘以该通道标称最大值，不是 ROI 自身的动态范围 |
| `MaximumBadPixelCandidates` / 最多返回坏点候选 | 1000 | 0–100000；0 跳过检测，正数限制保留行数 |

**百分位列名存在精度限制。** 参数校验允许高精度小数，但摘要列名及 Measurement 的 percentile qualifier 使用 `0.###` 格式。`50.0001` 和 `50.0002` 都变为 `P50`：摘要行的后值覆盖前值，Columns 仍重名，WPF DataTable 创建可能抛出重复列名异常。调用方应让格式化后的列名也唯一，通常使用不超过三位小数且互异的百分位；这项约束尚未由参数校验强制执行。JSON provenance 保留原始参数，不能据四舍五入后的 qualifier 还原完整请求精度。

## ROI 坐标与资源限制

整数 Pixel 坐标表示像素中心，原点在左上角。统计直接读取被包含的像素，不对边缘做插值或按覆盖面积加权：

- Rectangle 使用 `[x,x+width) × [y,y+height)` 半开区间；Circle/Polygon 包含边界像素中心。Polygon 的包含判断使用边界检测和奇偶规则。
- ROI 可以越界，扫描范围裁剪到图像；有裁剪时记录 `roi_clipped`，没有像素中心落入区域时返回 `roi_empty_after_clip`。结果 Geometry 保留转换后的原始 ROI，并不是裁剪后的轮廓。
- Physical 坐标单位为毫米，分别按 X/Y DPI 转为 Pixel。ImageView 的 WPF DIP 同样按 X/Y DPI 换算；核心格式和 Alpha 规范化见[平台输入边界](./image-algorithm-platform-v1.md#flow-与发布适配)。
- 非等向 DPI 下，ImageView 的圆转换为 64 点像素多边形，实际包含判断也使用该多边形。直接传入 Physical Circle 时，核心按椭圆方程判断，只将显示 Geometry 近似为 64 点；两条输入路径的边缘计数不承诺完全相同。
- 临时多边形选择器会拒绝共线或自交点集；公共 Polygon ROI 校验只检查至少三点且坐标有限，已有图元/API 调用不应把校验通过等同于简单多边形有效性。

### Float32 精确统计预算

Float32 为精确百分位保留并排序有限值。分配样本列表前，先数 ROI 内像素，按 `像素数×通道数×4` 字节检查 **16 MiB** 上限；检查不读取有效值比例，即使多数为 NaN/Infinity 也按全部 ROI 像素预留。关闭坏点检测、减少 HistogramBins 或减少百分位项都不会降低这项样本预算。

| 格式 | 可通过样本预算的最大 ROI 像素数 |
| --- | ---: |
| Gray32Float | 4194304 |
| Bgr96Float | 1398101 |
| Bgra128Float | 1048576 |

超过时返回 `roi_exact_float_statistics_budget_exceeded`，应缩小 ROI。比如 3840×2160 Gray32Float 需要 33177600 字节，仅样本就超过上限。16 MiB 约束的是浮点样本数组，不是输入、结果表、绘图副本或整个进程的内存上限；8/16-bit 使用精确值频数计数，不受这一浮点门槛限制。

坏点候选存储受 K 限制，正数 K 不减少邻域扫描；大区域可关闭检测以省去该阶段。浮点预检、主扫描、候选扫描和排序前后设有取消检查，单次框架排序不能中途取消。窗口会复制全部返回表格并绘制全部直方图桶，没有额外的 2000 行预览上限；大量候选或高分箱数仍会增加显示开销。

## 导出与调用

### 从结果窗口导出

点击“导出 JSON”或“导出 CSV”，选择新文件名。当前 ROI 结果窗口同步等待导出完成，没有导出进度或取消控件；分析进度窗口的取消不适用于此步骤。

JSON 保存算法 ID/版本、状态、诊断及全部七个 artifact，包括参数来源。CSV 是六个文件；以选择 `stats.csv` 为例：

| 文件 | 内容 |
| --- | --- |
| `stats.csv` | Measurement 数值和 qualifier |
| `stats_roi-statistics-summary.csv` | 每通道统计摘要 |
| `stats_roi-histogram.csv` | 每通道每桶的边界与计数 |
| `stats_bad-pixel-candidates.csv` | 本次实际保留的候选行 |
| `stats_roi-statistics-geometry.csv` | 原始 ROI 和保留候选点的 Pixel 几何 |
| `stats_roi-statistics-provenance.csv` | schema `colorvision.analysis.roi-statistics/v1`、输入格式/DPI、原始 ROI、参数与规则；来源内容位于 DataJson 列 |

导出使用 UTF-8 BOM，默认拒绝覆盖主文件或任一伴随文件。`AlgorithmResultExporter` 先写临时文件，再逐个提交；失败时尝试删除本次新建目标，清理失败仍可能留下部分文件，不能把整组 CSV 视作文件系统事务。收到“导出失败”时检查同名伴随文件和目录权限，改用新文件名并核对完整文件组。

### API、Batch 与本地 Flow

Catalog 别名 `RoiStatistics`、`ROIStatistics`、`RoiStatisticsRectangle`、`RoiStatisticsCircle`、`RoiStatisticsPolygon` 解析到同一算法。调用方通过 `AlgorithmInvocation.Create` 传入参数和 ROI，用 Runner 执行并释放整个 `AlgorithmResult`；结构化结果中的三个表、Measurement、Geometry、Overlay 和来源数据与上述界面/导出对应。

`BatchAlgorithmAnalysisProcessor` 使用保存的 Invocation 逐文件导出分析结果，ROI 不自动随图像尺寸缩放；Mat 转换默认 96 DPI。默认输出 `_analysis` 后缀的 JSON、拒绝覆盖，失败项可继续下一项，取消不撤销先前导出。它不进入只接收主图像 artifact 的批量图像处理列表。

`LocalFlowImageAlgorithmAdapter.ExecuteRawAsync` 复制 RAW 帧且不取得外层 lease 所有权，当前桥接为 8/16-bit、1/3/4 通道、默认 96 DPI。它是可调用 API，尚无对应生产画布节点；注册边界见[平台 Flow 说明](./image-algorithm-platform-v1.md#执行平面与兼容层)。ROI 统计未进入 Copilot 白名单；Catalog 可发现不代表现有 Copilot 工具可以提交 ROI。

## 排障与验证范围

| 现象 / 代码 | 检查 |
| --- | --- |
| 选择无法结束 | 矩形/圆是否过小；多边形是否至少三点、非零面积且不自交；Esc 可取消后重选 |
| `roi_required` / `roi_kind_unsupported` | 是否缺少 ROI 或传入 Polyline；本算法只接受区域形状 |
| `roi_empty_after_clip` | 坐标单位、DPI、图像尺寸，以及区域是否实际包含像素中心 |
| `roi_exact_float_statistics_budget_exceeded` | 按上表缩小浮点 ROI；仅调小分箱数或关闭坏点检测无效 |
| 顶部候选数与表格行数不同 | 顶部按位置去重、表格按通道展开且受 K 截断；查看 truncation 诊断 |
| 坏点数为零 | 先确认检测开关及 K，再检查有限邻居数和阈值；不能仅凭零计数判断图像正常 |
| 平均值空白或直方图只有一行 | 检查 ValidCount；没有有限值与常量浮点分布是不同情况 |
| 百分位设置后结果窗口失败 | 检查格式化至三位小数后的 P 列名是否重复 |

`ImageAlgorithmPlatformTests` 覆盖矩形/圆/多边形、物理坐标、Gray8/BGR/Float32、统计/百分位/直方图、非有限值、饱和、候选与取消释放。`RoiStatisticsV1Tests` 补充 Gray16、精确浮点百分位、样本预算、常量桶、候选上限、六文件导出、Batch/Flow、窗口释放与 session/DPI 适配；`TransientRoiSelectionSessionTests` 验证选择器规则。`ImageAlgorithmPerformanceGateTests` 的 1024² Gray8、K=1 用例检查候选存储和耗时，不代表所有尺寸/参数都达到同一性能。

现有用例未覆盖百分位列名精度冲突，也不能证明完整鼠标操作、所有通道/ROI 组合和导出失败清理都经过运行验证。公共执行门禁见[统一平台](./image-algorithm-platform-v1.md#m0-验收门禁)。

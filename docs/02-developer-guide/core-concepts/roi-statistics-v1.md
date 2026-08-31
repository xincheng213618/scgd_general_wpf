---
knowledge_id: "algorithms.roi-statistics"
knowledge_type: "reference"
status: "current"
summary: "RoiStatistics 的输入、参数、结果、宿主接入与定向验证契约。"
aliases: ["ROI 统计如何计算灰度直方图和坏点候选","RoiStatistics","RoiStatisticsAlgorithmProvider"]
code_paths: ["UI/ColorVision.ImageEditor/Algorithms/RoiStatisticsAlgorithmProvider.cs","UI/ColorVision.ImageEditor/Algorithms/StandardAlgorithmCatalog.cs","UI/ColorVision.ImageEditor/Algorithms/ImageAlgorithmPlatform.cs"]
test_paths: ["Test/ColorVision.UI.Tests/RoiStatisticsV1Tests.cs"]
related: ["algorithms.platform","algorithms.index"]
---

# ROI 统计 V1（M1）

M1 在统一图像算法平台上提供可执行、可展示和可导出的矩形、圆形与多边形 ROI 统计。它不包含灰度/颜色剖面；剖面属于 M2，不能复用本页接口假装已经实现。

## 盘点与边界

仓库原有 Histogram 工具只计算整幅显示图的 256 桶直方图；`TransientRoiSelectionSession` 已支持矩形、圆和多边形临时选择；部分旧分析工具各自解释 `RoiRect`。M1 复用临时选择和 `ImageFrameLease`，不复制旧 Histogram 实现，也不改动 M2 的 Profile 代码。

M1 的稳定身份是 `colorvision.analysis.roi-statistics`，算法版本 `1.0.0`，参数 schema 为 1。Descriptor 支持 G8/G16/G32F、BGR8/BGR16/BGR32F、BGRA8/BGRA16/BGRA32F，声明 `Interactive | Batch | Flow | Headless | Local | Deterministic | Roi`，不产生图像输出。别名 `RoiStatistics`、`ROIStatistics` 和三个旧式形状名只用于兼容解析。

## 参数契约

| 参数 | 默认值 | 验证/语义 |
| --- | --- | --- |
| `HistogramBins` | 256 | 2..4096 |
| `Percentiles` | 1、5、50、95、99 | 1..32 个互异有限值，范围 0..100 |
| `DetectBadPixelCandidates` | true | 是否执行局部异常点检测 |
| `BadPixelNeighborhoodRadius` | 1 | 1..5，方形邻域 |
| `BadPixelSigmaThreshold` | 6 | 0.1..100，MAD Sigma 倍数 |
| `BadPixelMinimumDeviationFraction` | 0.02 | 0..1，相对标称范围的最小偏差 |
| `MaximumBadPixelCandidates` | 1000 | 0..100000；0 完全跳过候选扫描；正数时流式统计总数，Table/Geometry/Overlay 有界保留最强 top-K |

所有参数由 Catalog 提供同一默认值和校验，ImageView、Batch 与 Flow 不维护副本。坏点检测关闭时仍返回空候选表和零计数，保持结果 schema 稳定。

## ROI 与数值规则

- 坐标原点在左上角，输出 Geometry 统一为 Pixel；ImageView 的 WPF DIP 按当前位图 X/Y DPI 分别换算。
- 矩形使用半开区间；圆和多边形包含边界上的像素中心。物理坐标以毫米表示，通过输入 DPI 转换。
- 非正方 DPI 下，ImageView 画出的圆换算为 64 点像素多边形，避免把椭圆误报为圆。
- ROI 可超出图像，执行时与图像求交并产生 `roi_clipped` 诊断；交集不包含像素中心时返回 `roi_empty_after_clip`。
- min/max/mean 和总体标准差只使用有限值；NaN、正/负 Infinity 分别计数。percentile 使用 `(n-1)` rank 的线性插值。32F 保持精确排序语义，但单次调用用于精确直方图/percentile 的样本存储上限为 16 MiB；超限在样本列表分配前返回 `roi_exact_float_statistics_budget_exceeded`，调用方应缩小 ROI。
- 8/16 位直方图覆盖完整标称 DN 范围；32F 直方图覆盖 ROI 内有限值的 min..max，不量化回 8 位。
- 8/16 位饱和分别定义为 `<=0` 和 `>=255/65535`；32F 采用平台 `[0,1]` 标称范围。32F 测量单位是值域值，不表示执行时进行了归一化。
- 坏点候选使用同 ROI、同通道的局部中位数与 `1.4826 × MAD`；同时满足 Sigma 阈值和标称范围最小偏差才返回，边界邻居不足 3 个时跳过。候选强度依次按置信度、偏差/阈值、绝对偏差排序，并以坐标/通道稳定打破并列；执行只维护有界 top-K，不保存全部候选坐标。

## 结构化结果

成功结果包含以下 artifact：

| 类型/名称 | 用途 |
| --- | --- |
| Measurement `roi-statistics` | ROI 像素数、每通道有效/无效/饱和/统计/percentile、唯一坏点像素数与通道候选数 |
| Table `roi-statistics-summary` | 每通道摘要，适合表格展示和 CSV |
| Table `roi-histogram` | 每通道每桶边界、闭区间标志和计数 |
| Table `bad-pixel-candidates` | 每个“坐标 × 通道”候选的值、局部中位数、偏差、阈值、置信度和原因 |
| Geometry `roi-statistics-geometry` | ROI 和已返回候选点，统一 Pixel 坐标 |
| Overlay `roi-statistics-overlay` | transient ROI/候选标记，不包含 WPF 对象 |
| StructuredData `roi-statistics-provenance` | 输入格式/DPI、ROI、参数、边界、插值与直方图规则 |

失败使用 `roi_required`、`roi_empty_after_clip`、`roi_exact_float_statistics_budget_exceeded`、Runner 的 `unsupported_format` 或参数校验 failure；取消返回 `Cancelled`。调用方始终释放整个 `AlgorithmResult`。

## 宿主接入

ImageView 的“算法调用 → ROI 统计”提供矩形、圆形和多边形入口，已有矩形/圆/多边形图元右键也可调用。参数窗口取消后不执行；进度窗口可取消。结果窗口展示摘要、直方图和候选表，CSV/JSON 默认拒绝覆盖。

ImageView session 对每个文档只接受最新 Invocation：新请求取消旧请求；展示前同时核对 `DocumentInstanceId`、source revision 和 `InvocationId`。切图、提交其他图像修改、关闭/取消或迟到结果都不会弹出旧结果。结果窗口关闭时释放结果并移除 transient overlay，不改变 source revision。

`BatchAlgorithmAnalysisProcessor` 使用同一 Invocation/Runner，逐文件输出 JSON 或 CSV bundle；它不复用图像输出格式策略。`LocalFlowImageAlgorithmAdapter` 的直接 API/测试可对进程内 RAW 帧执行同一分析且不取得外层 frame lease 所有权，但仓库当前没有把它注册为生产 Flow 画布节点；这项 `Flow` capability 表示适配资格，不是节点交付证明。旧 MQTT/设备 `AlgorithmNode` 才是现有生产 Flow 接入，且未改动。

M1 没有加入 Copilot 白名单：当前 Copilot 图像工具没有受审批 ROI 输入契约，Catalog 的可发现性不能绕过审批、目录、覆盖和数量限制。

## 性能与复现

基础扫描是 `O(ROI 像素数 × 通道数)`。整数图每通道使用 256 或 65536 个精确计数；32F 在分配前做可取消的 ROI 像素预检，为精确 percentile 保存并排序有限值，总样本数组预算固定为 16 MiB。排序前后均检查取消；预算把单次不可中断的框架内排序限制在有界范围。坏点检测另做一次邻域扫描，候选存储为 `O(MaximumBadPixelCandidates)`，唯一像素数在逐像素通道循环中流式计数，不再构造全量 `HashSet`；cap=0 时不进入第二次扫描。检测每 8 行且每 256 列检查取消，大图也可关闭该参数。

JSON 保留算法 ID/版本、诊断和全部结构化 artifact；CSV bundle 先预留全部文件名、以 UTF-8 BOM 临时文件写入，再移动到目标。默认不覆盖；无覆盖模式下提交中途失败会清理由本次新建的文件。

## M1 验收门禁

定向测试覆盖矩形/圆/多边形、物理/DPI 坐标、G8/G16/G32F/BGR、NaN/Infinity、饱和、percentile、直方图、坏点、结构化失败、输入只读、取消/释放、Batch/Flow 一致性、导出拒绝覆盖、ImageView latest-wins、revision 失效和 overlay 释放。

ROI 统计与[图像剖面](./image-profile-v1.md)保持独立契约和定向门禁，不能用其中一项通过代替另一项验证。构建、公共平台回归和 native 依赖检查遵守[平台验收门禁](./image-algorithm-platform-v1.md#m0-验收门禁)，报告当次实际运行结果及缺口。

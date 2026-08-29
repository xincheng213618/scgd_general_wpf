# 灰度与颜色剖面 V1（M2）

M2 在统一 Catalog/Invocation/Runner/Result 上提供水平、垂直和任意折线剖面。它迁移仓库原有 `ProfileDataExtractor` 能力，但不包含图像比较；M3 及后续能力没有提前实现。

## 盘点与迁移边界

原实现由直线/多边形右键菜单直接读取 `WriteableBitmap.BackBuffer`，固定约 500 步并把越界点静默跳过；图表和 CSV 只保存序号，没有实际坐标、距离、插值、DPI、执行诊断或资源契约。M2 保留 `ProfileDataExtractor`、`ProfileData` 和 `ProfileChartWindow` 的公开形状，Extractor 变为统一算法的同步兼容 façade；新 ImageView、Batch 与本地 Flow adapter API 只执行同一个 provider。

稳定身份为 `colorvision.analysis.image-profile`，算法版本 `1.1.0`，参数 schema 为 1。1.1 在不改变采样数值语义的前提下加入有界结果预算和有界 WPF 预览。Descriptor 支持 G8/G16/G32F、BGR8/BGR16/BGR32F、BGRA8/BGRA16/BGRA32F，仅接受 `PolylineAlgorithmRoi`，不产生图像输出。

## 参数契约

| 参数 | 默认值 | 验证与含义 |
| --- | --- | --- |
| `SampleSpacingPixels` | 1 | 0.01..1000000；沿分段欧氏像素距离采样 |
| `Interpolation` | Bilinear | Nearest 或 Bilinear |
| `BoundaryMode` | Reject | Reject、Clamp 或 Skip |
| `ClosePath` | false | 连接尾点到首点，结果不重复首点 |
| `IncludeLuminance` | true | 彩色图增加 Rec.601 亮度曲线 |
| `IncludeAlpha` | true | 四通道图输出 alpha 曲线 |
| `MaximumSamples` | 100000 | 2..1000000；保留 schema-v1 的历史持久化范围，调用方可继续收紧 |

ImageView、Batch、本地 Flow adapter API 和兼容 façade 均从同一 Catalog 默认值与验证进入 Runner；不存在第二套通道或插值实现。这里的 adapter API 还没有注册为生产 Flow 节点。

## 坐标与采样规则

- 输入路径可用 Pixel 或 Physical（毫米）坐标；provider 先按 X/Y DPI 分别转换为 Pixel，结果 Geometry 统一为 Pixel。
- 零长度分段被忽略；Table 的 `SegmentIndex` 保留输入路径中的原始分段编号；全部分段为零返回 `profile_path_degenerate`。
- 开放路径在距离 0 开始，以固定 spacing 采样并始终追加精确尾点；尾段不足一个 spacing 也不丢失。
- 闭合路径连接尾点到首点，在 `[0,totalLength)` 采样，避免在总长度处重复首点。
- 路径跨分段时使用累计距离定位；Table 同时给出像素距离和按各分段 X/Y DPI 计算的毫米距离。
- Nearest 使用非负图像坐标的 `floor(value + 0.5)`；Bilinear 对四邻域逐通道线性插值，恰在边界/像素中心时保持端点的 NaN/Infinity 分类。
- Reject 遇到第一个越界点即结构化失败；Clamp 把坐标限制到图像边界并诊断数量；Skip 保留原请求序号和距离，但不输出该行。

## 通道与数值语义

灰度图输出 `Gray`。彩色图按核心缓冲的 `B/G/R` 顺序输出原始通道，可选 `A`，并可增加 `Luminance = 0.114B + 0.587G + 0.299R`。8/16 位保持 DN 精度；32F 不量化、不隐式归一化。

每个曲线值都有配对的 Status：`Finite`、`NaN`、`+Infinity` 或 `-Infinity`。JSON/Table 对非有限 number 写 null，并用 Status 无损表达分类，避免生成非法 JSON；图表把非有限值显示为曲线间断。

WPF 的 Rgb24/Rgb48/Rgba64 在进入核心 BGR/BGRA 缓冲时显式交换 R/B，核心 16 位 BGR/BGRA 图回到 WPF 时反向交换，避免旧路径的颜色通道漂移。

## 结构化结果

| artifact | 内容 |
| --- | --- |
| Measurement `image-profile` | 返回/请求/跳过/钳制数、像素/mm 路径长度、每曲线有限/无效计数及 min/max/mean |
| Table `image-profile-samples` | 输出/请求序号、分段、距离、像素坐标、各通道 value/status |
| Geometry `image-profile-geometry` | Pixel 坐标的 Polyline 或闭合 Polygon |
| Overlay `image-profile-overlay` | transient 路径显示，不包含 WPF 对象 |
| StructuredData `image-profile-provenance` | 输入格式/DPI、原 Invocation ROI、参数和全部采样规则 |

常见失败为 `profile_path_required`、`profile_path_degenerate`、`profile_sample_limit_exceeded`、`profile_execution_sample_budget_exceeded`、`profile_result_budget_exceeded`、`profile_sample_out_of_bounds` 和 `profile_no_samples`。取消返回 `Cancelled`，转移输入由 Runner 在所有结局释放。

## 宿主接入与兼容

ImageView 的“算法调用 → 灰度与颜色剖面”提供水平、垂直、任意折线入口；前两者由矩形临时选择的中心行/列确定，任意折线使用多点临时选择。已有直线和多边形图元的“截面图”菜单保留原 Guid/入口，但新工厂实例路由到统一工具。

结果窗口最多把均匀覆盖首尾的 2000 行复制到 DataTable/ScottPlot，摘要明确显示“总行数/预览行数”；完整 Table 仍由用户显式导出。JSON/CSV bundle 使用异步流式写入、进度和取消，先写临时文件再原子替换目标；关闭窗口会取消导出、释放 Result 并移除 transient overlay。M1/M2 共用 ImageView analysis session，新 Invocation 取消旧调用，展示前核对 document、source revision 和 invocation，分析从不提交像素或递增 revision。

`BatchAlgorithmAnalysisProcessor` 可用保存的 Polyline Invocation 输出结构化文件；`LocalFlowImageAlgorithmAdapter` 的直接 API/测试可复用同一调用且不取得外层帧租约，但当前没有对应的生产节点模板、注册或 STN 序列化类型。旧远端 MQTT/STN `AlgorithmNode` 是既有生产接入且保持不变。M2 未进入 Copilot 白名单，因为现有 Copilot 图像工具没有经审批的折线路径输入契约。

## 性能与门禁

采样执行为 `O(样本数 × 通道数)`，Bilinear 每样本最多读取四个像素并复用单个通道缓冲。执行在任何结果行分配前同时检查：路径最多 4096 点、当前一次执行最多 50000 点、按 Table 列数估算的结果预算最多 64 MiB；失败分别返回 `profile_path_point_limit_exceeded`、`profile_execution_sample_budget_exceeded` 或 `profile_result_budget_exceeded`。50,000 是可调整的运行资源门禁，不改变 schema-v1 中 `MaximumSamples` 的默认值 100,000 和合法上限 1,000,000；因此旧预设仍能反序列化和通过参数校验，但过大的实际请求会得到明确执行诊断。这使 4K/8K、0.01 px 间距和高通道请求在大分配前结构化拒绝。执行每 1024 个请求点检查取消并报告进度；仅 UI 预览降采样，算法结果本身不隐藏抽样。

M2 定向门禁覆盖水平/垂直/分段折线、开放/闭合端点、Nearest/Bilinear、三种边界模式、Pixel/Physical/DPI、8/16/32F、BGR/BGRA/alpha/亮度、NaN/Infinity、输入只读、结构化失败、采样上限、取消/释放、ImageView 图表/overlay、Batch/Flow 一致性和旧 Extractor 兼容。

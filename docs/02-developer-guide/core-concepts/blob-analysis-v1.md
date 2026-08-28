# Blob / 连通域 V1（M5.1）

M5.1 是工业测量阶段的第一个独立纵向切片，只交付 Blob/连通域；轮廓、亚像素边缘、直线和圆拟合仍属于后续里程碑。仓库原有 Conoscope 除尘和若干专用 native 算法内部使用过连通域，但没有可供 ImageView、Batch 与本地 Flow 共用的稳定契约。本阶段复用 OpenCvSharp `ConnectedComponentsWithStats`，不复制插件私有 API，也不改变旧 MQTT/设备算法 execution plane。

## 身份与能力

稳定算法 ID 为 `colorvision.analysis.blob-components`，算法版本和参数 schema 均为 `1.0.0`/1。兼容别名包括 `BlobAnalysis`、`ConnectedComponents` 和 `Blob`。Descriptor 接受 Gray8/Gray16/Gray32Float、Bgr24/Bgr48/Bgr96Float、Bgra32/Bgra64/Bgra128Float，支持可选矩形、圆和多边形 ROI，声明 `Interactive | Batch | Flow | Headless | Local | Deterministic | Roi`，输出策略为 `no-image-output`。

Blob 没有加入 Copilot 白名单。当前获批 Copilot 图像工具只提交图像输出，尚无经过审批、目录与执行证据约束的结构化分析结果工具；Catalog 可发现性不会自动获得 Copilot 执行权限。

## 参数契约

| 参数 | 默认值 | 验证与语义 |
| --- | --- | --- |
| `Threshold` | 128 | 有限值，0..255 标称刻度 |
| `ForegroundPolarity` | `Bright` | `Bright` 使用 `intensity >= threshold`，`Dark` 使用 `intensity <= threshold` |
| `Connectivity` | `Eight` | 4 或 8 连通 |
| `MinimumArea` / `MaximumArea` | 1 / 0 | 最小值至少 1；最大值 0 表示不限，否则不得小于最小值 |
| `MinimumWidth` / `MaximumWidth` | 1 / 0 | 单位 px，同上 |
| `MinimumHeight` / `MaximumHeight` | 1 / 0 | 单位 px，同上 |
| `ExcludeImageBorder` | false | 排除接触原图边界的分量，不把 ROI 边界误当成图像边界 |
| `MaximumCandidates` | 10000 | 1..100000；超过时返回 `component_limit_exceeded`，不构造无界 artifact |
| `MaximumOverlayComponents` | 500 | 0..5000；只截断显示，不截断 Table 或 Geometry |

阈值由同一参数模型按输入格式映射：8 位峰值 255、16 位峰值 65535、float 标称峰值 1。彩色输入按 `0.114×B + 0.587×G + 0.299×R` 计算亮度，alpha 不参与。任何参与亮度计算的 float 通道为 NaN/Infinity 时，该像素记为 invalid 并保持背景。Provider 只读输入缓冲区。

## ROI 与结构化结果

未提供 ROI 时分析整图；矩形采用半开边界，圆和多边形沿用统一 Pixel/Physical 坐标转换与边界包含规则。ROI 外的 mask 永远为背景。所有输出坐标都是原图 Pixel 坐标；边界框的右、下点是 exclusive pixel edge。

成功结果包含：

| Artifact | 内容 |
| --- | --- |
| Measurement `blob-summary` | ROI、前景、invalid 像素数，候选/接受/拒绝数量及接受总面积 |
| Table `blob-components` | label、接受状态、稳定过滤原因、面积、边界框、质心、填充率和图像边界接触状态 |
| Geometry `blob-geometry` | ROI 和每个候选的 Rectangle；measurements 含面积、质心、宽高和填充率 |
| Overlay `blob-overlay` | transient ROI 与通过筛选的区域；不含 WPF 类型 |
| StructuredData `blob-provenance` | schema `colorvision.analysis.blob-components/v1`、输入、ROI、参数、阈值/坐标/置信度规则和计数 |

过滤原因按固定顺序组合：`area_below_minimum`、`area_above_maximum`、`width_below_minimum`、`width_above_maximum`、`height_below_minimum`、`height_above_maximum`、`touches_image_border`。Geometry 的 `Confidence` 是 `area / (width × height)` 的边界框填充率，只用于表达区域紧致程度，不是检测类别概率。

## 宿主与所有权

ImageView 的“算法调用 → Blob / 连通域”提供整图、矩形 ROI、圆形 ROI 和多边形 ROI 入口。参数窗口取消后不执行；运行和结果展示复用统一 analysis session，以 `DocumentInstanceId + SourceRevision + InvocationId` 拒绝旧结果。结果窗口展示组件表并可导出 CSV bundle 或 JSON；窗口关闭时释放 Result，并由 overlay 管理器同步移除实际 WPF Visual。

`BatchAlgorithmAnalysisProcessor` 使用同一 Invocation/Runner，逐文件导出结构化结果；不把格式转换伪装成算法。`LocalFlowImageAlgorithmAdapter.ExecuteRawAsync` 可对进程内 RAW frame 调用相同 provider 并返回相同 artifact；这证明本地 Flow adapter 可用，不表示已经新增生产画布节点。旧远端 MQTT/设备 `AlgorithmNode` 未改动。

Runner 继续拥有 transferred input 的释放。Provider 在 mask 扫描、组件读取及 native 连通域调用前后检查取消；成功、失败和取消都由现有 Result/Input 所有权规则收口。mask 是一次 Gray8 工作缓冲区，OpenCV 通过只读 pin header 借用它，不再复制第二份 mask。

## M5.1 验收边界

数值测试覆盖两个已知区域的面积/边界框/质心、4/8 连通对角规则、Gray8/Gray16/Gray32Float/Bgr24 标称阈值一致性、NaN、三类 ROI、过滤原因、图像边界、overlay 与候选上限。宿主测试覆盖 Batch JSON、Flow RAW、ImageView 菜单、结果表、实际 Visual/transient overlay 释放；取消测试验证 transferred input 释放。轮廓提取明确不在 M5.1 内，必须作为下一个串行增量单独通过门禁。

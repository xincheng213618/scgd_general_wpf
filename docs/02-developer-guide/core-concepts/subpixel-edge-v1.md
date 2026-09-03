---
knowledge_id: "algorithms.subpixel-edge"
knowledge_type: "reference"
status: "current"
summary: "SubpixelEdge 保留实现的参数、结果与验证契约；默认运行时由 Experimental 门禁拒绝执行。"
aliases: ["亚像素边缘如何采样、为什么默认不可执行","SubpixelEdge","SubpixelEdgeAlgorithmProvider"]
code_paths: ["UI/ColorVision.ImageEditor/Algorithms/SubpixelEdgeAlgorithmProvider.cs","UI/ColorVision.ImageEditor/Algorithms/StandardAlgorithmCatalog.cs","UI/ColorVision.ImageEditor/Algorithms/ImageAlgorithmPlatform.cs"]
test_paths: ["Test/ColorVision.UI.Tests/SubpixelEdgeV1Tests.cs","Test/ColorVision.UI.Tests/AlgorithmReleaseGateTests.cs"]
related: ["algorithms.platform","algorithms.index"]
---

# 亚像素边缘 V1

## 当前发布边界

此算法为 Experimental，默认不展示或执行；门禁和错误码见[统一平台发布清单](./image-algorithm-platform-v1.md#当前发布清单)。


亚像素边缘是独立测量契约。仓库已有发光区域、十字和模板匹配中的专用亚像素逻辑，但它们绑定各自 native/模板流程，不能作为统一卡尺契约复用。该 provider 保留通用“有向卡尺 → 单个亚像素边缘点”实现，不包含直线拟合或圆拟合；后两项由 [直线拟合](./line-fit-v1.md)、[圆拟合](./circle-fit-v1.md) 的独立点集契约消费结构化结果，三者默认门禁分别生效。

## 稳定身份与边界

- AlgorithmId：`colorvision.measurement.subpixel-edge`
- 算法版本：`1.0.0`
- 参数 schema：`1`
- provider：`colorvision.subpixel-edge.cpu`，CPU / Local / deterministic
- 输入：一个 Gray8、Gray16、Gray32Float、Bgr/Bgra 8/16/float 中立帧
- ROI：必须是开放 `PolylineAlgorithmRoi`；每一对相邻点是一个独立、有方向的卡尺
- 宿主：ImageView、结构化 Batch、本地 Flow/headless
- 输出：Measurement、Table、Geometry、Overlay、StructuredData，无图像输出

Descriptor 不声明 Copilot。当前 Copilot 获批批处理工具要求主图像输出，不能把结构化测量伪装成图片绕过白名单。旧 MQTT/设备算法仍位于远端 execution plane；本地 Flow 能力表示可通过 `LocalFlowImageAlgorithmAdapter` 调用，并不表示已经新增生产 Flow 节点或 STN 类型。

## 数值规则

1. ROI 坐标先按统一 DPI 规则转换为全图像素坐标。整数坐标是像素中心，左上角为原点。
2. 灰度直接读取；彩色使用 canonical BGR 顺序的 `0.114B + 0.587G + 0.299R`，alpha 不参与。8 位、16 位和规范化 float 分别映射到同一 0..255 标称强度；NaN/Infinity 是无效样本。
3. 卡尺沿首点到末点等间距双线性采样，并始终包含两端。`NormalAveragingRadiusPixels` 可沿卡尺法向按整数像素间隔做带宽平均。
4. `SmoothingSigmaPixels > 0` 时，用三个有限 box pass 以线性复杂度近似一维高斯平滑；随后使用中心差分得到单位为“标称 8-bit DN/px”的有符号梯度。
5. `Rising`、`Falling` 或 `Either` 选择方向匹配的最强响应。峰值及相邻两个响应做抛物线插值，偏移限制在相邻采样间隔内。
6. `MinimumGradient` 是 0..255 标称梯度门槛。`RejectCaliper` 在任一中心/带宽样本越界时拒绝整条卡尺；`Clamp` 明确钳位并在结果中计数，不会静默改变规则。

`Confidence` 由峰值响应、卡尺内离峰 RMS 噪声和最小梯度共同构成，是 `[0,1]` 的确定性质量分数，不是概率。`LocalizationUncertainty`/Point Geometry 的 `Residual` 综合实际采样间距、局部响应曲率与离峰噪声，单位为像素；它是用于排序和诊断的启发式量，不是经过相机标定的计量置信区间。

## Result artifact

- `subpixel-edge-summary`：卡尺、接受/拒绝、总采样、钳位采样数，以及接受点的梯度、置信度和定位不确定度汇总；
- `subpixel-edges`：每条卡尺的起终点、长度、实际间距、接受状态、稳定拒绝原因、边缘坐标/距离/比例、有符号梯度、极性、置信度和定位不确定度；
- `subpixel-edge-geometry`：每条卡尺的 Line 与每个接受边缘的 Point；Point 的 `Residual`、`Confidence` 和测量字典与 Table 一致；
- `subpixel-edge-overlay`：transient 青色卡尺和绿色边缘点；结果窗口关闭、切图、Clear 或提交新图时统一释放实际 WPF Visual；
- `subpixel-edge-provenance`：schema `colorvision.measurement.subpixel-edge/v1`，记录输入、参数、坐标、强度、采样、平滑、响应、插值和质量语义。

稳定拒绝原因包括 `degenerate_caliper`、`sample_out_of_bounds`、`invalid_sample`、`insufficient_samples`、`gradient_below_minimum`、`sample_limit_exceeded` 和 `total_sample_limit_exceeded`。卡尺数量越过 `MaximumCalipers` 时返回 `subpixel_edge_caliper_limit_exceeded` 结构化失败；结果不会用截断数据冒充完整测量。

## 保留的 ImageView、Batch 与 Flow 适配（默认禁用）

下文描述保留代码及测试中的宿主接入，默认产品菜单与 Runner 仍拒绝该 Experimental provider。

ImageView 的“算法调用 → 亚像素边缘”提供水平卡尺、垂直卡尺和折线卡尺组：前两项用矩形选择范围的中心线作为搜索方向，折线入口把每一相邻点对作为卡尺。结果窗口显示完整 Table、在图上显示 transient 几何并导出 CSV/JSON；运行使用统一 analysis session 的 document/revision/invocation latest-wins 语义。

结构化 Batch 使用 `BatchAlgorithmAnalysisProcessor` 导出相同 Invocation 的 JSON/CSV；本地 Flow 使用 `LocalFlowImageAlgorithmAdapter` 返回同一 Result。它不进入只接受图像 artifact 的 `BatchImageProcessingWindow`。

## 资源与性能边界

Provider 只读 Runner 提供的中立帧，不创建第二份整图、不取得 borrowed 输入所有权。每条卡尺仅分配与该卡尺采样数成线性的 profile、两块平滑工作区和响应数组；三次 box pass 是 `O(samples)`，不随 Sigma 扩成二次卷积。卡尺数、单卡尺采样数、总采样数和 overlay 数都有显式上限。取消在卡尺、采样和平滑 pass 内轮询，Runner 在成功、失败或取消后按统一规则释放 transferred input。

数值测试使用已知 31.35 px 的 logistic 边缘，在全部九种 canonical 格式上以 0.18 px 容差验证同一坐标；同时覆盖方向极性、物理坐标、多卡尺、带宽越界/钳位、NaN、弱边缘、短卡尺、资源上限、输入只读、取消与所有权，以及 ImageView/Batch/Flow 的 artifact 和 Visual 生命周期。

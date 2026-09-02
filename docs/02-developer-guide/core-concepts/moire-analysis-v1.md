---
knowledge_id: "algorithms.moire-analysis"
knowledge_type: "reference"
status: "current"
summary: "MoireAnalysis 保留实现的参数、结果与验证契约；默认运行时由 Experimental 门禁拒绝执行。"
aliases: ["摩尔纹分析与 RemoveMoire 有什么区别","MoireAnalysis","MoireAnalysisAlgorithmProvider"]
code_paths: ["UI/ColorVision.ImageEditor/Algorithms/MoireAnalysisAlgorithmProvider.cs","UI/ColorVision.ImageEditor/Algorithms/StandardAlgorithmCatalog.cs","UI/ColorVision.ImageEditor/Algorithms/ImageAlgorithmPlatform.cs"]
test_paths: ["Test/ColorVision.UI.Tests/MoireAnalysisV1Tests.cs","Test/ColorVision.UI.Tests/AlgorithmReleaseGateTests.cs"]
related: ["algorithms.platform","algorithms.index"]
---

# 摩尔纹分析 V1（M11）

## 当前发布边界

此算法为 Experimental，默认不展示或执行；门禁和错误码见[统一平台发布清单](./image-algorithm-platform-v1.md#当前发布清单)。


## 适用范围

M11 保留实现使用稳定 ID `colorvision.frequency.moire-analysis`，复用 [FFT / 频域分析](./frequency-spectrum-v1.md) 的亮度、窗函数、二维 DFT、带符号频率坐标、实数共轭规范化和峰值检测规则，在此基础上加入窄带周期证据评分、同半径背景解释、共轭 notch 建议、可选对称 notch 滤波和频域证据热力图。源码包含 Catalog、Invocation、Runner、Result、ImageView、Batch 与本地 Flow 适配，但默认产品发布门禁尚未解除。

score 表示周期频谱证据，不能确定显示、传感器、采样或光学成因。`RemoveMoire`/`M_RemoveMoire` 是独立的 Gaussian blur、pyrDown/pyrUp 和锐化处理路径，不提供频谱测量；本算法不依赖 ONNX/AI 模型运行时。

## 输入、参数和数值契约

输入采用 M10 的九种 canonical format：Gray8/16/32F、Bgr24/48/96F、Bgra32/64/128F。彩色亮度固定为 `0.114B + 0.587G + 0.299R`，alpha 忽略；整数映射为标称 0..255 DN，float 必须是有限 `[0,1]` 编码样本后再映射到同一标称范围。越界或非有限 float 返回 `moire_float_out_of_nominal_range`，其他非有限亮度返回 `moire_nonfinite_input`。算法不修改输入。

检测参数明确以下边界：

- Rectangular/Hann/Hamming/Blackman 窗和可选均值移除与 M10 完全同义；
- `Minimum/MaximumFrequencyCyclesPerPixel` 约束候选频带；
- `RelativePowerThreshold` 相对当前频带最大功率做第一层筛选；
- `MinimumProminenceRatio` 要求 `peak power / same-radius-bin mean power` 达到阈值，不把所有高频峰都解释为摩尔纹；
- `PeakNeighborhoodRadius` 使用 M10 的环绕频率栅格非极大抑制；
- `MaximumSuggestions` 限制结构化建议和后续滤波工作量；
- `MaximumPixels` 在创建 OpenCV 工作区前拒绝超限输入。

V1 是全图频域分析，不接受 ROI。ROI 摩尔纹分析若未来加入，必须先定义裁剪边界、窗泄漏和 ROI 坐标回写规则，不能在当前 provider 内隐式裁图。

## 候选、评分和方向

M10 先返回实数输入共轭对的规范半平面代表。M11 再按同一半径频谱箱的 mean power 计算 prominence，仅保留达到阈值的峰。每条建议同时返回 `(fx, fy)` 与 `(-fx, -fy)`；频率单位是 cycles/pixel，周期是 `1/frequency`，频率方向归一到 `[0,180)`，空间条纹方向与频率方向正交。

评分固定为：

`100 * sqrt(clamp(2 * sum(candidatePower) / totalPower, 0, 1) * (1 - 1 / maximumProminence))`

系数 2 补回结构化结果中省略的实数共轭半平面能量。没有候选时 score 为 0、候选表为空，并返回 `moire_no_periodic_candidate`；有候选时返回 `moire_score_is_evidence`，提醒调用者不得把数值当作因果结论。分类只用于 UI 阅读：低 `<20`、中 `<50`、高 `<75`、很高 `>=75`，自动化应使用原始 score、power fraction 和 prominence。

## 对称 notch 滤波

`EnableNotchFilter=false` 是默认值，此时算法只分析和建议。启用后，provider 对未加窗、已移除均值的标称亮度执行第二次复数 DFT。每个规范候选同时应用中心在正负频率的 Gaussian notch：

`response = product(1 - attenuation * exp(-wrappedDistance² / (2 * sigma²)))`

频率距离在周期 DFT 环面上计算，避免 Nyquist 边界处的单边截断；正负 notch 共同保持实值逆变换的共轭对称。逆 DFT 使用 `RealOutput | Scale`，恢复原亮度均值，裁剪到 `[0,255]` 后输出归一化 Gray32Float `[0,1]`。这张图是明确的单通道亮度结果，不冒充原彩色格式；是否替换原图由宿主决定。`moire.filtered_candidate_power_retention` 是建议频点处滤波响应平方的估计，不代替对输出重新测量。

## Result artifact

| Artifact | 内容 |
| --- | --- |
| `moire-magnitude-spectrum` | Gray8 对数归一化、中心化幅度显示；不是定量 magnitude |
| `moire-frequency-heatmap` | Gray8 候选 prominence Gaussian 显示；坐标为中心化频率栅格 |
| `moire-filtered-luminance` | 仅启用滤波时存在；Gray32Float 归一化亮度，均值已恢复 |
| `moire-analysis-summary` | score、候选数、候选功率占比、最大 prominence、估计保留功率及主周期/方向 |
| `moire-notch-suggestions` | 排名、正/负共轭频率、周期/方向、功率、径向背景、prominence、sigma/attenuation 和解释 |
| `moire-analysis` | schema=`colorvision.frequency.moire-analysis/v1`；输入、检测、评分定义、滤波规则、参数 schema 和完整候选 |

频谱和热力图只承担显示；可复现的数值保留在 Measurement、Table 与 StructuredData。M11 不产生 WPF Visual 或核心层 overlay。

## 保留的宿主、所有权与导出（默认禁用）

本节描述保留实现及测试场景；默认菜单与 Runner 不开放摩尔纹分析，不能与条件启用的旧 `RemoveMoire` 混淆。

- ImageView：“算法 → 摩尔纹分析...”打开事务参数窗口，经统一 analysis session 执行；Clear、切图、revision 变化或同 revision 更新 Invocation 会淘汰旧结果。结果窗口显示频谱、热力图、可选滤波亮度和建议表，可保存滤波 TIFF 或导出 CSV/JSON；关闭窗口释放 Result。
- Batch：`BatchAlgorithmAnalysisProcessor` 执行同一 Invocation/Runner 并导出结构化证据；算法没有 Batch 图像格式转换策略。
- Flow：`LocalFlowImageAlgorithmAdapter.ExecuteRawAsync` 可对本地像素帧运行同一 provider；旧 MQTT/device `AlgorithmNode` 和 STN 保持在远端 execution plane。
- Copilot：M11 不在显式白名单中。alias、Catalog 投影和 provider 注册都不会令它自动成为 Copilot 工具。

Runner 继续拥有解析、参数验证、provider 选择、调度、取消、诊断和 `Transferred` 输入释放。Result 拥有两张 Gray8 图以及可选 Gray32Float 图；失败、异常或取消不会泄漏已创建 artifact。OpenCV DFT 调用内部不可抢占，取消在扫描、加窗、DFT 前后、聚合、热力图、notch 栅格和逆变换输出循环的检查点生效。

## 测试和性能门禁

`MoireAnalysisV1Tests` 覆盖稳定 ID/alias/schema/default/JSON、参数校验、平坦图零分、固定正弦 period/direction/高证据 golden、共轭建议、宽带噪声误报边界、对称 notch 的主谐波衰减与均值恢复、九种格式只读、float/像素上限/ROI 结构化失败、成功/失败/取消所有权、Batch/Flow、WPF 结果窗口释放和禁止覆盖导出。

可选 `MoireAnalysisPipelineProbe` 对 4K Gray16、4K Bgra32 与 8K Gray16 启用 notch，记录延迟、managed allocation、private-memory delta 和结果 retained bytes。结果上限按两张 Gray8 加一张 Gray32Float，即 6 bytes/pixel；管理分配预算是 retained bytes 加 64 MiB，native/private 工作集预算是 48 bytes/pixel 加 256 MiB，单次上限 180 秒。执行期仍需要 M10 的 spatial/complex DFT Mat，并在启用滤波时创建第二组 DFT/reconstruction 工作区；这些不进入 Result，也没有额外整幅 managed magnitude/power 副本。

M11 不做自动因果判定、时序视频摩尔纹跟踪、彩色通道分别滤波、盲反卷积、非均匀 FFT、GPU provider 或自动提交滤波图。每项若加入都需要独立数值、色彩和所有权契约。

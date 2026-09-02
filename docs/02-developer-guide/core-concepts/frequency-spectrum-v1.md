---
knowledge_id: "algorithms.frequency-spectrum"
knowledge_type: "reference"
status: "current"
summary: "FrequencySpectrum 保留实现的参数、结果与验证契约；默认运行时由 Experimental 门禁拒绝执行。"
aliases: ["如何读取 FFT 频谱、为什么 FFT 默认不可执行","FrequencySpectrum","FrequencySpectrumAlgorithmProvider"]
code_paths: ["UI/ColorVision.ImageEditor/Algorithms/FrequencySpectrumAlgorithmProvider.cs","UI/ColorVision.ImageEditor/Algorithms/StandardAlgorithmCatalog.cs","UI/ColorVision.ImageEditor/Algorithms/ImageAlgorithmPlatform.cs"]
test_paths: ["Test/ColorVision.UI.Tests/FrequencySpectrumV1Tests.cs","Test/ColorVision.UI.Tests/AlgorithmReleaseGateTests.cs"]
related: ["algorithms.platform","algorithms.index"]
---

# FFT / 频域分析 V1（M10）

## 当前发布边界

此算法为 Experimental，默认不展示或执行；门禁和错误码见[统一平台发布清单](./image-algorithm-platform-v1.md#当前发布清单)。


## 阶段边界与仓库盘点

M10 提供稳定 ID `colorvision.frequency.spectrum-analysis`，把二维频谱、径向/方向统计、峰值周期与方向估计以及逆变换验证接入统一 Catalog、Invocation、Runner 和 Result。Native `M_RemoveMoire` 使用 Gaussian blur、pyrDown/pyrUp 与锐化，不属于频谱计算。

本算法提供通用频域测量。摩尔纹评分、峰值的摩尔纹语义解释、notch 建议/滤波、逆滤波结果和热力图属于 M11，不能由本结果的“高峰值”自动推断；ONNX/AI 已延期，只在平台文档中保留未来接入设计，当前不引入运行时依赖。

## 输入与数值契约

V1 接受九种 canonical format：Gray8/16/32F、Bgr24/48/96F、Bgra32/64/128F。彩色输入按 `0.114B + 0.587G + 0.299R` 转为单通道亮度，alpha 不参与频谱。8/16 位映射到 0..255 标称 DN；canonical float 约定为归一化 `[0,1]` 编码样本并映射到 0..255。参与亮度的 float 通道超出 `[0,1]` 或非有限时返回 `frequency_float_out_of_nominal_range`；计算亮度的其他非有限路径返回 `frequency_nonfinite_input`，都不会静默替换。Alpha 被明确忽略，因此其数值不参与此检查。

`RemoveMean` 在加窗前移除全图亮度均值。窗函数为 Rectangular、Hann、Hamming 或 Blackman，二维权重是独立 X/Y 窗的乘积；宽或高小于 3 时该轴使用单位窗，避免退化为零。`MaximumPixels` 在分配 OpenCV 工作 Mat 前拒绝超限输入。V1 是全图分析，不接受 ROI；ROI 频谱若需要独立坐标语义，应作为后续独立版本设计。

二维复数 DFT 的定量定义固定为：

- `magnitude = sqrt(re² + im²) / windowSum`；
- `power = magnitude²`；
- 频率坐标以 `cycles/pixel` 表示，X/Y 使用带符号 DFT bin；
- 频率方向归一到 `[0,180)`；对应空间条纹方向是频率方向加 90° 后再归一；
- 周期为 `1 / frequency`，DC 不产生有限周期。

`CenterSpectrum` 只控制两张显示 artifact 是否执行 fftshift，不改变表格的带符号频率坐标和统计值。Linear/Logarithmic 只控制 Gray8 显示映射；显示图不是定量频谱数据。原始 magnitude/power 数值保留在径向、方向和峰值表以及 StructuredData 中，避免把对数显示值误当测量值。

## 统计与峰值规则

径向分箱按欧氏频率半径 `[0,sqrt(0.5)]` 聚合 sample count、mean magnitude、mean/maximum power；方向分箱按 `[0,180)` 聚合 mean magnitude、mean/total/maximum power。参数分别明确 `cycles/pixel` 和 degree 的分箱宽度。

峰值只在配置的最小/最大频率范围内搜索，以范围内最大功率乘 `PeakRelativePowerThreshold` 为阈值，使用环绕 DFT 栅格的方形非极大抑制邻域。实数图像的共轭对只返回规范半平面的一个代表；相等平台用 raw row-major 坐标确定性打破平局。峰值表返回 raw/display 坐标、fx/fy、频率、周期、频率方向、空间方向、magnitude、power 与 relative power。没有合格峰值时算法仍成功，并返回 `frequency_no_peak_above_threshold` 诊断。

## Result artifact

| Artifact | 内容 |
| --- | --- |
| `magnitude-spectrum` | Gray8 显示图；metadata 明确 linear/log1p、中心化和定量表位置 |
| `power-spectrum` | Gray8 显示图；同样不伪装成原始功率值 |
| `frequency-spectrum-summary` | 源尺寸/均值、窗增益、最大 magnitude/power、峰值数、主频/周期/方向、逆变换 RMSE/最大误差 |
| `frequency-radial-spectrum` | 径向频率区间、等效周期、样本数、mean magnitude、mean/maximum power |
| `frequency-directional-spectrum` | 频率/空间方向区间、样本数、mean magnitude、mean/total/maximum power |
| `frequency-peaks` | 确定性排序的规范半平面峰值 |
| `frequency-spectrum` | schema=`colorvision.frequency.spectrum-analysis/v1`；输入、亮度、窗、DFT、坐标、归一化和峰值规则 |

逆变换使用同一复数 DFT 与 `RealOutput | Scale`，误差目标是“已移除均值并加窗的空间信号”。因此非 Rectangular 窗不会声称重建未加窗原图；结果会附 `frequency_inverse_windowed_target` 诊断。测试对四种窗分别校验 RMSE 和最大误差。

## 保留的宿主、所有权与导出（默认禁用）

下述宿主适配描述保留实现及测试；默认产品菜单与 Runner 仍受 Experimental 门禁阻止。

- ImageView：“算法 → FFT / 频域分析...”使用统一 analysis session；Clear、切图、revision 变化或同 revision 的新 Invocation 会取消/淘汰旧结果。结果窗口显示幅度/功率图、径向/方向曲线和峰值表，可保存 PNG 或导出 CSV/JSON，关闭时释放 Result 图像。
- Batch：`BatchAlgorithmAnalysisProcessor` 使用同一 Invocation/Runner，导出 JSON 或 CSV bundle；只有 Invocation 确实含 ROI 时才要求 `Roi` capability。
- Flow：`LocalFlowImageAlgorithmAdapter.ExecuteRawAsync` 在本地帧执行同一 descriptor/provider；旧 MQTT/device `AlgorithmNode` 与 STN 仍在独立远端 execution plane。
- Copilot：M10 未进入显式白名单，Catalog alias 或反射不会自动暴露该工具。

Runner 在成功、结构化失败、异常和取消后释放 `Transferred` 输入；Result 拥有两张 Gray8 显示图并在 Dispose 时释放。provider 不修改 `Borrowed` 输入。OpenCV DFT 本身不是可抢占调用，因此取消在亮度扫描、加窗、DFT 前后、聚合、显示、峰值和逆变换误差循环的行级检查点生效。

## 验证与性能预算

`FrequencySpectrumV1Tests` 覆盖 Catalog/alias/schema/default/JSON、参数验证、常量定量 golden、中心化、固定正弦主频/周期/方向、四窗逆变换容差、九种格式只读、NaN/像素上限/ROI 结构化失败、成功/失败/取消所有权、Batch/Flow 一致性、WPF 结果窗口释放和不覆盖导出。

可选 `FrequencySpectrumPipelineProbe` 在 4K Gray16、4K Bgra32 与 8K Gray16 上记录延迟、managed allocation、private-memory delta 和两张显示图的 retained bytes。实现只保留两张 Gray8 显示图；单通道 float spatial、双通道 complex spectrum 与 inverse Mat 都是执行期 native 工作集，结果中不复制整幅 float magnitude/power。管理分配预算是两张结果图加 64 MiB，private 工作集预算是 32 bytes/pixel 加 256 MiB，单次上限 120 秒。该预算是回归门禁，不是所有硬件的实时承诺。

M10 不做多通道分别 DFT、非均匀 FFT、GPU provider、流式/分块近似、相位解包或频域编辑；这些能力需要各自的数值和坐标契约，不能以隐藏转换加入 V1。

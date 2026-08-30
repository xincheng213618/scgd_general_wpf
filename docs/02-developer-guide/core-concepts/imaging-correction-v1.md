---
knowledge_id: "algorithms.imaging-correction"
knowledge_type: "reference"
status: "current"
summary: "ImagingCorrection 的输入、参数、结果、宿主接入与定向验证契约。"
aliases: ["暗场、平场和坏点校正如何配置","ImagingCorrection","ImagingCorrectionAlgorithmProvider"]
code_paths: ["UI/ColorVision.ImageEditor/Algorithms/ImagingCorrectionAlgorithmProvider.cs","UI/ColorVision.ImageEditor/Algorithms/StandardAlgorithmCatalog.cs","UI/ColorVision.ImageEditor/Algorithms/ImageAlgorithmPlatform.cs"]
test_paths: ["Test/ColorVision.UI.Tests/ImagingCorrectionV1Tests.cs"]
related: ["algorithms.platform","algorithms.index"]
---

# 成像校正 V1（M9）

## 阶段边界与已有能力盘点

M9 提供稳定 ID `colorvision.calibration.imaging-correction`，以命名参考图像执行 Dark-frame、Flat-field、Shading/Non-uniformity 和 Bad-pixel 校正。仓库原有相机校正管线位于设备执行平面，读取长期存在的 `cvCamera` 文件 ABI；其中 DarkNoise 还必须保留历史循环和 16 位钳位行为。M9 不改变该 ABI，也不把设备文件伪装成通用图像参考，而是在 Catalog/Invocation/Runner/Result 控制面上提供可复现的 reference-image 契约。

V1 的固定执行顺序是：

1. `dark-frame` 逐像素、逐通道相减；
2. `flat-field` 按通道计算有效响应均值，并应用 `target / (flat - dark)` 的逐像素增益；
3. `shading-reference` 在 dark/flat 之后计算跨颜色通道共享的残余照度增益，避免改变色比；
4. `bad-pixel-map` 用有效、未标记邻居的逐通道中值替换坏点。

输入角色固定为 `source`、`dark-frame`、`flat-field`、`shading-reference` 和 `bad-pixel-map`。只有 `source` 必需；启用的阶段必须有对应输入，禁用阶段不得偷偷消费参考图。Dark/Flat/Shading 必须与 source 尺寸、规范格式和 `ColorSpace` 完全一致；坏点 map 必须同尺寸 Gray8，非零程度由 `BadPixelThresholdNormalized` 判定。V1 不接受 ROI，避免参考帧坐标与局部裁剪发生歧义。

## 数值、零值与过曝规则

整数样本先除以标称峰值（8 位 255、16 位 65535）进入统一计算；32F 保留原值，校准参考约定在 `[0,1]` 范围。四通道图默认只校正 B/G/R 并逐字节保留直通 alpha；只有 `CorrectAlpha=true` 才把 alpha 纳入参考统计和校正。

Flat/Shading 的参考响应不有限、`<= ReferenceZeroThresholdNormalized`，或在开启过曝保护时原始参考 `>= ReferenceSaturationThresholdNormalized`，均视为无效。Dark 的零值是合法零偏，只拒绝非有限值和启用保护时的过曝样本。一个像素只有在该阶段的所有被校正通道都有效时才计为有效；BGRA 默认忽略 alpha，`CorrectAlpha=true` 时 alpha 也必须有效。`MinimumValidReferenceFraction` 按该全通道像素规则检查每个启用阶段，阶段有效像素为零时即使最小比例配置为零也结构化失败。样本级比例仅作为诊断保留，不参与放行。

- `RejectInvocation`：返回 `invalid_reference_pixel` 或 `reference_valid_fraction_too_low`，不产生可提交图像；有效像素为零也使用后者并在 details 中报告零计数。
- `PreserveSource`：该像素保留能由目标格式有限表示的 source 样本并在 mask 中记为无效；若 source 本身是 NaN/Infinity，则写入有限的安全零值，不能把非有限值发布进成功结果；
- `FillConstant`：写入 `InvalidReferenceFillNormalized` 并记为无效。

Flat 和 shading 增益分别受 `MinimumGain/MaximumGain` 限制。整数输出始终钳位到位深范围；float 可选择 `ClampToNominalRange` 或 `PreserveFloatingPoint`。每个实际写出的样本先检查 double 计算结果和目标 `float` 表示是否有限；高增益溢出会使整像素 mask 失效、增加 `non_finite_output_sample_count` 并应用无效样本策略，全部像素失效则返回 `correction_no_valid_pixels`。未校正的有限 alpha 原样保留；非有限 alpha 也不能作为有效输出发布。未启用任何阶段时走逐行精确复制路径，包含超出 `[0,1]` 的有限 float、DPI、格式和 alpha 都不会被默认范围策略改变。

坏点只从未标记且有效的邻居采样，邻域半径为 1..7。没有可用邻居时按同一个无效策略拒绝、保留或填充；结果分别记录 marked、corrected 和 unresolved 数量。

## Preset、参考定位与追溯

`ImagingCorrectionParameters` 中的四个路径只是 ImageView/Batch 的宿主定位提示；provider 不做文件 I/O，只消费 Runner 传入的 `AlgorithmInput`。文件入口统一经过 `AlgorithmReferenceImageLoader`，保留 WPF 像素格式语义、转为 canonical buffer，并记录绝对 URI 与 SHA-256。直接 API/Flow 调用可使用帧 revision 或外部 checksum，无需伪造文件路径。

`CalibrationSource`、`CalibrationVersion`、`CalibrationChecksum`、每个输入的 URI/revision/checksum/ColorSpace 和可选 PresetId 都进入结果。`ImagingCorrectionPresetSerializer` 严格校验 AlgorithmId、算法版本和参数 schema；保存使用 `CreateNew`，不会覆盖已有 preset。

## Result artifact

| Artifact | 内容 |
| --- | --- |
| `corrected-image` | role=`primary`，与 source 同尺寸、DPI、位深、通道和规范格式 |
| `correction-validity-mask` | Gray8；255 表示最终有效或已成功替换，0 表示无效参考/未解决坏点 |
| `imaging-correction-summary` | 最终有效像素、各参考的全通道像素有效比例与样本级诊断比例、坏点数量、裁剪/不可表示样本和 alpha 策略 |
| `imaging-correction-stages` | 每个阶段的启用状态、有效/无效像素与样本、两种比例和逐通道目标值 |
| `imaging-correction-provenance` | source 与全部参考的 URI、revision、checksum、ColorSpace 和格式 |
| `imaging-correction` | `colorvision.calibration.imaging-correction/v1`；固定阶段、数值语义、参数、输入和校正来源 |

所有输入在 provider 中只读。`Borrowed` 仍归调用方；`Transferred` 在成功、结构化失败、异常、取消和 preview 启动前过期路径都被释放。Result 拥有校正图和 mask，窗口关闭或调用方 Dispose 后统一释放。

## 宿主接入

- ImageView：“算法 → 成像校正...”选择参考图或加载 preset，通过同一个 document/revision/invocation preview session 执行并提交。结果窗口显示校正图、mask、阶段表和参考追溯，可保存 PNG 或导出 CSV/JSON。
- Batch：Catalog 自动投影同一参数对象；启用阶段时从参数路径加载参考图，输出文件格式仍由 Batch 保存策略决定。缺路径、文件不存在或格式不匹配会明确失败。
- Flow：`LocalFlowImageAlgorithmAdapter.ExecuteRawSetAsync` 接受含 `source` 的命名 `LocalFlowFrameLease` 集合并复用同一 Runner。它是本地 adapter/API，不注册或改写旧 MQTT/device `AlgorithmNode` 和 STN。
- Copilot：未进入显式白名单；含相机参考和文件权限的校正不能因 alias、反射或 Catalog 注册自动暴露。

## 验证与性能

`ImagingCorrectionV1Tests` 覆盖 Catalog/alias/schema/preset、九种规范格式 identity、float dark/flat/shading 数值 golden、多通道逐像素有效性、NaN/Infinity 与高增益溢出、零值/过曝与范围策略、坏点中值及未解决策略、尺寸/格式/ColorSpace/ROI 结构化失败、输入只读、成功/失败/取消/过期的所有权释放、ImageView 提交/结果窗口、Batch 文件参考、Flow 命名帧和不覆盖导出。

可选 `ImagingCorrectionPipelineProbe` 在 4K Gray16/Bgra32 上执行真实 dark-frame 校正。实现只分配一份紧凑输出和一份 Gray8 mask，不建立全帧 float 工作副本或增益图；每个参考按只读 span 扫描。管理内存门禁为结果图像加 mask 再加固定 16 MiB 余量，延迟上限 30 秒。扫描每 32 行、输出和坏点阶段每 16 行观察取消。

M9 不从多张曝光自动生成参考帧，不估计相机响应或温漂，不读取/迁移旧设备校正文件，也不声称 GPU provider。参考采集、暗场温度/曝光一致性和外部校准报告仍由采集流程负责。

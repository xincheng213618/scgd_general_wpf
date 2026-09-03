---
knowledge_id: "algorithms.imaging-correction"
knowledge_type: "reference"
status: "current"
summary: "成像校正的参考图、固定阶段、参数/preset、执行并提交、mask与PNG/CSV/JSON保存；明确Alpha裁剪、无效样本、精确复制和批量只保存主图的边界。"
aliases: ["成像校正", "暗场校正", "平场校正", "坏点校正", "成像校正参考图", "成像校正preset", "执行并提交", "有效性mask", "ImagingCorrection", "DarkNoiseCalibration", "DarkFrameCorrection", "FlatFieldCorrection", "ShadingCorrection", "BadPixelCorrection", "ImagingCorrectionAlgorithmProvider", "ImagingCorrectionParameters", "ImagingCorrectionEditorTool", "ImagingCorrectionParametersWindow", "ImagingCorrectionResultWindow", "ImagingCorrectionPresetSerializer", "AlgorithmReferenceImageLoader", "EnableDarkFrame", "EnableFlatField", "EnableShading", "EnableBadPixelCorrection", "CorrectAlpha", "BadPixelThresholdNormalized", "BadPixelRadius", "MinimumValidReferenceFraction", "InvalidReferencePolicy", "PreserveSource", "OutputRangePolicy", "CalibrationChecksum", "imaging_correction_identity", "reference_input_missing", "reference_size_mismatch", "reference_format_mismatch", "reference_color_space_mismatch", "bad_pixel_map_format_mismatch", "reference_valid_fraction_too_low", "correction_no_valid_pixels", "correction_valid_fraction_too_low", "invalid_reference_pixel", "invalid_output_sample", "bad_pixel_unresolved", "disabled_reference_supplied"]
code_paths: ["UI/ColorVision.ImageEditor/Algorithms/ImagingCorrectionAlgorithmProvider.cs", "UI/ColorVision.ImageEditor/Algorithms/ImagingCorrectionParameters.cs", "UI/ColorVision.ImageEditor/Algorithms/ImagingCorrectionPresetSerializer.cs", "UI/ColorVision.ImageEditor/Algorithms/StrictAlgorithmParameterPresetSerializer.cs", "UI/ColorVision.ImageEditor/Algorithms/AlgorithmReferenceImageLoader.cs", "UI/ColorVision.ImageEditor/Algorithms/StandardAlgorithmCatalog.cs", "UI/ColorVision.ImageEditor/Algorithms/ImageAlgorithmPlatform.cs", "UI/ColorVision.ImageEditor/Algorithms/ImageAlgorithmApplier.cs", "UI/ColorVision.ImageEditor/Algorithms/ImageAlgorithmPreviewSession.cs", "UI/ColorVision.ImageEditor/Algorithms/ImageAlgorithmInputFactory.cs", "UI/ColorVision.ImageEditor/Algorithms/AlgorithmResultExporter.cs", "UI/ColorVision.ImageEditor/EditorTools/Algorithms/Calculate/ImagingCorrection", "UI/ColorVision.ImageEditor/EditorTools/Algorithms/AlgorithmsContextMenu.cs", "UI/ColorVision.ImageEditor/BatchProcessing/BatchImageAlgorithms.cs", "Engine/ColorVision.Engine/FlowProcessing/Algorithms/LocalFlowImageAlgorithmAdapter.cs", "Native/opencv_helper/algorithm/calibration/calibration_basic.cpp"]
test_paths: ["Test/ColorVision.UI.Tests/ImagingCorrectionV1Tests.cs", "Test/ColorVision.UI.Tests/ImageAlgorithmPerformanceGateTests.cs"]
related: ["algorithms.platform", "algorithms.index", "operations.calibration"]
---

# 成像校正：参考图、执行与结果保存

成像校正使用已有暗场、平场、照度参考图和坏点 map 处理整幅图像，返回校正图、有效性 mask、阶段统计与参考追溯。它不采集或自动生成参考图，也不从普通图像推断相机响应、曝光或温漂。参考采集条件和外部校准结论由采集流程负责。

稳定算法 ID 为 `colorvision.calibration.imaging-correction`，算法版本 `1.0.0`、参数 schema 为 1，默认运行时启用 CPU Provider。这里的命名图像输入与设备校正文件 ABI 是不同契约；本算法不读取或迁移旧 `cvCamera` 校正文件。相机资源、文件校准和结果落库见[校准服务](../../01-user-guide/devices/calibration.md)。

设备侧 `DarkNoiseCalibration` 仍按 height 索引循环，8/16-bit 分支均以 255 钳位，以保留 cvCamera 的逐字节兼容行为；这不是本页 Dark-frame 的全图逐像素相减。旧文件与执行 ABI 见 [native 校准接口](../../04-api-reference/engine-components/opencv-helper-api.md)。

## 在 ImageView 中执行

1. 打开需要处理的图像，在图像算法菜单选择“成像校正…”。
2. 勾选需要的阶段，通过各行“选择…”设置参考图，或“加载 preset…”恢复一组参数。选择文件会自动勾选对应阶段；四个阶段默认全部关闭。
3. 用“高级参数…”设置参考保护、增益、坏点、输出范围和追溯字段。高级参数窗口使用事务编辑，取消不提交本次编辑。
4. 核对源图和参考图后点击“执行并提交”。参考加载成功后开始处理；成功会替换主窗口内存图像、推进 source revision，然后打开“成像校正结果”。
5. 在结果窗口检查校正图、有效性 mask、阶段统计和参考追溯，按需保存图像或导出结果。

**执行成功即已提交。** 结果窗口不是提交前确认页，关闭它只释放本次结果，不撤销主图，也不自动保存原始图像文件。保留磁盘输出需使用保存按钮或批量输出。

当前专用入口没有可交互的计算进度/取消按钮。“取消”仅用于执行前的参数窗口；内部 preview session 的取消和过期检查属于执行保护。该 session 在异步参考加载完成后才捕获当前源图，未绑定打开参数窗口时的图像；参考加载期间切图可能使后续处理作用于新图。开始处理后的 document/revision/invocation 变化才按[平台 session 契约](./image-algorithm-platform-v1.md#m0-执行与所有权规则)拒绝迟到提交。

## 准备参考图

核心输入支持 Gray、BGR、BGRA 的 8/16-bit 和 Float32，尺寸与格式检查在规范化后执行。输入角色区分大小写且不能重复，不接受 ROI：

| 角色 | 何时需要 | 要求 |
| --- | --- | --- |
| `source` | 始终必需 | 一张源图；最多再附加四张参考 |
| `dark-frame` | EnableDarkFrame=true | 同宽高、同规范格式、同 ColorSpace |
| `flat-field` | EnableFlatField=true | 同宽高、同规范格式、同 ColorSpace |
| `shading-reference` | EnableShading=true | 同宽高、同规范格式、同 ColorSpace |
| `bad-pixel-map` | EnableBadPixelCorrection=true | 同宽高的 Gray8；按归一化阈值标记位置 |

不会自动缩放、配准、匹配曝光或转换不同 ColorSpace。参考 DPI 不参与匹配，处理按同一像素坐标对应；输出沿用 source DPI。`ColorSpace` 是调用方传入的字符串标签，精确比较；标签一致不证明已做物理或色彩校准。

文件入口 `AlgorithmReferenceImageLoader` 使用 WPF 解码器的第一帧，保留解码格式后转换为 canonical buffer。文件选择器允许的扩展名不保证本机解码器可读取。RGB/RGBA 交换、预乘 Alpha 和调色板展开规则见[平台输入边界](./image-algorithm-platform-v1.md#flow-与发布适配)。例如 Indexed8 展开为 BGRA，不能直接充当要求 Gray8 的坏点 map；应准备实际 Gray8 图。

宿主只加载已启用阶段的路径，空路径、文件不存在或无法解码会在执行前报错。Provider 不做文件 I/O；API 必须显式传入图像，不能只填写路径。向已禁用阶段传入参考会返回 `disabled_reference_supplied`，不会静默忽略。

## 固定处理顺序与数值规则

整数样本先除以 255/65535 进入归一化计算；Float32 保留原值，参考值约定已校准到 `[0,1]`。该约定不是自动缩放，开启过曝保护后仍使用配置阈值判断原始参考。

| 顺序 | 运算 |
| --- | --- |
| Dark-frame | 每像素、每校正通道执行 `source−dark`；未启用 dark 时后续阶段中的 dark 值为 0 |
| Flat-field | 分通道求有效 `flat−dark` 响应均值 T，再乘以 `clamp(T/(flat−dark),MinimumGain,MaximumGain)` |
| Shading / Non-uniformity | 参考先做 dark/flat 校正，再按校正通道的算术均值求局部残余照度和全图目标；对该像素的校正通道乘同一个受限增益 |
| Bad-pixel map | 在上述输出上，用未标记且有效的邻居逐通道中值替换被标记位置 |

Flat 的目标均值按各通道有效样本计算，参考放行比例则要求同一像素的全部校正通道有效；两者不是同一个计数口径。Shading 的全图目标只累计全部校正通道都有效的像素。共享 shading 增益本身不改变通道比，后续范围裁剪和无效样本处理仍可能改变最终比例。

例如 Float32 的 source=`[0.5,0.5]`、dark=`[0.1,0.1]`、flat=`[0.5,0.5]`、shading=`[0.3,0.5]`，启用前三阶段并保留浮点范围时，结果约为 `[0.6,0.3]`：dark 后为 0.4，flat 增益为 1，残余照度目标为 0.3，两处 shading 增益分别为 1.5 和 0.75。

### 参考有效性与失败策略

Dark 允许有限零值，也未单独禁止有限负值；启用过曝保护时要求 dark 小于过曝阈值。Flat/Shading 要求原始参考及响应有限、响应严格大于零值阈值，且启用过曝保护时原始参考严格小于过曝阈值。

每个启用的 Dark/Flat/Shading 阶段都要求：至少有一个全通道有效像素，且其比例不低于 `MinimumValidReferenceFraction`。把比例设为 0 仍不能放行零有效像素。Reference 的样本有效比例只用于诊断；坏点修复发生在这项预检之后，不能挽救已被预检拒绝的参考集合。

| InvalidReferencePolicy | 处理 |
| --- | --- |
| RejectInvocation | 无效参考/源样本拒绝整次调用；预检比例失败优先返回，不能保证总是同一个错误代码 |
| PreserveSource（默认） | 对无效样本尝试写回原始 source 值，仍受输出范围和有限表示限制 |
| FillConstant | 对无效样本写 InvalidReferenceFillNormalized，并将该像素 mask 标为无效 |

无效处理按样本执行，mask 按整个像素记录；mask=0 的像素中，其他通道仍可能已经校正，不能把 PreserveSource 理解为整像素回滚。辐射阶段写入时会检查 double 和目标 Float32 是否有限；PreserveSource 无法写入 NaN/Infinity 时回退有限零值并记为无效。仅在首次写入尝试不可表示时增加 `non_finite_output_sample_count`，它不是原始输入非有限值的通用计数器。

### 输出范围与 Alpha

整数输出始终钳制到 `[0,1]` 后乘位深峰值，以中点远离零的方式取整。Float32 的 `ClampToNominalRange` 同样钳制到 `[0,1]`；`PreserveFloatingPoint` 保留范围外有限值，但拒绝超出 Float32 可表示范围的结果。有限高值被钳制与不可表示样本失效是两种结果，应同时看 clipping 计数和 mask。

`CorrectAlpha=false` 默认只让 B/G/R 参与参考统计和校正公式，但输出写入仍遍历所有通道。**Alpha 仍受范围策略影响**：启用任一阶段时，范围外有限浮点 Alpha 在默认策略下会被钳制，非有限 Alpha 也会走无效处理；不能笼统承诺逐字节保留。`CorrectAlpha=true` 才把 Alpha 加入参考有效性、增益和坏点中值处理。

**全部阶段关闭是精确复制路径。** 合法参数下逐行复制有效像素字节，保留格式、DPI、范围外值和 Alpha，mask 全为 255，并给出 `imaging_correction_identity`。该路径不做有限值扫描，NaN/Infinity 也会被复制，因此全白 mask 在这个模式下不表示通过了校正或数值有效性检查。

### 坏点 map 与无邻居情况

map 值按 `Gray8值/255 > BadPixelThresholdNormalized` 标记，默认阈值 0.5 对应 128–255；不是任意非零值都被标记。阈值为 1 时没有位置被标记。

中值邻域半径为 1–7，排除中心、图像外、map 已标记位置和 validity=0 的邻居；至少一个可用邻居即可求中值，偶数个值取中间两者的平均。已修复的坏点仍被 map 排除，不会变成后续坏点的邻居。

成功替换的像素记入 corrected 并将 mask 设为 255；无邻居时记入 unresolved、mask=0，按策略拒绝或回填。无邻居的 PreserveSource 回填路径直接要求源值可有限写入，NaN/Infinity 会导致 `invalid_output_sample`，不能套用辐射阶段的零值回退。

最终有效比例检查只在启用 Dark/Flat/Shading 参考时执行；仅开启坏点阶段且采用保留/填充策略时，即使所有坏点 unresolved，调用仍可能成功。应看 mask 与 marked/corrected/unresolved 数量，而不能仅看 Succeeded。

## 参数参考

四个 `Enable*` 开关默认 false，四个 `*Path` 默认空串。数值范围包含端点且必须有限；关闭阶段不会绕过参数校验。

| 参数 | 默认值 | 合法范围 / 含义 |
| --- | --- | --- |
| ReferenceZeroThresholdNormalized | 0.000001 | 0–1；Flat/Shading 响应须严格大于它 |
| RejectSaturatedReferencePixels | true | 是否拒绝达到过曝阈值的参考样本 |
| ReferenceSaturationThresholdNormalized | 0.999 | 0–1；启用过曝保护时须大于零值阈值 |
| MinimumValidReferenceFraction | 0.5 | 0–1；阶段及有辐射参考时的最终全通道像素比例 |
| InvalidReferencePolicy | PreserveSource | RejectInvocation / PreserveSource / FillConstant |
| InvalidReferenceFillNormalized | 0 | 0–1 |
| MinimumGain / MaximumGain | 0 / 16 | 分别为 0–1000000、0.000001–1000000，最小值不大于最大值；Flat/Shading 各自限幅 |
| BadPixelThresholdNormalized | 0.5 | 0–1；map 严格大于阈值才标记 |
| BadPixelRadius | 1 | 1–7 |
| CorrectAlpha | false | 是否将 Alpha 纳入校正公式与参考统计 |
| OutputRangePolicy | ClampToNominalRange | ClampToNominalRange / PreserveFloatingPoint；整数仍钳制 |
| CalibrationSource | manual-reference-set | 必填非空白，最长 1024 字符 |
| CalibrationVersion | unspecified | 必填非空白，最长 128 字符 |
| CalibrationChecksum | 空串 | 非 null，最长 256 字符；来源声明，不自动校验集合 |
| DarkFramePath / FlatFieldPath / ShadingReferencePath / BadPixelMapPath | 空串 | 非 null，各最长 4096 字符；宿主加载时才检查启用项路径 |

## Preset 与参考追溯

“保存 preset…”只保存参数、路径提示和 preset ID，不复制参考图，也不验证每个启用路径已经可用。保存用新文件名，已有文件会拒绝覆盖；“加载 preset…”要求算法 ID 匹配、算法主版本兼容、参数 schema 相同，并严格检查 JSON 字段、重复键、类型和参数范围。

相对参考路径由进程当前工作目录解析，不按 preset 文件所在目录解析。移动 preset 到另一台机器或目录后，先检查四个参考路径。文件选择通常写入绝对路径。

加载后直接修改阶段开关或路径仍可能保留原 PresetId；提交高级参数会清空 ID，另存 preset 后使用新文件名作为 ID。因此 PresetId 是追溯标签，不证明参数与最初文件完全一致；核对结果中实际参数和输入 checksum。

参考加载器记录绝对路径、SHA-256 和 `encoded-device-values` 标签；它先计算文件哈希再解码，文件以 FileShare.ReadWrite 打开，不提供防并发修改的不可变快照保证。需要复现时保留稳定的参考文件及匹配校验和。ImageView 的 source 通常记录 revision，无参考文件式哈希；API/Flow 可提供自身 URI、revision、checksum，字段缺失不能补写成已验证来源。

## 检查与保存结果

| Artifact | 读取方式 |
| --- | --- |
| Image `corrected-image`，role=primary | 与 source 同宽高、DPI、位深和规范格式；结果拥有独立像素缓冲 |
| Image `correction-validity-mask` | Gray8；255 为当前路径最终标为有效/修复的像素，0 为无效或未解决位置；精确复制例外见上文 |
| Measurement `imaging-correction-summary` | 最终有效比例、参考像素/样本比例、坏点数量、裁剪/不可表示样本及 Alpha 策略 |
| Table `imaging-correction-stages` | 三行 Dark/Flat/Shading；Enabled=false 行计数为零且比例为 1，不表示参考已通过检查；坏点不在此表中 |
| Table `imaging-correction-provenance` | source 与参考的 Role、Uri、Revision、Checksum、ColorSpace、Format |
| StructuredData `imaging-correction` | schema `colorvision.calibration.imaging-correction/v1`；完整参数、阶段、输入定位、校正来源和 preset ID |

阶段 Target0..3 按 Gray 或 B/G/R/A 顺序；Dark 的 Target 是参考均值诊断，Flat 是逐通道目标，Shading 是重复显示的共享目标，未用通道为零。最终有效像素数量由 mask 重新统计，与辐射阶段曾经失效的像素数不一定相同，因为坏点阶段可以修复位置。

保存与导出均同步执行，当前窗口没有导出进度或取消按钮：

- “保存校正图…”和“保存 mask…”分别将当前结果位图交给 WPF PNG 编码器，不自动保存另一张图。PNG 路径拒绝覆盖，但编码失败可能留下新建的部分文件；它也不承诺核心 Float32 字节无损保存。
- JSON 保存状态、诊断、Measurement、两个 Table 和 StructuredData；两个 Image artifact 只写角色和 metadata，**不含校正图或 mask 的像素**。
- CSV 是四个文件。选择 `correction.csv` 时，另有 `correction_imaging-correction-stages.csv`、`correction_imaging-correction-provenance.csv` 和 `correction_imaging-correction.csv`；最后一个文件的 DataJson 保存结构化详情。CSV 不输出图像像素。

JSON/CSV 使用 UTF-8 BOM，拒绝覆盖主文件及任何伴随文件。统一导出器先暂存再提交，CSV 失败时尝试清理本次新建目标，不能保证整组文件原子回滚。核心支持九种格式，但 WPF 输出桥不支持 Bgr96Float/Bgra128Float；直接 API 返回这些格式不代表此结果窗口也可显示。

## Batch、Flow 与所有权

Catalog 别名 `ImagingCorrection`、`DarkFrameCorrection`、`FlatFieldCorrection`、`ShadingCorrection`、`BadPixelCorrection` 都解析到同一个参数化算法，不自动开启某个阶段。

批量图像处理列表可选择成像校正并编辑同一参数对象，默认后缀为 `_corrected`。每张源图执行时重新从启用路径加载参考，source Mat 使用默认 96 DPI；同批尺寸/格式不同的图像不能自动匹配同一参考。标准 Batch 只取得 primary 校正图，mask 和追溯 artifact 随结果释放，不自动另存；最终编码格式遵循批量输出策略。

`LocalFlowImageAlgorithmAdapter.ExecuteRawSetAsync` 接受含 `source` 的命名帧集合，复制 RAW 输入、不取得外层 lease 所有权；当前桥接支持 8/16-bit、1/3/4 通道、默认 96 DPI。它不从参数路径读取参考，也不注册生产 Flow 节点，接入边界见[统一平台](./image-algorithm-platform-v1.md#执行平面与兼容层)。本算法未进入 Copilot 白名单。

Provider 对所有输入只读。Borrowed 由调用方释放，Transferred 由 Runner/宿主在成功、失败、取消或过期路径释放；API 调用方必须释放整个 AlgorithmResult，不能在结果释放后继续使用其图像缓冲。

## 排障与验证范围

| 现象 / 代码 | 检查 |
| --- | --- |
| `imaging_correction_identity`，图像未变 | 四个阶段是否均关闭；别名和填写路径不会替代 Enable 开关 |
| 路径加载失败 / `reference_input_missing` | 区分宿主缺少可读路径与 API 未提供命名输入；检查相对路径解析位置 |
| `unknown_input_role` / `duplicate_input_role` / `source_input_missing` | 按输入表核对大小写、唯一角色及 source |
| `reference_size_mismatch` / `reference_format_mismatch` / `reference_color_space_mismatch` | 比较规范化后的宽高/格式及标签；不会自动缩放或变换色彩空间 |
| `bad_pixel_map_format_mismatch` | map 必须是真正 Gray8；灰色外观或 Indexed8 不等于 Gray8 |
| `reference_valid_fraction_too_low` | 检查每阶段全部校正通道同时有效的像素比例，以及零值/过曝阈值；不能用样本比例替代 |
| `invalid_reference_pixel` / `invalid_output_sample` | 核对无效策略、源值是否有限、目标格式与坏点无邻居回填路径 |
| `correction_no_valid_pixels` / `correction_valid_fraction_too_low` | 有辐射参考时，检查写出后的最终 mask；参考预检通过仍可能因输出失效而失败 |
| `bad_pixel_unresolved` | 选定半径内是否有未标记且有效的邻居；放大半径也不保证存在 |
| `commit_superseded` | 处理过程中源图/revision/调用已变化；重新核对当前图像后执行 |
| JSON/CSV 导出后没有图像 | 这些文件没有像素，使用两个独立 PNG 保存按钮或另行处理 API 图像结果 |

`ImagingCorrectionV1Tests` 检查九种格式的有限数据精确复制、dark/flat/shading 数值、全通道有效比例、范围/非有限输出、坏点中值、输入匹配、所有权、ImageView 提交、Batch/Flow 和 JSON 拒绝覆盖。它没有覆盖禁用路径的非有限数据、默认裁剪下范围外 Alpha、参考加载期间切图、并发改写参考文件，以及所有 PNG 编码格式。

Provider 分配紧凑校正图和 Gray8 mask，不建立全帧浮点中间图；这不是总内存上限。处理还存在逐样本消息字符串分配、宿主输入/显示副本及表格开销。可选 `ImagingCorrectionPipelineProbe` 对 4K Gray16/Bgra32 暗场校正设定“管理分配不超过结果图+mask+16 MiB、延迟小于 30 秒”的门槛，必须显式启用；源码中的门槛不是实际通过证明，也不覆盖所有阶段组合。参考扫描每 32 行、输出/坏点每 16 行检查取消；单个检查间隔的工作量仍依赖宽度、通道与邻域。公共门禁见[统一平台](./image-algorithm-platform-v1.md#m0-验收门禁)。

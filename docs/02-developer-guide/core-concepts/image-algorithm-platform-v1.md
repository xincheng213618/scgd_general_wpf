---
knowledge_id: "algorithms.platform"
knowledge_type: "topic"
status: "current"
summary: "统一图像算法Catalog、Invocation和Runner；普通像素预览、应用/取消、所有权与发布门禁；ONNX仅设计。"
aliases: ["有哪些本地图像算法","为什么算法有源码但菜单没有","ONNX 是否已经支持","Microsoft.ML.OnnxRuntime","AlgorithmRunner","ImageAlgorithmPlatform","ExperimentalAlgorithmProviderGate","AlgorithmsContextMenu","ImageAlgorithmPreviewSession","ImageAlgorithmApplier","BasicAdjustmentWindow","WhiteBalanceWindow","ThresholdWindow","算法预览","应用与保存","基础调整","图像反相","白平衡","图像阈值","ConvertBatchImages","OpenBatchImageProcessing","colorvision-batch-image-conversion","批量图片处理"]
code_paths: ["UI/ColorVision.Algorithms/","UI/ColorVision.ImageEditor/Algorithms/ImageAlgorithmPlatform.cs","UI/ColorVision.ImageEditor/Algorithms/StandardAlgorithmCatalog.cs","UI/ColorVision.ImageEditor/Algorithms/StandardAlgorithmParameters.cs","UI/ColorVision.ImageEditor/Algorithms/ImageAlgorithmPreviewSession.cs","UI/ColorVision.ImageEditor/Algorithms/ImageAlgorithmApplier.cs","UI/ColorVision.ImageEditor/EditorTools/Algorithms/README.md","UI/ColorVision.ImageEditor/EditorTools/Algorithms/AlgorithmsContextMenu.cs","UI/ColorVision.ImageEditor/EditorTools/Algorithms/BasicAdjustmentWindow.xaml.cs","UI/ColorVision.ImageEditor/EditorTools/Algorithms/WhiteBalanceWindow.xaml.cs","UI/ColorVision.ImageEditor/EditorTools/Algorithms/ThresholdWindow.xaml.cs","UI/ColorVision.ImageEditor/EditorTools/Algorithms/InvertEditorTool.cs","UI/ColorVision.ImageEditor/BatchProcessing/BatchImageAlgorithms.cs","UI/ColorVision.ImageEditor/BatchProcessing/BatchImageProcessor.cs","UI/ColorVision.ImageEditor/BatchProcessing/BatchImageOutput.cs","Engine/ColorVision.Engine/Media/CVRawBatchImageLoader.cs","ColorVision/Copilot/Agent/Tools/Application/CopilotConvertBatchImagesTool.cs","ColorVision/Copilot/Agent/Tools/Application/CopilotOpenBatchImageProcessingTool.cs","ColorVision/Copilot/Skills/colorvision-batch-image-conversion","Engine/ColorVision.Engine/FlowProcessing/Algorithms/LocalFlowImageAlgorithmAdapter.cs"]
test_paths: ["Test/ColorVision.UI.Tests/ImageAlgorithmPlatformTests.cs","Test/ColorVision.UI.Tests/AlgorithmReleaseGateTests.cs","Test/ColorVision.Copilot.Tests/CopilotBatchImageProcessingTests.cs","Scripts/tests/test_algorithm_package_contract.py"]
related: ["algorithms.index","algorithms.onnx","ui.index","ui.image-editor","engine.cv-image-export"]
---

# 统一图像算法平台 V1

统一图像算法平台把算法身份、参数、调用、执行和结果从具体 UI、OpenCV、设备通信及 Flow 节点中分离。它采用串行里程碑交付；本页记录已落地的契约和兼容边界，不把后续能力当作当前实现。

检索“ONNX 是否可用 / Microsoft.ML.OnnxRuntime / AI 推理”时，当前答案是未实现：没有 ONNX runtime、模型或默认执行能力。未来设计单独标记为 [planned](./onnx-inference-future-design.md)，不能用该设计回答当前已支持的功能。查询 Blob、轮廓、亚像素边缘、直线/圆拟合、FFT 或摩尔纹时，也必须先读取下方默认发布门禁，不能只看 provider 源码存在。

## 里程碑范围

M0 只覆盖平台基础、现有普通 ImageEditor 算法和兼容适配。ROI 统计、剖面和图像比较分别在 [M1](./roi-statistics-v1.md)、[M2](./image-profile-v1.md)、[M3–M4](./image-comparison-v1.md) 形成增量；图像比较的当前行为与 schema 迁移由同一主题维护。M5–M11 的页面同时记录实现候选和验证契约；是否进入默认产品执行面必须以本页的发布清单为准，不能因为 Catalog 已有 Descriptor 或仓库已有 provider 就写成已发布。原计划的 M12 ONNX/AI 已标记为 Deferred；当前不引入运行时依赖，未来边界见 [ONNX / AI 推理接入设计](./onnx-inference-future-design.md)。

## 当前发布清单

默认 `ImageAlgorithmPlatform.Runtime` 使用现有 `IAlgorithmProviderAvailability` 做失败即停的发布门禁。Catalog 继续保留完整 Descriptor、alias、参数 schema 和文档入口；未发布 provider 不进入菜单、Batch 等可执行投影，`CanExecuteDescriptor`/`CanAttemptExecution` 返回 false，绕过 UI 直接调用 Runner 也只会得到 `provider_unavailable`，其 `provider_dependency_unavailable` 详情包含 `algorithm_experimental` 和稳定的待验证原因码。实现源码和 provider 级测试可继续用于收口验证，但不构成产品可用承诺。

| 发布状态 | 能力 |
| --- | --- |
| 当前默认启用 | 14 个既有像素算法；ROI 统计；图像剖面；图像比较；几何变换；图像配准；镜头畸变校正；成像校正 |
| 条件启用 | `RemoveMoire` 属于上述既有像素算法，但只有 `opencv_helper.dll` 可加载且包含 `M_RemoveMoire` export 时才显示和执行；依赖缺失时结构化拒绝 |
| 暂缓发布（Experimental） | Blob / 连通域、轮廓提取、亚像素边缘、直线拟合、圆拟合、FFT / 频域分析、摩尔纹分析 |
| 仅设计（Deferred） | ONNX / AI 推理；没有运行时、模型、Execution Provider、产品菜单或默认 Runner 能力 |

暂缓项的 Descriptor 和实现不删除；重新启用必须分别闭环文档中记录的最坏情况资源上限、数值/测量正确性和生产规模测试，再从这一处默认 provider 注册门禁移除，不能在菜单、Batch、Flow 或其他 Runner 调用方单独开旁路。未完成的改进和验证不因文档整理而自动成为已发布能力。

## 当前普通像素算法执行入口

普通像素算法的参数定义与默认执行已收口到 Catalog、Invocation 和 Runner。以下描述当前源码，不是待实施的迁移清单；历史上分散的 Batch 参数或执行实现不能据此重新引入。

| 当前入口 | 实现位置与调用链 | 必须保留的边界 |
| --- | --- | --- |
| ImageView 菜单与预览 | `EditorTools/Algorithms/AlgorithmsContextMenu.cs` 从 `AlgorithmCatalogProjection.ForInteractiveMenu` 投影菜单；普通像素预览由 `Algorithms/ImageAlgorithmPreviewSession.cs` 调用所属 runtime 的 Runner | 专用参数窗口是兼容适配器；菜单仍检查 provider 可用性，预览遵守 document/revision/invocation 有效性 |
| 默认图像输出 Batch | `BatchProcessing/BatchImageAlgorithms.cs` 的 `CreateAll` 从 `ForBatchImageProcessing` 投影能力，`CreateDefaultParameters` 读取 Descriptor 默认值，`BatchImageAlgorithmDefinition.Apply` 构造 Invocation 并调用同一 runtime 的 Runner | 保留同步 façade 和部分旧参数归一化；“仅转换格式”及调用方显式构造的 legacy delegate 不冒充 Catalog 算法 |
| Canny 参数与执行 | `Algorithms/StandardAlgorithmCatalog.cs` 注册 `StandardAlgorithmParameters.cs` 的 `CannyParameters`，低/高阈值默认为 `50/150`；默认 Batch 读取同一参数并经 Runner 执行 | Batch 不另设一套 Canny 默认值或私有执行路径；位深转换与输出 Gray8 的契约由同一 provider 负责 |

以上路径相对 `UI/ColorVision.ImageEditor/`。`Test/ColorVision.UI.Tests/ImageAlgorithmPlatformTests.cs` 的 `EightBitBatchAndRunnerUseIdenticalCannyParametersAndPixels` 对照 Batch 与 Runner 的参数和像素；测试存在不表示本次已经运行。RemoveMoire 的 native 依赖和允许的宿主入口仍以下方能力矩阵及前述发布门禁为准。

ImageView 适配器通过 `ImageFrameStore`/`ImageFrameLease` 读取 source，并把 revision 与 `DocumentInstanceId`、`InvocationId` 一起交给专属 session；平台不维护第二套源帧生命周期。租约、位图复制与显式失效的实现及测试范围见[源图像帧契约](../../04-api-reference/ui-components/image-frame-lifetime.md)，不把内存仍有效当作结果仍可发布。

### ImageEditor 参数窗口、应用与取消

`AlgorithmsContextMenu` 使用 `ImageProcessingContext`（矩形/分析适配器另需 `DrawEditorContext`），不是旧 README 中直接传 `ImageView` 的构造方式。`InvertEditorTool`、`BasicAdjustmentWindow` 等也接收处理上下文。反相等“直接应用”工具的 `Execute()` 当前是 `async void`，内部等待 `ImageAlgorithmApplier.ApplyAsync`；外部调用返回并不是算法完成的信号，不应照旧示例随后立即读取或导出结果。

`BasicAdjustmentWindow`、`WhiteBalanceWindow` 和 `ThresholdWindow` 在构造时建立预览会话并发起计算；滑动变化使用各窗口独立的 50ms 防抖键。每次运行从会话的源图副本构造输入，不把上一次预览反复叠加为新输入。不同窗口、直接应用及其他分析会争用同一文档/revision 的调用所有权，旧会话不能提交或恢复掉后继结果。

| 动作 | 当前效果与边界 |
| --- | --- |
| 基础调整关闭“预览” | 取消待触发的防抖回调并调用 `ShowOriginal()`；不取消已经在途的计算或其 invocation，较晚结果仍可能重新显示，因此不保证持续保持原图；不是关闭会话或重读源文件 |
| 点击“应用” | 取消待触发回调，以当前参数重新执行一次，成功且仍是当前 invocation 才 `Commit()`；基础调整即使未勾预览也会执行应用 |
| 成功提交 | 替换内存中的 `ViewBitmapSource`、清 `FunctionImage` 并推进一次 source revision；不写图像文件、不代表测量验收或可通过图元撤销恢复 |
| 点击“取消”或窗口关闭 | 取消/释放该会话；只有仍拥有预览时才恢复宿主当前基准图，不应覆盖已换图、已提交或被其他调用取代的内容 |

“应用后已保存原图”是错误推断。需要落盘时继续核对[图像编辑器输出](../../04-api-reference/ui-components/ColorVision.ImageEditor.md)的 source/rendered 格式、像素保真与覆盖边界。`ImageAlgorithmPlatformTests` 的预览有效性、同宿主会话、提交 revision 和换图/清空回归只覆盖各自契约，不替代所有真实参数窗口和驱动验收。

参数的界面范围也不等于全部像素格式都能执行：`ThresholdWindow` 当前最大刻度固定为 `255`，使用标称范围而不是旧教程的按位深扩大到 `65535`；非 8-bit 中值滤波的大核会由 provider 拒绝，即使滑动条允许选择。白平衡菜单还检查当前 Channel 大于 1，Runner 仍另行校验实际格式。参数与输出以以下 Catalog 契约为准，不在 README 维护第二份数值表。

## M0 Catalog 能力矩阵

格式缩写：`G8/G16/G32F` 分别表示 Gray8、Gray16、Gray32Float；`BGR8/BGR16/BGR32F` 表示 Bgr24、Bgr48、Bgr96Float；`BGRA8/BGRA16/BGRA32F` 表示 Bgra32、Bgra64、Bgra128Float。M0 的普通像素算法都以整幅图为输入，不声明 ROI；ROI 裁剪或 mask 不会被宿主静默应用。Batch 保存时的 TIFF/PNG/JPEG 等转换仍是输出策略。

| 稳定 AlgorithmId | 参数与默认值（schema 1） | 输入 → 输出 | ImageView | Batch | 本地 Flow adapter | Copilot |
| --- | --- | --- | :---: | :---: | :---: | :---: |
| `colorvision.image.invert` | 无 | 全部格式 → 同输入 | 是 | 是 | 是 | 是 |
| `colorvision.image.canny` | low=50、high=150、aperture=3、L2=false | 全部格式 → G8 | 是 | 是 | 是 | 是 |
| `colorvision.image.basic-adjustment` | exposure=0、brightness=0、contrast=0、gamma=1 | 全部格式 → 同输入 | 是 | 是 | 是 | 是 |
| `colorvision.image.threshold` | nominal=true、threshold=128（0..255 标称刻度，schema 2） | 全部格式 → 同输入 | 是 | 是 | 是 | 是 |
| `colorvision.image.sharpen` | 无 | 全部格式 → 同输入 | 是 | 是 | 是 | 是 |
| `colorvision.image.gaussian-blur` | kernel=5、sigma=1.5 | 全部格式 → 同输入 | 是 | 是 | 是 | 是 |
| `colorvision.image.median-blur` | kernel=5；非 8 位图的 kernel 最大为 5 | 全部格式 → 同输入 | 是 | 是 | 是 | 是 |
| `colorvision.image.morphology` | erode、kernel=3、iterations=1 | 全部格式 → 同输入 | 是 | 是 | 是 | 是 |
| `colorvision.image.denoise` | bilateral、kernel=5、nominalColorSigma=true、sigmaColor=75、sigmaSpace=75（schema 2） | 全部格式 → 同输入 | 是 | 是 | 是 | 是 |
| `colorvision.image.auto-levels` | 无 | 全部格式 → 同输入 | 是 | 是 | 是 | 是 |
| `colorvision.image.white-balance` | R/G/B scale=1 | BGR8/BGR16/BGR32F/BGRA8/BGRA16/BGRA32F → 同输入 | 是 | 是 | 是 | 是 |
| `colorvision.image.histogram-equalization` | 无 | 灰度 → G8；彩色 → BGR8 | 是 | 是 | 是 | 是 |
| `colorvision.image.remove-moire` | 无 | 全部格式 → 同输入 | 条件可用 | 否 | 否 | 否 |
| `colorvision.image.pseudo-color` | Jet、标称范围、0..255、channel=-1 | 全部格式 → BGR8 | 是 | 是 | 是 | 是 |

这里的“全部格式”只指 G8/G16/G32F/BGR8/BGR16/BGR32F/BGRA8/BGRA16/BGRA32F；需要标称范围的 32F 运算使用 `[0,1]`。Threshold 和 bilateral `SigmaColor` 的当前 schema 2 参数统一使用 0..255 标称刻度：provider 对 8-bit 保持原值、对 16-bit 乘 257、对 float 除 255，因此 ImageView、Batch、Flow 和 Copilot 的默认数值不再按位深漂移。schema 1 Invocation 由显式 migrator 进入绝对 DN 兼容模式，保留旧整数行为；绝对阈值超出目标格式标称峰值（例如 float 的 128）返回 `parameter_format_unsupported`，不再静默生成全黑图。Canny 本来就在 Gray8 规范化边界执行，Basic Adjustment 使用比例参数，核大小及空间 Sigma 使用像素单位，其他普通算法没有同类强度参数漂移。Descriptor 和 provider 都会检查格式；像非 8 位中值滤波大核这样的参数/格式组合同样结构化失败。旧 Batch façade 对偶数 kernel 仍按既有行为向上归一化为奇数；直接 Invocation、Flow 和 Copilot 使用严格参数校验。

## 公共控制面

`ColorVision.Algorithms` 是不依赖 WPF、OpenCvSharp、`HImage`、MQTT、STNode、`DeviceAlgorithm` 或 `MessageBox` 的公共契约项目：

- `AlgorithmId` 和 `AlgorithmVersion` 是持久化身份；旧菜单名、Flow 名称和 STN 名称只能通过 alias/adapter 映射，不能成为 provider 类型名。
- `AlgorithmDescriptor` 描述逻辑能力；`AlgorithmProviderMetadata` 单独描述 CPU/native/GPU/remote 实现和执行平面。
- `IAlgorithmParameters` 给出 schema 版本和只读校验；`IAlgorithmParameterMigrator` 只允许显式、逐版本迁移。
- `AlgorithmInvocation` 可 JSON 往返，携带参数 schema、输入引用、ROI、preset 和调用 ID。
- `AlgorithmResult` 用 Image、Measurement、Table、Geometry、StructuredData 和 Overlay artifact 表达结果；核心 Overlay 只引用几何和样式，不包含 WPF drawing 对象。
- `AlgorithmRunner` 负责解析、版本/格式/ROI/参数验证、provider 可用性与选择、按资源类型调度、取消、诊断和转移输入的释放。

像素坐标统一使用左上角原点。整数坐标表示像素中心；矩形是半开区间 `[x, x + width) × [y, y + height)`。物理坐标统一使用毫米，必须显式声明并通过图像 DPI/标定转换，核心结果不得暗中混用 WPF DIP。

## M0 执行与所有权规则

1. Runner 把输入视为只读；provider 必须在独立输出上工作。
2. `Borrowed` 输入由调用方释放；`Transferred` 输入无论成功、失败或取消都由 Runner 在结束时释放。
3. 成功结果拥有其 Image artifact；使用者提交、导出或展示后释放整个 `AlgorithmResult`。
4. ImageView session 只在 document、source revision 和 invocation 三者仍匹配时显示或提交结果。新调用使旧调用过期；关闭、取消、切图和 source revision 改变都会阻止迟到结果。
5. Preview 不改变 source revision；Commit 原子替换 `ViewBitmapSource` 后只递增一次 revision；Cancel 不改变 source。
6. Batch 输出格式属于保存策略，不注册为图像算法。
7. `Clear()`、`SetImageSource(...)` 和 `NotifySourcePixelsChanged()` 收口为同一文档变更边界：每次只推进一次 frame-store revision，取消/失效当前 preview 与 analysis，并拒绝旧 Invocation 提交、展示或打开窗口。非 WPF 的 `AlgorithmInvocationCoordinator` 按 `(DocumentInstanceId, SourceRevision)` 仲裁 preview 与 analysis；不同入口/owner 在同一 scope 中原子抢占并取消旧 run，不同文档或 revision 相互隔离，旧 claim 的完成、异常或释放不能清除后继；preview session 可在同 revision 被抢占后重新 claim，因此 PseudoColor 不会永久停在 `Superseded`。ImageView 的 `AlgorithmOverlayManager` 把 artifact、实际 WPF Visual、document、revision 和 registration token 作为一个所有权单元：原地提交清 transient 并把 persistent 关联到新 revision，换图/Clear/宿主释放清全部，窗口关闭只释放 transient，旧 session 不能删除同名后继。兼容的 `AlgorithmOverlays` façade 清理也同步移除其受管 Visual。

## 执行平面与兼容层

本地像素算法和远端 MQTT/设备算法共享 Descriptor/Invocation/Result 控制面，但保持不同 execution plane。旧 `AlgorithmNode`、STN 序列化字段、公开 EditorTool 构造方法和菜单 Guid 保留；适配器只把适合的本地算法路由到 Runner，不反射发现或重写远端节点。能力矩阵的“本地 Flow adapter=是”表示 `LocalFlowImageAlgorithmAdapter.ExecuteRawAsync` 可从进程内 `LocalFlowFrameLease` 调用同一 Catalog/Invocation/Runner，并已有直接适配器测试；它不表示生产 Flow 画布已经注册新的本地算法节点。当前真实生产接入仍只有既有远端 MQTT/设备 `AlgorithmNode`；仓库尚无引用 `LocalFlowImageAlgorithmAdapter` 的节点模板、节点注册或 STN 序列化类型。因此本地 Flow 目前是可调用 adapter/API 边界，不能在发布说明中写成已完成生产节点接入。

### Copilot 批量图像工具

Copilot 仅能看到显式白名单中同时声明 `Headless | Local | Deterministic | Copilot` 的算法。算法 Catalog 本身不授予目录访问、覆盖、数量或审批权限；宿主现有策略仍是最终授权边界。“对图片执行反相/Canny/白平衡”等明确算法动作与格式转换一起路由到受保护的 `ConvertBatchImages` 工具，并由 execution contract 强制收集逐文件成功/失败与输出路径证据。该工具仍要求原生审批，只接受可读/可写范围，最多 500 个文件，从不覆盖已有文件，并在解析 Catalog 后再次检查显式白名单与 `Batch | Headless | Local | Deterministic | Copilot` 能力；反射发现、远端 provider 和未列入白名单的算法不会因此暴露。

`ConvertBatchImages` 执行转换或获准的图像算法；`OpenBatchImageProcessing` 只打开交互窗口，不产生转换完成证据。

| 输入 | 当前约束 |
| --- | --- |
| `sources` | 必填，1–32 个当前授权范围内的文件或目录；展开后最多 500 个支持的图像文件 |
| `outputDirectory`、`preserveFolderStructure` | 目录必须位于可写范围；省略目录时输出在源文件旁，提供目录时默认保留源根下的子目录 |
| `format` | `same-as-source`（默认）、`tiff`、`png`、`jpeg`、`bmp`、`webp`；CVRAW/CVCIE 的 `same-as-source` 输出 `.tiff` |
| `algorithm`、`parameters` | 可选的 Catalog ID/兼容别名及对应参数；省略算法表示仅格式转换 |
| `recursive`、`suffix` | 默认不递归；可指定文件名后缀，算法存在且未给后缀时可使用算法默认后缀 |

批量路径通过 `CVRawBatchImageLoader` 为每个专有源文件加载一个图像，再由 `BatchImageOutput` 生成一个输出；它不拆分 CVCIE 的 X/Y/Z 通道集合。需要原生通道导出或显式 Python/CLI 包装器时，使用 [CVRAW / CVCIE 图像导出](../../04-api-reference/engine-components/cv-image-export.md)，按该路径核对参数、覆盖和退出码。

工具使用 `AvoidOverwrite = true`，已存在或本批次已占用的路径追加 `_2`、`_3` 等编号。无算法、输出目录和后缀，且目标扩展名与源相同时记入 `skipped_identity`，不把无变化文件算成重新转换。响应提供总计和最多 100 条 `results`；`results_truncated` 表示逐文件列表被截断，完整数量仍以 `requested`、`processed`、`succeeded`、`failed`、`skipped_identity` 与 `cancelled` 为准。某项失败可继续处理后续项，取消或部分失败的整体结果不是成功。工具结果范围不等于用户最初发现的全部目录，汇报前应核对选定输入。

### Flow 与发布适配

普通算法的 ImageView 菜单兼容 ID/顺序和图像输出 Batch 顺序由 Descriptor 的中立 `AlgorithmPresentationMetadata` 承载。`AlgorithmCatalogProjection` 先按 `Interactive | Local` 或 `Batch | Headless | Local` 过滤，再投影给 `AlgorithmsContextMenu` 与 `BatchImageAlgorithms`；宿主不再维护成员清单。现有专用预览窗口仍作为 WPF 兼容命令适配器，未知的单输入菜单项使用 Catalog 默认参数的通用编辑/执行回退。Batch 列表保持旧 UI 顺序，`BatchImageAlgorithmDefinition` 的公开构造方法和同步 `Apply(Mat)` façade 保留；Canny 不再由 Batch 覆盖 50/150 的统一默认值。ROI 统计和剖面虽声明 Batch capability，但由结构化 `BatchAlgorithmAnalysisProcessor` 执行，不设置 `BatchImageProcessingOrder`，因此不会错误进入只接受主图像 artifact 的 `BatchImageProcessingWindow`。旧菜单 Guid（例如 `InvertImage`、`EdgeDetection`、`Erode`、`BilateralFilter`）继续作为 Catalog alias 解析。Flow 的 `LocalFlowImageAlgorithmAdapter` 只复制并执行进程内 RAW 帧，不取得调用者 `LocalFlowFrameLease` 的所有权；旧远端 `AlgorithmNode` 及其 STN/MQTT 字段没有改写。`ColorVision.Algorithms` 作为独立同名 NuGet 包生成 `net8.0` 与 `net10.0` 资产，ImageEditor 的项目引用在打包时成为包依赖，CI 发布顺序固定为先 Algorithms、后 ImageEditor。该中立包不携带 provider/native runtime；`opencv_helper.dll` 的 RemoveMoire provider 在候选选择阶段探测 DLL 可加载性和 `M_RemoveMoire` export，也会解析打包目录 `runtimes/win-x64/native`。验证成功的模块保留到进程结束，避免探针卸载与后续 P/Invoke 之间的竞态；失败由 Runner 返回带拒绝诊断的 `provider_unavailable`。

统一路径继续复用 `ImageFrameLease/ImageFrameStore` 的 revision 与延迟释放；WPF 原图到带完整格式语义的 canonical snapshot，以及每次可独立取消的 preview run snapshot，仍是明确的安全复制边界。canonical input 直接 pin 成只读 OpenCV header，native provider 也在同步调用期间 pin 输入；provider 输出仍复制到由 Result 拥有的 buffer。Gray8/Gray16/Gray32Float/Bgr24/Bgra32 写入 WPF 时直接 pin，不再先 `ToArray()`；Bgr48/Bgra64 因 WPF 端是 RGB/RGBA 布局仍需一次通道交换副本。中立帧彩色数据统一为交错 BGR，四通道统一为有意义的直通（非预乘）Alpha；Rgb24/Rgb48/Rgba64 在入口交换，Bgr32 未用字节置 255，Pbgra32 反预乘，Indexed8 按 palette 展开。`HImage` 只有 depth/channels，不能表达这些语义，直接适配必须显式声明 canonical `AlgorithmImageFormat`。

## 调用和失败处理

调用方使用 `AlgorithmInvocation.Create(AlgorithmId, parameters, roi)` 创建当前 schema 的调用，并在 `AlgorithmRunRequest` 中明确输入是 `Borrowed` 还是 `Transferred`。调用 `ImageAlgorithmPlatform.Runner.RunAsync(request, cancellationToken)` 后必须释放整个 `AlgorithmResult`；不要单独缓存其中受结果拥有的图像缓冲。

常见结构化失败包括 `algorithm_not_found`、`algorithm_version_incompatible`、`parameter_schema_newer`、`parameter_migration_missing`、`unsupported_format`、`roi_kind_unsupported`、`provider_unavailable` 和 `provider_output_format_violation`。发布门禁或运行时依赖拒绝都使用 `provider_unavailable`，具体原因位于失败详情的 `provider_dependency_unavailable`；暂缓算法包含 `algorithm_experimental` 和对应 `release_validation_pending` 原因码。取消返回 `Cancelled` 结果；旧 invocation 或旧 source revision 在 ImageView 中返回 `Superseded`，不会提交到当前图。

## M0 验收门禁

验收以命令、测试类别和对应日志为准，不在文档中维护会随测试增长而失效的通过数量。固定入口是两个 `dotnet test` 项目 `Test\ColorVision.UI.Tests` 与 `Test\ColorVision.Copilot.Tests`（均使用 `-p:Platform=x64`）、`dotnet build .\UI\ColorVision.Algorithms\ColorVision.Algorithms.csproj --no-incremental`、`dotnet build .\ColorVision\ColorVision.csproj -p:Platform=x64 --no-incremental`，以及 `python .\Scripts\tests\test_algorithm_package_contract.py`。

定向回归至少覆盖 Catalog/provider 合同矩阵、同 scope latest-wins 与可重入取消、preview/analysis 宿主恢复、overlay token、九种公共图像格式的分析展示、Batch artifact 所有权、Copilot 白名单/审批/目录/覆盖拒绝以及包内双 TFM 资产。分析结果窗口的自动展示有固定预算：最多 8 张图、单图快照 16 MiB、总快照 32 MiB、预览最长边 1600 且不超过 1,048,576 像素、JSON 自动摘要不超过 32,768 字符；超预算显示诊断占位，完整 JSON/CSV 只在用户显式导出时生成。

构建验收只承诺零错误。增量构建显示“0 个警告”可能只是目标未重建，不能作为 fresh 构建的零警告证据；每次报告必须单独标明是否使用 `--no-incremental`，并如实记录 fresh 构建产生的 analyzer、nullable 或其他仓库既有警告数量。若默认主项目构建在 `Native/opencv_helper/opencv_helper.vcxproj:28` 以 MSB4278 失败，说明当前机器缺少 `Microsoft.Cpp.Default.props` / C++ workload；零字节 DLL 占位或因引用条件变化得到的成功结果一律无效。native 项目必须在安装 C++ workload 的 Visual Studio Developer PowerShell 中另行验证。

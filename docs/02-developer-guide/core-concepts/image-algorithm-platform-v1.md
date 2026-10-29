---
knowledge_id: "algorithms.platform"
knowledge_type: "topic"
status: "current"
summary: "图像编辑器本地算法扩展：Catalog 描述、provider 注册、Invocation 和 Runner 执行；像素预览与应用/取消、几何结果和叠加显示分流，保留所有权与发布门禁；ONNX仅设计。"
aliases: ["有哪些本地图像算法","为什么算法有源码但菜单没有","ONNX 是否已经支持","Microsoft.ML.OnnxRuntime","AlgorithmRunner","AlgorithmImageBuffer","RetainReadOnly","AlgorithmImageBufferLease","ImageAlgorithmPlatform","ExperimentalAlgorithmProviderGate","AlgorithmsContextMenu","ImageAlgorithmPreviewSession","StartForCatalogPreview","ImageAlgorithmApplier","BasicAdjustmentWindow","WhiteBalanceWindow","ThresholdWindow","算法预览","应用与保存","基础调整","图像反相","白平衡","图像阈值","ConvertBatchImages","OpenBatchImageProcessing","colorvision-batch-image-conversion","批量图片处理","本地算法扩展","StandardAlgorithmCatalog","IImageAlgorithmProvider"]
code_paths: ["ColorVision/Copilot/Agent/Tools/Application/CopilotBatchImageSourceResolver.cs","UI/ColorVision.ImageEditor/BatchProcessing/BatchImageProcessingWindow.xaml.cs","ColorVision/Copilot/Agent/Tools/Application/CopilotConvertBatchImagesTool.Evidence.cs","UI/ColorVision.Algorithms/", "UI/ColorVision.ImageEditor/Algorithms/ImageAlgorithmPlatform.cs", "UI/ColorVision.ImageEditor/Algorithms/ImageAlgorithmInputFactory.cs", "UI/ColorVision.ImageEditor/Algorithms/AlgorithmBitmapWriteTarget.cs", "UI/ColorVision.ImageEditor/Algorithms/OpenCvImageAlgorithms.cs", "UI/ColorVision.ImageEditor/Algorithms/OpenCvAlgorithmProvider.cs", "UI/ColorVision.ImageEditor/Algorithms/AlgorithmImageInterop.cs", "UI/ColorVision.ImageEditor/Algorithms/StandardAlgorithmCatalog.cs", "UI/ColorVision.ImageEditor/Algorithms/StandardAlgorithmParameters.cs", "UI/ColorVision.ImageEditor/Algorithms/ImageAlgorithmPreviewSession.cs", "UI/ColorVision.ImageEditor/Algorithms/ImageAlgorithmApplier.cs", "UI/ColorVision.ImageEditor/EditorTools/Algorithms/README.md", "UI/ColorVision.ImageEditor/EditorTools/Algorithms/AlgorithmsContextMenu.cs", "UI/ColorVision.ImageEditor/EditorTools/Algorithms/AlgorithmParameterInputs.cs", "UI/ColorVision.ImageEditor/EditorTools/Algorithms/BasicAdjustmentWindow.xaml.cs", "UI/ColorVision.ImageEditor/EditorTools/Algorithms/WhiteBalanceWindow.xaml.cs", "UI/ColorVision.ImageEditor/EditorTools/Algorithms/ThresholdWindow.xaml.cs", "UI/ColorVision.ImageEditor/EditorTools/Algorithms/GaussianBlurWindow.xaml.cs", "UI/ColorVision.ImageEditor/EditorTools/Algorithms/MedianBlurWindow.xaml.cs", "UI/ColorVision.ImageEditor/EditorTools/Algorithms/EdgeDetectionWindow.xaml.cs", "UI/ColorVision.ImageEditor/EditorTools/Algorithms/FilterDenoiseWindow.xaml.cs", "UI/ColorVision.ImageEditor/EditorTools/Algorithms/MorphologyWindow.xaml.cs", "UI/ColorVision.ImageEditor/EditorTools/Algorithms/InvertEditorTool.cs", "UI/ColorVision.ImageEditor/BatchProcessing/BatchImageAlgorithms.cs", "UI/ColorVision.ImageEditor/BatchProcessing/BatchImageProcessor.cs", "UI/ColorVision.ImageEditor/BatchProcessing/BatchImageOutput.cs", "Engine/ColorVision.Engine/Media/CVRawBatchImageLoader.cs", "ColorVision/Copilot/Agent/Tools/Application/CopilotConvertBatchImagesTool.cs", "ColorVision/Copilot/Agent/Tools/Application/CopilotOpenBatchImageProcessingTool.cs", "ColorVision/Copilot/Skills/colorvision-batch-image-conversion", "Engine/ColorVision.Engine/FlowProcessing/Algorithms/LocalFlowImageAlgorithmAdapter.cs", "UI/ColorVision.ImageEditor/Operations/ImageOperationCoordinator.cs", "UI/ColorVision.ImageEditor/Contexts/ImageProcessingContext.cs"]
test_paths: ["Test/ColorVision.Copilot.Tests/CopilotOpenBatchImageInputTests.cs","Test/ColorVision.Copilot.Tests/CopilotBatchImageSourceResolverTests.cs","Test/ColorVision.UI.Tests/BatchImageWindowInputTests.cs","Test/ColorVision.Copilot.Tests/CopilotBatchImageApprovalTests.cs","Test/ColorVision.Copilot.Tests/CopilotBatchImageCancellationEvidenceTests.cs","Test/ColorVision.UI.Tests/AlgorithmReleaseGateTests.cs","Test/ColorVision.Copilot.Tests/CopilotBatchImageProcessingTests.cs","Scripts/tests/test_algorithm_package_contract.py","Test/ColorVision.UI.Tests/AlgorithmImageBufferTests.cs","Test/ColorVision.UI.Tests/ImageAlgorithmPixelFormatTests.cs","Test/ColorVision.UI.Tests/AlgorithmBitmapWriteTargetTests.cs","Test/ColorVision.UI.Tests/OpenCvImageAlgorithmsInPlaceTests.cs","Test/ColorVision.UI.Tests/BasicAdjustmentProviderTests.cs","Test/ColorVision.UI.Tests/BasicAdjustmentWindowOwnershipTests.cs","Test/ColorVision.UI.Tests/LazyBasicAdjustmentTests.cs","Test/ColorVision.UI.Tests/AlgorithmImageInteropTests.cs","Test/ColorVision.UI.Tests/ImageAlgorithmSessionArbitrationTests.cs","Test/ColorVision.UI.Tests/ImageAlgorithmPreviewSessionTests.cs", "Test/ColorVision.UI.Tests/ImageDocumentSourceSnapshotTests.cs"]
related: ["algorithms.index","algorithms.onnx","ui.index","ui.image-editor","ui.image-editor-context","engine.results","engine.cv-image-export"]
---

# 统一图像算法平台 V1

统一图像算法平台把算法身份、参数、调用、执行和结果从具体 UI、OpenCV、设备通信及 Flow 节点中分离。本页维护公共契约、普通像素处理和当前发布范围；实验实现与未来设计分别标明可用性。

检索“ONNX 是否可用 / Microsoft.ML.OnnxRuntime / AI 推理”时，当前答案是未实现：没有 ONNX runtime、模型或默认执行能力。未来设计单独标记为 [planned](./onnx-inference-future-design.md)，不能用该设计回答当前已支持的功能。查询 Blob、轮廓、亚像素边缘、直线/圆拟合、FFT 或摩尔纹时，也必须先读取下方默认发布门禁，不能只看 provider 源码存在。

## 能力范围

普通像素算法和兼容适配在本页维护；[ROI 统计](./roi-statistics-v1.md)、[图像剖面](./image-profile-v1.md)、[图像比较](./image-comparison-v1.md)及其他分析、几何与校正能力各有专题，参数和 schema 迁移由所属主题负责。查询任何能力时先检查下方发布清单，再阅读具体用法；Catalog 中存在描述或源码中存在 provider，不等于默认可执行。

## 新增本地算法与结果展示

在 `StandardAlgorithmCatalog.Create()` 登记 Descriptor、参数与宿主能力，在 `ImageAlgorithmPlatform` 的 runtime provider 列表登记 `IImageAlgorithmProvider`；菜单复用 Catalog 投影与发布门禁。像素输出沿用本页的预览/应用链；几何结果在图上叠加走 `AlgorithmGeometryArtifact/AlgorithmOverlayArtifact → AlgorithmOverlayRenderer → AlgorithmOverlayManager`，接入方法与清理契约见[结果交接](../../04-api-reference/engine-components/result-handoff-chain.md#统一算法-overlay-是另一条链)。两类结果都必须遵守本页的 document/revision/invocation 有效性；Engine 数据库历史 handler 和客户判定、导出继续按结果交接页的对应责任维护。

## 当前发布清单

默认 `ImageAlgorithmPlatform.Runtime` 使用现有 `IAlgorithmProviderAvailability` 做失败即停的发布门禁。Catalog 继续保留完整 Descriptor、alias、参数 schema 和文档入口；未发布 provider 不进入菜单、Batch 等可执行投影，`CanExecuteDescriptor`/`CanAttemptExecution` 返回 false，绕过 UI 直接调用 Runner 也只会得到 `provider_unavailable`，其 `provider_dependency_unavailable` 详情包含 `algorithm_experimental` 和稳定的待验证原因码。实现源码和 provider 级测试可继续用于收口验证，但不构成产品可用承诺。

| 发布状态 | 能力 |
| --- | --- |
| 当前默认启用 | 14 个既有像素算法；ROI 统计；图像剖面；图像比较；几何变换；图像配准；镜头畸变校正；成像校正 |
| 当前默认启用（离线图案评价） | 显示计量六项：RGB 套色、鬼影与杂散光、屏体缺陷、双目几何与信号一致性、Eyebox 扫描汇总、全视场斜边 SFR；明确输入图案、信号口径、资源预算与无效结果，不承诺现场精度 |
| 条件启用 | `RemoveMoire` 属于上述既有像素算法，但只有 `opencv_helper.dll` 可加载且包含 `M_RemoveMoire` export 时才显示和执行；依赖缺失时结构化拒绝 |
| 暂缓发布（Experimental） | Blob / 连通域、轮廓提取、亚像素边缘、直线拟合、圆拟合、FFT / 频域分析、摩尔纹分析 |
| 仅设计（Deferred） | ONNX / AI 推理；没有运行时、模型、Execution Provider、产品菜单或默认 Runner 能力 |

暂缓项的 Descriptor 和实现不删除；重新启用必须分别闭环文档中记录的最坏情况资源上限、数值/测量正确性和生产规模测试，再从这一处默认 provider 注册门禁移除，不能在菜单、Batch、Flow 或其他 Runner 调用方单独开旁路。未完成的改进和验证不因文档整理而自动成为已发布能力。

显示计量通过 `DisplayMetrologyCatalog` 与 `DisplayMetrologyProvider` 注册，不启用上述暂缓 provider。菜单复用统一分析会话的取消、换图失效、结果窗口和 overlay 生命周期；多图由专用适配器组织为左右眼对或显式扫描序列，不进入像素输出 Batch/Copilot 白名单。方法、操作、公开依据与合成样本见[显示图案计量](../../04-api-reference/algorithms/detectors/display-metrology.md)。Flow capability 仍只表示 Runner/API 可调用，不表示已注册量产节点。

## 当前普通像素算法执行入口

普通像素算法的参数定义与默认执行已收口到 Catalog、Invocation 和 Runner。以下描述当前源码，不是待实施的迁移清单；历史上分散的 Batch 参数或执行实现不能据此重新引入。

| 当前入口 | 实现位置与调用链 | 必须保留的边界 |
| --- | --- | --- |
| ImageView 菜单与预览 | `EditorTools/Algorithms/AlgorithmsContextMenu.cs` 从 `AlgorithmCatalogProjection.ForInteractiveMenu` 投影菜单；普通像素预览由 `Algorithms/ImageAlgorithmPreviewSession.cs` 调用所属 runtime 的 Runner | 专用参数窗口是兼容适配器；菜单仍检查 provider 可用性，预览遵守 document/revision/invocation 有效性 |
| 默认图像输出 Batch | `BatchProcessing/BatchImageAlgorithms.cs` 的 `CreateAll` 从 `ForBatchImageProcessing` 投影能力，`CreateDefaultParameters` 读取 Descriptor 默认值，`BatchImageAlgorithmDefinition.Apply` 构造 Invocation 并调用同一 runtime 的 Runner | 保留同步 façade 和部分旧参数归一化；“仅转换格式”及调用方显式构造的 legacy delegate 不冒充 Catalog 算法 |
| Canny 参数与执行 | `Algorithms/StandardAlgorithmCatalog.cs` 注册 `StandardAlgorithmParameters.cs` 的 `CannyParameters`，低/高阈值默认为 `50/150`；默认 Batch 读取同一参数并经 Runner 执行 | Batch 不另设一套 Canny 默认值或私有执行路径；位深转换与输出 Gray8 的契约由同一 provider 负责 |

以上路径相对 `UI/ColorVision.ImageEditor/`。测试存在不表示本次已经运行。RemoveMoire 的 native 依赖和允许的宿主入口仍以下方能力矩阵及前述发布门禁为准。

ImageView 适配器从 `ImageDocument` 同步取得源引用与共享 `ImageFrameStore` revision，再建立私有 canonical 输入，并把 revision 与 `DocumentInstanceId`、`InvocationId` 一起交给专属 session；平台不维护第二套源版本或 native 缓存。需要读取 native 帧的消费者继续使用 `ImageFrameLease`。源状态捕获、租约、位图复制与显式失效的实现及测试范围见[源图像帧契约](../../04-api-reference/ui-components/image-frame-lifetime.md)，不把内存仍有效当作结果仍可发布。

ImageEditor 中的具体仲裁 owner 是 `Operations/ImageOperationCoordinator`，`ImageProcessingContext` 保留同名兼容入口并转发。协调器管理 preview claim、analysis、发布/回滚和 overlay 注册；文档源及 revision 归 `ImageDocument`，显示成对赋值归 `ImagePresentation`。普通显示 generation 不能代替算法 invocation claim。静态伪彩仍可使用算法预览会话，连续帧则由 `ImageStreamPresentation` 使用冻结帧调度；不能假定两条输入/处理路径在数值和取消语义上等价。完整职责见[编辑器上下文](../../04-api-reference/ui-components/image-editor-context.md)。

### ImageEditor 参数窗口、应用与取消

`AlgorithmsContextMenu` 使用 `ImageProcessingContext`（矩形/分析适配器另需 `DrawEditorContext`）。`InvertEditorTool`、`BasicAdjustmentWindow` 等也接收处理上下文。反相等“直接应用”工具的 `Execute()` 当前是 `async void`，内部等待 `ImageAlgorithmApplier.ApplyAsync`；外部调用返回并不是算法完成的信号，读取或导出结果须等待实际应用完成。

基础调整、白平衡、阈值、边缘检测、高斯模糊、中值滤波、降噪滤波和形态学参数窗口在构造时通过 `ImageAlgorithmPreviewSession.StartForCatalogPreview` 建立预览会话并发起计算；参数变化使用各窗口独立的 50ms 防抖键。该启动入口仍同步复制私有 canonical 输入并取得初始 claim，但不复制占位 `WriteableBitmap`：同一所有权事务内恢复当前基准源、清空 `FunctionImage`；会话仍拥有宿主时，首个有效结果发布前显示原图。不同窗口、直接应用及其他分析会争用同一文档/revision 的调用所有权，旧会话不能提交或恢复掉后继结果。每次运行仍从会话的源图副本构造输入，不把上一次预览反复叠加为新输入。

静态伪彩也使用 `StartForCatalogPreview`，首个有效结果发布前保留原图。`Start` 保留创建并发布独立、可修改初始位图的兼容行为，供单次应用及同步兼容入口使用。`StartForCatalogPreview` 尚未取得结果位图时没有 `PreviewBitmap`，读取该属性会拒绝，不会触发延迟复制。成功发布的结果仍是独立、可修改的位图。

这些窗口通过 `ImageAlgorithmPreviewSession.GuardDeferredPreview(Action)` 包装参数刷新回调，再交给 `DebounceTimer.AddOrResetTimerDispatcher`；守卫的捕获与执行均在参数窗口的 Dispatcher 中。登记时捕获 `DocumentInstanceId`、source revision、`AlgorithmClaimSequence` 和 `AlgorithmPreviewGeneration`，回调只有在会话未完成且未释放、视图未释放且这些宿主状态仍全部相同时才发起普通预览。最后已接受的 claim sequence 在 owner 释放后仍保留，因此分析即使已经开始并结束、当前 claim 又为空，也会使更早的参数刷新过期；预览 generation 同时识别显示选择或仅取消带来的失效。被丢弃的是此前延迟的普通刷新：会话在同一文档/revision 仍有效时，用户后续新参数操作会捕获新的状态，仍可主动重新取得预览 claim。明确点击“应用”继续按当前参数执行与提交检查。

几何变换、镜头畸变校正和成像校正的单次应用入口在打开参数窗口前建立会话，绑定原 document、source revision 和调用所有权，并保留源图快照至参数取消、失败或执行结束。确认参数及必要的异步参考准备后，`ImageAlgorithmApplier` 使用调用方持有的会话执行并提交；过期操作返回 `Superseded/commit_superseded`，清理参考输入而保留后继显示。附加输入非空时才要求 `MultiInput` 能力，空列表仍按单输入算法执行。没有参数窗口的直接应用入口在调用时建立并释放自己的会话，复用同一应用核心。此单次应用检查不改变可重复预览会话在同 revision 下重新取得 claim 的既有能力。

`ImageAlgorithmSessionArbitrationTests` 的共享单次应用用例检查参数确认期间切图、revision 变化、新会话接管、空附加输入的单输入能力与正常提交一次；保留 context 入口作为生命周期兼容控制。它们检查应用边界，不代表每个参数窗口和结果窗口都已经完成点击验收。

| 动作 | 当前效果与边界 |
| --- | --- |
| 基础调整关闭“预览” | 取消待触发的防抖回调，`ShowOriginal()` 撤销当前预览 claim、请求取消在途计算并恢复当前基准图；会话保留，重新勾选或点击“应用”仍可重新计算 |
| 点击“应用” | 有可编辑数值文本的窗口先提交并校验绑定，再取消待触发回调；其余窗口直接取消待触发回调。八类参数窗口若当前已完成预览仍对应运行开始时的参数版本（有可编辑文本时还包括文本版本）及当前 invocation，则直接提交该独立位图，不重复执行。该次提交失败时保留窗口及后继操作，不立即重算夺回所有权。未命中复用条件时仍以当前参数执行，成功且期间参数状态未变化才 `Commit()`；基础调整未勾预览时也会执行应用 |
| 成功提交 | 替换内存中的 `ViewBitmapSource`、清 `FunctionImage` 并推进一次 source revision；不写图像文件、不代表测量验收或可通过图元撤销恢复 |
| 点击“取消”或窗口关闭 | 取消/释放该会话；只有仍拥有预览时才恢复宿主当前基准图，不应覆盖已换图、已提交或被其他调用取代的内容 |

`Commit()` 只接受与当前完整 claim 绑定的位图。新的 invocation 尚未发布有效结果、失败或取消时，画布可能仍保留前一次成功预览，但不能把该旧位图作为新调用的结果提交；此时返回 `false`，不因此取消仍在执行的新调用。`DocumentInstanceId`、source revision、invocation 和宿主所有权检查继续参与最终提交。

八类参数窗口还将每次“应用”绑定到点击时的参数状态。计算期间继续改参数，会保留窗口并拒绝旧值的提交，即使新的防抖预览尚未开始；基础调整的重置和预览开关变化也使该次应用过期。关闭预览时继续调参会立即撤销旧请求并保持基准图，用户可再次点击“应用”明确提交当前值；预览开启时仍按原防抖规则更新画面。

基础调整、白平衡、阈值、形态学和去噪的数值文本框共用 `AlgorithmParameterInputs`：点击“应用”先提交绑定，再取消此次回写可能排入的防抖刷新，只有绑定状态为 `Active` 且无验证错误时才开始计算。保留 WPF 的数值转换和滑条范围夹值，成功后回显实际值；失败时保留原文本、不执行算法或关窗。不能只检查验证错误标志：例如 `NaN` 可能只产生 `UpdateSourceError`。文本草稿变化独立推进输入版本，因此尚未失焦、尚未写回滑条的新文本也会使在途应用过期；基础调整关闭预览时还会立即撤销旧请求。版本在整次绑定提交完成后捕获，避免格式化回写使本次有效应用自行失效；窗口关闭时解除文本事件。默认失焦回写与主题的 Enter 移焦行为保持原样，直接默认按钮确认的验证不等同于物理按键或 IME 验收。

数值确认失败沿用 WPF 验证标记和错误说明；没有原生验证标记的 source 更新错误由会话输入跟踪器补上标记。没有现有提示时附加错误 Tooltip，不覆盖已有提示。原文不变的失败重试保留拒绝和错误标记，不再次 `UpdateSource()`，以免框架清掉标记却仍保留失败状态。文本实际改变后才释放本次记录的拒绝状态；只按绑定及错误对象的身份清理自有标记，WPF 原生错误由绑定转换自然恢复，后继代码设置的错误和提示保持其所有权。窗口关闭时同样只清理自有反馈，不改变数值解析或范围规则。

关闭预览会使此前已开始准备的请求过期；预览 claim 的注册、会话交接与关闭操作在 Dispatcher 中串行检查代际，避免关闭前的旧请求在关闭后取得发布权。已进入的单次 native 调用仍可在取消后完成，provider 在后续协作检查点观察取消；迟到结果按失效 claim 丢弃，不再显示。精确取消失败时不会恢复旧图或影响后继 owner；同步兼容会话仍保留原有逐像素恢复路径。基础调整中已经排到 Dispatcher 的普通刷新也检查当前预览开关，只有明确点击“应用”才在关闭预览时继续重算。

“应用后已保存原图”是错误推断。需要落盘时继续核对[图像编辑器输出](../../04-api-reference/ui-components/ColorVision.ImageEditor.md)的 source/rendered 格式、像素保真与覆盖边界。

参数的界面范围也不等于全部像素格式都能执行：`ThresholdWindow` 当前最大刻度固定为 `255`，使用标称范围而不是旧教程的按位深扩大到 `65535`；非 8-bit 中值滤波的大核会由 provider 拒绝，即使滑动条允许选择。白平衡菜单还检查当前 Channel 大于 1，Runner 仍另行校验实际格式。参数与输出以以下 Catalog 契约为准，不在 README 维护第二份数值表。

<span id="m0-catalog-能力矩阵"></span>

## 普通像素算法能力矩阵

格式缩写：`G8/G16/G32F` 分别表示 Gray8、Gray16、Gray32Float；`BGR8/BGR16/BGR32F` 表示 Bgr24、Bgr48、Bgr96Float；`BGRA8/BGRA16/BGRA32F` 表示 Bgra32、Bgra64、Bgra128Float。本表的普通像素算法都以整幅图为输入，不声明 ROI；ROI 裁剪或 mask 不会被宿主静默应用。Batch 保存时的 TIFF/PNG/JPEG 等转换仍是输出策略。

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

基础调整的 `OpenCvImageAlgorithms.AdjustBasic` 保留原位入口，并提供独立 source/destination 入口；两者使用相同的色调数学与取消检查。它先验证像素深度并观察取消，再判断有效变换是否中性：仅非空 `CV_8U/CV_16U`，且曝光/对比度合成增益 `alpha == 1`、亮度/对比度合成偏置 `beta == 0`、裁剪后的 `gammaValue == 1` 时，原位调用保留原像素与 Mat 存储直接返回，独立目标调用则复制源像素到目标。判断依据是这些精确系数，默认参数及合成后等价的参数都可满足；不以容差近似判定。低层 helper 的 `CV_32F/CV_64F` 即使使用默认参数仍执行 `[0,1]` 钳制，四通道的 Alpha 继续保留，公共格式清单仍以上表为准。空图不进入中性快路。上层 provider 仍从只读输入创建独立输出，经过 Runner、预览发布与提交检查；跳过中性色调运算不等于跳过这些所有权和生命周期边界。

## 公共控制面

`ColorVision.Algorithms` 是不依赖 WPF、OpenCvSharp、`HImage`、MQTT、STNode、`DeviceAlgorithm` 或 `MessageBox` 的公共契约项目：

- `AlgorithmId` 和 `AlgorithmVersion` 是持久化身份；旧菜单名、Flow 名称和 STN 名称只能通过 alias/adapter 映射，不能成为 provider 类型名。
- `AlgorithmDescriptor` 描述逻辑能力；`AlgorithmProviderMetadata` 单独描述 CPU/native/GPU/remote 实现和执行平面。
- `IAlgorithmParameters` 给出 schema 版本和只读校验；`IAlgorithmParameterMigrator` 只允许显式、逐版本迁移。
- `AlgorithmInvocation` 可 JSON 往返，携带参数 schema、输入引用、ROI、preset 和调用 ID。
- `AlgorithmResult` 用 Image、Measurement、Table、Geometry、StructuredData 和 Overlay artifact 表达结果；核心 Overlay 只引用几何和样式，不包含 WPF drawing 对象。
- `AlgorithmRunner` 负责解析、版本/格式/ROI/参数验证、provider 可用性与选择、按资源类型调度、取消、诊断和转移输入的释放。

像素坐标统一使用左上角原点。整数坐标表示像素中心；矩形是半开区间 `[x, x + width) × [y, y + height)`。物理坐标统一使用毫米，必须显式声明并通过图像 DPI/标定转换，核心结果不得暗中混用 WPF DIP。

<span id="m0-执行与所有权规则"></span>

## 执行与所有权规则

1. Runner 把输入视为只读；provider 必须在独立输出上工作。
2. `Borrowed` 输入由调用方释放；`Transferred` 输入无论成功、失败或取消都由 Runner 在结束时释放。
3. 成功结果拥有其 Image artifact；使用者提交、导出或展示后释放整个 `AlgorithmResult`。
4. ImageView session 只在 document、source revision 和 invocation 三者仍匹配时显示或提交结果。新调用使旧调用过期；关闭、取消、切图和 source revision 改变都会阻止迟到结果。
5. Preview 不改变 source revision；Commit 原子替换 `ViewBitmapSource` 后只递增一次 revision；Cancel 不改变 source。
6. Batch 输出格式属于保存策略，不注册为图像算法。
7. `Clear()`、`SetImageSource(...)` 和 `NotifySourcePixelsChanged()` 收口为同一文档变更边界：每次只推进一次 frame-store revision，取消/失效当前 preview 与 analysis，并拒绝旧 Invocation 提交、展示或打开窗口。非 WPF 的 `AlgorithmInvocationCoordinator` 按 `(DocumentInstanceId, SourceRevision)` 仲裁 preview 与 analysis；不同入口/owner 在同一 scope 中原子抢占并取消旧 run，不同文档或 revision 相互隔离，旧 claim 的完成、异常或释放不能清除后继；preview session 可在同 revision 被抢占后重新 claim，因此 PseudoColor 不会永久停在 `Superseded`。ImageView 的 `AlgorithmOverlayManager` 负责结果 artifact、WPF Visual 与注册句柄的生命周期；兼容 `AlgorithmOverlays` façade 的后台清理会通过 Dispatcher 排队。清理时机、同名替换与旧会话保护统一见[结果交接](../../04-api-reference/engine-components/result-handoff-chain.md#统一算法-overlay-是另一条链)。

`AlgorithmImageBuffer` 将像素存储与持有它的对象寿命分开；选择入口时须明确需要复制像素，还是只延长读取寿命：

| 入口 | 存储与释放契约 |
| --- | --- |
| `Clone()` | 深复制像素，返回具有独立存储的 buffer；用于确实需要隔离像素存储的边界 |
| `RetainReadOnly()` | 不复制像素，返回保留格式、尺寸、stride 和 DPI 的独立 buffer 句柄；两个对象共享已有只读数据，释放任一对象不会使另一个失效，适合转交 `AlgorithmInput.Image` |
| `AcquireReadOnlyLease()` | 不复制像素，返回独立的 `AlgorithmImageBufferLease`，供 presenter 等持有只读数据；不是 `AlgorithmInput.Image` 所需的 buffer 类型 |

每个新句柄或 lease 都要由其所有者释放；从已释放 buffer 取得数据或新句柄会抛 `ObjectDisposedException`。只读共享不授予修改像素的能力，也不改变 `Borrowed` / `Transferred` 的释放责任。构造者不得经保留的数组引用修改仍在使用的共享数据，provider 不得通过 OpenCV header、native 指针或取回底层数组来原地改写输入；需要写入时使用独立输出。数据仍可读取不代表 document、revision 或 invocation 仍有效，展示与提交继续检查原有 claim。

`AlgorithmImageBuffer.StorageId` 标识一份进程内不可变像素快照；`RetainReadOnly()` 保持同一 ID，`Clone()` 或重新构造 buffer 都得到新 ID，即使重新包装相同数组也不复用身份。释放后读取 ID 同样会拒绝。它不是内容校验和、持久化标识或文档 revision，不能用于判断结果可否发布。

静态伪彩由 `PseudoColorProcessor` 直接写入独立 Bgr24 artifact。8/16 位输入把原有两次量化、色图和上下界比较合并成查表；浮点输入保留原有归一化后再查表。单通道和有效选定通道直接读取原 stride，不抽取整幅通道图。每个 OpenCV provider 至多保留一份、不超过 128 MiB 的合成灰度快照，以 `StorageId` 匹配；选定通道、换快照或超出缓存上限都会淘汰旧项，超限时正常执行而不缓存。缓存只保留派生灰度，不保留原图；淘汰不使正在读取的独立句柄失效。此缓存不替代 preview 的文档/revision/claim 检查。`PseudoColorProviderTests` 对照独立旧算法检查全部 ushort 值、支持的像素格式和色图、缓存淘汰、并发与输出所有权。

## 执行平面与兼容层

本地像素算法和远端 MQTT/设备算法共享 Descriptor/Invocation/Result 控制面，但保持不同 execution plane。旧 `AlgorithmNode`、STN 序列化字段、公开 EditorTool 构造方法和菜单 Guid 保留；适配器只把适合的本地算法路由到 Runner，不反射发现或重写远端节点。能力矩阵的“本地 Flow adapter=是”表示 `LocalFlowImageAlgorithmAdapter.ExecuteRawAsync` 可从进程内 `LocalFlowFrameLease` 调用同一 Catalog/Invocation/Runner，并已有直接适配器测试；它不表示生产 Flow 画布已经注册新的本地算法节点。当前真实生产接入仍只有既有远端 MQTT/设备 `AlgorithmNode`；仓库尚无引用 `LocalFlowImageAlgorithmAdapter` 的节点模板、节点注册或 STN 序列化类型。因此本地 Flow 目前是可调用 adapter/API 边界，不能在发布说明中写成已完成生产节点接入。

### Copilot 批量图像工具

Copilot 仅能看到显式白名单中同时声明 `Headless | Local | Deterministic | Copilot` 的算法。算法 Catalog 本身不授予目录访问、覆盖、数量或审批权限；宿主现有策略仍是最终授权边界。“对图片执行反相/Canny/白平衡”等明确算法动作与格式转换一起路由到受保护的 `ConvertBatchImages` 工具，并由 execution contract 强制收集逐文件成功/失败与输出路径证据。该工具仍要求原生审批，只接受可读/可写范围，最多 500 个文件，从不覆盖已有文件，并在解析 Catalog 后再次检查显式白名单与 `Batch | Headless | Local | Deterministic | Copilot` 能力；反射发现、远端 provider 和未列入白名单的算法不会因此暴露。

`ConvertBatchImages` 执行转换或获准的图像算法；`OpenBatchImageProcessing` 只打开交互窗口，不产生转换完成证据。

`OpenBatchImageProcessing` 可选传 `sources`（1–32 个已授权文件或目录）及 `recursive`（默认 `false`），预填最多 500 个图像；省略参数仍只打开或激活窗口。源展开在后台完成，确认授权、取消状态和数量后才一次提交 UI，不解码图像；相同格式的 PNG 等也保留在待处理列表。已有窗口按路径去重追加，保留原项的源根目录、算法及输出设置，处理期间或关闭后的窗口拒绝添加。目录来源保留 `SourceRoot`，供用户启动处理时维持子目录结构；工具响应中的“已准备／已加入”数量不是转换结果。

预填与直接转换复用源解析器及其取消、数量和链接限制。目录读取范围来自当前请求的搜索根和可读目录；只有精确文件授权时，只允许该文件，不由工具自行扩展到父目录或兄弟文件。请求工厂已经显式加入搜索根的目录继续按该目录范围处理。

批处理审批的 `ReviewDetails` 展示全部源选择器、输出位置、格式、原始及有效算法参数、解析后的算法与版本、参数 Schema 版本、递归和目录布局、请求及有效后缀、数量上限与不覆盖规则。简要描述仍只列前五个输入，完整详情使用既有审查编码且不静默截断；编码后超过 128K 字符时不创建待审批动作，应缩小本次请求。审批准备与执行使用同一个注入的算法 Runtime 校验白名单、provider 和参数，非法请求在待审前拒绝；目录范围和逐图格式约束仍在执行时核对。这里的源目录选择器允许读取其范围内的当前图像，不是已展开文件清单或图像内容快照。

| 输入 | 当前约束 |
| --- | --- |
| `sources` | 必填，1–32 个当前授权范围内的文件或目录；展开后最多 500 个支持的图像文件 |
| `outputDirectory`、`preserveFolderStructure` | 省略目录时输出在源文件旁，提供目录时默认保留源根下的子目录；基目录及每项最终子目录都必须位于可写范围，拒绝重解析点 |
| `format` | `same-as-source`（默认）、`tiff`、`png`、`jpeg`、`bmp`、`webp`；CVRAW/CVCIE 的 `same-as-source` 输出 `.tiff` |
| `algorithm`、`parameters` | 可选的 Catalog ID/兼容别名及对应参数；省略算法表示仅格式转换 |
| `recursive`、`suffix` | 默认不递归；可指定文件名后缀，算法存在且未给后缀时可使用算法默认后缀 |

批量路径通过 `CVRawBatchImageLoader` 为每个专有源文件加载一个图像，再由 `BatchImageOutput` 生成一个输出；它不拆分 CVCIE 的 X/Y/Z 通道集合。需要原生通道导出或显式 Python/CLI 包装器时，使用 [CVRAW / CVCIE 图像导出](../../04-api-reference/engine-components/cv-image-export.md)，按该路径核对参数、覆盖和退出码。

目录准备阶段逐文件检查取消和 500 项上限，超限拒绝整批而不开始转换。输出预检复用 `BatchImageOutput.ResolveOutputDirectory` 的实际布局规则；保留子目录结构不能把授权扩大到链接目标。`BatchImageProcessingRequest.ValidateOutputPath` 是可选的中性宿主校验回调，Copilot 在每次保存前再次核对最终目录，重新编号后也会校验；处理期间目录变成重解析点时，该项记录 `output_not_authorized`，保留已经完成的逐文件结果。

工具使用 `AvoidOverwrite = true`，已存在或本批次已占用的路径追加 `_2`、`_3` 等编号。编码先写入目标目录内保留真实扩展名的唯一临时文件，再原子移动到最终路径；无覆盖落位若发现该名称刚被其他写入者占用，会重新编号并重试，不删除或覆盖竞争者的文件。临时文件在成功或失败后尽力清理。无算法、输出目录和后缀，且目标扩展名与源相同时记入 `skipped_identity`，不把无变化文件算成重新转换。响应提供总计和最多 100 条 `results`，失败项含 `error` 和稳定的 `error_code`；`results_truncated` 表示逐文件列表被截断，完整数量仍以 `requested`、`processed`、`succeeded`、`failed`、`skipped_identity` 与 `cancelled` 为准。某项失败可继续处理后续项，取消或部分失败的整体结果不是成功。工具结果范围不等于用户最初发现的全部目录，汇报前应核对选定输入。

### Flow 与发布适配

普通算法的 ImageView 菜单兼容 ID/顺序和图像输出 Batch 顺序由 Descriptor 的中立 `AlgorithmPresentationMetadata` 承载。`AlgorithmCatalogProjection` 先按 `Interactive | Local` 或 `Batch | Headless | Local` 过滤，再投影给 `AlgorithmsContextMenu` 与 `BatchImageAlgorithms`。现有专用预览窗口仍作为 WPF 兼容命令适配器，未知的单输入菜单项使用 Catalog 默认参数的通用编辑/执行回退。Batch 列表保持旧 UI 顺序，`BatchImageAlgorithmDefinition` 的公开构造方法和同步 `Apply(Mat)` façade 保留。ROI 统计和剖面虽声明 Batch capability，但由结构化 `BatchAlgorithmAnalysisProcessor` 执行，不设置 `BatchImageProcessingOrder`，因此不会错误进入只接受主图像 artifact 的 `BatchImageProcessingWindow`。旧菜单 Guid（例如 `InvertImage`、`EdgeDetection`、`Erode`、`BilateralFilter`）继续作为 Catalog alias 解析。Flow 的 `LocalFlowImageAlgorithmAdapter` 只复制并执行进程内 RAW 帧，不取得调用者 `LocalFlowFrameLease` 的所有权；旧远端 `AlgorithmNode` 及其 STN/MQTT 字段没有改写。`ColorVision.Algorithms` 作为独立同名 NuGet 包生成 `net8.0` 与 `net10.0` 资产，ImageEditor 的项目引用在打包时成为包依赖，CI 发布顺序固定为先 Algorithms、后 ImageEditor。该中立包不携带 provider/native runtime；`opencv_helper.dll` 的 RemoveMoire provider 在候选选择阶段探测 DLL 可加载性和 `M_RemoveMoire` export，也会解析打包目录 `runtimes/win-x64/native`。验证成功的模块保留到进程结束，避免探针卸载与后续 P/Invoke 之间的竞态；失败由 Runner 返回带拒绝诊断的 `provider_unavailable`。

统一路径继续使用 `ImageFrameStore` 的同一个 revision。ImageView 的 binding 提供可选 `CaptureSourceState`，由 `ImageDocument` 在 UI Dispatcher 同步源引用失效并返回 `(Source, Revision)`，不为取得版本或尺寸先创建 native 像素副本。`ImageAlgorithmInputFactory` 从捕获的 WPF 位图复制私有 canonical snapshot，复制后同时核对源引用与 revision；已提供委托但当前源为空时拒绝输入。旧宿主未提供该委托时保留 `ImageFrameLease` 路径，在复制期间持有租约并核对位图尺寸。native 读者的租约和延迟释放契约不变。

预览会话的两种启动入口都保留 WPF 原图到完整格式语义输入的同步安全复制边界，隔离宿主可变像素；如果捕获期间的失效回调更换了原源对象或文档身份，则拒绝启动，不能把已打开的操作重新绑定到后继图像。`ImageAlgorithmInputFactory.Copy(BitmapSource)` 对 Rgb48/Rgba64 先将像素复制到新私有数组，再在该数组内原地交换红蓝通道；复用 16-bit 行转换的 SSSE3/普通字节循环，不改写或共享宿主像素。省去 Catalog 预览的初始占位位图不等于省去 canonical 快照，也不把可变 WPF 源直接交给后台 worker。

每次 `PreviewAsync` 在会话状态检查内调用 `RetainReadOnly()`，为本次运行取得独立可释放的输入句柄，再以 `Transferred` 交给 Runner；不再为每次参数预览深复制整幅 canonical 像素。会话释放不会使在途运行的句柄失效，Runner 释放该句柄也不影响会话后续预览；每次运行的独立取消、claim、source revision 与关闭代际检查仍然有效。

canonical input 直接 pin 成只读 OpenCV header，native provider 也在同步调用期间 pin 输入。`OpenCvAlgorithmProvider` 的基础调整通过 `AdjustBasicTo(source, createDestination, ...)` 延迟创建独立输出：非四通道只在最后一次色调写回前、四通道只在最终 merge 前、中性整数只在复制源像素前请求一次调用方目标。factory 才分配紧密布局的私有 byte 数组，pin 后以匹配尺寸与类型的 Mat header 作为目标，直接写入最终输出。写回前已经观察到取消或失败的运行可以不申请这份结果存储；成功结果仍需要完整输出数组，取消发生在目标请求期间也可能已经分配，不能据此承诺所有被取消运行都零分配。

写入完成后 provider 先释放目标 Mat header，再释放 pin，检查取消后由 `AlgorithmImageBuffer` 接管数组，随 Result 管理寿命；这条路径不先 clone 整幅输入，也不经 `FromMat` 再复制结果。目标的指针与 step 必须保持原外部存储，若运算重分配目标则失败，不交付未写入的数组。同 provider 的其他算法仍走既有工作 Mat 与 `FromMat` 输出路径。原位与固定 destination 入口继续可用，保持原色调运算顺序、Alpha 和浮点语义。色调运算仍需要规范化工作区及四通道拆分等临时存储，不能把延迟结果分配等同于峰值内存下降。

同步兼容入口 `ToWriteableBitmap` 对 Gray8/Gray16/Gray32Float/Bgr24/Bgra32 直接 pin 后写入新位图；将 Bgr48/Bgra64 转为 WPF 的 Rgb48/Rgba64 时，直接把通道交换结果写入新建、可修改的 `WriteableBitmap.BackBuffer`，不分配整图中间数组。16-bit 转换按源和目标各自的 stride 逐行寻址，兼容任意合法源 stride，包括奇数 stride 和带 padding 的行；支持 SSSE3 时局部向量化重排，不支持时按字节完成相同转换。两种执行方式均保留 16-bit 通道值、straight Alpha 与 DPI，源像素和 padding 不被改写，目标存储独立。普通结果窗口仍使用同步转换入口。

交互 `ImageAlgorithmPreviewSession` 的主图像预览对上述七种既有 WPF 输出格式共用 `AlgorithmBitmapWriteTarget`：先在宿主 Dispatcher 建立并锁定独立的 `WriteableBitmap`，保留结果图像的只读 lease 和 pin；后台 worker 只操作已取得的源/目标指针，逐行复制，16-bit 彩色复用通道交换逻辑，并在行边界观察取消，不在 worker 调用 WPF API。填充成功后仍须回到 UI，通过最终 claim 检查后 `TakeBitmap` 标脏、解锁并发布。转交的位图不 Freeze，保持可修改；建图、锁定、解锁和显示发布仍是 UI 工作，后台复制不构成完整 UI 无阻塞或性能保证。

`TakeBitmap` 只允许一次成功转交，并清除 owner 持有的位图、pin、lease 和指针。未转交时 `DisposeAsync` 必须等待填充任务退出，再到所属 Dispatcher 解锁并放弃位图引用，最后释放输入 pin/lease；不能在 worker 仍写入时提前释放这些资源。owner 的释放表示锁和引用的退役，不承诺立即强制释放 WPF native 存储。填充任务本身不返回位图，调用方通过 UI 转交取得结果。

中立帧彩色数据统一为交错 BGR，四通道统一为有意义的直通（非预乘）Alpha；Rgb24/Rgb48/Rgba64 在入口交换，Bgr32 未用字节置 255，Pbgra32 反预乘，Indexed8 按 palette 展开。`HImage` 只有 depth/channels，不能表达这些语义，直接适配必须显式声明 canonical `AlgorithmImageFormat`。

## 调用和失败处理

调用方使用 `AlgorithmInvocation.Create(AlgorithmId, parameters, roi)` 创建当前 schema 的调用，并在 `AlgorithmRunRequest` 中明确输入是 `Borrowed` 还是 `Transferred`。调用 `ImageAlgorithmPlatform.Runner.RunAsync(request, cancellationToken)` 后必须释放整个 `AlgorithmResult`；不要单独缓存其中受结果拥有的图像缓冲。

常见结构化失败包括 `algorithm_not_found`、`algorithm_version_incompatible`、`parameter_schema_newer`、`parameter_migration_missing`、`unsupported_format`、`roi_kind_unsupported`、`provider_unavailable` 和 `provider_output_format_violation`。发布门禁或运行时依赖拒绝都使用 `provider_unavailable`，具体原因位于失败详情的 `provider_dependency_unavailable`；暂缓算法包含 `algorithm_experimental` 和对应 `release_validation_pending` 原因码。取消返回 `Cancelled` 结果；旧 invocation 或旧 source revision 在 ImageView 中返回 `Superseded`，不会提交到当前图。

取消是协作式检查，不会终止线程或打断已经进入的单次 native 调用。`OpenCvAlgorithmProvider` 在创建独立输出存储前、算法结束及输出 buffer 交付前检查 token；采用工作 Mat 路径的算法还在 clone 前后检查。基础调整在中性快路前、色调变换的每个 native 阶段之间，以及四通道 split、逐通道处理和 merge 的边界检查；请求目标 factory 前后及写入结束后也检查取消。`AlgorithmImageInterop.FromMat` 接收调用者的 token，在整图数组分配前、每行复制前和最后一行复制后检查；取消时不返回部分复制的 buffer。调用方应把已有执行 token 继续传入这些 helper；省略 token 或使用 `CancellationToken.None` 时保留原有运算顺序与数值。

`AdjustBasicTo` 不接管 factory 返回的 Mat。调用方须记录其创建的目标及相关 pin，并在成功、失败或取消时清理；即使 factory 内部发出取消后返回了 Mat，helper 随后的检查也只抛异常，不替调用方 Dispose 该目标。普通 provider 用外层 finally 保证按 Mat → pin 的顺序清理；factory 失败不会改写只读源像素。

provider 观察取消或失败时释放尚未交付的临时 Mat 与输出 buffer；已转移输入仍由 Runner 在退出时释放，不能在发送取消请求时提前使其像素失效。直接调用原地修改 Mat 的 helper 时，取消不承诺恢复调用前的像素，调用方须自行管理工作副本。latest-wins 的 claim 保证旧结果不能发布，不保证新旧计算串行：执行器允许并发时，旧运行在观察取消前仍可能与新运行重叠。检查点不承诺固定的取消时限，验证时应区分调度等待、native 阶段和逐行复制。

预览会话的 linked cancellation source 延续到结果位图填充、最终发布检查和目标清理结束，随后才登记本次运行结束并释放；Runner 已完成不代表预览转换已经结束。在最终发布检查中，即使填充已经成功，失去 claim 的请求仍返回 `Superseded`；仍持有 claim 而观察到调用方取消则返回 `Cancelled`，都不发布该未交付位图。清理异常同样必须释放尚未交给调用者的 `AlgorithmResult`。

<span id="m0-验收门禁"></span>

## 验收门禁

验收以命令、测试类别和对应日志为准，不在文档中维护会随测试增长而失效的通过数量。固定入口是两个 `dotnet test` 项目 `Test\ColorVision.UI.Tests` 与 `Test\ColorVision.Copilot.Tests`（均使用 `-p:Platform=x64`）、`dotnet build .\UI\ColorVision.Algorithms\ColorVision.Algorithms.csproj --no-incremental`、`dotnet build .\ColorVision\ColorVision.csproj -p:Platform=x64 --no-incremental`，以及 `python .\Scripts\tests\test_algorithm_package_contract.py`。

定向回归至少覆盖 Catalog/provider 合同矩阵、同 scope latest-wins 与可重入取消、preview/analysis 宿主恢复、overlay token、九种公共图像格式的分析展示、Batch artifact 所有权、Copilot 白名单/审批/目录/覆盖拒绝以及包内双 TFM 资产。分析结果窗口的自动展示有固定预算：最多 8 张图、单图快照 16 MiB、总快照 32 MiB、预览最长边 1600 且不超过 1,048,576 像素、JSON 自动摘要不超过 32,768 字符；超预算显示诊断占位，完整 JSON/CSV 只在用户显式导出时生成。

`AlgorithmImageBufferTests.cs` 检查只读保留共享同一存储且保持图像布局、buffer 与 lease 的独立释放寿命，以及 `Clone` 仍深复制。`ImageAlgorithmSessionArbitrationTests.cs` 检查会话关闭并换图后，不观察取消的测试 provider 仍可读原输入，Runner 结束时释放本次 `Transferred` 句柄，迟到结果及其图像被释放且不改动新图。`ImageAlgorithmPixelFormatTests.cs` 检查私有 16-bit 输入快照原地归一化的向量/行尾边界及宿主图像独立性，以及 Bgr48/Bgra64 输出的奇偶源 stride、多行 padding、通道/Alpha/DPI 保留、输入不变与独立可修改性，并以 Bgr48 紧密行覆盖向量读取边界和行尾处理。

`ImageDocumentSourceSnapshotTests.cs` 是源状态捕获的边界验证入口：算法输入不创建 native 缓存且保持私有像素、仅捕获元数据后换源仍使用同一 revision 失效、旧 native 租约退役后可读，以及失效回调嵌套换源时拒绝过期绑定。它不证明捕获期间任意不通知的原位写入可被识别。

`BasicAdjustmentProviderTests.cs` 经真实 Runner 比较非中性基础调整的 Gray8/Gray16 全样本范围与 Gray32Float 样本，并检查带 padding 的 Bgra64 输入得到独立、紧密布局且保留 Alpha/DPI 的输出，输入与 Result 释放互不影响；中性输入也必须返回独立 owned 输出。`OpenCvImageAlgorithmsInPlaceTests.cs` 检查原位入口的中性整数全样本、等价参数、非中性 BGRA64 非连续子矩形的外围像素与存储保持，以及 32/64-bit 浮点精确比较和默认钳制。两者复用测试专用 `BasicAdjustmentReference` 保留的原分步计算作为数值 oracle，不把生产 helper 自身作为预期。

`BasicAdjustmentWindowOwnershipTests.cs` 经真实 XAML 参数事件、计时器和 Dispatcher 检查基础调整的分析/新预览/显示选择接管、分析往返后无当前 owner，以及后续新操作重新发起预览；其余七类窗口分别检查分析接管后旧参数回调不能夺回所有权，并保留白平衡后续新输入可主动接管的正向用例。八类窗口共用就绪结果提交、提交回调接管和计算期间改参用例：边缘检测、高斯和中值滤波只有参数版本，其余五类同时检查可编辑文本版本。共享接入回归不等于每个窗口的全部生命周期或大图响应性能都已验收。

`ImageAlgorithmSessionArbitrationTests.cs` 的提交就绪用例在旧结果已发布后，用 provider gate 挂起新的 invocation，检查旧位图不能被当作新结果提交，拒绝提交不取消新运行，且新结果真正发布后仍能正常提交。这种确定性的边界验证不以等待固定毫秒数推断结果是否已就绪。

`AlgorithmBitmapWriteTargetTests.cs` 检查 Bgr48/Bgra64 带 padding 的输入可在原 buffer 释放后完成填充，通道/Alpha/DPI 保留、输出独立可修改、仅 UI 可单次转交，以及取消拒绝转交、等待填充结束后清理和 retained lease 释放。不需要 16-bit 彩色通道交换的分支以 Bgr24 的奇数宽与行 padding、Gray32Float 含 NaN/负值的原始位样本检查逐行复制，避免把旧同步转换测试当作 worker 路径的数值证明。`ImageAlgorithmSessionArbitrationTests.cs` 还在真实填充完成后的发布边界分别触发分析接管与调用方取消，检查终态、宿主显示隔离及 provider 图像释放；这些检查不证明 WPF native 存储立即回收或所有输出格式的全部线程交错。

`LazyBasicAdjustmentTests.cs` 直接调用实际 factory API，检查预取消或不支持深度时不请求目标，成功时只请求一次并保持外部目标存储与数值；factory 内取消后不写目标且仍由调用方释放，factory 失败则保留源图可读且不变。这些边界测试不代表真实连续调参中的所有取消交错或分配行为已经验收。

`OpenCvImageAlgorithmsInPlaceTests.cs` 与 `AlgorithmImageInteropTests.cs` 的预取消用例只验证传入已取消 token 时入口抛出 `OperationCanceledException`，并保留原 Mat；不能单独证明运行中 native 阶段或逐行复制已经观察取消。中途取消需结合真实 provider 的连续调参验证及分阶段轨迹，检查数值、资源释放和旧结果发布隔离，不能把单并发调度门的等待当作协作取消生效。测试关联不表示已运行或通过，也不覆盖违反只读合同的 provider。

预览响应验证应区分会话 `Start`、`PreviewAsync` 返回未完成 Task 前的同步准备、provider 执行与结果展示，记录输入尺寸/格式、采样次数和分配口径。独立方法重放与整次 provider 执行是不同测量，不能拼成同一次执行的内部阶段轨迹。用受控 provider 隔离出的同步耗时与分配只能说明该阶段，不能作为完整 UI 响应、算法吞吐或生产现场性能的验收结果。

正常菜单先构造参数窗口并发起首轮计算，再设置 Owner 和调用 `ShowDialog()`，因此首个结果的 `Source` 发布不以 `Loaded` 或 `ContentRendered` 为前提。参数窗口只有控制项，没有图像视口；图像绘制须同时观察原宿主的 `ImageView.ImageShow`。claim 检查与 `Source` 变化只证明请求进入显示发布，不能据此宣布像素已经呈现。

`Loaded`、有效布局尺寸、`ContentRendered` 和发布后的 `CompositionTarget.Rendering` 帧回调分别提供加载、布局及 WPF 帧事件证据，都不确认 GPU 或屏幕上的像素已呈现。`RenderTargetBitmap` 读回只能证明指定 visual 的离屏栅格结果；离屏、非模态 `Show()` 可以验证真实控件布局与输出，但不等同于正常模态窗口的激活、Owner 禁用及桌面合成行为。隐藏窗口的构造耗时也不能单独代表可见操作的流畅度。

离屏捕获须核对目标坐标、布局偏移和 DPI。实际图像视口应包含 Zoombox 的缩放与裁切；带 Margin 的 Content 子 visual 不能直接按内部尺寸当作零原点画布，否则可能裁掉按钮等内容。参数面板可按 Window 的完整 WPF 坐标捕获，并记录内容与按钮在捕获区域内的边界；这种图像仍不包含完整 native 非客户区。固定抓图规格后比较调参前后与取消恢复的像素，保留原始失败截图，不能把捕获坐标错误当作产品布局故障。

构建验收只承诺零错误。增量构建显示“0 个警告”可能只是目标未重建，不能作为 fresh 构建的零警告证据；每次报告必须单独标明是否使用 `--no-incremental`，并如实记录 fresh 构建产生的 analyzer、nullable 或其他仓库既有警告数量。若默认主项目构建在 `Native/opencv_helper/opencv_helper.vcxproj:28` 以 MSB4278 失败，说明当前机器缺少 `Microsoft.Cpp.Default.props` / C++ workload；零字节 DLL 占位或因引用条件变化得到的成功结果一律无效。native 项目必须在安装 C++ workload 的 Visual Studio Developer PowerShell 中另行验证。

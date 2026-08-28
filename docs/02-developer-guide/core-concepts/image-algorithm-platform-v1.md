# 统一图像算法平台 V1

统一图像算法平台把算法身份、参数、调用、执行和结果从具体 UI、OpenCV、设备通信及 Flow 节点中分离。它采用串行里程碑交付；本页记录已落地的契约和兼容边界，不把后续能力当作当前实现。

## M0 边界与现状盘点

M0 只覆盖平台基础、现有普通 ImageEditor 算法和兼容适配。ROI 统计、剖面、基础与高级图像比较已分别在 [M1](./roi-statistics-v1.md)、[M2](./image-profile-v1.md)、[M3](./image-comparison-v1.md)、[M4](./image-comparison-advanced-v1.md) 形成独立增量。M5–M11 的页面同时记录实现候选和验证契约；是否进入默认产品执行面必须以本页的发布清单为准，不能因为 Catalog 已有 Descriptor 或仓库已有 provider 就写成已发布。原计划的 M12 ONNX/AI 已标记为 Deferred；当前不引入运行时依赖，未来边界见 [ONNX / AI 推理接入设计](./onnx-inference-future-design.md)。

## 当前发布清单

默认 `ImageAlgorithmPlatform.Runtime` 使用现有 `IAlgorithmProviderAvailability` 做失败即停的发布门禁。Catalog 继续保留完整 Descriptor、alias、参数 schema 和文档入口；未发布 provider 不进入菜单、Batch 等可执行投影，`CanExecuteDescriptor`/`CanAttemptExecution` 返回 false，绕过 UI 直接调用 Runner 也只会得到 `provider_unavailable`，其 `provider_dependency_unavailable` 详情包含 `algorithm_experimental` 和稳定的待验证原因码。实现源码和 provider 级测试可继续用于收口验证，但不构成产品可用承诺。

| 发布状态 | 能力 |
| --- | --- |
| 本轮保持启用 | 14 个既有像素算法；ROI 统计；图像剖面；图像比较；几何变换；图像配准；镜头畸变校正；成像校正 |
| 条件启用 | `RemoveMoire` 属于上述既有像素算法，但只有 `opencv_helper.dll` 可加载且包含 `M_RemoveMoire` export 时才显示和执行；依赖缺失时结构化拒绝 |
| 暂缓发布（Experimental） | Blob / 连通域、轮廓提取、亚像素边缘、直线拟合、圆拟合、FFT / 频域分析、摩尔纹分析 |
| 仅设计（Deferred） | ONNX / AI 推理；没有运行时、模型、Execution Provider、产品菜单或默认 Runner 能力 |

暂缓项的 Descriptor 和实现不删除；重新启用必须分别闭环文档中记录的最坏情况资源上限、数值/测量正确性和生产规模测试，再从这一处默认 provider 注册门禁移除，不能在菜单、Batch、Flow 或其他 Runner 调用方单独开旁路。P2/P3 改进只记录，不借本轮发布收口扩大实现范围。

盘点得到的既有执行路径如下：

| 能力 | 交互路径 | Batch 路径 | M0 风险/迁移动作 |
| --- | --- | --- | --- |
| Invert | `ImageAlgorithmApplier` + `OpenCvImageAlgorithms` | 独立委托 | 作为统一 Runner 冒烟样板 |
| Canny | Native `M_ApplyCannyEdgeDetection`，窗口防抖 | Batch 私有 OpenCV 实现；默认值不同 | 使用同一 `CannyParameters` 和 provider，覆盖 8/16 位及 1/3/4 通道 |
| Basic Adjustment | 通用同步预览 session | Batch 私有参数类 | 统一参数、校验和实现 |
| Threshold/Gaussian/Median/Morphology/Denoise | 通用同步预览 session | Batch 私有参数类 | 统一参数、校验和实现 |
| AutoLevels | Native 结果只写 `FunctionImage` | Batch 私有实现 | 经 Runner 正常提交 source revision |
| WhiteBalance | Native + 全局 debounce key | Batch 私有实现 | 独立 Invocation、取消、关闭和切图后不得提交 |
| Sharpen | `ImageAlgorithmApplier` | 独立委托 | Catalog 适配 |
| Histogram Equalization/RemoveMoire | 各自后台 Native 调用 | 前者有独立 Batch 实现，后者无 Batch | 统一资源释放与 latest-wins；RemoveMoire 保留 Native provider 能力边界 |
| PseudoColor | 独立 controller | Batch 私有实现 | 统一参数/执行定义，保留现有工具 façade |

现有 `ImageFrameStore`/`ImageFrameLease` 已经测试了 revision 和延迟释放。ImageView 适配器继续通过该租约读取 source，并把 revision 与 `DocumentInstanceId`、`InvocationId` 一起交给专属 session；平台不维护第二套源帧生命周期。

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
7. M0.5.2 把 `Clear()`、`SetImageSource(...)` 和 `NotifySourcePixelsChanged()` 收口为同一文档变更边界：每次只推进一次 frame-store revision，取消/失效当前 preview 与 analysis，并拒绝旧 Invocation 提交、展示或打开窗口。M0.5.3 使用非 WPF 的 `AlgorithmInvocationCoordinator` 按 `(DocumentInstanceId, SourceRevision)` 仲裁 preview 与 analysis；不同入口/owner 在同一 scope 中原子抢占并取消旧 run，不同文档或 revision 相互隔离，旧 claim 的完成、异常或释放不能清除后继；preview session 可在同 revision 被抢占后重新 claim，因此 PseudoColor 不会永久停在 `Superseded`。M0.5.4 由 ImageView 的 `AlgorithmOverlayManager` 把 artifact、实际 WPF Visual、document、revision 和 registration token 作为一个所有权单元：原地提交清 transient 并把 persistent 关联到新 revision，换图/Clear/宿主释放清全部，窗口关闭只释放 transient，旧 session 不能删除同名后继。

## 执行平面与兼容层

本地像素算法和远端 MQTT/设备算法共享 Descriptor/Invocation/Result 控制面，但保持不同 execution plane。旧 `AlgorithmNode`、STN 序列化字段、公开 EditorTool 构造方法和菜单 Guid 保留；适配器只把适合的本地算法路由到 Runner，不反射发现或重写远端节点。能力矩阵的“本地 Flow adapter=是”表示 `LocalFlowImageAlgorithmAdapter.ExecuteRawAsync` 可从进程内 `LocalFlowFrameLease` 调用同一 Catalog/Invocation/Runner，并已有直接适配器测试；它不表示生产 Flow 画布已经注册新的本地算法节点。当前真实生产接入仍只有既有远端 MQTT/设备 `AlgorithmNode`；仓库尚无引用 `LocalFlowImageAlgorithmAdapter` 的节点模板、节点注册或 STN 序列化类型。因此本地 Flow 目前是可调用 adapter/API 边界，不能在发布说明中写成已完成生产节点接入。

Copilot 仅能看到显式白名单中同时声明 `Headless | Local | Deterministic | Copilot` 的算法。算法 Catalog 本身不授予目录访问、覆盖、数量或审批权限；宿主现有策略仍是最终授权边界。M0.5.7 把“对图片执行反相/Canny/白平衡”等明确算法动作与格式转换一起路由到受保护的 `ConvertBatchImages` 工具，并由 execution contract 强制收集逐文件成功/失败与输出路径证据。该工具仍要求原生审批，只接受可读/可写范围，最多 500 个文件，从不覆盖已有文件，并在解析 Catalog 后再次检查显式白名单与 `Batch | Headless | Local | Deterministic | Copilot` 能力；反射发现、远端 provider 和未列入白名单的算法不会因此暴露。

M0.5.6 把普通算法的 ImageView 菜单兼容 ID/顺序和图像输出 Batch 顺序写入 Descriptor 的中立 `AlgorithmPresentationMetadata`。`AlgorithmCatalogProjection` 先按 `Interactive | Local` 或 `Batch | Headless | Local` 过滤，再投影给 `AlgorithmsContextMenu` 与 `BatchImageAlgorithms`；宿主不再维护成员清单。现有专用预览窗口仍作为 WPF 兼容命令适配器，未知的单输入菜单项使用 Catalog 默认参数的通用编辑/执行回退。Batch 列表保持旧 UI 顺序，`BatchImageAlgorithmDefinition` 的公开构造方法和同步 `Apply(Mat)` façade 保留；Canny 不再由 Batch 覆盖 50/150 的统一默认值。ROI 统计和剖面虽声明 Batch capability，但由结构化 `BatchAlgorithmAnalysisProcessor` 执行，不设置 `BatchImageProcessingOrder`，因此不会错误进入只接受主图像 artifact 的 `BatchImageProcessingWindow`。旧菜单 Guid（例如 `InvertImage`、`EdgeDetection`、`Erode`、`BilateralFilter`）继续作为 Catalog alias 解析。Flow 的 `LocalFlowImageAlgorithmAdapter` 只复制并执行进程内 RAW 帧，不取得调用者 `LocalFlowFrameLease` 的所有权；旧远端 `AlgorithmNode` 及其 STN/MQTT 字段没有改写。`ColorVision.Algorithms` 作为独立同名 NuGet 包生成 `net8.0` 与 `net10.0` 资产，ImageEditor 的项目引用在打包时成为包依赖，CI 发布顺序固定为先 Algorithms、后 ImageEditor。该中立包不携带 provider/native runtime；`opencv_helper.dll` 的 RemoveMoire provider 在候选选择阶段探测 DLL 可加载性和 `M_RemoveMoire` export，也会解析打包目录 `runtimes/win-x64/native`。验证成功的模块保留到进程结束，避免探针卸载与后续 P/Invoke 之间的竞态；失败由 Runner 返回带拒绝诊断的 `provider_unavailable`。

统一路径继续复用 `ImageFrameLease/ImageFrameStore` 的 revision 与延迟释放；WPF 原图到带完整格式语义的 canonical snapshot，以及每次可独立取消的 preview run snapshot，仍是明确的安全复制边界。M0.5.8 将 canonical input 直接 pin 成只读 OpenCV header，native provider 也在同步调用期间 pin 输入；provider 输出仍复制到由 Result 拥有的 buffer。Gray8/Gray16/Gray32Float/Bgr24/Bgra32 写入 WPF 时直接 pin，不再先 `ToArray()`；Bgr48/Bgra64 因 WPF 端是 RGB/RGBA 布局仍需一次通道交换副本。中立帧彩色数据统一为交错 BGR，四通道统一为有意义的直通（非预乘）Alpha；Rgb24/Rgb48/Rgba64 在入口交换，Bgr32 未用字节置 255，Pbgra32 反预乘，Indexed8 按 palette 展开。`HImage` 只有 depth/channels，不能表达这些语义，直接适配必须显式声明 canonical `AlgorithmImageFormat`。

## 调用和失败处理

调用方使用 `AlgorithmInvocation.Create(AlgorithmId, parameters, roi)` 创建当前 schema 的调用，并在 `AlgorithmRunRequest` 中明确输入是 `Borrowed` 还是 `Transferred`。调用 `ImageAlgorithmPlatform.Runner.RunAsync(request, cancellationToken)` 后必须释放整个 `AlgorithmResult`；不要单独缓存其中受结果拥有的图像缓冲。

常见结构化失败包括 `algorithm_not_found`、`algorithm_version_incompatible`、`parameter_schema_newer`、`parameter_migration_missing`、`unsupported_format`、`roi_kind_unsupported`、`provider_unavailable` 和 `provider_output_format_violation`。发布门禁或运行时依赖拒绝都使用 `provider_unavailable`，具体原因位于失败详情的 `provider_dependency_unavailable`；暂缓算法包含 `algorithm_experimental` 和对应 `release_validation_pending` 原因码。取消返回 `Cancelled` 结果；旧 invocation 或旧 source revision 在 ImageView 中返回 `Superseded`，不会提交到当前图。

## M0 验收门禁

验收以命令、测试类别和对应日志为准，不在文档中维护会随测试增长而失效的通过数量。固定入口是两个 `dotnet test` 项目 `Test\ColorVision.UI.Tests` 与 `Test\ColorVision.Copilot.Tests`（均使用 `-p:Platform=x64`）、`dotnet build .\UI\ColorVision.Algorithms\ColorVision.Algorithms.csproj --no-incremental`、`dotnet build .\ColorVision\ColorVision.csproj -p:Platform=x64 --no-incremental`，以及 `python .\Scripts\tests\test_algorithm_package_contract.py`。

定向回归至少覆盖 Catalog/provider 合同矩阵、同 scope latest-wins 与可重入取消、preview/analysis 宿主恢复、overlay token、九种公共图像格式的分析展示、Batch artifact 所有权、Copilot 白名单/审批/目录/覆盖拒绝以及包内双 TFM 资产。分析结果窗口的自动展示有固定预算：最多 8 张图、单图快照 16 MiB、总快照 32 MiB、预览最长边 1600 且不超过 1,048,576 像素、JSON 自动摘要不超过 32,768 字符；超预算显示诊断占位，完整 JSON/CSV 只在用户显式导出时生成。

构建验收只承诺零错误。增量构建显示“0 个警告”可能只是目标未重建，不能作为 fresh 构建的零警告证据；每次报告必须单独标明是否使用 `--no-incremental`，并如实记录 fresh 构建产生的 analyzer、nullable 或其他仓库既有警告数量。若默认主项目构建在 `Native/opencv_helper/opencv_helper.vcxproj:28` 以 MSB4278 失败，说明当前机器缺少 `Microsoft.Cpp.Default.props` / C++ workload；零字节 DLL 占位或因引用条件变化得到的成功结果一律无效。native 项目必须在安装 C++ workload 的 Visual Studio Developer PowerShell 中另行验证。

# 统一图像算法平台 V1

统一图像算法平台把算法身份、参数、调用、执行和结果从具体 UI、OpenCV、设备通信及 Flow 节点中分离。它采用串行里程碑交付；本页记录已落地的契约和兼容边界，不把后续能力当作当前实现。

## M0 边界与现状盘点

M0 只覆盖平台基础、现有普通 ImageEditor 算法和兼容适配。ROI 统计、剖面、基础与高级图像比较已分别在 [M1](./roi-statistics-v1.md)、[M2](./image-profile-v1.md)、[M3](./image-comparison-v1.md)、[M4](./image-comparison-advanced-v1.md) 形成独立增量；工业测量、几何校正、频域和 AI 算法仍必须在各自后续里程碑通过门禁后加入。

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

| 稳定 AlgorithmId | 参数与默认值（schema 1） | 输入 → 输出 | ImageView | Batch | 本地 Flow | Copilot |
| --- | --- | --- | :---: | :---: | :---: | :---: |
| `colorvision.image.invert` | 无 | 全部格式 → 同输入 | 是 | 是 | 是 | 是 |
| `colorvision.image.canny` | low=50、high=150、aperture=3、L2=false | 全部格式 → G8 | 是 | 是 | 是 | 是 |
| `colorvision.image.basic-adjustment` | exposure=0、brightness=0、contrast=0、gamma=1 | 全部格式 → 同输入 | 是 | 是 | 是 | 是 |
| `colorvision.image.threshold` | threshold=128 | 全部格式 → 同输入 | 是 | 是 | 是 | 是 |
| `colorvision.image.sharpen` | 无 | 全部格式 → 同输入 | 是 | 是 | 是 | 是 |
| `colorvision.image.gaussian-blur` | kernel=5、sigma=1.5 | 全部格式 → 同输入 | 是 | 是 | 是 | 是 |
| `colorvision.image.median-blur` | kernel=5；非 8 位图的 kernel 最大为 5 | 全部格式 → 同输入 | 是 | 是 | 是 | 是 |
| `colorvision.image.morphology` | erode、kernel=3、iterations=1 | 全部格式 → 同输入 | 是 | 是 | 是 | 是 |
| `colorvision.image.denoise` | bilateral、kernel=5、sigmaColor=75、sigmaSpace=75 | 全部格式 → 同输入 | 是 | 是 | 是 | 是 |
| `colorvision.image.auto-levels` | 无 | 全部格式 → 同输入 | 是 | 是 | 是 | 是 |
| `colorvision.image.white-balance` | R/G/B scale=1 | BGR8/BGR16/BGR32F/BGRA8/BGRA16/BGRA32F → 同输入 | 是 | 是 | 是 | 是 |
| `colorvision.image.histogram-equalization` | 无 | 灰度 → G8；彩色 → BGR8 | 是 | 是 | 是 | 是 |
| `colorvision.image.remove-moire` | 无 | 全部格式 → 同输入 | 是 | 否 | 否 | 否 |
| `colorvision.image.pseudo-color` | Jet、标称范围、0..255、channel=-1 | 全部格式 → BGR8 | 是 | 是 | 是 | 是 |

这里的“全部格式”只指 G8/G16/G32F/BGR8/BGR16/BGR32F/BGRA8/BGRA16/BGRA32F；需要标称范围的 32F 运算沿用现有归一化约定 `[0,1]`。Descriptor 和 provider 都会检查格式；像非 8 位中值滤波大核这样的参数/格式组合返回 `parameter_format_unsupported`，不会把 OpenCV 异常伪装成成功。旧 Batch façade 对偶数 kernel 仍按既有行为向上归一化为奇数；直接 Invocation、Flow 和 Copilot 使用严格参数校验。

## 公共控制面

`ColorVision.Algorithms` 是不依赖 WPF、OpenCvSharp、`HImage`、MQTT、STNode、`DeviceAlgorithm` 或 `MessageBox` 的公共契约项目：

- `AlgorithmId` 和 `AlgorithmVersion` 是持久化身份；旧菜单名、Flow 名称和 STN 名称只能通过 alias/adapter 映射，不能成为 provider 类型名。
- `AlgorithmDescriptor` 描述逻辑能力；`AlgorithmProviderMetadata` 单独描述 CPU/native/GPU/remote 实现和执行平面。
- `IAlgorithmParameters` 给出 schema 版本和只读校验；`IAlgorithmParameterMigrator` 只允许显式、逐版本迁移。
- `AlgorithmInvocation` 可 JSON 往返，携带参数 schema、输入引用、ROI、preset 和调用 ID。
- `AlgorithmResult` 用 Image、Measurement、Table、Geometry、StructuredData 和 Overlay artifact 表达结果；核心 Overlay 只引用几何和样式，不包含 WPF drawing 对象。
- `AlgorithmRunner` 负责解析、版本/格式/ROI/参数验证、provider 选择、按资源类型调度、取消、诊断和转移输入的释放。

像素坐标统一使用左上角原点。整数坐标表示像素中心；矩形是半开区间 `[x, x + width) × [y, y + height)`。物理坐标统一使用毫米，必须显式声明并通过图像 DPI/标定转换，核心结果不得暗中混用 WPF DIP。

## M0 执行与所有权规则

1. Runner 把输入视为只读；provider 必须在独立输出上工作。
2. `Borrowed` 输入由调用方释放；`Transferred` 输入无论成功、失败或取消都由 Runner 在结束时释放。
3. 成功结果拥有其 Image artifact；使用者提交、导出或展示后释放整个 `AlgorithmResult`。
4. ImageView session 只在 document、source revision 和 invocation 三者仍匹配时显示或提交结果。新调用使旧调用过期；关闭、取消、切图和 source revision 改变都会阻止迟到结果。
5. Preview 不改变 source revision；Commit 原子替换 `ViewBitmapSource` 后只递增一次 revision；Cancel 不改变 source。
6. Batch 输出格式属于保存策略，不注册为图像算法。

## 执行平面与兼容层

本地像素算法和远端 MQTT/设备算法共享 Descriptor/Invocation/Result 控制面，但保持不同 execution plane。旧 `AlgorithmNode`、STN 序列化字段、公开 EditorTool 构造方法和菜单 Guid 保留；适配器只把适合的本地算法路由到 Runner，不反射发现或重写远端节点。

Copilot 仅能看到显式白名单中同时声明 `Headless | Local | Deterministic | Copilot` 的算法。算法 Catalog 本身不授予目录访问、覆盖、数量或审批权限；宿主现有策略仍是最终授权边界。

Batch 列表保持旧 UI 顺序，`BatchImageAlgorithmDefinition` 的公开构造方法和同步 `Apply(Mat)` façade 保留。旧菜单 Guid（例如 `InvertImage`、`EdgeDetection`、`Erode`、`BilateralFilter`）作为 Catalog alias 解析。Flow 的 `LocalFlowImageAlgorithmAdapter` 只复制并执行进程内 RAW 帧，不取得调用者 `LocalFlowFrameLease` 的所有权；旧远端 `AlgorithmNode` 及其 STN/MQTT 字段没有改写。

统一路径为隔离 UI/native 生命周期会进行显式输入和输出拷贝。它避免跨线程借用裸指针，代价是每次执行约两次紧密像素缓冲拷贝；Runner 的 CPU/native/GPU/remote 并发门分别限流。M0 不做隐藏降采样，大图性能审查应以真实尺寸继续观察峰值内存和延迟。

中立帧的像素布局是唯一 canonical 边界：彩色数据统一为交错 BGR，四通道统一为有意义的直通（非预乘）Alpha。WPF 的 Rgb24/Rgb48/Rgba64 在入口交换为 BGR，Bgr32 的未用字节规范化为 255，Pbgra32 显式反预乘，Indexed8 按 palette 展开为 BGRA。`HImage` 本身只有 depth/channels，不能表达这些语义；因此算法入口不再从 HImage 猜格式，直接 HImage 适配必须显式声明已经规范化的 `AlgorithmImageFormat`。

## 调用和失败处理

调用方使用 `AlgorithmInvocation.Create(AlgorithmId, parameters, roi)` 创建当前 schema 的调用，并在 `AlgorithmRunRequest` 中明确输入是 `Borrowed` 还是 `Transferred`。调用 `ImageAlgorithmPlatform.Runner.RunAsync(request, cancellationToken)` 后必须释放整个 `AlgorithmResult`；不要单独缓存其中受结果拥有的图像缓冲。

常见结构化失败包括 `algorithm_not_found`、`algorithm_version_incompatible`、`parameter_schema_newer`、`parameter_migration_missing`、`unsupported_format`、`roi_kind_unsupported`、`provider_unavailable` 和 `provider_output_format_violation`。取消返回 `Cancelled` 结果；旧 invocation 或旧 source revision 在 ImageView 中返回 `Superseded`，不会提交到当前图。

## M0 验收门禁

M0 已按上述边界完成实现和差异审查，验收证据如下：

- 公共契约项目构建为 0 错误；M0 UI 定向回归 104/104 通过。主项目 x64 构建不能计为通过：移除临时零字节 `Native/opencv_helper/x64/Release/opencv_helper.dll` 后，`dotnet build` 在 `Native/opencv_helper/opencv_helper.vcxproj:28` 以 MSB4278 失败，因为当前环境缺少 `Microsoft.Cpp.Default.props` / C++ workload。任何依赖该占位 DLL 改变项目引用条件所得的“0 错误”结果均不作为验收证据。
- UI 全量测试 1483/1486 通过；3 项仅因工作区缺少真实 `opencv_helper.dll`（POI 两项、native log bridge 一项）无法运行。
- Copilot 全量测试 1446/1446、白名单/Schema 定向测试 7/7、文档 176 文件及 31 个重定向校验均通过。

本机未安装可生成该专有 native 库的 C++ 工作负载；M0 保留既有 ABI 与 native provider 适配，但不把主宿主构建或缺少真实 DLL 的执行路径标记为已验证。最终合并前须在 Visual Studio Developer PowerShell 中重跑主项目 x64 构建。

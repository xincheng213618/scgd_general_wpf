---
knowledge_id: "ui.image-editor-context"
knowledge_type: "topic"
status: "current"
summary: "ImageView 的文档、会话、显示、算法协调和扩展所有权；说明源提交、连续帧有界处理、工具生命周期及临时 ROI 有效期。"
aliases: ["图像编辑器上下文", "ImageView重构", "状态归属", "工具栏刷新", "配置作用域", "临时ROI", "临时选区", "四边形", "选区坐标", "白色画布选区", "矢量画布布点", "EditorContext", "ImageProcessingContext", "ImageDocument", "ImageEditorSession", "ImagePresentation", "ImageStreamPresentation", "ImageDisplayEffects", "ImageShaderPresentation", "ImageOperationCoordinator", "CommitSourcePixels", "ImageViewConfig", "ImageViewPropertyScope", "IEditorToolFactory", "BeginSelectAsync", "SelectShapeType", "SelectResult", "TransientRoiSelectionSession", "ImageSelectionScope", "EnableEditorImageServices"]
code_paths: ["UI/ColorVision.ImageEditor/ARCHITECTURE.md", "UI/ColorVision.ImageEditor/Documents", "UI/ColorVision.ImageEditor/ImageEditorSession.cs", "UI/ColorVision.ImageEditor/Presentation", "UI/ColorVision.ImageEditor/Operations", "UI/ColorVision.ImageEditor/Output", "UI/ColorVision.ImageEditor/Tooling", "UI/ColorVision.ImageEditor/Navigation", "UI/ColorVision.ImageEditor/Abstractions/PseudoColorFrameRequest.cs", "UI/ColorVision.ImageEditor/EditorContext.cs", "UI/ColorVision.ImageEditor/Contexts/ImageProcessingContext.cs", "UI/ColorVision.ImageEditor/ImageViewConfig.cs", "UI/ColorVision.ImageEditor/ImageViewPropertyMetadata.cs", "UI/ColorVision.ImageEditor/EditorToolFactory.cs", "UI/ColorVision.ImageEditor/ImageView.xaml.cs", "UI/ColorVision.ImageEditor/TransientRoiSelectionSession.cs", "UI/ColorVision.ImageEditor/EditorTools/PseudoColor", "UI/ColorVision.UI/AssemblyHandler.cs", "UI/ColorVision.ImageEditor/Draw/ImageDrawingPresentation.cs"]
test_paths: ["Test/ColorVision.UI.Tests/ImageDocumentPresentationTests.cs", "Test/ColorVision.UI.Tests/ImageStreamPresentationTests.cs", "Test/ColorVision.UI.Tests/ImageAlgorithmPreviewSessionTests.cs", "Test/ColorVision.UI.Tests/ImageGroupNavigationTests.cs", "Test/ColorVision.UI.Tests/EditorToolFactoryLifecycleTests.cs", "Test/ColorVision.UI.Tests/ImageDisplayEffectsTests.cs", "Test/ColorVision.UI.Tests/TransientRoiSelectionSessionTests.cs"]
related: ["ui.image-editor", "ui.discovery", "ui.configuration", "algorithms.platform", "algorithms.roi-routes", "algorithms.local-native-analysis"]
---

# ImageEditor：上下文、工具装配与临时选区

`ImageView` 是 WPF 视图宿主，文档、显示能力、算法协调和工具装配有各自的状态拥有者。扩展通过 `EditorContext` 或更窄的上下文访问这些能力；新增功能应先确定其写入的是源像素、显示结果还是绘图对象，再选择入口。文件打开、撤销、保存、视频和 3D 操作见 [ImageEditor](./ColorVision.ImageEditor.md)。

## 状态由谁持有

| 拥有者 | 状态与责任 |
| --- | --- |
| `Documents/ImageDocument` | 当前 `ImageSource`、文档 ID、source revision 与 `ImageFrameStore`；租约缓存跟随文档失效和释放 |
| `ImageEditorSession` | 协调源替换、源像素提交、配置清理及处理能力释放；通过 `ImageSourceMetadata` 解析像素元数据并应用当前视图标定 |
| `Presentation/ImagePresentation` | 当前显示源、`FunctionImage` 与显示请求代次；在视图 Dispatcher 上成对发布或恢复基准源 |
| `ImageDisplayEffects` | 每视图伪彩 state/controller 及 `ImageShaderPresentation`；工具栏不拥有这些基础能力 |
| `ImageStreamPresentation` / `ImageChannelPresenter` | 连续帧的有界后台处理与发布、单通道异步显示；都经 `ImagePresentation` 写入显示 |
| `Operations/ImageOperationCoordinator` | preview/analysis claim、结果发布事务、回滚和文档变更失效，持有所选 runtime 的协调入口及本视图 overlay manager |
| `Output/ImageSnapshotCapture` / `ImageSnapshotEncoder` | UI 捕获、独立快照和后台编码；输出不自行提交文档源 |
| `IEditorToolFactory` / `Tooling/EditorToolbarComposer` | 扩展发现与实例生命周期、工具栏元素装配；刷新 UI 不创建新的文档或显示能力 |
| `Navigation/ImageGroupNavigation` | 图像组项目、当前索引、手动选择及自动跟随；视图负责 Dispatcher 适配和导航按钮 |
| `Draw/ImageDrawingPresentation` / `Tooling/ImageContextMenuComposer` | 绘图列表同步、显示配置与缩放订阅、活动编辑提交，以及按命中对象/扩展构建菜单；绘图上下文、临时 ROI 与算法 overlay 各自保留所有权 |

`ImageView.CreateEditorContext` 创建视图配置、绘图上下文和处理上下文，再由 `EditorContext` 聚合。`ImageProcessingContext` 提供强类型 `Presentation`、`DisplayEffects`、`StreamPresentation` 和算法入口，算法协调转给 `ImageOperationCoordinator`。binding 仅提供同一个文档的身份、版本、源及操作委托；所有宿主的 `FunctionImage` 都由 `ImagePresentation` 持有，binding 不存储显示状态。文档没有 `Source` 时取帧返回 null，不从显示图推断计算源。`EditorContext` 不是任意服务的注册容器：绘图列表、选择态、画布和缩放主要转发给 `DrawEditorContext`，文本编辑上下文按需创建。

### 源像素提交与显示发布

`ViewBitmapSource` 实际类型是 `ImageSource`，可持有基准位图或 `DrawingImage` 等矢量画布；`FunctionImage` 表示处理/预览显示结果。兼容 setter 只赋值，**不自动推进 revision，也不等于完整源替换或显示发布**。内部显示生产者应使用 `Presentation.Publish(displaySource, functionImage)`，恢复基准图用 `RestoreSource()`；这些操作本身不提交源像素。

`CommitSourcePixels(source)` 在 `ImageView`、`EditorContext` 和 `ImageProcessingContext` 提供等价入口，统一转发到 `ImageEditorSession` 的同一个实现：先赋基准源，再显式推进一次 source revision，不重新打开文件或执行完整换图重置。原位改写现有位图仍可调用 `NotifySourcePixelsChanged()`。`SetImageSource` 则走完整源替换，保留元数据、标定、图层和加载通知契约。缓存取帧发现源对象更换时也会失效，但不能以此代替生产者主动提交，尤其不能自动识别同一位图的字节改写；详见[源帧寿命](./image-frame-lifetime.md)。

源赋值与显示发布要求 UI Dispatcher；`NotifySourcePixelsChanged()` 保留后台生产者的通知入口。帧存储同步退役旧版本、原子失效显示请求，随后算法与图元清理调度到 UI。同步回调为新版本创建的 preview、analysis 或 overlay 必须保留，不能让迟到的旧版本清理将它移除。文档释放后拒绝源赋值，失败的提交也不能重新挂回像素引用。

异步显示请求通过 `BeginRequest()` 捕获 `(DocumentId, SourceRevision, Generation)`，`TryPublish` 只接受仍有效的请求；新的显示选择或文档变更使旧请求失效。单通道提取持有读取租约直到后台读取完成，迟到输出即使仍拥有有效内存，也不能覆盖新的显示选择。`Publish` 是 UI 同步发布边界，不提供文件、像素运算或任意外部回调的整体事务。

文档变更先使显示请求失效，再调用宿主 revision hook，随后失效算法 scope 并通知订阅者，最后使伪彩请求失效。算法提交的 claim 消费、回滚和重入检查归 `ImageOperationCoordinator`，不能用普通显示请求取代 invocation 仲裁；详见[统一算法平台](../../02-developer-guide/core-concepts/image-algorithm-platform-v1.md)。

### 配置分类不是隔离容器

`ImageViewConfig.Properties` 是一个按字符串键索引的字典。`ImageViewPropertyScope` 包含 `ImageMetadata`、`ViewState`、`OpenerRuntime` 三项。

- `SetImageMetadata`、`SetViewState`、`SetOpenerRuntime` 共用 `SetProperty`；同名键会覆盖原值及 scope/owner/description。scope 和 owner 是说明元数据，不是命名空间或权限隔离。新键应避免与已有键冲突。
- `GetProperties<T>` 只接受值本身属于 `T`，否则返回默认值，不自动转换。直接写公开 `Properties` 字典可能绕过或留下旧的分类元数据；没有分类记录的条目在 `GetPropertyEntries` 中归为 `ViewState`。
- `ClearProperties` 清空文件路径、全部属性及分类记录，再触发 `Cleared`，不是只清 `ImageMetadata`。`ClearCommand` 本身只发 `Cleared` 通知，并不调用 `ClearProperties`；实际后果还取决于订阅者。
- `Configs` 是另一份按精确 `Type` 缓存的 `IImageEditorConfig` 字典，`GetRequiredService<T>` 缺项时构造并缓存。`ClearProperties` 不清这份字典；它也不等于全局 `ConfigService` 的配置实例或保存入口。`Properties` 与分类记录标了 `JsonIgnore`，不能把临时属性写入当成已保存。

`ImageViewConfig.Calibration` 是当前视图的独立标定状态，比例尺、尺子和参考网格从同一上下文读取。显示滤镜在构造时复制默认值，普通调整不回写默认；CVCIE 探针缓存于 `Configs`。设置的范围、保存和兼容契约见[图像设置](../../02-developer-guide/core-concepts/image-editor-settings-plan.md)。

### 关闭编辑服务也会改变图像文档

`SetImageSource(source)` 使用视图的 `EnableEditorImageServices`，并启用默认图层控制器；三参入口 `(source, enableEditorImageServices, configureDefaultLayerController)` 可分别指定这两个行为。它总会先登记 `ImageSourceReplaced`、重置伪彩并清旧源，再检查/设置新像素源。`enableEditorImageServices=false` 主要关闭图层选择以及本次伪彩图像配置和当前视图标定应用，不会跳过文档版本推进、源替换、像素元数据、加载通知或状态栏刷新。不支持的像素格式可能在旧源已清除后抛出异常。

伪彩换图保留配色与自动范围偏好，关闭效果并重新计算图像相关范围；自动范围在新源赋值后计算。`Presentation/PseudoColor` 持有伪彩控制器、`PseudoColorState` 和默认配置，`PseudoColorEditorTool` 仅绑定控件及操作同一能力；释放工具不释放 controller。连续帧通过 `DisplayEffects.TryCapturePseudoColorRequest` 取得参数，并统一由 `StreamPresentation` 提交源与显示结果，不提供另一条外部处理结果发布路径。默认配置类型保留原完整类型名作为已有用户配置的稳定持久键，运行状态不依赖工具实例。静态图预览、连续帧 native 处理和显示 shader 保持各自输入与数值契约，不能因同属“效果”就视为等价算法。

`ImageShaderPresentation` 拥有独立滤镜状态和 effect 附着，工具负责界面与显式持久化。滤镜沿用完整画布的 `SceneEffect` 范围，包含图像和叠加内容；没有静默缩小成只处理底图。关闭时仅在当前 effect 仍是自身对象时恢复先前 effect。

### 连续帧显示

相机 `RealtimeFramePresenter` 和视频打开器把帧交给 `ImageStreamPresentation`。可变输入先复制并冻结；后台处理读取该快照，不读取生产者下次改写的暂存位图。伪彩开启时最多有一帧执行、一帧等待，新输入替换等待帧，不建立无界队列；在途帧可以先发布，随后处理最新等待帧。

处理输出也复制成独立冻结位图，不借用或改写已有 `FunctionImage`，以免破坏其它预览或输出仍保留的图像。发布时先通过 `CommitSourcePixels` 提交此次处理对应的冻结源，再通过 `Presentation.Publish` 发布同帧结果。因此当前基准源与伪彩显示来自同一帧，source revision 仍按帧提交推进；流身份和序号用于调度，不替代文档版本。

提交期间若重入回调又换图、推进版本、重置流、选择新显示或取得新的算法预览，此帧停止发布并重置流状态。`SelectionVersion` 区分显式显示选择与文档失效时清理旧预览的基准恢复，后者不应被当成新选择而阻断正常帧；新的用户显示选择和预览则优先于正在提交的流帧。

换图、释放、流重置、参数变化和调用方的发布条件均参与过期检查。伪彩不可用时直接发布原图；native 处理异常时，仍有效的请求回退到该帧原图，清除旧处理显示。重置不承诺中断正在执行的 native 调用，迟到输出会释放。参数调整可通过 `RefreshCurrent()` 重处理最后的冻结帧，关闭伪彩可立即恢复它；实时请求仍需已有基准源，缺少基准时先发布原图建立源。视频解码与相机指标计算的上游限制分别见[视频模式](./ColorVision.ImageEditor.md#视频模式)和[相机实时链](../../01-user-guide/devices/camera.md#本地视频与实时伪彩)。

## 扩展发现、构造与刷新

新增功能先确定状态的寿命与写入边界：

| 新增能力 | 放置与接入方式 |
| --- | --- |
| 新文件格式或连续数据源 | 打开器负责格式与元数据；有原生资源或播放状态时使用独立内容 session，连续像素交给 `StreamPresentation` |
| 新显示效果 | 在 `Presentation` 中持有处理能力和当前状态，由处理上下文提供；工具负责绑定与配置。先明确效果作用于源像素、显示像素还是整个场景 |
| 新交互算法 | 使用现有 runtime 和 `ImageOperationCoordinator` 的 preview/analysis 契约，算法输入取源帧租约，结果通过既有提交或 overlay 路径交付 |
| 新图元或编辑工具 | 图元、选择与历史操作放在 `Draw`，工具与菜单使用现有扩展点；不把文档源或 native 句柄放进工具栏控件 |
| 新输出格式 | `ImageSnapshotCapture` 确定捕获内容，`ImageSnapshotEncoder` 负责后台编码；后台不访问活动 WPF 控件 |

这些边界不要求每项功能新增一个接口，也不要求把 `ImageView` 变成通用服务容器。优先复用已有上下文，只有确实存在不同实现或需要隔离外部资源时才引入新的替换接缝。

`EditorToolFactory.cs` 中的 `IEditorToolFactory` 实际是类。构造时各扩展点走不同发现入口，并非全部统一为无参反射：

| 扩展点 | 当前构造约束 |
| --- | --- |
| `IDVContextMenu` | `AssemblyHandler` 的程序集/类型集合；优先可解析上下文构造，无匹配时才尝试 public 无参构造 |
| `IIEditorToolContextMenu`、全局 `IEditorTool` | `Application.Current.GetAssemblies()` 与 `AssemblyHandler.GetTypes`；要求可解析的 public 上下文构造，不提供无参回退 |
| `IImageComponent` | `AssemblyService.LoadImplementations<IImageComponent>()`；不同于上述上下文注入通道 |
| `IImageOpen` | `AssemblyService` 程序集内查 `FileExtensionAttribute`，对每个后缀用 `Activator.CreateInstance(type, context)` 创建实例；后缀转小写后 `Dictionary.Add`，重复后缀会冲突，不是后者自动覆盖 |

上下文构造只匹配 `EditorContext`、`DrawEditorContext`、`ImageProcessingContext`、`DrawCanvas`、`TextEditingContext`、`ImageViewConfig` 六种精确类型；选择参数最多且所有参数可解析的 public 构造。它不是通用 DI，不自动匹配基类、任意接口或新增服务。每个视图装配自己的扩展实例，程序集与类型的缓存规则见[UI 扩展发现](./ui-runtime-handoff.md)。

只有实现 `IAlgorithmCatalogBoundMenu` 的菜单走 runtime descriptor/adapter/capability 门禁；不能把该门禁泛化到所有右键菜单或直接 native 工具。后者见[本地 Native 分析](../algorithms/local-native-analysis.md)。

`RefreshToolBars` 委托 `EditorToolbarComposer` 移除自己生成的 UI 元素，并从现有工具集合重新装配；它不重扫程序集、重建打开器或发现新加载插件。composer 只拥有 UI 附着，工具实例仍由工厂管理。初始化时工厂早于 `Crosshair` 创建，随后才执行 `IImageComponent.Execute` 等步骤；扩展构造不能假定所有视图服务都已就绪。

打开器通过 `IImageOpenEditorToolProvider` 贡献当前工具。`GetEffectiveEditorTools` 先放打开器工具，再加入未被其非空 `GuidId` 覆盖的全局工具；比较区分大小写，空 ID 不参与覆盖，也不会自动去重打开器内部的重复 ID。`ApplyImageOpenTools` 先通知旧 lifecycle 停用、替换集合和刷新工具栏，再通知新 lifecycle 启用；停用不等于所有旧工具已 `Dispose`。

工厂 `Dispose` 停用当前打开器 lifecycle，并对全局及当前打开器工具中的 `IDisposable` 去重释放，不承诺释放每个 opener/component。`ImageEditorSession.Dispose` 依次释放连续帧、显示能力、算法 overlay 和文档。`ImageView.Unloaded` 仅解绑窗口快捷键，不是 `Dispose`；宿主仍需负责真正释放。工具栏重建、控件卸载和文档资源释放不能混用。

## 临时 ROI：形状、坐标与有效期

`ImageView.BeginSelectAsync` 每次创建一个 `TransientRoiSelectionSession`，支持 Rectangle、Circle、Polygon、Quadrilateral。临时 visual 直接加入/移出画布，不登记撤销命令，也不是持久注释。

手动选择只需要有效画布，不要求 `BitmapSource`：宽高有限且大于零的 `ImageSource` 均可进入选区，包括 POI 默认白色 `DrawingImage`。选择过程不将矢量画布栅格化，也不生成虚构像素。涉及统计、识别等像素计算时，`ImageAlgorithmInputFactory` 仍要求可读取的源帧与匹配的位图；能绘制选区不代表能执行图像计算。

多边形模式的完成键为 **Enter、Space、End、Tab**；**Escape** 取消任意形状并返回 `null`。

| 形状 | 绘制与完成 | 右键行为 |
| --- | --- | --- |
| Rectangle | 拖拽后松开左键；矩形宽高均大于 1 且数值有限时完成 | session 不处理右键 |
| Circle | 按下点为圆心，拖拽距离为半径；松开左键且包围框通过同一校验时完成 | session 不处理右键 |
| Polygon | 逐点点击，再按完成键或右键；至少三个点，形状须非退化且不自交 | 尝试完成；无效时继续等待 |
| Quadrilateral | 第四次点击后尝试完成；第四点无效时，下一次点击替换第四点 | 不足四点时取消；已有四点时尝试完成 |

四边形的键盘完成路径也使用 `TryCompletePolygon()`，当前没有额外要求恰好四点，因此三个有效点后按完成键也可能返回结果。这是四边形交互的实现缺口；需要四点的调用方必须复核 `Points.Count`。

- 位置来自 `e.GetPosition(DrawCanvas)`，是 WPF 画布坐标；此类没有统一进行原始像素换算或边界裁剪。非 96 DPI、图像变换或裁剪后不能直接把坐标当源像素 ROI，需按调用链明确转换，见 [ROI 路由](../algorithms/primitives/roi.md)。
- 矩形/圆拖拽无效时清除本次临时形状并继续等待；无效多边形保留选点继续等待。这些情况不立即返回 `null`。
- 绑定源失效时清理并返回 `null`。正常启动后完成/取消会解绑事件、删除临时 visual、释放鼠标捕获并恢复记录的 cursor、ActivateOn 和编辑模式。初始源为空、尺寸无效或文档已释放时，`Start` 在激活前直接返回 `null`，不更改这些交互状态，也不释放其它操作的鼠标捕获。
- 绑定 `ImageProcessingContext` 时捕获不可变 `ImageSelectionScope`：文档身份、source revision 和 WPF 逻辑画布宽高 `CanvasWidth/CanvasHeight`。位图同时保存真实像素宽高和 DPI；非位图的 `HasPixels=false`、像素宽高为零、DPI 占位值为 96，逻辑尺寸保留小数，不能将它当作可计算的像素源。选取期间换图、源版本变化或文档释放使 session 取消；成功结果携带该 scope。单独构造无处理上下文的绘图 session 则不能提供同样的图像绑定保证。
- 已完成结果不会在日后换图时自动消失。调用方须保留 `SelectResult.SourceScope`，使用 `ImageAlgorithmInputFactory.Acquire(context, scope)` 等入口再次核验；不能丢掉 scope 后复用旧坐标。每次调用各建 session，不保证新调用自动取消旧调用，也没有统一单活动 session 调度器。

## 验证范围

`ImageDocumentPresentationTests` 覆盖文档/显示版本隔离、源替换后旧租约存活以及矢量文档不生成像素帧。`ImageStreamPresentationTests` 用注入处理器检查冻结源独立性、最新等待帧替换、换图/释放拒绝、提交通知中的重入选择以及失败原图回退，不运行真实 native 伪彩。算法 claim 和预览回滚还需结合算法平台及 `ImageAlgorithmPreviewSessionTests` 的契约验证。

`EditorToolFactoryLifecycleTests` 覆盖重复工具栏刷新时图标元素复用，不覆盖任意插件、重复后缀或所有构造失败。`ImageDisplayEffectsTests` 覆盖参数捕获的基准源、启用与存活门禁，以及不可变参数和无发布副作用。`ImageGroupNavigationTests` 覆盖去重、手动暂停跟随及导航事件顺序，不证明真实按钮/打开器交互。

`TransientRoiSelectionSessionTests` 覆盖退化/自交形状、白色矢量画布上四类形状完成、分数画布尺寸、位图像素尺寸与 DPI、版本变化/释放取消、临时 visual 清理和交互状态恢复；同时验证算法拒绝无像素选区或过期范围，正常位图仍可获取输入。部分通过反射驱动内部状态，不等于真实鼠标和任意 DPI 的整链验收。配置同名键、实际工具发现、四边形键盘完成和真实窗口行为仍需按改动补验证。

---
knowledge_id: "ui.image-editor"
knowledge_type: "topic"
status: "current"
summary: "图像/视频打开、绘图撤销、叠加层、3D 与快照输出边界，区分渲染图、当前源像素和重读源文件的模型导出。"
aliases: ["打开图像","看图","视频模式","标注","撤销标注","自由套索","闭合多边形","图形旋转","绘图连续模式","绘图锁定","紧凑属性条","CompactInspector","保存原图还是截图","图像叠加层为什么没有显示","像素数字显示","PixelValueOverlay","ColorVision.ImageEditor","ImageView","OpenImage","ImageSourceLoaded","ExternalRenderCompleted","TIFF","Gray32Float","ImageViewSnapshot","AlgorithmOverlayManager","3D高度图","3D模型查看器","ModelViewer3D","ModelViewer3DControl","ModelViewer3DModel","Window3D","HeightMapPixelSampler","ImageGroupNavigation","VideoPlaybackSession","ImageSnapshotCapture","ImageDrawingPresentation","ImageContextMenuComposer"]
code_paths: ["UI/ColorVision.ImageEditor/Cie","UI/ColorVision.ImageEditor/Zoombox.cs","UI/ColorVision.ImageEditor/EditorTools/FullScreen","UI/ColorVision.ImageEditor/ImageView.xaml","UI/ColorVision.ImageEditor/ImageView.xaml.cs","UI/ColorVision.ImageEditor/ImageViewLifecycleEventArgs.cs","UI/ColorVision.ImageEditor/ImageView.Snapshot.cs","UI/ColorVision.ImageEditor/EditorContext.cs","UI/ColorVision.ImageEditor/EditorToolFactory.cs","UI/ColorVision.ImageEditor/CompactInspector.cs","UI/ColorVision.ImageEditor/DrawCanvas.cs","UI/ColorVision.ImageEditor/Draw/SelectEditorVisual.cs","UI/ColorVision.ImageEditor/Draw/RegionProperties.cs","UI/ColorVision.ImageEditor/Draw/Polygon","UI/ColorVision.ImageEditor/Draw/Circle/CircleManager.cs","UI/ColorVision.ImageEditor/Draw/Rectangle/RectangleManager.cs","UI/ColorVision.ImageEditor/Draw/Annotations/AnnotationMapper.cs","UI/ColorVision.ImageEditor/Tif","UI/ColorVision.ImageEditor/Video/VideoOpen.cs","UI/ColorVision.ImageEditor/Algorithms/AlgorithmOverlayManager.cs","UI/ColorVision.ImageEditor/Algorithms/AlgorithmOverlayRenderer.cs","UI/ColorVision.ImageEditor/ColorVision.ImageEditor.csproj","Engine/ColorVision.Engine/Media/CVRawOpen.cs","UI/ColorVision.ImageEditor/README.md","UI/ColorVision.ImageEditor/EditorTools/ThreeD","UI/ColorVision.ImageEditor/PixelValueOverlay.cs","UI/ColorVision.ImageEditor/Settings/DefaultImageViewDisplayConfig.cs","UI/ColorVision.ImageEditor/Settings/ImageViewSettingsWindow.xaml.cs","UI/ColorVision.ImageEditor/Settings/ImageViewSettingsEntry.cs","Engine/ColorVision.Engine/Media/CvcieDisplaySettingProvider.cs","UI/ColorVision.ImageEditor/ImageEditorSession.cs","UI/ColorVision.ImageEditor/Documents","UI/ColorVision.ImageEditor/Presentation","UI/ColorVision.ImageEditor/Output","UI/ColorVision.ImageEditor/Navigation","UI/ColorVision.ImageEditor/Draw/ImageDrawingPresentation.cs","UI/ColorVision.ImageEditor/Tooling/ImageContextMenuComposer.cs","UI/ColorVision.ImageEditor/Video/VideoPlaybackSession.cs","UI/ColorVision.ImageEditor/Video/VideoPlaybackBackend.cs","UI/ColorVision.ImageEditor/Video/VideoAudioTrack.cs"]
test_paths: ["Test/ColorVision.UI.Tests/CieWindowCompositionTests.cs","Test/ColorVision.UI.Tests/WindowFullScreenTests.cs","Test/ColorVision.UI.Tests/ImageOpenCompletionContractTests.cs","Test/ColorVision.UI.Tests/CvcieDisplaySettingsTests.cs","Test/ColorVision.UI.Tests/AlgorithmOverlayManagerTests.cs","Test/ColorVision.UI.Tests/ImageViewSnapshotSaveTests.cs","Test/ColorVision.UI.Tests/ImageViewContextMenuTests.cs","Test/ColorVision.UI.Tests/EraseManagerUndoTests.cs","Test/ColorVision.UI.Tests/DrawShapeCompatibilityTests.cs","Test/ColorVision.UI.Tests/EditorToolFactoryLifecycleTests.cs","Test/ColorVision.UI.Tests/VideoLifecycleTests.cs","Test/ColorVision.UI.Tests/HeightMapPixelSamplerTests.cs","Test/ColorVision.UI.Tests/ModelViewer3DStateTests.cs","Test/ColorVision.UI.Tests/ModelViewer3DModelTests.cs","Test/ColorVision.UI.Tests/HeightMapDxGeometryTests.cs","Test/ColorVision.UI.Tests/HeightMapOrbitCameraTests.cs","Test/ColorVision.UI.Tests/HeightMapModelExporterTests.cs","Test/HeightMap.Validation/Program.cs","Test/ColorVision.UI.Tests/ImageGroupNavigationTests.cs","Test/ColorVision.UI.Tests/VideoPlaybackSessionTests.cs","Test/ColorVision.UI.Tests/DrawCanvasTests.cs"]
related: ["ui.discovery","ui.image-editor-context","ui.property-grid","engine.results","algorithms.platform","algorithms.local-native-analysis","operations.first-run","ui.publishing"]
---

# ColorVision.ImageEditor：打开、绘制与输出

`ImageView` 承载 WPF 画布、导航按钮和交互入口，`EditorContext` 向扩展提供配置、绘图与处理上下文。源数据由 `ImageDocument` 持有，`ImageEditorSession` 协调文档变更，显示统一经 `ImagePresentation` 发布；算法协调、显示能力、输出和工具栏装配有独立拥有者，详见[上下文与状态归属](./image-editor-context.md)。创建控件会装配工具、菜单和服务，不是无副作用的轻量图片框；运行主程序仍先遵守[启动前提](../../00-getting-started/first-steps.md)。

客户 OK/NG、MES 字段及业务导出不属于此模块；历史结果与中立算法的分界见[结果展示链](../engine-components/result-handoff-chain.md)。

## 图像全屏与恢复

图像顶部工具栏的全屏按钮只展示当前图像及其工具栏。进入后按新视口等比例适配图像；再次点击该按钮、按 F11 / Esc，或点击顶部退出按钮，恢复原文档位置和进入前的缩放、平移，并同步更新倍率显示与绘图缩放。鼠标移到屏幕顶边时显示退出按钮，进入时短暂显示按键提示。

`ImageFullScreenMode` 暂时将图像内容移到宿主窗口，退出时恢复原父容器及子项顺序。窗口边框、显示器边界和原窗口状态由 Common 的 `WindowFullScreenSession` 负责；紧凑标题栏在改变窗口样式之前暂停，不能让标题拖动区截获图像顶部工具栏的鼠标命中。窗口尺寸改变和图像缩放是两个独立状态。

图像预览有键盘焦点或鼠标位于预览内时，F11 与图像工具栏全屏按钮效果相同，适用于主窗口中的图像和独立 ImageView 宿主。未由图像处理的 F11 才在冒泡阶段进入[主窗口全屏](../../01-user-guide/interface/main-window.md#全屏与最大化)。若先进入主窗口全屏，再通过图像按钮进入图像全屏，第一次 F11 / Esc 只返回全屏工作区，第二次才恢复普通窗口。已处理的按键、带修饰键的 F11 和长按重复事件不再次切换。

`WindowFullScreenTests` 覆盖窗口及紧凑 chrome 往返、显示器边界、顶部原生鼠标命中、父容器顺序、嵌套退出和启动位置保护；真实图像工具栏与缩放仍需结合 WPF 预览检查。

## CIE 色度图与手动色域计算

图像右侧的 CIE 入口打开紧凑的独立窗口，继续接收图像取样点。默认窗口为 860 × 680 DIP，色度图占主体；模式切换与坐标系、缩放、适应和显示选项浮在图上。画布留白与图表底色统一为白色，不以黑色边带填补宽高比差异，也不拉伸坐标轴比例。

“色度图”支持 CIE 1931 xy、CIE 1960 uv 和 CIE 1976 u′v′。默认显示 sRGB 与 D65，其余色域、白点、色温线和日光轨迹可从“显示选项”展开；展开时图表避让面板。当前点的 xy 常驻左下角，展开可看 uv、u′v′、CCT 和 Duv；图上游标读数显示在右下角。显示选项只控制参考叠加，不改变图像测量结果。

“色域计算”保留 RGB xy 输入、多标准选择、自动计算和原 CSV 导出。右侧参数可收起，底部结果可折叠，结果表按内容增高并在上限内滚动；表格优先显示标准、面积比、样品面积和标准面积，RGB 坐标保留在输入区与完整导出中。这里计算的是 xy 平面三角形面积比，不是相交覆盖率，也不是三维色域体积。

`CieDiagramView` 在可见且完成布局后合并适配请求，打开、坐标系切换和适应模式下的视口改变会保持等比例居中。滚轮、平移或缩放按钮进入手动视图状态；修改计算输入和窗口布局不覆盖手动视图，点击“适应”恢复自动适配。未加载和不可见的页不持续排队缩放，卸载会取消待执行请求。计算页和色度图页分别保留自己的叠加与输入状态。

入口与实现位于 `UI/ColorVision.ImageEditor/Cie/`。`CieWindowCompositionTests` 检查窗口组合、坐标系/视口适配、面板避让、计算与视图状态；最终文字、配色和面板展开效果仍需用真实 WPF 窗口确认。

## 打开图像与完成信号

`ImageView.OpenImageCore` 按扩展名查询 `IEditorToolFactory.IImageOpens`，不是只凭文件后缀就能保证解码成功：

| 输入 | 打开器与限制 |
| --- | --- |
| BMP、JPG/JPEG、PNG、WEBP、ICO、GIF | `Tif/CommonImageOpen.cs` 声明这些扩展名，使用 WPF 解码；具体编码、像素格式及本机解码能力仍需满足，GIF 被识别不代表提供动画播放 |
| TIF/TIFF | `Tif/Opentif.cs`；默认 `ConvertGray32FloatToGray16OnOpen=true`，Gray32Float 测量图可被映射为 Gray16 显示代理，不能把显示值或随后导出的底图当成原始浮点样本 |
| CVRAW/CVCIE | 由已加载的 `Engine/ColorVision.Engine/Media/CVRawOpen.cs` 扩展注册，不是 ImageEditor 自带的普通位图解码器 |
| 视频文件 | 走 `Video/VideoOpen.cs`，与静态图像的打开、播放和释放语义不同，见下文 |

`SetImageSource` 对 `WriteableBitmap` 接受 Bgr32、Bgra32、Pbgra32、Bgr24、Rgb24、Indexed8、Rgb48、Gray8、Gray16、Gray32Float；其他格式会报不支持。不要将“打开器识别扩展名”扩大成“该格式的任意位深、通道或编码均支持”。

- 普通同路径打开会跳过重载；“还原原图”在源文件仍存在且有打开器时强制重走文件打开流程，不是撤销栈中的一次图元操作，也不是恢复任意历史像素。
- 普通图片和 TIFF 打开器异步解码，以请求编号和当前文件路径拒绝过期结果。只有未冻结且尺寸、格式兼容的旧位图可以原位复用；从视频或实时冻结帧切入时创建新的可写位图，保留旧帧像素。调用 `OpenImage` 返回不等于像素已经就绪；失败也不保证各打开器都有同样的弹窗或日志，TIFF 解码异常目前可直接返回。
- `ImageSourceLoaded` 表示当前像素源已载入或更新；`ExternalRenderCompleted` 只在外部渲染者显式通知时发出。该事件也可携带 `Succeeded=false` 或空 `Source`，发生事件不等于渲染成功。需要导出结果叠图时，核对成功标志、当前任务 `Context` 与 `ImageRevision`，再在视图 Dispatcher 捕获；不能仅凭像素加载或事件名称判断标注已完整。快照 API 自身不会等待或校验外部渲染状态，事件字段见 `ImageViewLifecycleEventArgs.cs`。

CVCIE 的全局默认显示在“图像设置 → 文件打开 → CVCIE”中配置，由 Engine 的 `CvcieDisplayConfig` 持久化，`CvcieDisplaySettingProvider` 注册到设置窗口，加载 Engine 后无需先打开 CVCIE 即可设置；开启“启用真彩显示”后新打开的 CVCIE 默认采用 XYZ 真彩 sRGB，关闭后默认原图；亮度可选择自动适配或固定参考白。图层下拉框允许临时切换当前图片，不改全局开关。XYZ 转换与原图/Y 灰度回退也由 Engine 提供，异常只记日志，详见 [CV 文件的显示与校正边界](../engine-components/ColorVision.FileIO.md)。

`SetLayerController` 替换或清空控制器时会 Dispose 实现 `IDisposable` 的旧控制器，同一实例重设选择不释放。Engine 的 CVCIE 控制器借此取消后台切换并释放显示缓存；选择返回不表示新图层已显示，消费方仍以 `ImageSourceLoaded` 为完成信号。

### 图像组导航

`OpenImageGroup`、`AppendImageToGroup` 和 `SelectImageInGroup` 通过 `ImageGroupNavigation` 管理项目与索引，`ImageView` 只适配 Dispatcher、按钮状态和实际打开回调。组内路径按大小写不敏感去重，保留首次出现的项目元数据，忽略空白路径；初始索引按规范化后的集合截取到有效范围。空组输入执行完整 `Clear`。

自动跟随默认开启；手动选择旧图后暂停跟随，选到末项恢复。关闭自动跟随后追加新项目不自动切图，仍可显式选择；追加已存在路径不会创建重复项，`open=true` 仍可选择该项。选择先更新导航状态和按钮，再打开图像，最后发出选中事件；该事件本身不替代打开器的图像加载完成信号。

## 设置扩展与模块边界

`ImageView.RegisterSettings` 保留原有 provider 入口；`RegisterSettingsProvider` 还返回可注销的句柄。条目声明稳定 ID、作用范围、提供方、当前绑定对象及可选保存/默认动作。窗口按页面 ID 组织显示，只有应用偏好和默认值的实际改动在保存/完成时持久化；当前视图调整、信息浏览和标定档案不通过普通关闭隐式保存。旧条目需要显式调用其保存按钮。范围、兼容和错误语义见[图像设置](../../02-developer-guide/core-concepts/image-editor-settings-plan.md)。ImageEditor 不反向依赖 Engine 的配置类型，FileIO 也不承担用户显示设置。

`CvcieDisplaySettingsTests` 覆盖无图设置可用、多个视图共享全局配置、文件打开页归属及保存委托。`ImageSettingsScopeTests` 覆盖当前状态隔离、显式默认值和保存生命周期；测试入口不代表真实设备或跨显示器 DPI 验收。

## 绘图、选择与撤销

缩放和平移定位图像区域；绘图工具向同一 `DrawCanvas` 添加矩形、圆、线、多边形、曲线或文本等对象。对象选不中时先确认当前绘图/选择状态和对象是否支持选择，再查命中测试与[属性编辑器](./property-grid.md)，不要先修改设备或算法配置。

`DrawCanvas` 保留 `Image` 的布局和 Stretch 行为，将底图绘制到独立 `DrawingVisual` 子层；编辑图元集合、命中测试和撤销栈不把底图当作注释。`ImageDrawingPresentation` 同步绘图列表、文字/消息显示配置、缩放布局和活动编辑提交，并在组件初始化后附着订阅、释放时解绑。图像与图元的承载分离没有改变现有 shader 的作用范围：`ImageShaderPresentation` 仍将 `SceneEffect` 附着到整个画布，兼容整场景显示效果。

圆形和矩形绘图工具在底部紧凑属性条中提供持续选项。连续模式关闭时显示 `1×`，开启时显示 `∞`；尺寸锁定关闭时显示开锁，开启时显示闭锁。图标与点击热区应大于普通状态文字，使缩放画布上的高频切换仍容易命中。开启后的持久选中态必须通过稳定的背景、边框或前景反馈与未选中态区分；鼠标按下只提供瞬时反馈，不能与持续选中态共用唯一的视觉差异。`ImageView.xaml` 定义紧凑控件样式与承载区域，`CompactInspector.cs` 创建属性元素，圆形和矩形管理器提供连续、锁定及尺寸状态。

椭圆可独立编辑 `Rx`、`Ry`，相等时为圆。椭圆、矩形和多边形通过属性条的 `θ°` 或选框上方的旋转手柄围绕中心旋转；拖动时按 Shift 以 15° 对齐，完成一次拖动登记一笔撤销。非零旋转时，外接选择框缩放保持图形比例，独立长宽仍可在属性中编辑，避免把旋转后的外接框误当成原始长宽。

多边形默认在 Enter 完成时闭合，也可点击起点附近完成；至少需要三个顶点。关闭工具的“闭合”选项可保留折线，对已有多边形可在属性或右键菜单切换“闭合区域”。自由套索按住鼠标拖动采集轮廓，松开时自动闭合，Esc 取消；它生成同一种多边形对象。注释往返保存 `IsClosed` 和 `Rotation`。这些封闭图形的 CVCIE 计算及传统节点边界见 [CVCIE POI](../engine-components/cvcie-results.md)。
画布右键由整个 `Zoombox` 承接，`ImageContextMenuComposer` 按当前绘图和扩展贡献构建菜单：命中支持上下文操作的图元时优先显示对象菜单；没有图像、绘图模式未命中图元或对象没有专用动作时回退到标准图像菜单，至少保留打开图像等入口，不显示空菜单壳。

`DrawCanvas` 的撤销/重做只覆盖登记到 `ActionCommand` 的操作；加入新命令会清空 redo，清空画布会清空两栈。不能把换图、任意像素处理或外部保存都视为可撤销。橡皮擦多对象删除是一笔撤销事务；临时框选图形不进入撤销历史。

注释由 `AnnotationMapper` 映射圆、矩形、文本、线、多边形和 Bézier 曲线。未知图元导出时可能跳过，不能承诺任意自定义图元完整往返；新增类型需补映射和坐标往返验证。注释 JSON 不等于像素图，也不等于客户结果记录。

## 保存前分清输出语义

主窗口可配置的“另存为”（默认 Ctrl+Shift+S）沿焦点调用 SaveAs。图像保留其渲染 PNG 输出语义；3D 查看器将同一命令接到截图入口，模型未就绪、加载或导出中不可用。

`ImageView.Snapshot.cs` 保留公开输出入口，捕获由 `Output/ImageSnapshotCapture` 负责，编码由 `Output/ImageSnapshotEncoder` 负责。`ImageViewSnapshot` 携带可独立消费的冻结源或缓冲租约及绘图快照；后台编码不再持有整个视图，也不改变文档源或当前显示。

| 操作或 API | 实际输出 |
| --- | --- |
| 界面“另存为” / `Save` / `CaptureSnapshot` | `CaptureSnapshot` 在 UI 线程提交活动绘图编辑并产生 Pbgra32 渲染位图，“另存为”和 `Save` 将其编码为 PNG；不保留原图的高位深 |
| `CaptureSnapshotForBackgroundSave` 后输出 rendered | 捕获当前基准位图和可支持的绘图，后台 STA 合成；可选 PNG/JPEG 和缩小比例，是渲染图 |
| `SaveSnapshotExportsAsync` 的 source 分支 | 跳过场景渲染，保存捕获的当前基准位图，支持 PNG/TIFF/BMP 选项；无缩放或有损质量选项，编码器和像素格式仍必须兼容 |
| 注释导入/导出 | `.cvanno.json` 等 JSON；导出会提交活动绘图编辑并写目标文件，导入在解析/转换成功后清除现有注释再加入新对象，不是追加或整图快照 |

后台捕获优先使用 `ViewBitmapSource`，只有它不是位图时才回退到 `ImageShow.Source`，因此当前屏幕上的 `FunctionImage` 不一定就是后台输出的底图。“source”是当前载入/处理后的基准位图，不是源文件字节副本；经过 TIFF 显示转换后也不会凭空恢复原始浮点数据。BMP 仅允许代码明确支持的格式，Rgb48 等不能无损保留时会拒绝，而不是静默降位深。

捕获必须在视图 Dispatcher 上完成。无有效图像或尺寸时可能返回 `null`；后台包含叠加层时，仅支持可复制的 `DrawingVisual`，叠加图元带 Effect、CacheMode 或为不支持的 Visual 时会拒绝快照。后台合成复制底图与图元绘制内容，不复制整个画布的 `SceneEffect`，不能假定与 UI 的 `CaptureSnapshot` 像素相同。成功捕获包含叠加层时会提交活动文本等编辑；`includeOverlays=false` 不提交这些草稿。冻结的 `BitmapSource` 可交给后台编码，`ImageViewSnapshot` 由保存 API 消耗并释放；放弃保存时调用者也必须释放。

保存会创建目录、写临时文件并替换同名目标，需确认写入范围和覆盖授权。rendered 与 source 必须使用不同路径；双输出先保存 rendered 再保存 source，并非整体事务：前者失败时后者不执行，后者失败时前者可能已写入。不要以调用已发起或某一文件存在宣告全部导出完成。

## 叠加层与算法入口

普通注释、Engine 历史结果图元和统一算法 overlay 不是同一种持久数据。统一算法由 `AlgorithmOverlayRenderer` 生成图元，`AlgorithmOverlayManager` 将图元与 artifact 一起绑定文档、source revision 和注册 token。transient 随会话释放或源像素提交清理；persistent 可以跨会话释放和源像素提交保留，但换图/清理仍会移除，名称中的 persistent 不代表已经保存到磁盘。完整替换、过期会话和历史 handler 契约以[结果展示链](../engine-components/result-handoff-chain.md)为准。

统一算法菜单由当前 Runtime 能力和 provider 可用性决定；有 Descriptor 或源码不等于默认可执行。查询 Blob、轮廓、亚像素边缘、拟合、FFT、摩尔纹等能力时，先核对[统一算法平台](../../02-developer-guide/core-concepts/image-algorithm-platform-v1.md)的发布门禁，再读对应专题的输入约束与预览/提交/导出边界。[本地 Native 分析](../algorithms/local-native-analysis.md)等直接入口不自动受这套门禁控制。工具构造、刷新与临时 ROI 见[编辑器上下文](./image-editor-context.md)。不能依据实现文件存在就构造一个产品菜单，也不能假设关闭算法窗口必然恢复原图。

## 视频模式

`VideoOpen` 声明 MP4、AVI、MKV、MOV、WMV、FLV、WEBM；原生打开失败会返回，后缀命中不是编解码保证。打开成功读取首帧，不自动播放。`VideoOpen` 负责文件元数据、播放控件和图像接入，`VideoPlaybackSession` 负责原生句柄、回调帧寿命、播放状态与独立音轨；`IVideoPlaybackBackend`、`IVideoAudioTrack` 提供有界验证接缝，不要求打开器持有 native 生命周期细节。

首帧复制并冻结后通过 `SetImageSource` 建立文档，其余帧交给共用 `ImageStreamPresentation`，沿[连续帧契约](./image-editor-context.md#连续帧显示)完成原图/伪彩显示。工具提供播放/暂停、跳转、0.25x 到 4x 的离散倍速选项、预览缩放和静音：

- 跳转在拖动完成或点击滑块后提交，不是逐帧拖动预览；停止/结束会暂停并 seek 到起点，但不保证暂停状态立即重读和显示首帧。
- 预览缩放调用原生视频 resize，等待后续帧应用，区别于画布 Zoom；UI 忙时会丢弃新帧，不能承诺高分辨率或任意倍速稳定满帧。
- 音频由独立 WPF `MediaPlayer` 播放，静音和同步修正有实现；不能承诺每个文件都有音轨、所有编码可播或严格音画同步。
- 自动隐藏只调整播放工具栏透明度，不折叠布局。换文件、`Clear`、打开器停用及 `Dispose` 触发 `VideoOpen.Close`，释放原生句柄、音频、定时器和事件，并仅重置属于该视频的流；单纯控件 `Unloaded` 不能替代释放。

播放器以 handle generation 拒绝关闭/重开后的回调，即使 native 重用了相同句柄值也不能接纳旧回调。跳转另推进 timeline 和流 `SessionId`，拒绝已排队的旧帧及状态；同一播放器后续回调仍可继续交付。UI 交付期间最多保留一个待交付 native 帧，繁忙时新帧立即释放；交付给打开器的是同步回调期借用，关闭重入时也要等借用结束才释放。该解码队列与显示处理器的一在途/一最新等待帧是两个不同边界。

## 3D：亮度高度图与模型场景

### 图像亮度高度图

“图像 → 3D 视图”把当前 `DrawCanvas.Source` 的 `WriteableBitmap` 交给 `Window3D`。高度定义保持为 `Z = byte灰度 / 255 × 显示高度`，默认显示高度为 100。它表达当前显示图像的亮度，不是物理深度、原始 RGB48/Gray16/Gray32Float 测量值，也不从亮度重建真实物体。切换源图层或改变上游显示映射可能改变输入亮度。

`HeightMapPixelSampler` 保留端点对齐的双线性采样：先对每个邻点按 `0.114B + 0.587G + 0.299R` 四舍五入为 byte，再插值并舍入。Gray8/Bgr24/Bgr32/Bgra32 可直接读取，其他格式沿用 WPF 到 Bgra32 的显示转换；灰度和 straight alpha 独立插值。透明阈值仍为 `alpha > 127`，一个格子的四角均有效才生成两三角；交互粗网格还检查细网格内部的透明点，避免跨洞连接。浮点 NaN/Infinity 沿用 WPF 的显示转换，并未新增物理测量有效性判断。采样使用有界行/条带缓冲，不额外复制整张 BGRA 大图；源属于 UI Dispatcher，数值网格和上传集合的构建在后台进行。

`HeightMapDxRenderer` 复用项目现有 HelixToolkit.Wpf.SharpDX / DirectX 11 依赖。静止与交互网格的顶点、索引常驻缓存，旋转/缩放只更新相机，高度只更新 Z 变换；伪彩色切换只替换 256 项 LUT 纹理。`HeightMapDxGeometry` 的网格射线遍历用于悬停命中，避免对所有三角形逐个测试。24 个伪彩色资源名称保持可用；光照影响曲面明暗，图例始终表示未加光照的 0–255 LUT。

| 设置/操作 | 行为 |
| --- | --- |
| `DetailResolution` | 静止网格最长边，默认 1536，可设 128–2048；按图像比例采样且不放大超过源尺寸（单像素维度复制成两点供网格使用） |
| `TargetPixelsX` / `TargetPixelsY` | 保留旧配置键，作为缓存交互网格的 X/Y 上限；默认 512，设置界面允许 128–1024 |
| `AdaptiveDetail` | 默认开启，拖动/相机平滑期间显示较低细节，停止约 160 ms 后恢复；关闭后始终显示细网格。较低性能显卡可先用静止 768 / 交互 256 |
| 显示高度 | 仅调整显示比例；渲染质量设置不改变 XY 尺度（以原图适配 512×512 的默认平面为基准）。不静默平滑灰度、不抹掉峰值；有限采样仍可能遗漏原图细节 |
| 左键 / 右键或中键 / 滚轮 | 围绕稳定目标旋转 / 在相机平面平移 / 缩放；Shift+左键也可平移。`HeightMapOrbitCamera` 按经过的时间平滑趋向目标 |
| Home、重置、双击 | 在完整网格就绪后按高度包围盒八角与真实视图区比例重新取景，并恢复默认方向；俯视提供正面查看。初始取景和重置使用同一逻辑 |
| 悬停读数 | 底栏显示源图映射坐标、最近细网格采样灰度、该灰度对应 Z，以及插值曲面交点 Z；采样值不能冒充源像素的原始测量读数 |
| 截图 / 导出 | 截图用 DirectX 视口读取 PNG/JPEG/BMP；模型导出由 `HeightMapModelExporter` 在后台流式输出完整细节和当前高度的 OBJ（MTL/256 项 PNG）或二进制 STL；OBJ 附带伪彩材质和纹理并保留隐藏纹理文件设置，STL 只含几何，二者均不包含相机方向。导出开始后固定网格/LUT/高度快照，后续视角或采样变化不会混入文件 |

侧栏、视图区、图例和读数底栏各占布局区域，控制内容可滚动。首次取景等网格准备完成后才执行，不在只有坐标轴时调用加载自动缩放。保持完整取景状态时，窗口比例和显示高度改变会重新适配；用户手动旋转/平移/缩放后保留其观察位置，Home 可再次完整取景。关闭会取消未完成构建并释放视口/GPU资源，不强制对整个进程执行 GC。

### OBJ/STL 模型场景

- `ModelViewer3DControl` / `ModelViewer3DModel` 是 SharpDX/Assimp 的 OBJ/STL 模型查看链，支持场景树、可见性和隔离状态。线框由 `MeshNode.RenderWireframe` 控制，不能照旧说明把它写成 `FindEdges` 加边圆柱，或把高度曲面的 WPF 工具链直接套过来。
- 界面 `ExportModel_Click` 调用 `ModelViewer3DLoader.ExportAsync(model.FilePath, ...)`，导出时重新由 `Importer` 读取源文件，再交给 `Exporter`，不是序列化当前显示场景。因此隐藏/隔离、线框与窗口变换不构成模型导出内容；源文件后续变化也可能影响输出。`ModelViewer3DModel.ExportToFile()` 则是另一条对已有场景操作的 API，不能因其存在就推断界面使用了它。
- 模型导出会写入用户选择的目标；格式支持、材质/纹理、配套文件和输出保真须按实际导出器及样本核验，不能笼统承诺 OBJ/STL 都完整保留材质和纹理。导出接口返回成功不替代重新导入检查。

相关确定性测试为 `HeightMapPixelSamplerTests`、`HeightMapDxGeometryTests`、`HeightMapOrbitCameraTests`、`HeightMapModelExporterTests`，分别覆盖显示采样契约、网格/透明洞/射线命中、取景和按时间平滑，以及 OBJ/STL 流式导出、取消和 Assimp 重导入。`Test/HeightMap.Validation` 可在独立配置中运行旧/新窗口并记录真实图像的采样、构网格、进程 CPU 和 CompositionTarget 回调间隔；回调间隔不是 GPU 帧时间或呈现延迟，不能据此宣称渲染倍率。`ModelViewer3DStateTests` 和 `ModelViewer3DModelTests` 继续覆盖模型可见性/加载状态及重读源文件导出。这些检查不覆盖所有格式、显卡驱动或低配机器。

## 入口缺失与失败定位

| 现象 | 先查的代码边界 |
| --- | --- |
| 图像区空白或仍像旧图 | 实际打开器、文件/编码、最终 `ImageSourceLoaded` 与当前路径；不要把旧显示当成本次打开成功，也不要一概先重装 native DLL |
| 工具栏缺项或工具重复 | `EditorToolFactory.cs` 的发现集合、上下文构造、可见性和 opener 的 `GuidId` 覆盖；算法入口另查 Runtime 门禁；通用规则见[UI 发现链](./ui-runtime-handoff.md) |
| 标注或结果偏移、换图后残留 | 图像坐标空间、裁剪/旋转、画布缩放；再按注释、历史 handler 或统一 overlay 分流，避免混用清理机制 |
| 保存缺标注、位深变化或只生成一个文件 | 捕获是否成功、使用哪种输出分支、外部渲染是否完成、源像素格式和双输出异常 |
| 伪彩/滤镜、CIE 或 3D 显示异常 | 当前输入类型及工具配置，再查 shader、colormap、CIE 数据/图片资源或 3D 依赖；视觉效果不构成测量正确性证明 |
| 放大后没有像素数字 | `PixelValueOverlay.TryGetRenderState` 要求有效且支持的位图格式、画布采用 `NearestNeighbor`、单像素显示宽高达到 `PixelValueOverlayMinPixelCellSize`、可见区域非空且像素数不超过 `PixelValueOverlayMaxVisiblePixelCount`；仅放大不保证满足全部条件，阈值归 `DefaultImageViewDisplayConfig` |
| 关闭视频后仍占资源 | `Config.Cleared → VideoOpen.Close`、打开器停用与 `VideoPlaybackSession.Close` 是否执行；核对 handle generation、timeline 和帧借用释放，不用反复启动播放器代替定位 |

发布资源与依赖以 `ColorVision.ImageEditor.csproj` 和[UI 模块交付](./publishing.md)为准；本页不另列一份发布流程，也不把 DLL 存在当作工具或 native 功能通过。

## 验证边界

`ImageEditorRealSampleTests` 是显式启用的本地 CVRAW/CVCIE 验证：通过 `COLORVISION_IMAGE_EDITOR_SAMPLE_FILES` 指定分号分隔的文件，输出目录可由 `COLORVISION_IMAGE_EDITOR_SAMPLE_OUTPUT` 指定。它检查真实打开器、图层切换、旧源租约、TIFF 解码像素以及输入文件前后 SHA256；未指定样本时明确跳过。普通位图的通道选择只改变显示；`CVRawOpen` 的文件图层控制器会重载基准图，测试按实际打开器的版本契约验证。

```powershell
$env:COLORVISION_IMAGE_EDITOR_SAMPLE_FILES = 'C:\samples\image.cvraw;C:\samples\measurement.cvcie'
$env:COLORVISION_IMAGE_EDITOR_SAMPLE_OUTPUT = Join-Path $env:TEMP 'ColorVision-ImageEditor-Samples'
dotnet test .\Test\ColorVision.UI.Tests\ColorVision.UI.Tests.csproj -p:Platform=x64 --filter FullyQualifiedName~ImageEditorRealSampleTests
```

元数据中的测试分别涉及图像完成/过期请求、overlay 生命周期、快照与源像素输出、擦除撤销、注释类型兼容、工具栏重复装配及上述 3D 子契约。`ImageGroupNavigationTests` 检查纯状态去重、跟随和事件顺序；`VideoPlaybackSessionTests` 以替代 backend/audio 检查首帧借用、播放控制、重开/跳转过期回调、UI 丢帧及关闭重入释放，`VideoLifecycleTests` 检查打开器清理。它们不证明所有真实视频编码和音画同步。

测试文件存在不是已经运行：新增自定义图元往返、未知输入格式、真实视频编码/音频、全部绘图工具、CIE、3D 及 native 资源仍须按改动在获授权环境验证。最小检查使用非敏感本地样本；另存、导入替换和真实运行须分别确认副作用，不连接设备来验证纯图像交互。

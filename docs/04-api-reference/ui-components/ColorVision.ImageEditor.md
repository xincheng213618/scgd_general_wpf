---
knowledge_id: "ui.image-editor"
knowledge_type: "topic"
status: "current"
summary: "图像/视频打开、绘图撤销、叠加层、3D 与快照输出边界，区分渲染图、当前源像素和重读源文件的模型导出。"
aliases: ["打开图像","看图","视频模式","标注","撤销标注","保存原图还是截图","图像叠加层为什么没有显示","像素数字显示","PixelValueOverlay","ColorVision.ImageEditor","ImageView","OpenImage","ImageSourceLoaded","ExternalRenderCompleted","TIFF","Gray32Float","ImageViewSnapshot","AlgorithmOverlayManager","3D高度图","3D模型查看器","ModelViewer3D","ModelViewer3DControl","ModelViewer3DModel","Window3D","HeightMapPixelSampler"]
code_paths: ["UI/ColorVision.ImageEditor/ImageView.xaml.cs","UI/ColorVision.ImageEditor/ImageViewLifecycleEventArgs.cs","UI/ColorVision.ImageEditor/ImageView.Snapshot.cs","UI/ColorVision.ImageEditor/EditorContext.cs","UI/ColorVision.ImageEditor/EditorToolFactory.cs","UI/ColorVision.ImageEditor/DrawCanvas.cs","UI/ColorVision.ImageEditor/Draw/Annotations/AnnotationMapper.cs","UI/ColorVision.ImageEditor/Tif","UI/ColorVision.ImageEditor/Video/VideoOpen.cs","UI/ColorVision.ImageEditor/Algorithms/AlgorithmOverlayManager.cs","UI/ColorVision.ImageEditor/Algorithms/AlgorithmOverlayRenderer.cs","UI/ColorVision.ImageEditor/ColorVision.ImageEditor.csproj","Engine/ColorVision.Engine/Media/CVRawOpen.cs","UI/ColorVision.ImageEditor/README.md","UI/ColorVision.ImageEditor/EditorTools/ThreeD","UI/ColorVision.ImageEditor/PixelValueOverlay.cs","UI/ColorVision.ImageEditor/Settings/DefaultImageViewDisplayConfig.cs","UI/ColorVision.ImageEditor/Settings/ImageViewSettingsWindow.xaml.cs","UI/ColorVision.ImageEditor/Settings/ImageViewSettingsEntry.cs","Engine/ColorVision.Engine/Media/CvcieDisplaySettingProvider.cs"]
test_paths: ["Test/ColorVision.UI.Tests/ImageOpenCompletionContractTests.cs","Test/ColorVision.UI.Tests/CvcieDisplaySettingsTests.cs","Test/ColorVision.UI.Tests/AlgorithmOverlayManagerTests.cs","Test/ColorVision.UI.Tests/ImageViewSnapshotSaveTests.cs","Test/ColorVision.UI.Tests/EraseManagerUndoTests.cs","Test/ColorVision.UI.Tests/DrawShapeCompatibilityTests.cs","Test/ColorVision.UI.Tests/EditorToolFactoryLifecycleTests.cs","Test/ColorVision.UI.Tests/VideoLifecycleTests.cs","Test/ColorVision.UI.Tests/HeightMapPixelSamplerTests.cs","Test/ColorVision.UI.Tests/ModelViewer3DStateTests.cs","Test/ColorVision.UI.Tests/ModelViewer3DModelTests.cs"]
related: ["ui.discovery","ui.image-editor-context","ui.property-grid","engine.results","algorithms.platform","algorithms.local-native-analysis","operations.first-run","ui.publishing"]
---

# ColorVision.ImageEditor：打开、绘制与输出

`ImageView` 承载当前图像、缩放画布、工具和绘图对象；`EditorContext` 将这些状态及打开器、处理上下文交给扩展。创建控件会装配工具、菜单和服务，不是无副作用的轻量图片框；运行主程序仍先遵守[启动前提](../../00-getting-started/first-steps.md)。

客户 OK/NG、MES 字段及业务导出不属于此模块；历史结果与中立算法的分界见[结果展示链](../engine-components/result-handoff-chain.md)。

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
- 普通图片和 TIFF 打开器异步解码，以请求编号和当前文件路径拒绝过期结果。调用 `OpenImage` 返回不等于像素已经就绪；失败也不保证各打开器都有同样的弹窗或日志，TIFF 解码异常目前可直接返回。
- `ImageSourceLoaded` 表示当前像素源已载入或更新；`ExternalRenderCompleted` 只在外部渲染者显式通知时发出。该事件也可携带 `Succeeded=false` 或空 `Source`，发生事件不等于渲染成功。需要导出结果叠图时，核对成功标志、当前任务 `Context` 与 `ImageRevision`，再在视图 Dispatcher 捕获；不能仅凭像素加载或事件名称判断标注已完整。快照 API 自身不会等待或校验外部渲染状态，事件字段见 `ImageViewLifecycleEventArgs.cs`。

CVCIE 的全局默认显示在“图像设置 → 默认值 → CVCIE 显示”中配置，由 Engine 的 `CvcieDisplayConfig` 持久化，`CvcieDisplaySettingProvider` 注册到设置窗口，加载 Engine 后无需先打开 CVCIE 即可设置；开启“启用真彩显示”后新打开的 CVCIE 默认采用 XYZ 真彩 sRGB，关闭后默认原图；亮度可选择自动适配或固定参考白。图层下拉框允许临时切换当前图片，不改全局开关。XYZ 转换与原图/Y 灰度回退也由 Engine 提供，异常只记日志，详见 [CV 文件的显示与校正边界](../engine-components/ColorVision.FileIO.md)。

`SetLayerController` 替换或清空控制器时会 Dispose 实现 `IDisposable` 的旧控制器，同一实例重设选择不释放。Engine 的 CVCIE 控制器借此取消后台切换并释放显示缓存；选择返回不表示新图层已显示，消费方仍以 `ImageSourceLoaded` 为完成信号。

## 设置扩展与模块边界

`ImageView.RegisterSettings` 接受返回 `ImageViewSettingsEntry` 的 provider，条目携带分组、标题、配置对象及可选保存委托。`ImageViewSettingsWindow` 按分组名复用内置或已创建的设置页，将扩展设置追加到该页；同名分组不会再生成一个重复导航项。保存或关闭窗口时执行条目的保存委托。扩展模块应通过此入口提供专有配置，ImageEditor 不反向依赖 Engine 的配置类型，FileIO 也不承担用户显示设置。

`CvcieDisplaySettingsTests` 覆盖未打开 CVCIE 时的设置可用性、多个视图共享全局配置、合并默认值页和保存委托；测试入口不代表已经完成真实窗口交互验收。

## 绘图、选择与撤销

缩放和平移定位图像区域；绘图工具向同一 `DrawCanvas` 添加矩形、圆、线、多边形、曲线或文本等对象。对象选不中时先确认当前绘图/选择状态和对象是否支持选择，再查命中测试与[属性编辑器](./property-grid.md)，不要先修改设备或算法配置。

`DrawCanvas` 的撤销/重做只覆盖登记到 `ActionCommand` 的操作；加入新命令会清空 redo，清空画布会清空两栈。不能把换图、任意像素处理或外部保存都视为可撤销。橡皮擦多对象删除是一笔撤销事务；临时框选图形不进入撤销历史。

注释由 `AnnotationMapper` 映射圆、矩形、文本、线、多边形和 Bézier 曲线。未知图元导出时可能跳过，不能承诺任意自定义图元完整往返；新增类型需补映射和坐标往返验证。注释 JSON 不等于像素图，也不等于客户结果记录。

## 保存前分清输出语义

主窗口可配置的“另存为”（默认 Ctrl+Shift+S）沿焦点调用 SaveAs。图像保留其渲染 PNG 输出语义；3D 查看器新增同一命令接线到截图入口，模型未就绪、加载或导出中不可用。没有将它改成保存原图/模型源文件；独立查看器原有快捷键仍保留。

| 操作或 API | 实际输出 |
| --- | --- |
| 界面“另存为” / `Save` / `CaptureSnapshot` | `CaptureSnapshot` 在 UI 线程提交活动绘图编辑并产生 Pbgra32 渲染位图，“另存为”和 `Save` 将其编码为 PNG；不保留原图的高位深 |
| `CaptureSnapshotForBackgroundSave` 后输出 rendered | 捕获当前基准位图和可支持的绘图，后台 STA 合成；可选 PNG/JPEG 和缩小比例，是渲染图 |
| `SaveSnapshotExportsAsync` 的 source 分支 | 跳过场景渲染，保存捕获的当前基准位图，支持 PNG/TIFF/BMP 选项；无缩放或有损质量选项，编码器和像素格式仍必须兼容 |
| 注释导入/导出 | `.cvanno.json` 等 JSON；导出会提交活动绘图编辑并写目标文件，导入在解析/转换成功后清除现有注释再加入新对象，不是追加或整图快照 |

后台捕获优先使用 `ViewBitmapSource`，只有它不是位图时才回退到 `ImageShow.Source`，因此当前屏幕上的 `FunctionImage` 不一定就是后台输出的底图。“source”是当前载入/处理后的基准位图，不是源文件字节副本；经过 TIFF 显示转换后也不会凭空恢复原始浮点数据。BMP 仅允许代码明确支持的格式，Rgb48 等不能无损保留时会拒绝，而不是静默降位深。

捕获必须在视图 Dispatcher 上完成。无有效图像或尺寸时可能返回 `null`；后台包含叠加层时，仅支持可复制的 `DrawingVisual`，遇到 Effect、CacheMode 或不支持的 Visual 会拒绝快照。成功捕获包含叠加层时会提交活动文本等编辑；`includeOverlays=false` 不提交这些草稿。冻结的 `BitmapSource` 可交给后台编码，`ImageViewSnapshot` 由保存 API 消耗并释放；放弃保存时调用者也必须释放。

保存会创建目录、写临时文件并替换同名目标，需确认写入范围和覆盖授权。rendered 与 source 必须使用不同路径；双输出先保存 rendered 再保存 source，并非整体事务：前者失败时后者不执行，后者失败时前者可能已写入。不要以调用已发起或某一文件存在宣告全部导出完成。

## 叠加层与算法入口

普通注释、Engine 历史结果图元和统一算法 overlay 不是同一种持久数据。统一算法由 `AlgorithmOverlayRenderer` 生成图元，`AlgorithmOverlayManager` 将图元与 artifact 一起绑定文档、source revision 和注册 token。transient 随会话释放或源像素提交清理；persistent 可以跨会话释放和源像素提交保留，但换图/清理仍会移除，名称中的 persistent 不代表已经保存到磁盘。完整替换、过期会话和历史 handler 契约以[结果展示链](../engine-components/result-handoff-chain.md)为准。

统一算法菜单由当前 Runtime 能力和 provider 可用性决定；有 Descriptor 或源码不等于默认可执行。查询 Blob、轮廓、亚像素边缘、拟合、FFT、摩尔纹等能力时，先核对[统一算法平台](../../02-developer-guide/core-concepts/image-algorithm-platform-v1.md)的发布门禁，再读对应专题的输入约束与预览/提交/导出边界。[本地 Native 分析](../algorithms/local-native-analysis.md)等直接入口不自动受这套门禁控制。工具构造、刷新与临时 ROI 见[编辑器上下文](./image-editor-context.md)。不能依据实现文件存在就构造一个产品菜单，也不能假设关闭算法窗口必然恢复原图。

## 视频模式

`VideoOpen` 声明 MP4、AVI、MKV、MOV、WMV、FLV、WEBM；原生打开失败会返回，后缀命中不是编解码保证。打开成功读取首帧，不自动播放。工具提供播放/暂停、跳转、0.25x 到 4x 的离散倍速选项、预览缩放和静音：

- 跳转在拖动完成或点击滑块后提交，不是逐帧拖动预览；停止/结束会暂停并 seek 到起点，但不保证暂停状态立即重读和显示首帧。
- 预览缩放调用原生视频 resize，等待后续帧应用，区别于画布 Zoom；UI 忙时会丢弃新帧，不能承诺高分辨率或任意倍速稳定满帧。
- 音频由独立 WPF `MediaPlayer` 播放，静音和同步修正有实现；不能承诺每个文件都有音轨、所有编码可播或严格音画同步。
- 自动隐藏只调整播放工具栏透明度，不折叠布局。换文件、`Clear`、`Dispose` 通过配置清理触发 `CloseVideo`，释放原生句柄、音频、定时器和事件；单纯控件 `Unloaded` 不能替代释放。

## 3D：高度曲面与模型场景不是同一条链

- 图像高度曲面由 `EditorTools/ThreeD/Window3D.xaml.cs` 使用 WPF Helix 与 `Viewport3DHelper` 呈现。`HeightMapPixelSampler.Sample()` 先转 `Bgra32`，按目标尺寸双线性采样为 byte 灰度/alpha，再生成网格；可打开 RGB48 不等于高度值仍是原始高位深测量数据。代码列出 24 个 colormap 名称，资源加载失败的项会被跳过。高度缩放、伪彩、视角和截图是可视化操作，不构成物理高度校准。
- `ModelViewer3DControl` / `ModelViewer3DModel` 是 SharpDX/Assimp 的 OBJ/STL 模型查看链，支持场景树、可见性和隔离状态。线框由 `MeshNode.RenderWireframe` 控制，不能照旧说明把它写成 `FindEdges` 加边圆柱，或把高度曲面的 WPF 工具链直接套过来。
- 界面 `ExportModel_Click` 调用 `ModelViewer3DLoader.ExportAsync(model.FilePath, ...)`，导出时重新由 `Importer` 读取源文件，再交给 `Exporter`，不是序列化当前显示场景。因此隐藏/隔离、线框与窗口变换不构成模型导出内容；源文件后续变化也可能影响输出。`ModelViewer3DModel.ExportToFile()` 则是另一条对已有场景操作的 API，不能因其存在就推断界面使用了它。
- 模型导出会写入用户选择的目标；格式支持、材质/纹理、配套文件和输出保真须按实际导出器及样本核验，不能笼统承诺 OBJ/STL 都完整保留材质和纹理。导出接口返回成功不替代重新导入检查。

相关测试为 `HeightMapPixelSamplerTests`、`ModelViewer3DStateTests` 和 `ModelViewer3DModelTests`，分别涉及像素采样/网格、可见性/加载状态以及材质范围/重读源文件导出。它们不覆盖所有模型格式、驱动或真实窗口交互。

## 入口缺失与失败定位

| 现象 | 先查的代码边界 |
| --- | --- |
| 图像区空白或仍像旧图 | 实际打开器、文件/编码、最终 `ImageSourceLoaded` 与当前路径；不要把旧显示当成本次打开成功，也不要一概先重装 native DLL |
| 工具栏缺项或工具重复 | `EditorToolFactory.cs` 的发现集合、上下文构造、可见性和 opener 的 `GuidId` 覆盖；算法入口另查 Runtime 门禁；通用规则见[UI 发现链](./ui-runtime-handoff.md) |
| 标注或结果偏移、换图后残留 | 图像坐标空间、裁剪/旋转、画布缩放；再按注释、历史 handler 或统一 overlay 分流，避免混用清理机制 |
| 保存缺标注、位深变化或只生成一个文件 | 捕获是否成功、使用哪种输出分支、外部渲染是否完成、源像素格式和双输出异常 |
| 伪彩/滤镜、CIE 或 3D 显示异常 | 当前输入类型及工具配置，再查 shader、colormap、CIE 数据/图片资源或 3D 依赖；视觉效果不构成测量正确性证明 |
| 放大后没有像素数字 | `PixelValueOverlay.TryGetRenderState` 要求有效且支持的位图格式、画布采用 `NearestNeighbor`、单像素显示宽高达到 `PixelValueOverlayMinPixelCellSize`、可见区域非空且像素数不超过 `PixelValueOverlayMaxVisiblePixelCount`；仅放大不保证满足全部条件，阈值归 `DefaultImageViewDisplayConfig` |
| 关闭视频后仍占资源 | `Config.Cleared → CloseVideo` 是否执行、旧回调是否被会话句柄拒绝，不用反复启动播放器代替定位 |

发布资源与依赖以 `ColorVision.ImageEditor.csproj` 和[UI 模块交付](./publishing.md)为准；本页不另列一份发布流程，也不把 DLL 存在当作工具或 native 功能通过。

## 验证边界

元数据中的测试分别涉及图像完成/过期请求、overlay 生命周期、快照与源像素输出、擦除撤销、注释类型兼容、工具栏重复装配、视频清理及上述 3D 子契约。测试文件存在不是已经运行：新增自定义图元往返、未知输入格式、真实视频编码/音频、全部绘图工具、CIE、3D 及 native 资源仍须按改动在获授权环境验证。最小检查使用非敏感本地样本；另存、导入替换和真实运行须分别确认副作用，不连接设备来验证纯图像交互。

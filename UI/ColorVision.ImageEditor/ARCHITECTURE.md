# ColorVision.ImageEditor 架构梳理

这份文档只回答三个问题：

1. 这个模块的主控制链路是什么。
2. 已经实现了哪些能力，代码在哪一层。
3. 后续优化时应该优先盯哪些高耦合点。

如果只需要一个最短结论，可以先看“总览”和“优化切入点”两节。

## 1. 总览

`UI/ColorVision.ImageEditor` 不是单纯的图片控件，而是一套“图像宿主 + 可缩放画布 + 绘图/算法工具插件 + 打开器 + 设置系统”的组合模块。

运行时的核心对象关系是：

```text
ImageView
  -> EditorContext
      -> DrawEditorContext
          -> DrawCanvas
          -> Zoombox
          -> DrawEditorManager
      -> ImageProcessingContext
      -> ImageViewConfig
      -> IEditorToolFactory
```

可以把它理解为四层：

1. `ImageView` 是宿主层，负责初始化、载图、编辑态切换、右键菜单、状态栏、标注导入导出、工具栏区域和大部分编排逻辑。
2. `EditorContext` 是每个视图实例的运行时容器，保存当前 `ImageView` 关联的主要对象与少量服务。
3. `DrawEditorContext` 和 `ImageProcessingContext` 分别收拢绘图上下文、图像处理上下文。
4. `DrawCanvas` 和各类 `IEditorTool` / `IImageOpen` / `IDVContextMenu` 实现是具体能力层。

## 2. 生命周期和主链路

### 2.1 初始化链路

入口在 `ImageView.xaml` 和 `ImageView.xaml.cs`。

`ImageView.UserControl_Initialized` 会做这些事：

1. 创建 `EditorContext`、`DrawEditorContext` 和 `ImageProcessingContext`。
2. 初始化 `EditorContext` 的运行时 UI 状态，包括 `SelectionVisual`、选择属性面板宿主和编辑态标记；随后再创建 `Crosshair` 和 `IEditorToolFactory`。
3. 由 `IEditorToolFactory` 反射扫描并实例化：
   - `IEditorTool`
   - `IIEditorToolContextMenu`
   - `IDVContextMenu`
   - `IImageComponent`
   - `IImageOpen`
4. `ImageView` 遍历 `IImageComponents` 执行一次性初始化。
5. `ImageView` 继续接线 `Config`、`Zoombox`、`DrawCanvas`、`PixelValueOverlay`、标准命令和设置窗口提供者。

结论：这个模块的初始化不是显式注册式，而是“主控件创建后反射拉起全部扩展点”。后续要优化启动复杂度，首先要看 `ImageView.UserControl_Initialized`、`ImageView.CreateEditorContext` 和 `EditorToolFactory` 构造函数。

### 2.2 载图链路

默认载图入口是 `ImageView.OpenImage(string? filePath)`。

实际流程：

1. `ImageView.OpenImage` 清空 `Config.Properties` 和旧的图层切换事件。
2. 根据后缀从 `IEditorToolFactory.IImageOpens` 里找到对应 `IImageOpen`。
3. `ImageView` 先清掉上一个 opener 的 runtime tool overlay，再把当前 opener 写入 `EditorContext.IImageOpen`。
4. 打开器负责读取文件、提取元数据、必要时转换像素格式。
5. 打开器调用 `ImageView.SetImageSource(...)`。
6. 如果当前 opener 同时实现了 `IImageOpenEditorToolProvider`，`EditorToolFactory` 会按 `GuidId` 把它提供的工具覆盖到全局工具集之上，并重建受管工具栏。
7. `SetImageSource` 统一做：
   - 清理 `FunctionImage`、`ViewBitmapSource`、`HImageCache`
   - 重新写入像素格式、宽高、通道、位深、DPI 等元数据
   - 可选触发 `PseudoColorService.ConfigureForImage()`
   - 可选触发 `ImageCalibrationService.ApplyToDefault(EditorContext)`
   - 设置 `ImageShow.Source`
   - 通知状态栏刷新

这说明 `SetImageSource` 不是纯显示方法，而是图像上下文切换的核心入口。任何想做“只显示，不附带编辑副作用”的场景，都必须注意 `EnableEditorImageServices` 这个开关。

补充：`IImageOpenEditorToolProvider` 和 `IImageOpenEditorToolLifecycle` 让 opener 可以像右键菜单 overlay 一样，在当前文档上下文里临时贡献或替换工具栏工具；当前实现已经用于把 CVCIE 的 `CIE1931` 按钮从全局 if/else 分支改成 opener-scoped override。

### 2.3 编辑态链路

编辑态对外入口是 `ImageView.ImageEditMode`，实际运行时状态落到 `EditorContext.IsImageEditMode`。

打开编辑态后：

1. `Config.IsToolBarDrawVisible = true`
2. `Zoombox.ActivateOn = ModifierKeys.Control`
3. 鼠标光标切到十字
4. `DrawEditorManager` 负责维护当前激活的绘图工具

关闭编辑态后：

1. 绘图工具栏隐藏
2. `Zoombox` 恢复普通浏览模式
3. 当前绘图工具被清空

真正的绘图工具不是集中调度，而是每个工具在自己的 `IsChecked` 切换里给 `DrawCanvas` 挂/卸鼠标键盘事件。例如：

- `Draw/Rectangle/RectangleManager.cs`
- `Draw/Text/TextManager.cs`
- `Draw/BrushManager.cs`
- `Draw/Ruler/MeasureManager.cs`
- `Draw/Circle/CircleManager.cs`
- `Draw/Polygon/PolygonManager.cs`
- `Draw/Line/LineManager.cs`
- `Draw/BezierCurve/BezierCurveManager.cs`
- `Draw/EraseManager.cs`

结论：编辑逻辑是“多工具分散事件绑定”模型，不是中心状态机。

### 2.4 图元与选择链路

`DrawCanvas` 是真实的视觉树容器。

它负责：

1. `Image` 基础显示。
2. `Visual` 集合增删。
3. Undo/Redo 的 `ActionCommand` 栈。
4. 命中测试、区域命中、视觉树插拔。
5. 布局缩放时把比例应用到支持 `ILayoutScaleDrawingVisual` 的图元。

`EditorContext.DrawingVisualLists` 不是源数据，而是 `ImageView` 通过 `ImageShow.VisualsAdd/VisualsRemove` 维护的镜像集合，方便按 `IDrawingVisual` 维度做业务操作。

选择框由 `Draw/SelectEditorVisual.cs` 负责：

1. 选中图元时绘制控制框和八个调整点。
2. 处理拖拽缩放、移动、置顶、栅格化。
3. 发出选择变化，驱动底部 `CompactInspectorBar` 在“当前工具”和“当前单选图元”之间切换紧凑属性项；当前单选 `Line` / `Polygon` / `BezierCurve` 已经可以通过各自 `BaseProperties` 直接暴露颜色、线宽。

当前绘图工具骨架已经分成三类：

1. `Draw/DragDrawingToolBase.cs`：拖拽一次成形，当前接 `RectangleManager`、`CircleManager`。
2. `Draw/MultiPointDrawingToolBase.cs`：多点折线式成形，当前接 `LineManager`、`PolygonManager`、`BezierCurveManager`，并内建一套共享的 `CompactInspector` 默认样式配置（颜色、线宽）。
3. `Draw/RegionOperationToolBase.cs`：区域操作工具，当前接 `EraseManager`，不和成形类共用骨架。

### 2.5 临时选区链路

`TransientRoiSelectionSession.cs` 提供一套不入图元栈、不进撤销栈的临时选择模式。

它支持：

- Rectangle
- Circle
- Polygon
- Quadrilateral

用途是让外部流程临时取一个 ROI，而不污染正式标注列表。调用入口在 `ImageView.BeginSelectAsync(...)`。

## 3. 目录和职责对照

### 3.1 宿主与基础设施

| 位置 | 主要职责 |
| --- | --- |
| `ImageView.xaml` | 定义 UI 宿主骨架：`Zoombox`、`DrawCanvas`、像素值 overlay、各方向工具栏、底部 `CompactInspectorBar` |
| `ImageView.xaml.cs` | 初始化编排、编辑态入口、右键菜单、载图、清理、保存、状态栏、注释导入导出、通道切换、缩放刷新 |
| `EditorContext.cs` | 当前 `ImageView` 的运行时容器、轻量服务注册表、`CompactInspectorPresenter`、编辑态运行时状态 |
| `ImageViewConfig.cs` | 当前视图配置与属性字典，区分 `ImageMetadata` / `ViewState` / `OpenerRuntime` / `Legacy` |
| `DrawCanvas.cs` | 图像画布、视觉树、Undo/Redo、命中测试 |
| `EditorToolFactory.cs` | 反射发现工具/菜单/打开器/初始化组件，维护“全局工具 + 当前 opener runtime tools”的生效视图，并重建受管工具栏 |
| `EditorToolVisibilityConfig.cs` | 工具显示隐藏的持久化配置 |

### 3.2 抽象接口层

`Abstractions/` 是扩展点边界：

- `IEditorTool.cs`: 工具栏工具接口，定义按钮位置、顺序、图标、命令。
- `IImageEditor.cs`: `IImageComponent`、`IImageOpen`，以及 opener-scoped runtime tool overlay 的 `IImageOpenEditorToolProvider` / `IImageOpenEditorToolLifecycle`。
- `IEditorContextService.cs`: 当前主要承载 `IPseudoColorService` 这类挂在 `EditorContext` 上的运行时服务。
- `Abstractions/Draw/IDrawing.cs`: 图元、文本属性、选择图元等基础能力接口。

### 3.3 绘图子系统

`Draw/` 是图像标注与测量的主体，结构相对统一：

| 子目录 | 主要内容 |
| --- | --- |
| `Circle/` | 圆/椭圆图元、属性、工具、annotation 模块 |
| `Rectangle/` | 矩形图元、属性、工具、annotation 模块 |
| `Line/` | 直线/剖面线图元、属性、工具、annotation 模块 |
| `Polygon/` | 多边形图元、属性、工具、annotation 模块 |
| `BezierCurve/` | 贝塞尔曲线图元、属性、工具、annotation 模块 |
| `Text/` | 文字图元、默认文本样式、annotation 模块 |
| `BrushManager.cs` | 自由画笔/荧光笔标注工具与笔迹图元 |
| `Ruler/` | 标尺、物理尺寸、标定、比例尺工具 |
| `Special/` | 十字线、网格、参考线、放大镜等辅助视觉元素 |
| `Annotations/` | 标注导入导出 DTO 和 mapper |
| `Rasterized/` | 选区栅格化后的视觉对象 |

统一规律基本是：

1. `*Properties.cs` 保存业务属性。
2. `DV*` 是具体 `DrawingVisual`。
3. `*Manager.cs` 是挂在工具栏上的绘图工具。
4. `*AnnotationModule.cs` 负责导入导出。

当前例外是 `BrushManager.cs`：它已经提供自由画笔和荧光笔式标注，但暂时还没有独立的 annotation module 接到导入导出链路。

### 3.4 非绘图工具子系统

`EditorTools/` 放的是“不是图元本身，但作用于当前视图”的工具。

| 子目录 | 功能 |
| --- | --- |
| `AppCommand/` | 打开、另存为、关闭、标注导入导出 |
| `Zoom/` | 放大、缩小、适应、缩放比文本框、右键缩放菜单 |
| `FullScreen/` | 全屏窗口化查看 |
| `Rotate/` | 旋转相关操作与菜单 |
| `Histogram/` | 直方图窗口 |
| `GraphicEditing/` | 图形编辑弹窗，聚合更多图形编辑逻辑 |
| `PseudoColor/` | 伪彩色工具控件、运行态、默认值、渲染控制器 |
| `ThreeD/` | 2D 图像转 3D、OBJ/STL 模型查看、共享 3D 辅助类 |
| `Algorithms/` | 图像算法菜单与参数窗口 |

#### 伪彩色工具

`EditorTools/PseudoColor/` 是当前最完整的一套“当前值 + 默认值 + 运行时服务”模式样板：

- `PseudoColorEditorTool.cs`: 把工具控件挂到右侧工具栏，并把 `IPseudoColorService` 注册进 `EditorContext`。
- `PseudoColorController.cs`: 监听状态变化，执行范围计算、预览图刷新、异步渲染、恢复原图。
- `PseudoColorToolState.cs`: 当前视图运行态。
- `PseudoColorDefaultConfig.cs`: 全局默认值。
- `PseudoColorToolControl.xaml`: 实际 UI。

#### 算法工具

`EditorTools/Algorithms/` 主要通过右键菜单暴露能力。

当前实现包括：

- 自动色阶
- 反相
- 白平衡
- Gamma
- 亮度/对比度
- 阈值处理
- 高斯模糊
- 中值滤波
- 边缘检测
- 锐化
- 直方图均衡化
- 去摩尔纹
- 以及 `Calculate/` 下偏业务化的检测与测量逻辑

这部分大量依赖 `ColorVision.Core.OpenCVMediaHelper`，参数型算法通常配一个 XAML 窗口做实时预览。

#### 3D 工具

`EditorTools/ThreeD/` 其实包含两套能力：

1. `Window3D.xaml(.cs)`：把当前图像当高度图渲染成 3D 曲面。
2. `ModelViewer3DControl.xaml(.cs)` / `ModelViewer3DWindow.xaml(.cs)`：单独查看 OBJ/STL 模型。

### 3.5 打开器子系统

`IImageOpen` 的实现主要分三类：

| 文件 | 作用 |
| --- | --- |
| `Tif/CommonImageOpen.cs` | 常规图片格式：bmp/jpg/jpeg/png/webp/ico/gif |
| `Tif/Opentif.cs` | TIFF，处理高位深、Gray32Float、元数据和图层切换 |
| `Video/VideoOpen.cs` | 视频文件，附带播放工具栏、音频同步、拖动进度和降采样显示 |

打开器除了读文件，还负责给 `ImageViewConfig` 写文件元数据和业务元数据，所以它们本身也是图像上下文的一部分。

现在打开器还可以可选提供“当前文档专属工具栏工具”：

1. `IImageOpenEditorToolProvider` 返回当前 opener 想贡献的 `IEditorTool`。
2. `EditorToolFactory` 会按 `GuidId` 合并工具集，当前 opener 的工具优先，全局工具回退。
3. `IImageOpenEditorToolLifecycle` 可以在切换图像或关闭图像时收到 deactivated 回调，处理窗口关闭、事件解绑等清理动作。
4. 这层机制比把 opener-specific 分支继续堆在全局 `IEditorTool` 里更清晰，适合文档类型差异明显的工具，例如 CVCIE 专属的 `CIE1931` 行为。

### 3.6 设置系统

`Settings/` 不是简单配置页，而是一套 provider 模式：

1. `ImageViewSettingsWindow.xaml(.cs)` 负责把所有 provider 暴露的设置分组展示成 Tab。
2. `ImageViewSettingMetadata.cs` 定义设置项元数据和 provider 接口。
3. `ImageView` 初始化时先注册内建 provider。
4. `EditorToolFactory` 在实例化 `IEditorTool` 时，如果工具本身也实现了 `IImageViewSettingProvider`，会自动注册进去。

当前内建分组大致是：

- 显示
- 上下文
- 默认值
- 工作台
- 加载器
- 工具自己追加的分组，例如伪彩色

### 3.7 CIE 子系统

这部分分成两层：

1. `WindowCIE.xaml(.cs)` 是独立窗口宿主，控制 gamut、illuminant、参考线、预设切换。
2. `Cie/` 目录提供真正的色度图实现：
   - `CieDiagramView.xaml(.cs)`
   - `CieBackgroundRenderer.cs`
   - `CieColorConverter.cs`
   - `CieGamut.cs`
   - `CieIlluminants.cs`
   - 以及其它 profile / overlay / 光谱轨迹类型

当前 `CIE1931` 工具也已经分成两条路径：

1. 全局 `CieDiagramEditorTool` 只处理普通图像的 `MouseMoveColorHandler -> ChangeSelect(imageInfo)`。
2. `CVRawOpen` 在 CVCIE 场景下通过 opener runtime tool override 提供 `CvcieDiagramEditorTool`，复用同一个 `GuidId`，但内部改成 `ConvertXYZ` + `WindowCIE.ChangeSelect(dx, dy)` 的专用链路。

这样 CVCIE 和普通图像不再继续共享同一个工具里的 `IsCvcieImage()` 分支。

### 3.8 像素值叠层

`PixelValueOverlay.cs` 是一层独立 overlay，不走 `DrawingVisualBase` 标注链。

它只有在这些条件都满足时才显示：

1. 当前源图是受支持格式。
2. `BitmapScalingMode` 是 `NearestNeighbor`。
3. 当前缩放下单个像素映射到屏幕的尺寸足够大。
4. 当前可见像素总数没超过阈值。

它会把可见区域一次性 `CopyPixels` 后预建 `DrawingGroup`，`OnRender` 只负责 `DrawDrawing`。

## 4. 当前状态模型

后续做任何优化前，建议先记住这几个运行时对象代表的含义。

### 4.1 图像数据层

| 成员 | 含义 |
| --- | --- |
| `ImageShow.Source` | 当前实际显示在画布上的图像 |
| `ViewBitmapSource` | 当前“已确认”的图像基底，通常是原图或已应用处理后的结果 |
| `FunctionImage` | 临时处理结果或预览结果，例如算法窗口预览、伪彩色、通道抽取 |
| `HImageCache` | 从当前显示位图延迟构建的原生图像缓存，供 OpenCV/底层算法复用 |

可理解为：

- `ViewBitmapSource` 是稳定层。
- `FunctionImage` 是可丢弃的派生层。
- `ImageShow.Source` 是最终显示层。
- `HImageCache` 是底层算法缓存层。

### 4.2 图元层

| 成员 | 含义 |
| --- | --- |
| `DrawCanvas.visuals` | WPF 视觉树中的真实 visual 列表 |
| `EditorContext.DrawingVisualLists` | 通过事件同步出来的业务镜像列表 |
| `SelectEditorVisual` | 选择框和控制点 overlay，不等于业务图元本身 |

### 4.3 配置层

`ImageViewConfig.Properties` 现在已经明确分了四种作用域：

- `ImageMetadata`: 文件或像素内容决定的上下文
- `ViewState`: 当前视图临时状态
- `OpenerRuntime`: 某个打开器专属运行态
- `Legacy`: 尚未迁移的旧键

这对后续收口状态非常重要。新增状态尽量不要再直接丢进未分类字典。

## 5. 已实现功能与代码位置

下面按“能力”而不是按目录列一次，方便后续查入口。

| 功能 | 主要位置 |
| --- | --- |
| 普通图片打开 | `Tif/CommonImageOpen.cs` |
| TIFF / 高位深 / Gray32Float | `Tif/Opentif.cs`, `Tif/TifOpenConfig.cs` |
| 视频播放 | `Video/VideoOpen.cs` |
| 缩放、适应窗口、缩放比显示 | `Zoombox.cs`, `ZoomCommands.cs`, `EditorTools/Zoom/*` |
| 图层切换（Src/R/G/B） | `ImageView.ComboBoxLayersSelectionChanged`, `ImageView.ExtractChannel` |
| 伪彩色 | `EditorTools/PseudoColor/*` |
| 标注绘制 | `Draw/*/*Manager.cs`, `Draw/*/DV*.cs`, `Draw/BaseProperties.cs` |
| 图元选择与拖拽调整 | `Draw/SelectEditorVisual.cs` |
| 标尺与物理尺寸 | `Draw/Ruler/*` |
| 十字线、参考线、放大镜、网格 | `Draw/Special/*` |
| 标注导入导出 | `Draw/Annotations/*`, `ImageView.ExportAnnotations`, `ImageView.ImportAnnotations` |
| 栅格化选区 | `Draw/Rasterized/RasterizedSelectVisual.cs`, `SelectEditorVisual` 右键菜单 |
| 图像算法 | `EditorTools/Algorithms/*` |
| 直方图 | `EditorTools/Histogram/*` |
| 图形编辑弹窗 | `EditorTools/GraphicEditing/*` |
| 旋转 | `EditorTools/Rotate/*` |
| 全屏 | `EditorTools/FullScreen/*` |
| 3D 曲面显示 | `EditorTools/ThreeD/Window3D.xaml(.cs)` |
| 3D 模型查看 | `EditorTools/ThreeD/ModelViewer3D*` |
| CIE 色度图 | `WindowCIE.xaml(.cs)`, `Cie/*` |
| 当前图像上下文查看 | `Settings/ImageViewContextSettingsView*` |
| 工作台设置（工具可见性/打开器列表） | `Settings/ImageViewWorkspaceSettingsView*` |
| 像素值叠层 | `PixelValueOverlay.cs`, `Settings/DefaultImageViewDisplayConfig.cs` |
| 临时 ROI 选择 | `TransientRoiSelectionSession.cs`, `ImageView.BeginSelectAsync` |

## 6. 复杂度热点

下面这些点是当前复杂度真正高的地方，也是后续优化最值得先拆的地方。

### 6.1 `ImageView` 过重

`ImageView.xaml.cs` 同时承担了：

- 初始化编排
- 文件打开与保存
- 图像切换
- 注释导入导出
- 状态栏提供者
- 伪彩与通道切换协作
- overlay 刷新
- 清理与生命周期

这已经是一个典型的 God Object。后续拆分最优先的候选通常是：

1. 载图与图像状态管理
2. 标注导入导出
3. 状态栏与上下文展示
4. overlay/缩放联动

### 6.2 `EditorToolFactory` 每视图反射扫描

当前每创建一个 `ImageView`，都会重新扫描程序集、重新实例化工具和菜单。

问题：

- 启动成本高
- 责任混合了“发现扩展点”和“装配 UI”
- 难缓存、难测试

如果后面要拆，建议先把“类型发现缓存”和“实例装配”分开。

### 6.3 状态分散

当前至少有三类状态容器：

1. `EditorContext` 里的对象引用和轻量服务。
2. `ImageViewConfig` 里的当前视图配置和属性字典。
3. 各工具自己的 `Config` / `State` / `DefaultConfig`。

这不是错，但会让调用方很难判断一个状态该放哪里。后续优化时建议按下面的原则收口：

- UI 运行态放当前 `ImageView` / `EditorContext`
- 当前视图临时状态放 `ImageViewConfig.ViewState`
- 默认值放独立 `IConfig`
- 算法或工具的可复用运行时逻辑放服务对象

### 6.4 绘图工具没有统一调度器

现在每个 `*Manager` 都自己挂鼠标事件、自己实现按键规则、自己决定是否退出连续模式。

优点是写起来直接。
缺点是：

- 工具行为很难统一
- 手势冲突要逐个排查
- 生命周期很分散

如果后面要继续收敛绘图逻辑，可以考虑把公用交互模式抽成：

- 单击创建
- 拖拽创建
- 多点创建
- 选择后调整

### 6.5 `DrawCanvas` 的撤销系统仍是旧式 Action 栈

现在 Undo/Redo 是 `DrawCanvas` 内部的 `ActionCommand` 列表。它对简单的 visual 增删足够，但对更复杂的状态事务不够显式。

如果后续要做更稳定的批量编辑、跨图元操作或更强的一致性检查，这一层会是限制点。

### 6.6 `SetImageSource` 有副作用

调用 `SetImageSource` 不只是“换一张图”，还可能：

- 改变伪彩状态
- 改变校准状态
- 清缓存
- 改图层切换可见性
- 改状态栏上下文

这导致很多“只是想显示图”的调用方不敢随便走这条链。这个入口后续很适合再拆成：

- 纯显示
- 编辑器上下文重建

### 6.7 缩放联动与 overlay 刷新是隐式性能热点

当前至少这些能力会跟着缩放或布局变化触发：

- 图元线宽和字体重算
- `SelectEditorVisual` 重绘
- `PixelValueOverlay` 刷新

问题不一定是逻辑错误，而是优化时很容易在这里引入性能回退。

## 7. 建议的优化切入点

如果你接下来要继续优化，我建议优先顺序如下：

1. 先清掉过期文档、死代码、明显拼写和错文件名，保证读到的东西就是当前源码事实。
2. 进一步明确状态归属，避免新功能继续往 `Config.Properties` 或 `EditorContext` 里随手塞数据。
3. 把 `EditorToolFactory` 的“类型发现缓存”和“实例装配”边界理清，避免每个视图都全量反射。
4. 在不改变外部行为的前提下，逐步收口 `SetImageSource` 的副作用。
5. `ImageView.xaml.cs` 是否拆成 partial，要等上面几步稳定后单独评估。

## 8. 读代码建议

如果后面你要继续追一条具体功能，推荐的阅读顺序是：

1. 先看 `ImageView.xaml.cs` 有没有直接编排。
2. 再看 `EditorToolFactory.cs` 是否通过反射把实现挂进来了。
3. 如果是绘图能力，进入 `Draw/<模块>/`。
4. 如果是视图工具，进入 `EditorTools/<模块>/`。
5. 如果是载图行为，优先看 `IImageOpen` 实现。
6. 如果是设置项，先看 `Settings/ImageViewSettingMetadata.cs` 和对应 provider。

这样读，通常能避开“从 UI 一路跟到随机工具类里”的迷路状态。

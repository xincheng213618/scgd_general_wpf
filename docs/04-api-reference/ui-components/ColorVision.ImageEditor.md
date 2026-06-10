# ColorVision.ImageEditor

本页只描述 UI/ColorVision.ImageEditor 当前已经落地的主控链和扩展点，不再继续维护旧文档里那种“功能大全 + 教程示例 + 性能数字承诺”的写法。

## 模块定位

ColorVision.ImageEditor 当前不是单纯的图片显示控件，而是一套“图像宿主 + 可缩放画布 + 绘图工具 + 打开器 + 运行时工具覆盖 + 设置系统”的组合模块。

它的主线更接近：

- `ImageView` 作为宿主
- `EditorContext` 作为当前视图运行时容器
- `DrawCanvas` 作为真实绘制画布
- `IEditorToolFactory` 负责发现和装配工具、菜单、打开器
- `IImageOpen` 负责不同文件类型的打开链

## 当前最关键的目录

从项目目录看，最值得优先阅读的是：

- `ImageView.xaml(.cs)`：主控件和运行时编排入口
- `EditorContext.cs`：每个视图实例的运行时容器
- `DrawCanvas.cs`：视觉树和绘图画布
- `EditorToolFactory.cs`：工具、菜单、打开器发现与装配
- `Abstractions/`：编辑器扩展点边界
- `Draw/`：图元、工具、选择框、注释导入导出
- `EditorTools/`：非图元类工具，例如缩放、伪彩色、3D、算法、全屏
- `Video/`：视频打开器
- `Layers/`：图层/通道切换语义
- `Realtime/`、`Settings/`：实时图像和设置相关支持

## 关键入口类型

### ImageView

`ImageView` 是当前编辑器模块的主入口。它负责：

- 初始化 `EditorContext`
- 绑定 `DrawCanvas`、`Zoombox`、上下文菜单和状态栏
- 执行 `IImageComponent` 一次性初始化
- 管理标准文件命令
- 接线配置变化、缩放变化和 overlay 刷新
- 处理图像打开、清理、保存、导入导出注释等流程

如果要理解这个模块，首要入口就是 `ImageView.xaml.cs`。

### EditorContext

`EditorContext` 是每个 `ImageView` 实例的运行时容器，当前会集中保存：

- 当前 `ImageView`
- `DrawCanvas`
- `Zoombox`
- `ImageViewConfig`
- `DrawEditorManager`
- `IEditorToolFactory`
- 当前 opener、上下文菜单、图元列表
- 一组轻量 service 注册表

它既是运行时状态容器，也带一点局部 service locator 性质，这也是当前模块的重要实现边界。

### DrawCanvas

`DrawCanvas` 是真实的绘图承载层。它不只是显示控件，还负责：

- 维护视觉对象集合
- 执行命中测试
- 处理图元增删
- 维护撤销/重做
- 作为大量绘图工具挂接鼠标事件的目标

### IEditorToolFactory

名字虽然叫 `IEditorToolFactory`，当前它其实是一个具体类，不是接口。它会在构造时反射扫描并装配：

- `IDVContextMenu`
- `IIEditorToolContextMenu`
- `IEditorTool`
- `IImageComponent`
- `IImageOpen`

同时，它还会维护“全局工具 + 当前 opener runtime tools”的生效视图，并按 `GuidId` 做覆盖重建工具栏。

这也是当前 ImageEditor 初始化成本和扩展能力最集中的地方。

### IImageOpen 及其扩展接口

当前打开链不靠一个统一文件管理器，而是由各个 `IImageOpen` 实现负责。

另外，`IImageOpen` 还可以可选实现：

- `IImageOpenEditorToolProvider`
- `IImageOpenEditorToolLifecycle`

这样某些特殊文件类型就能在打开后临时接管或覆盖工具栏能力，而不是把所有分支都堆进全局工具里。

### VideoOpen / Window3D / ModelViewer3DControl

视频和 3D 能力当前是编辑器模块里的真实子功能，但它们属于附加打开器或工具，不是整个模块唯一主线：

- `Video/VideoOpen.cs`：视频打开器
- `EditorTools/ThreeD/Window3D.xaml.cs`：图像转 3D 表面窗口
- `EditorTools/ThreeD/ModelViewer3DControl.xaml.cs`：OBJ/STL 查看控件

旧文档里对这些能力展开得很重，但更可靠的读法仍然是先搞清楚 `ImageView` 和工具工厂主链。

## 当前运行时主链

现有控制链大致是：

1. 创建 `ImageView`。
2. 初始化 `EditorContext`、`SelectionVisual`、`CompactInspectorPresenter`。
3. 创建 `IEditorToolFactory` 并反射装配全局工具、上下文菜单、图像组件、打开器。
4. 执行所有 `IImageComponent.Execute(ImageView)`。
5. 用户打开文件后，根据扩展名选中 `IImageOpen`。
6. opener 调用 `SetImageSource(...)`，并可选提供自己的 runtime tools。
7. `DrawCanvas`、overlay、状态栏、图层切换、伪彩色等围绕当前图像上下文继续工作。

## 作为 DLL 使用时

### 应该引用它的场景

- 窗口需要嵌入 `ImageView` 显示并交互式查看图像。
- Engine 或项目包需要把算法结果画成 ROI、POI、线、矩形、多边形、文本或曲线 overlay。
- 需要支持注释导入导出、伪彩色、CIE 图、直方图、3D 表面、实时图像显示。
- 需要为特殊文件类型增加 opener 或临时工具栏。

### 扩展图像打开方式

1. 实现 `IImageOpen`。
2. 在 opener 中声明支持的文件扩展名和打开逻辑。
3. 如需覆盖工具栏，实现 `IImageOpenEditorToolProvider`。
4. 如需生命周期回调，实现 `IImageOpenEditorToolLifecycle`。
5. 打开实际文件，确认 `EditorToolFactory` 能扫描并装配 opener。

### 扩展绘图或 overlay

| 需求 | 优先入口 |
| --- | --- |
| 新增图元 | `Draw/`、`Abstractions/Draw/`、对应 Manager |
| 新增图元持久化 | `Draw/Annotations/`、`AnnotationMapper` |
| 新增工具栏工具 | `EditorTools/`、`IEditorTool` |
| 新增右键菜单 | `IIEditorToolContextMenu` 或 `IDVContextMenu` |
| 新增结果 overlay | 优先复用现有图元和 `DrawCanvas`，再接入 Engine result handler |

### 发布注意

`ImageEditor` 包含 shader、CIE CSV、colormap 图片、图标和 OpenCV runtime 依赖。发布后要验证普通图片、CIE、伪彩色、3D、注释导入导出至少能打开一次。

### DLL 发布验收表

| 验收项 | 要查什么 | 通过标准 |
| --- | --- | --- |
| 目标框架 | `ColorVision.ImageEditor.csproj` 的 `net10.0-windows7.0`、`AnyCPU;x64` | 主程序 net10 产物能加载，x64 运行不缺 native 依赖 |
| 包元数据 | `GeneratePackageOnBuild`、`PackageReadmeFile`、`README.md` | 包内 README 和符号包完整 |
| 核心依赖 | `ColorVision.Common`、`ColorVision.Core`、`ColorVision.Themes`、`ColorVision.UI` | 发布目录中共享 DLL 版本和 ImageEditor 编译版本一致 |
| 图像运行时 | `OpenCvSharp4`、`OpenCvSharp4.runtime.win` | 普通图、TIF、大图和视频相关入口不报 runtime 缺失 |
| 资源文件 | `EditorTools/Filters/Shaders/*.ps`、`Assets/Colormap/colorscale_*.jpg`、`Assets/Data/CIE_cc_1931_2deg.csv` | 滤镜、伪彩、CIE 背景和谱线都能显示 |
| 工具发现 | `EditorToolFactory`、`IImageOpen`、`IEditorTool` | 设置页能看到工具和 opener，打开文件时能按扩展名命中 |
| 图元和 overlay | `DrawCanvas`、`Draw/Annotations/`、`AnnotationMapper` | ROI/POI/线/文本显示、导入导出和坐标一致 |
| 高阶窗口 | CIE、直方图、3D、实时图像 | 每类窗口至少打开一次且不空白 |

### 现场故障首查

| 现象 | 先查哪里 | 判断要点 |
| --- | --- | --- |
| 图片区域空白 | opener 是否命中、`SetImageSource(...)`、图像文件格式 | 先确认 `IImageOpen` 是否被 `EditorToolFactory` 装配 |
| TIF 或大图打开失败 | `OpenCvSharp4.runtime.win`、`ColorVision.Core` native 依赖 | DLL 加载成功不代表 native runtime 都齐 |
| 工具栏或右键菜单缺项 | `IEditorToolFactory`、工具可见性设置、反射扫描结果 | 设置页能否列出工具是第一检查点 |
| ROI/POI 坐标偏移 | `DrawCanvas`、缩放比例、图像裁剪/旋转 | 再回查 Engine 结果坐标系是否转换过 |
| 伪彩或滤镜无效果 | shader 和 `colorscale_*.jpg` 资源 | 资源缺失比算法错误更常见 |
| CIE 窗口空白 | `CIE_cc_1931_2deg.csv`、CIE 图片资源 | 包内缺 CSV 或背景图会造成显示不完整 |
| 3D 窗口空白 | `Window3D`、HelixToolkit、显卡/驱动 | 先验证示例图像和旋转缩放，再看数据源 |
| 设置不保存 | `ImageViewConfig`、设置文件写入权限 | 工具隐藏/显示后重开图像验证持久化 |

## 组件明细与交接验收

这部分按“发一个 `ColorVision.ImageEditor.dll` 或排查图像界面问题时，接手人员要看什么”来组织。它补足单模块页里最容易缺的控件、工具和资源验收。

### 运行时组件表

| 组件族 | 关键类/窗口 | 源码入口 | 运行时角色 | 最小验收 |
| --- | --- | --- | --- | --- |
| 图像宿主 | `ImageView`、`ImageViewModel` | `UI/ColorVision.ImageEditor/ImageView.xaml(.cs)` | 载图、状态栏、工具栏、注释导入导出、上下文菜单总入口 | 打开普通 PNG/JPG/TIF，缩放和平移正常 |
| 运行时上下文 | `EditorContext`、`IEditorContextService` | `EditorContext.cs`、`Abstractions/IEditorContextService.cs` | 保存当前图像、工具、画布、服务和 opener 状态 | 切换图片后旧 opener 工具被清理 |
| 绘图画布 | `DrawCanvas`、`DrawEditorContext` | `DrawCanvas.cs`、`Draw/` | 显示图像、承载图元、命中测试、Undo/Redo | 画矩形/线/文本，撤销重做可用 |
| 工具工厂 | `IEditorToolFactory` | `EditorToolFactory.cs` | 反射装配工具、右键菜单、打开器、图像组件 | 设置页能列出当前加载的工具和 opener |
| 打开器 | `IImageOpen`、`CommonImageOpen`、`Opentif`、`VideoOpen` | `Abstractions/IImageEditor.cs`、`Tif/`、`Video/` | 按文件扩展名打开图像、TIF、视频等文件 | 每类 opener 至少打开一个样例文件 |
| 工具栏 | `IEditorTool`、`IEditorToggleTool`、`IEditorCustomControlTool` | `Abstractions/IEditorTool.cs`、`EditorTools/` | 缩放、保存、导入导出、伪彩、滤镜、直方图、3D 等工具 | 工具显示、隐藏、点击和配置都可用 |
| 右键菜单 | `IDVContextMenu`、`IIEditorToolContextMenu` | `Abstractions/`、`EditorTools/*/*ContextMenu.cs` | 根据当前上下文补充菜单项 | 图像区域和工具区域右键菜单不报错 |
| 图元和注释 | `Draw/`、`AnnotationMapper`、`AnnotationDocument` | `Draw/`、`Draw/Annotations/` | ROI、POI、线、文本、标尺等图元和导入导出 | 导出注释后重新导入，图元位置一致 |
| 伪彩和滤镜 | `PseudoColorEditorTool`、`DisplayShaderFilterEditorTool` | `EditorTools/PseudoColor/`、`EditorTools/Filters/` | 显示伪彩、阈值、高亮和 shader 滤镜 | 切换色表和阈值后显示变化正确 |
| CIE | `CieDiagramEditorTool`、`CieDiagramView` | `CieDiagramEditorTool.cs`、`Cie/` | 展示 CIE 1931/1976 色度图和 overlay | CIE 窗口打开，背景和数据点正常 |
| 3D | `View3DEditorTool`、`Window3D`、`ModelViewer3DControl` | `EditorTools/ThreeD/` | 图像表面 3D 和 OBJ/STL 模型查看 | 3D 窗口非空，旋转缩放正常 |
| 实时图像 | `RealtimeImageViewService`、`RealtimeFramePresenter` | `Realtime/` | 接收实时帧、统计帧率、叠加相机 overlay | 连续帧刷新时 UI 不阻塞 |
| 图层 | `ImageLayerDescriptor`、`BitmapImageLayerController` | `Layers/` | 管理通道、图层切换和图像层描述 | 多通道或分层图像切换正常 |
| 设置 | `ImageViewSettingsWindow`、`ImageViewWorkspaceSettingsView` | `Settings/` | 管理工具可见性、默认显示、打开器支持列表 | 工具可见性修改后重新打开仍生效 |

### 资源和包内容核查

`ColorVision.ImageEditor.csproj` 把多类资源作为 WPF `Resource` 打进包。发布时必须抽检，而不是只看 DLL。

| 资源 | 项目文件入口 | 缺失表现 |
| --- | --- | --- |
| shader | `EditorTools/Filters/Shaders/*.ps` | 滤镜、伪彩高亮、阈值显示失效 |
| colormap | `Assets/Colormap/colorscale_*.jpg` | 伪彩色表为空或切换失败 |
| CIE 数据 | `Assets/Data/CIE_cc_1931_2deg.csv` | CIE 背景或谱线显示不完整 |
| CIE/应用图标 | `Assets/Image/*.ico`、`*.png` | CIE 窗口、标题栏或资源图标缺失 |
| OpenCV runtime | `OpenCvSharp4.runtime.win`、`ColorVision.Core` native 依赖 | 普通图像、视频、OpenCV 工具打开失败 |

包内容抽检示例见 [UI DLL 发布矩阵](./release-matrix.md)。如果用户机器只报 `ColorVision.ImageEditor.dll` 加载成功，仍不能证明这些资源都存在。

### 结果 overlay 交接边界

| 问题 | 应看 ImageEditor | 应看 Engine/项目 |
| --- | --- | --- |
| 图元不显示、颜色/线宽不对 | `Draw/` 图元、`DrawCanvas`、缩放和命中测试 | 结果 handler 是否传入正确图元参数 |
| 结果坐标偏移 | 图像显示比例、`Zoombox`、图元 layout scale | Engine 结果坐标系、裁剪/旋转/通道转换 |
| 注释不能导入导出 | `Draw/Annotations/`、`AnnotationMapper` | 项目是否使用同一注释格式 |
| 图片打开但没有结果层 | opener 是否设置图像上下文 | `IViewResult` / `IResultHandleBase` 是否注册 |
| 项目 CSV 缺字段 | 通常不在 ImageEditor | 项目包结果映射、DAO、导出逻辑 |

ImageEditor 负责“看见和交互”，不负责客户项目的 OK/NG 判定、MES 字段和 CSV 业务映射。结果业务链路继续看 [Engine 结果展示与项目交接链路](../engine-components/result-handoff-chain.md)。

### 发布后必须烟测

| 烟测项 | 操作 | 通过标准 |
| --- | --- | --- |
| 普通图像 | 打开 PNG/JPG/BMP/TIF | 正常显示，缩放和平移不卡顿 |
| TIF / OpenCV | 打开 TIF 或大图 | 无 OpenCV runtime 缺失错误 |
| 图元编辑 | 绘制矩形、线、文本，撤销/重做 | 图元位置、选择框、属性栏一致 |
| 注释 | 导出注释，再重新导入 | 图元数量和坐标一致 |
| 伪彩/滤镜 | 切换至少两个 colormap 和一个 shader 滤镜 | 显示变化正确，无资源缺失 |
| CIE | 打开 CIE 工具 | CIE 背景、谱线和点位显示正常 |
| 3D | 打开图像 3D 或模型查看器 | 画面非空，可旋转缩放 |
| 结果 overlay | 打开一条真实算法结果 | ROI/POI/文本层与原图坐标一致 |
| 设置 | 打开 ImageView 设置，隐藏/显示一个工具 | 重开图像后配置仍生效 |

## 当前实现有哪些边界

### ImageView 不是纯显示控件

`SetImageSource(...)` 当前不只是设置 `ImageShow.Source`，还可能触发伪彩色配置、标定服务和其他编辑器运行时副作用。

如果只是纯显示场景，需要注意 `EnableEditorImageServices` 这类开关，不能默认把整个 ImageView 当成无副作用图片框。

### 工具发现是反射驱动的

`IEditorToolFactory` 当前每个视图实例都会做多轮扫描和创建，这是一条真实而重要的控制链。旧文档里那种“静态工具架构图”会掩盖掉这件事。

### EditorContext 既是状态容器也带服务定位器属性

当前设计并没有把“配置”“工具状态”“运行时服务”彻底解耦，而是部分集中在 `EditorContext`、`ImageViewConfig` 和少量 service 上。这是阅读和后续重构时必须承认的现状。

### 注释导入导出已经有实际落点

图元持久化不是停留在概念层，当前实际落在 `Draw/Annotations/` 以及 `ImageView` 的导入导出入口上。阅读标注能力时，应该直接看这条链，而不是泛化成“任意绘图工具可自动持久化”。

## 当前更适合怎样读这个模块

### 想看主控链和初始化编排

先看：

- `ImageView.xaml.cs`
- `EditorContext.cs`
- `EditorToolFactory.cs`

### 想看绘图和选择逻辑

先看：

- `DrawCanvas.cs`
- `Draw/`
- `Abstractions/Draw/`

### 想看文件打开链和 runtime tool 覆盖

先看：

- `Abstractions/IImageEditor.cs`
- `Video/VideoOpen.cs`
- 具体 opener 实现所在目录

### 想看注释、伪彩色、3D 这些子能力

先看：

- `Draw/Annotations/`
- `EditorTools/PseudoColor/`
- `EditorTools/ThreeD/`

## 这页不再做什么

本页不再继续维护这些高风险内容：

- 大段性能数字承诺
- 覆盖全模块的教程式示例集
- 把视频或 3D 写成整个模块的唯一主线
- 编造与当前代码不匹配的统一视图模型或抽象接口

## 继续阅读

- [UI组件概览](./README.md)
- [ColorVision.UI](./ColorVision.UI.md)
- [ColorVision.Themes](./ColorVision.Themes.md)

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
# Configuration 目录说明

这个目录原来承载一套独立的实验性配置/命令框架，包括：

- 强类型 `IEditorConfiguration` / `ImageEditorConfiguration`
- 独立 `CommandManager` / `DeltaCommandBase`
- `ServiceLocator` / `ImageEditorInitializer`
- `DrawCanvasCommandManager` / `DrawCanvasCommandAdapter`

这套实现现在已经移除，原因很直接：

1. 当前仓库里没有任何运行时代码再依赖 `ColorVision.ImageEditor.Configuration`。
2. 现行 ImageEditor 已经形成了更清晰的实际架构，不再需要并行维护第二套未接入的配置/命令层。
3. 继续保留旧文件只会制造“看起来很完整、实际上没人用”的假复杂度。

## 当前实际架构

当前 ImageEditor 的有效配置与状态链路如下：

- `ImageViewConfig`
    - 当前 ImageView 的运行态显示/布局开关。
- `EditorContext`
    - 当前 ImageView 的上下文、服务和共享状态容器。
- `ImageViewSettingsWindow`
    - 统一设置入口，负责组织设置分组与保存时机。
- `IImageViewSettingProvider`
    - 各模块自己声明设置项，不再集中硬编码。
- `ImageViewWorkspaceSettingsView`
    - 当前工作台状态页，显示工具栏可见性、已加载 `IEditorTool`、支持的 `IImageOpen`。
- `ConfigService`
    - 默认值/全局配置的持久化入口。
- `DrawCanvas`
    - 目前仍通过 `ActionCommand` 维护实际可工作的 Undo/Redo。

## 当前关键文件

- `ImageView.xaml.cs`
    - 设置窗口入口与 ImageView 级 provider 注册。
- `Settings/ImageViewSettingMetadata.cs`
    - 设置元数据、provider 接口和当前默认 provider。
- `Settings/ImageViewSettingsWindow.xaml.cs`
    - 设置窗口装配逻辑。
- `Settings/ImageViewWorkspaceSettingsView.xaml.cs`
    - 工作台页；当前已加载工具和打开器的统一管理入口。
- `EditorToolFactory.cs`
    - 负责发现 `IEditorTool`、`IImageComponent`、`IImageOpen`。
- `Tif/Opentif.cs`
    - TIFF 打开器；Gray32Float 读取时会走专用加载配置。
- `Tif/TifOpenConfig.cs`
    - TIFF 打开器当前的专用配置样例。

## 为什么不保留旧 Configuration 实现

旧目录的问题不是“写得不好”，而是“没有进入真实控制路径”。

- 它没有接入当前 `ImageViewSettingsWindow`。
- 它没有接管当前 `EditorToolFactory` 的发现流程。
- 它没有取代 `DrawCanvas` 的实际 Undo/Redo 路径。
- 它引入了第二套抽象名称，容易让后续开发误判应该往哪一层继续扩展。

所以这次处理原则是：

- 删除无引用实现。
- 保留文档，明确现行架构。
- 后续迭代只在当前真实链路上演进，不再恢复并行实验层。

## 下一步

下一步迭代图见 `ROADMAP.md`。

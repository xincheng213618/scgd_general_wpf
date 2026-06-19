# UI 组件目录

这页按组件类型整理 `UI/` 下真实存在的 WPF 控件、窗口和扩展点。它和 [UI DLL 组件手册](./component-handbook.md) 的区别是：组件手册按发布 DLL 讲边界，本页按“我要改哪个 UI 能力”来定位源码。运行时发现机制和排障链路请看 [UI 运行时组件交接手册](./ui-runtime-handoff.md)。

## 使用方式

| 你要找什么 | 先看 |
| --- | --- |
| 菜单、状态栏、快捷键 | [菜单/状态栏/热键](#菜单-状态栏-热键) |
| 设置窗口、属性编辑器 | [PropertyGrid 和设置编辑](#propertygrid-和设置编辑) |
| 主题、窗口外观、通用控件 | [主题和通用控件](#主题和通用控件) |
| 图片显示、绘图、overlay | [图像编辑器组件](#图像编辑器组件) |
| 数据库、Socket、调度窗口 | [运行时工具窗口](#运行时工具窗口) |
| 工作区、文件树、终端、RBAC | [Solution 工作区组件](#solution-工作区组件) |
| 插件市场、下载器、向导 | [桌面辅助窗口](#桌面辅助窗口) |

## 基础 MVVM 和扩展契约

| 组件 | 源码 | 所属 DLL | 作用 |
| --- | --- | --- | --- |
| `ViewModelBase` | `UI/ColorVision.Common/MVVM/` | `ColorVision.Common` | 大多数 ViewModel 的属性通知基类 |
| `RelayCommand` / `RelayCommand<T>` | `UI/ColorVision.Common/MVVM/` | `ColorVision.Common` | WPF 命令绑定基础 |
| `IInitializer` / `InitializerBase` | `UI/ColorVision.Common/Interfaces/` | `ColorVision.Common` | 启动阶段初始化扩展 |
| `IMainWindowInitialized` | `UI/ColorVision.Common/Interfaces/Window/` | `ColorVision.Common` | 主窗口完成初始化后的扩展点 |
| `IConfig` | `UI/ColorVision.Common/Interfaces/Config/` | `ColorVision.Common` | 配置对象契约 |
| `IWizardStep` | `UI/ColorVision.Common/Interfaces/` | `ColorVision.Common` | 向导步骤契约 |

新增 UI 子模块时，先判断它是否只是 ViewModel/命令/共享接口。如果是，优先放在 `ColorVision.Common` 或引用它；不要为了一个命令对象去引用 `ColorVision.UI.Desktop` 或 `ColorVision.Solution`。

## 菜单、状态栏、热键

| 能力 | 入口 | 源码 | 说明 |
| --- | --- | --- | --- |
| 菜单项契约 | `IMenuItem`、`IMenuItemProvider`、`IRightMenuItemProvider` | `UI/ColorVision.Common/Interfaces/Menus/` | 插件和模块暴露菜单项的底层契约 |
| 菜单基类 | `MenuItemBase` | `UI/ColorVision.Common/Interfaces/Menus/MenuItemBase.cs` | 带命令、排序、权限检查的菜单基类 |
| 全局菜单基类 | `GlobalMenuBase` | `UI/ColorVision.UI/Menus/GlobalMenuBase.cs` | 主菜单常用扩展基类 |
| 菜单管理器 | `MenuManager` | `UI/ColorVision.UI/Menus/MenuManager.cs` | 反射、Provider、动态菜单刷新中心 |
| 文件/编辑菜单基类 | `MenuItemFileBase`、`MenuItemEditBase` | `UI/ColorVision.UI/Menus/Base/` | File/Edit 菜单分组基类 |
| 状态栏控件 | `StatusBarControl` | `UI/ColorVision.UI/StatusBar/StatusBarControl.xaml` | 主状态栏显示控件 |
| 状态栏 Provider | `IStatusBarProvider`、`IStatusBarProviderUpdatable` | `UI/ColorVision.Common/Interfaces/StatusBar/` | 各模块把状态写入状态栏的扩展点 |
| 全局热键 | `IHotKey`、`HotKeys`、`GlobalHotKey/` | `UI/ColorVision.UI/HotKey/` | 系统级或应用级热键 |
| 窗口热键 | `WindowHotKeyManager` | `UI/ColorVision.UI/HotKey/WindowHotKey/` | 只在某个窗口或控件范围内生效 |
| 热键设置 UI | `HotKeysSetting`、`HoyKeyControl` | `UI/ColorVision.UI/HotKey/` | 快捷键设置页和单项控件 |

新增菜单时，优先继承已有菜单基类或实现 Provider；新增状态栏信息时实现 Provider，不要直接操作主窗口状态栏控件。

## PropertyGrid 和设置编辑

ColorVision 的设置编辑不是每个窗口手写表单，而是大量依赖 `PropertyEditorWindow`、属性特性和 `IPropertyEditor`。

| 组件 | 源码 | 作用 |
| --- | --- | --- |
| `PropertyEditorWindow` | `UI/ColorVision.UI/PropertyEditor/PropertyEditorWindow.xaml` | 属性编辑主窗口 |
| `PropertyTreeNode` | `UI/ColorVision.UI/PropertyEditor/` | 属性树节点和分组模型 |
| `PropertyEditorTypeAttribute` | `UI/ColorVision.Common` / `UI/ColorVision.UI` 使用链 | 指定某个属性使用自定义编辑器 |
| `BoolPropertiesEditor` | `UI/ColorVision.UI/PropertyEditor/Editor/` | bool 编辑 |
| `EnumPropertiesEditor` | `UI/ColorVision.UI/PropertyEditor/Editor/` | enum 下拉编辑 |
| `TextboxPropertiesEditor` | `UI/ColorVision.UI/PropertyEditor/Editor/` | 普通文本编辑 |
| `TextSelectFilePropertiesEditor` | `UI/ColorVision.UI/PropertyEditor/Editor/` | 文件选择 |
| `TextSelectFolderPropertiesEditor` | `UI/ColorVision.UI/PropertyEditor/Editor/` | 文件夹选择 |
| `TextBaudRatePropertiesEditor` | `UI/ColorVision.UI/PropertyEditor/Editor/` | 串口波特率 |
| `CronExpressionPropertiesEditor` | `UI/ColorVision.UI/PropertyEditor/Editor/` | Cron 表达式 |
| `DictionaryJsonEditor` / `ListNumericJsonEditor` | `UI/ColorVision.UI/PropertyEditor/Editor/` | JSON 字典和数值列表 |
| `DictionaryEditorWindow` / `ListEditorWindow` | `UI/ColorVision.UI/PropertyEditor/Editor/Dictionary/`、`List/` | 集合编辑窗口 |
| `ThemePropertiesEditor` | `UI/ColorVision.UI/Themes/ThemeConfig.cs` | 主题配置编辑 |
| `LanguagePropertiesEditor` | `UI/ColorVision.UI/Languages/LanguageConfig.cs` | 多语言配置编辑 |
| `LevelPropertiesEditor` | `UI/ColorVision.UI/LogImp/Editors/` | 日志等级编辑 |
| `TextJsonPropertiesEditor` | `UI/ColorVision.Solution/Editor/AvalonEditor/` | JSON 大文本编辑 |

新增设置项时优先写成带 `[Category]`、`[DisplayName]`、`[Description]` 的配置对象。只有默认编辑器不够用时，才新增 `IPropertyEditor`，并通过 `PropertyEditorTypeAttribute` 绑定。

## 主题和通用控件

| 组件 | 源码 | 所属 DLL | 作用 |
| --- | --- | --- | --- |
| `ThemeManager` | `UI/ColorVision.Themes/` | `ColorVision.Themes` | 主题切换、资源字典注入 |
| `ThemeManagerExtensions` | `UI/ColorVision.Themes/` | `ColorVision.Themes` | `Application.ApplyTheme`、窗口标题栏应用 |
| `BaseWindow` | `UI/ColorVision.Themes/Themes/Window/BaseWindow.xaml` | `ColorVision.Themes` | 常用窗口基类和通知入口 |
| `LoadingOverlay` | `UI/ColorVision.Themes/Controls/LoadingOverlay.xaml` | `ColorVision.Themes` | 覆盖式加载提示 |
| `ProgressRing` | `UI/ColorVision.Themes/Controls/ProgressRing.xaml` | `ColorVision.Themes` | 环形进度 |
| `ToggleSwitch` | `UI/ColorVision.Themes/Controls/ToggleSwitch.cs` | `ColorVision.Themes` | 开关控件 |
| `MessageBoxWindow` | `UI/ColorVision.Themes/Controls/MessageBoxWindow.xaml` | `ColorVision.Themes` | 主题化消息窗口 |
| `UploadControl` / `UploadWindow` | `UI/ColorVision.Themes/Controls/Uploads/` | `ColorVision.Themes` | 上传提示和上传窗口 |
| 主题资源 | `UI/ColorVision.Themes/Themes/*.xaml` | `ColorVision.Themes` | Base、Dark、White、Pink、Cyan 等资源 |
| 图标资源 | `UI/ColorVision.Themes/Assets/images/*.xaml` | `ColorVision.Themes` | 常用矢量图标 |

主题层不应该知道插件、Engine 或客户项目。新增通用控件前先确认它是否真的跨模块复用；只服务某个窗口的控件应留在对应模块。

## 运行时工具窗口

| 能力 | 窗口/控件 | 源码 | 所属 DLL |
| --- | --- | --- | --- |
| MySQL 连接 | `MySqlConnect` | `UI/ColorVision.Database/MySqlConnect.xaml` | `ColorVision.Database` |
| 数据库浏览 | `DatabaseBrowserWindow` | `UI/ColorVision.Database/DatabaseBrowserWindow.xaml` | `ColorVision.Database` |
| 通用查询 | `GenericQueryWindow` | `UI/ColorVision.Database/GenericQueryWindow.xaml` | `ColorVision.Database` |
| 行编辑 | `DatabaseRowEditWindow` | `UI/ColorVision.Database/DatabaseRowEditWindow.xaml` | `ColorVision.Database` |
| Socket 管理 | `SocketManagerWindow` | `UI/ColorVision.SocketProtocol/SocketManagerWindow.xaml` | `ColorVision.SocketProtocol` |
| Socket 状态栏 | `SocketStatusBarProvider` | `UI/ColorVision.SocketProtocol/SocketStatusBarProvider.cs` | `ColorVision.SocketProtocol` |
| 任务管理 | `TaskViewerWindow` | `UI/ColorVision.Scheduler/TaskViewerWindow.xaml` | `ColorVision.Scheduler` |
| 新建任务 | `CreateTask` | `UI/ColorVision.Scheduler/CreateTask.xaml` | `ColorVision.Scheduler` |
| 执行历史 | `ExecutionHistoryWindow` | `UI/ColorVision.Scheduler/ExecutionHistoryWindow.xaml` | `ColorVision.Scheduler` |
| 调度状态栏 | `SchedulerStatusBarProvider` | `UI/ColorVision.Scheduler/SchedulerStatusBarProvider.cs` | `ColorVision.Scheduler` |

这些窗口多数都有状态栏入口或菜单入口。排查“窗口打不开”时先看 Provider/Menu 是否被加载，再看配置和数据库文件。

## 图像编辑器组件

| 能力 | 入口 | 源码 |
| --- | --- | --- |
| 图像主控件 | `ImageView` | `UI/ColorVision.ImageEditor/ImageView.xaml` |
| 运行时上下文 | `EditorContext` | `UI/ColorVision.ImageEditor/EditorContext.cs` |
| 绘图画布 | `DrawCanvas` | `UI/ColorVision.ImageEditor/DrawCanvas.cs` |
| 工具工厂 | `IEditorToolFactory` | `UI/ColorVision.ImageEditor/EditorToolFactory.cs` |
| 打开器扩展 | `IImageOpen` | `UI/ColorVision.ImageEditor/Abstractions/IImageEditor.cs` |
| 工具扩展 | `IEditorTool`、`IEditorToggleTool`、`IEditorCustomControlTool` | `UI/ColorVision.ImageEditor/Abstractions/IEditorTool.cs` |
| 右键菜单扩展 | `IDVContextMenu`、`IIEditorToolContextMenu` | `UI/ColorVision.ImageEditor/Abstractions/` |
| 图元和 overlay | `Draw/` | `UI/ColorVision.ImageEditor/Draw/` |
| 注释导入导出 | `Draw/Annotations/` | `UI/ColorVision.ImageEditor/Draw/Annotations/` |
| 图像设置 | `ImageViewSettingsWindow`、`ImageViewContextSettingsView`、`ImageViewWorkspaceSettingsView` | `UI/ColorVision.ImageEditor/Settings/` |

### 已有工具分组

| 分组 | 代表组件 | 用途 |
| --- | --- | --- |
| 文件命令 | `OpenImageEditorTool`、`SaveAsImageEditorTool`、`ImportAnnotationsEditorTool`、`ExportAnnotationsEditorTool` | 打开、保存、注释导入导出 |
| 缩放和视图 | `ZoomInEditorTool`、`ZoomOutEditorTool`、`ZoomRatioEditorTool`、`FullScreenEditorTool` | 缩放、填充、全屏 |
| 图像处理 | `AlgorithmsContextMenu`、`WhiteBalanceWindow`、`GammaCorrectionWindow`、`ThresholdWindow`、`EdgeDetectionWindow` | 常用算法工具 |
| 伪彩色 | `PseudoColorEditorTool`、`PseudoColorToolControl` | 伪彩显示 |
| 滤镜 | `DisplayShaderFilterEditorTool`、`DisplayShaderFilterToolControl` | shader 显示滤镜 |
| 直方图 | `HistogramEditorTool`、`HistogramChartWindow` | 直方图查看 |
| 图形编辑 | `GraphicEditorTool`、`GraphicEditingWindow` | ROI/图元编辑 |
| 3D/CIE | `View3DEditorTool`、`Window3D`、`ModelViewer3DControl`、`CieDiagramView` | 3D 表面、模型查看、CIE 图 |

新增图像工具时优先实现 `IEditorTool` 或相关工具接口；新增算法结果 overlay 时优先复用 `Draw/` 图元，不要另造一套无法导入导出的绘制系统。

## 桌面辅助窗口

| 能力 | 窗口/入口 | 源码 |
| --- | --- | --- |
| 设置窗口 | `SettingWindow`、`SettingWindowController` | `UI/ColorVision.UI.Desktop/Settings/` |
| 向导 | `WizardWindow`、`WizardManager` | `UI/ColorVision.UI.Desktop/Wizards/` |
| 插件市场 | `MarketplaceWindow`、`MarketplaceManager` | `UI/ColorVision.UI.Desktop/Marketplace/` |
| DLL 版本查看 | `ViewDllVersionsWindow` | `UI/ColorVision.UI.Desktop/Marketplace/` |
| 下载器 | `DownloadWindow`、`AddDownloadDialog` | `UI/ColorVision.UI.Desktop/Download/` |
| 菜单管理 | `MenuItemManagerWindow` | `UI/ColorVision.UI.Desktop/MenuItemManager/` |
| 配置管理 | `ConfigManagerWindow` | `UI/ColorVision.UI.Desktop/ConfigManagerWindow.xaml` |
| 第三方应用 | `ThirdPartyAppsWindow`、`AddCustomAppWindow` | `UI/ColorVision.UI.Desktop/ThirdPartyApps/` |
| 反馈 | `FeedbackWindow` | `UI/ColorVision.UI.Desktop/Feedback/` |
| 操作统计 | `TimedButtonOperationStatsWindow` | `UI/ColorVision.UI.Desktop/TimedButtons/` |

`ColorVision.UI.Desktop` 更像桌面辅助窗口集合，不是主程序入口。主程序仍在仓库根部 `ColorVision/`。

## Solution 工作区组件

| 能力 | 入口 | 源码 |
| --- | --- | --- |
| 解决方案管理 | `SolutionManager` | `UI/ColorVision.Solution/SolutionManager.cs` |
| 最近文件 | `MenuRecentFile` | `UI/ColorVision.Solution/RecentFile/` |
| 文件树 | `SolutionExplorer`、`SolutionNode`、`TreeViewControl` | `UI/ColorVision.Solution/Explorer/`、根目录控件 |
| 新建项 | `AddNewItemWindow`、`AddNewProjectWindow` | `UI/ColorVision.Solution/Explorer/` |
| 编辑器注册 | `IEditor`、`EditorManager`、`EditorForExtensionAttribute` | `UI/ColorVision.Solution/Editor/` |
| 文本编辑 | `AvalonEditControll`、`AvalonEditWindow` | `UI/ColorVision.Solution/Editor/AvalonEditor/` |
| 十六进制编辑 | `HexEditorView` | `UI/ColorVision.Solution/Editor/` |
| 工作区布局 | `WorkspaceManager`、`DockLayoutManager`、`LayoutMenuItems` | `UI/ColorVision.Solution/Workspace/` |
| 终端 | `TerminalControl` | `UI/ColorVision.Solution/Terminal/` |
| 多图浏览 | `MultiImageViewer` | `UI/ColorVision.Solution/MultiImageViewer/` |
| Markdown 预览 | `MarkdownViewWindow` | `UI/ColorVision.Solution/MarkdownViewWindow.xaml` |
| 本地 RBAC | `RbacManagerWindow`、`UserManagerWindow`、`PermissionManagerWindow`、`LoginWindow` | `UI/ColorVision.Solution/Rbac/` |

新增编辑器时实现 `IEditor` 并使用 `EditorForExtensionAttribute`、`GenericEditorAttribute` 或 `FolderEditorAttribute`。不要把客户项目流程或 Engine 设备控制放到 Solution 工作区里。

## 新增 UI 组件时的落点

| 新能力 | 建议落点 | 同步文档 |
| --- | --- | --- |
| 纯 ViewModel、命令、共享接口 | `ColorVision.Common` | [ColorVision.Common](./ColorVision.Common.md) |
| 通用主题控件 | `ColorVision.Themes/Controls/` | [ColorVision.Themes](./ColorVision.Themes.md)、本页 |
| 菜单、热键、状态栏、属性编辑器 | `ColorVision.UI/` | [ColorVision.UI](./ColorVision.UI.md)、本页 |
| 数据库维护窗口 | `ColorVision.Database/` | [ColorVision.Database](./ColorVision.Database.md)、本页 |
| Socket 管理或 handler 基础设施 | `ColorVision.SocketProtocol/` | [ColorVision.SocketProtocol](./ColorVision.SocketProtocol.md)、本页 |
| Quartz 调度窗口 | `ColorVision.Scheduler/` | [ColorVision.Scheduler](./ColorVision.Scheduler.md)、本页 |
| 图像工具、图元、overlay | `ColorVision.ImageEditor/` | [ColorVision.ImageEditor](./ColorVision.ImageEditor.md)、本页 |
| 设置、向导、市场、下载、诊断 | `ColorVision.UI.Desktop/` | [ColorVision.UI.Desktop](./ColorVision.UI.Desktop.md)、本页 |
| 工作区、编辑器、终端、RBAC | `ColorVision.Solution/` | [ColorVision.Solution](./ColorVision.Solution.md)、本页 |

## 维护要求

- 新增公开窗口、控件、Provider、PropertyEditor、EditorTool、IEditor 时，要同步更新本页或对应 DLL 页。
- 组件文档要写清楚“入口类、源码目录、所属 DLL、运行时发现方式”，不要只写功能名称。
- 如果新增的是通过反射、Provider、属性标注或插件装载发现的组件，还要同步更新 [UI 运行时组件交接手册](./ui-runtime-handoff.md)。
- 发布 DLL 前继续检查 [UI DLL 发布手册](./publishing.md)，尤其是 native runtime、资源文件和 README。

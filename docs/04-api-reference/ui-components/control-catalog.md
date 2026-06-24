# UI 组件目录

这页是“按任务找源码”的索引。运行时发现机制看 [UI 运行时组件](./ui-runtime-handoff.md)，DLL 边界看 [UI DLL 速查](./component-handbook.md)，具体模块细节看对应 DLL 页。

## 快速定位

| 你要改什么 | 先看源码 | 所属页 |
| --- | --- | --- |
| MVVM、命令、共享接口 | `UI/ColorVision.Common/` | [ColorVision.Common](./ColorVision.Common.md) |
| 菜单、热键、状态栏、搜索 | `UI/ColorVision.UI/` | [ColorVision.UI](./ColorVision.UI.md) |
| 设置窗口、市场、下载、向导 | `UI/ColorVision.UI.Desktop/` | [ColorVision.UI.Desktop](./ColorVision.UI.Desktop.md) |
| 主题、窗口外观、通用控件 | `UI/ColorVision.Themes/` | [ColorVision.Themes](./ColorVision.Themes.md) |
| PropertyGrid 和自定义编辑器 | `UI/ColorVision.UI/PropertyEditor/` | [ColorVision.UI](./ColorVision.UI.md) |
| 图像打开、工具栏、overlay | `UI/ColorVision.ImageEditor/` | [ColorVision.ImageEditor](./ColorVision.ImageEditor.md) |
| 数据库浏览和通用查询 | `UI/ColorVision.Database/` | [ColorVision.Database](./ColorVision.Database.md) |
| Socket 管理和状态栏 | `UI/ColorVision.SocketProtocol/` | [ColorVision.SocketProtocol](./ColorVision.SocketProtocol.md) |
| Quartz 调度窗口 | `UI/ColorVision.Scheduler/` | [ColorVision.Scheduler](./ColorVision.Scheduler.md) |
| 工作区、编辑器、终端、RBAC | `UI/ColorVision.Solution/` | [ColorVision.Solution](./ColorVision.Solution.md) |

## 扩展点

| 能力 | 接口/入口 | 备注 |
| --- | --- | --- |
| 启动初始化 | `IInitializer`、`InitializerBase` | 放共享启动扩展，不放业务流程 |
| 主窗口初始化后扩展 | `IMainWindowInitialized` | 适合菜单、状态、服务启动后的挂接 |
| 配置对象 | `IConfig`、`ConfigService` | 需要持久化的配置优先走这里 |
| 菜单 | `IMenuItem`、`IMenuItemProvider`、`MenuItemBase` | 注意 `OwnerGuid`、`GuidId`、权限和排序 |
| 状态栏 | `IStatusBarProvider`、`IStatusBarProviderUpdatable` | 不要直接操作主窗口状态栏控件 |
| 热键 | `IHotKey`、`WindowHotKeyManager` | 先区分全局热键和窗口热键 |
| 设置页 | `IConfigSettingProvider`、`[ConfigSetting]` | 搜不到不一定没注册，可能被过滤 |
| 属性编辑器 | `PropertyEditorTypeAttribute`、`IPropertyEditor` | 默认编辑器不够用时才新增 |
| 图像打开器 | `IImageOpen`、`FileExtensionAttribute` | 新格式优先走打开器 |
| 图像工具 | `IEditorTool`、`IEditorToggleTool`、`IEditorCustomControlTool` | 工具由 ImageEditor 工厂装配 |
| 图像右键菜单 | `IDVContextMenu`、`IIEditorToolContextMenu` | 根据是否需要 `EditorContext` 选接口 |
| 数据库浏览 | `IDatabaseBrowserProvider` | Provider 负责给浏览器提供库表入口 |
| Solution 编辑器 | `IEditor`、`EditorForExtensionAttribute` | 新文件类型不要硬写在文件树里 |
| 向导步骤 | `IWizardStep` | 分步骤初始化或配置时使用 |

## 常用窗口

| 目标 | 入口 |
| --- | --- |
| 设置 | `UI/ColorVision.UI.Desktop/Settings/SettingWindow.xaml` |
| 插件市场 | `UI/ColorVision.UI.Desktop/Marketplace/MarketplaceWindow.xaml` |
| 下载器 | `UI/ColorVision.UI.Desktop/Download/DownloadWindow.xaml` |
| 菜单管理 | `UI/ColorVision.UI.Desktop/MenuItemManager/MenuItemManagerWindow.xaml` |
| 日志 | `UI/ColorVision.UI/LogImp/WindowLog.xaml` |
| PropertyGrid | `UI/ColorVision.UI/PropertyEditor/PropertyEditorWindow.xaml` |
| 数据库浏览器 | `UI/ColorVision.Database/DatabaseBrowserWindow.xaml` |
| Socket 管理 | `UI/ColorVision.SocketProtocol/SocketManagerWindow.xaml` |
| 调度任务 | `UI/ColorVision.Scheduler/TaskViewerWindow.xaml` |
| 图像编辑器 | `UI/ColorVision.ImageEditor/ImageView.xaml` |
| 图形/ROI 编辑 | `UI/ColorVision.ImageEditor/EditorTools/GraphicEditing/GraphicEditingWindow.xaml` |
| 3D / CIE | `UI/ColorVision.ImageEditor/EditorTools/ThreeD/`、`UI/ColorVision.ImageEditor/Cie/` |
| Solution 文件树 | `UI/ColorVision.Solution/TreeViewControl.xaml` |
| Solution 终端 | `UI/ColorVision.Solution/Terminal/TerminalControl.xaml` |
| RBAC | `UI/ColorVision.Solution/Rbac/` |

## 落点规则

| 新能力 | 建议落点 | 避免 |
| --- | --- | --- |
| 共享接口、命令、ViewModel | `ColorVision.Common` | 引用高层窗口或项目包 |
| 壳层菜单、状态栏、热键、搜索 | `ColorVision.UI` | 写客户业务 |
| 通用视觉控件或主题资源 | `ColorVision.Themes` | 依赖插件或 Engine |
| 图像工具、图元、overlay | `ColorVision.ImageEditor` | 做项目字段导出 |
| 数据库查看和查询窗口 | `ColorVision.Database` | 为每个业务窗口重复写浏览器 |
| Socket 管理基础设施 | `ColorVision.SocketProtocol` | 写具体项目测试流程 |
| 调度窗口和 Job 管理 | `ColorVision.Scheduler` | 把长耗时算法写进窗口 |
| 工作区、编辑器、终端 | `ColorVision.Solution` | 写设备控制主链路 |
| 市场、下载、向导、诊断 | `ColorVision.UI.Desktop` | 当作主程序入口 |

## 修改后要同步

- 新增公开窗口、Provider、PropertyEditor、EditorTool、IEditor 时，同步本页或对应 DLL 页。
- 通过反射、Provider、属性标注或插件装载发现的能力，同步 [UI 运行时组件](./ui-runtime-handoff.md)。
- 发布相关改动同步 [UI DLL 发布](./publishing.md)。
- 用户操作入口变化同步 [UI 组件使用手册](../../01-user-guide/interface/ui-component-handbook.md)。

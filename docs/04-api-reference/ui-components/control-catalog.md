---
knowledge_id: "ui.control-catalog"
knowledge_type: "index"
status: "current"
summary: "按控件、窗口和扩展接口定位对应 UI 源码与专题。"
aliases: ["控件窗口源码在哪里","IEditorTool","IPropertyEditor","IEditor"]
code_paths: ["UI/ColorVision.Common/Interfaces","UI/ColorVision.UI/PropertyEditor","UI/ColorVision.ImageEditor/EditorToolFactory.cs","UI/ColorVision.Solution/Editor/EditorManager.cs"]
test_paths: []
related: ["ui.index","ui.property-grid","ui.settings","ui.wizards","ui.menus","ui.hotkeys","ui.search","ui.localization","ui.status-bar","ui.discovery"]
---

# UI 组件目录

这页是“按任务找源码”的索引。运行时发现机制看 [UI 运行时组件](./ui-runtime-handoff.md)，DLL 边界看 [UI DLL 速查](./component-handbook.md)，具体模块细节看对应 DLL 页。

## 快速定位

| 你要改什么 | 先看源码 | 所属页 |
| --- | --- | --- |
| MVVM、命令、共享接口 | `UI/ColorVision.Common/` | [ColorVision.Common](./ColorVision.Common.md) |
| 菜单、热键、状态栏、搜索 | `UI/ColorVision.UI/` | [ColorVision.UI](./ColorVision.UI.md) |
| 设置窗口、市场、下载、向导 | `UI/ColorVision.UI.Desktop/` | [桌面总览](./ColorVision.UI.Desktop.md)、[设置](./settings.md)、[向导](./wizards.md) |
| 主题、窗口外观、通用控件 | `UI/ColorVision.Themes/` | [ColorVision.Themes](./ColorVision.Themes.md) |
| PropertyGrid 和自定义编辑器 | `UI/ColorVision.UI/PropertyEditor/` | [PropertyGrid 契约](./property-grid.md) |
| 图像打开、工具栏、overlay | `UI/ColorVision.ImageEditor/` | [ColorVision.ImageEditor](./ColorVision.ImageEditor.md) |
| 数据库浏览和通用查询 | `UI/ColorVision.Database/` | [ColorVision.Database](./ColorVision.Database.md) |
| Socket 管理和状态栏 | `UI/ColorVision.SocketProtocol/` | [ColorVision.SocketProtocol](./ColorVision.SocketProtocol.md) |
| Quartz 调度窗口 | `UI/ColorVision.Scheduler/` | [ColorVision.Scheduler](./ColorVision.Scheduler.md) |
| 工作区与项目树 | `UI/ColorVision.Solution/` | [资源打开与切换](./ColorVision.Solution.md) |
| 编辑器、文档与布局 | `UI/ColorVision.Solution/Editor/`、`Workspace/` | [文档生命周期](./editor-document-lifecycle.md) |
| 终端与脚本 | `UI/ColorVision.Solution/Terminal/` | [终端契约](../../01-user-guide/interface/terminal.md) |
| 多图查看、缩略图、景深融合 | `UI/ColorVision.ImageTools/` | [ColorVision.ImageTools](./ColorVision.ImageTools.md) |
| 登录、用户、角色、权限、会话 | `UI/ColorVision.Rbac/` | [RBAC 模块](../../03-architecture/security/rbac.md) |

## 扩展点

| 能力 | 接口/入口 | 备注 |
| --- | --- | --- |
| 启动初始化 | `IInitializer`、`InitializerBase` | 放共享启动扩展，不放业务流程 |
| 主窗口初始化后扩展 | `IMainWindowInitialized` | 适合菜单、状态、服务启动后的挂接 |
| 配置对象 | `IConfig`、`ConfigService` | 需要持久化的配置优先走这里 |
| 菜单 | `IMenuItem`、`IMenuItemProvider`、`MenuItemBase`、`MenuItemAttribute` | [发现、父子树、隐藏与执行](./menus.md)；不将快捷键显示当成注册 |
| 状态栏 | `IStatusBarProvider`、`IStatusBarProviderUpdatable`、`IActiveDocumentStatusProvider` | [发现、绑定、文档通知与清理](./status-bar.md)；隐藏不停止业务 |
| 界面语言 | `LanguageConfig`、`LanguageManager`、`LanguagePropertiesEditor` | [资源发现、文化回退和重启切换](./localization.md)；不是全窗口实时翻译 |
| 热键 | `IHotkeyProvider`、`IHotKey`、`HotkeyService` | [定义身份、草稿、注册和释放](./hotkeys.md)；保存配置不证明注册成功 |
| 搜索候选 | `ISearch`、`ISearchProvider`、`IDynamicSearchProvider` | [静态刷新、动态缓存和执行边界](./search.md)；不是仓库知识检索 |
| 设置页 | `IConfigSettingProvider`、`[ConfigSetting]` | [发现缓存、搜索范围与活对象编辑](./settings.md) |
| 属性编辑器 | `PropertyEditorTypeAttribute`、`IPropertyEditor.GenProperties`、`PropertyEditorRegistry` | [选择、复用与失败契约](./property-grid.md)，不要在缓存编辑器实例里持有目标对象 |
| 图像打开器 | `IImageOpen`、`FileExtensionAttribute` | 新格式优先走打开器 |
| 图像工具 | `IEditorTool`、`IEditorToggleTool`、`IEditorCustomControlTool` | 工具由 ImageEditor 工厂装配 |
| 图像右键菜单 | `IDVContextMenu`、`IIEditorToolContextMenu` | 根据是否需要 `EditorContext` 选接口 |
| 数据库浏览 | `IDatabaseBrowserProvider` | Provider 负责给浏览器提供库表入口 |
| Solution 编辑器 | `IEditor`、`EditorForExtensionAttribute` | 新文件类型不要硬写在文件树里 |
| 向导步骤 | `IWizardStep`、`IWizardInitializer` | [步骤应用、初始化时序和完成标记](./wizards.md) |

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
| RBAC | `UI/ColorVision.Rbac/RbacManagerWindow.xaml` |

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
| 多图查看和融合工具 | `ColorVision.ImageTools` | 把工具实现放回工作区壳层 |
| 用户、角色和权限管理 | `ColorVision.Rbac` | 把账户数据或细权限实现放进 Common |
| 市场、下载、向导、诊断 | `ColorVision.UI.Desktop` | 当作主程序入口 |

## 修改后要同步

- 新增公开窗口、Provider、PropertyEditor、EditorTool、IEditor 时，同步本页或对应 DLL 页。
- 通过反射、Provider、属性标注或插件装载发现的能力，同步 [UI 运行时组件](./ui-runtime-handoff.md)。
- 发布相关改动同步 [UI DLL 发布](./publishing.md)。
- 可见行为、成功条件或故障入口变化同步对应组件主题；设置/下载/向导见 [桌面辅助组件](./ColorVision.UI.Desktop.md)，宿主入口见 [主窗口装配](../../01-user-guide/interface/main-window.md)，不另维护一份 UI 使用手册。

## 验证入口与缺口

本页只维护任务到源码的路由；验证入口随目标专题，不把目录存在当成扩展已在运行时加载。

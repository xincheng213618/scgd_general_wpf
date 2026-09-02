---
knowledge_id: "ui.discovery"
knowledge_type: "topic"
status: "current"
summary: "排查程序集加载后菜单、设置、PropertyGrid、工具和服务扩展的发现链。"
aliases: ["插件加载了但入口没有出现","AssemblyHandler","AssemblyService","MenuManager"]
code_paths: ["UI/ColorVision.UI/AssemblyHandler.cs","UI/ColorVision.UI/Plugins/PluginLoader.cs","UI/ColorVision.UI/Menus/MenuManager.cs","UI/ColorVision.ImageEditor/EditorToolFactory.cs"]
test_paths: ["Test/ColorVision.UI.Tests/PluginLoaderTests.cs","Test/ColorVision.UI.Tests/MenuDiscoveryExclusionTests.cs","Test/ColorVision.UI.Tests/ModuleCatalogTests.cs"]
related: ["ui.index","ui.configuration","ui.settings","ui.wizards","ui.menus","ui.hotkeys","ui.search","ui.status-bar","plugins.model","ui.property-grid","ui.image-editor","ui.socket-protocol","engine.results"]
---

# UI 运行时组件

这页只回答一个问题：主程序启动后，菜单、设置、插件、状态栏、热键、图像工具、Socket、调度和 Solution 编辑器是怎么被发现的。

具体控件和窗口源码看 [UI 组件目录](./control-catalog.md)，DLL 边界看 [UI DLL 速查](./component-handbook.md)，发布检查看 [UI DLL 发布](./publishing.md)。

## 先分流

| 现象 | 第一检查点 | 下一页 |
| --- | --- | --- |
| 插件目录存在但功能没出现 | 插件是否被 `PluginLoader` 加载，程序集是否刷新 | [插件装载与扩展发现](../../02-developer-guide/plugin-development/overview.md) |
| 菜单没有出现 | 类型缓存、`OwnerGuid`/`GuidId`、目标窗口、ID 过滤与条目 Visibility | [菜单发现与显示](./menus.md) |
| 快捷键不触发或关闭后仍响应 | 定义身份、宿主范围、注册返回和句柄所有权 | [快捷键注册与释放](./hotkeys.md) |
| 搜索候选缺失或陈旧 | 静态集合刷新、动态 provider 缓存、类型开关及业务索引 | [产品搜索契约](./search.md) |
| 设置项找不到 | `IConfigSettingProvider` 或 `[ConfigSetting]` 是否被扫描，配置对象是否仍是当前实例 | [配置来源与重载](./configuration.md)、[设置发现与搜索](./settings.md) |
| 向导步骤缺失或卡住 | 发现失败、Order、CanContinue、Refresh/Apply 和 initializer 时序 | [向导执行契约](./wizards.md) |
| PropertyGrid 显示不对 | 可见性、元数据 provider、属性标注、类型注册、只读状态 | [PropertyGrid 契约](./property-grid.md) |
| 状态栏项缺失或陈旧 | provider 缓存、TargetName、Source 绑定与活动文档通知分别核对 | [状态栏发现与刷新](./status-bar.md) |
| 图片工具栏少按钮 | 工具构造约束、发现集合和 opener 覆盖；刷新不等于重扫 | [编辑器上下文与工具装配](./image-editor-context.md) |
| Socket 有连接但业务不跑 | 消息历史、协议模式、`EventName`、handler 程序集 | [ColorVision.SocketProtocol](./ColorVision.SocketProtocol.md) |
| 调度任务不执行 | Quartz 是否启动，任务配置和历史库是否正常 | [UI 组件目录](./control-catalog.md) |
| Solution 文件打不开 | 先区分工作区路由、文件 action 与编辑器选择，再核对注册、文件锁和权限 | [资源路由](./ColorVision.Solution.md)、[编辑器与文档](./editor-document-lifecycle.md) |

不要因为问题发生在 WPF 窗口里，就直接归到 `ColorVision.UI`。先判断它属于发现链、控件目录、业务项目包还是 Engine 结果链路。

## 主链路

主程序启动后先加载配置和插件，插件程序集进入 `AssemblyHandler` / `AssemblyService`，再由菜单、设置、状态栏、热键、ImageEditor 工具、Socket、Scheduler 和 Solution 编辑器各自扫描扩展点。UI 扩展不出现时，先确认类型所在程序集已经进入 `AssemblyService`，再查具体扩展点。

## 发现机制

| 能力 | 发现入口 | 实现方式 | 常见失败原因 |
| --- | --- | --- | --- |
| 插件 | `PluginLoader.LoadPlugins("Plugins")` | `manifest.json`、`DllName`、`.deps.json` | 插件被禁用、DLL 缺失、依赖版本不满足 |
| 菜单 | `MenuManager.LoadMenuForWindow` | `IMenuItem`、`IMenuItemProvider`、`MenuItemAttribute` 的互斥发现路径 | 一次性类型缓存、缺父项/ID 冲突、窗口目标与显示过滤；[显示和命令检查分开](./menus.md) |
| 设置 | `ConfigSettingManager.GetAllSettings` | `IConfigSettingProvider`、`[ConfigSetting]` | 类型缓存未更新、对象解析失败、搜索范围；见[设置契约](./settings.md) |
| 向导 | `WizardManager.Initialized` | 分别扫描 `IWizardStep`、`IWizardInitializer` | 不做统一可见性过滤；反射/构造异常可中断发现，见[向导契约](./wizards.md) |
| 属性编辑 | `PropertyEditorHelper`、`PropertyEditorRegistry` | 元数据 provider → 属性指定类型 → 注册类型 → 嵌套对象 | 构造或生成失败、未匹配、可见性过滤；只读属性不等于必须消失 |
| 状态栏 | `StatusBarManager` | 全局 provider / Updatable 与 IActiveDocumentStatusProvider 两套来源 | 实例缓存不随全量刷新重扫；列表变化与绑定值更新分开，见[状态栏契约](./status-bar.md) |
| 热键 | `HotkeyService` | `IHotkeyProvider` / `IHotKey`、显式注册 | 重复 ID、窗口/全局注册失败、宿主与重载范围；见[快捷键契约](./hotkeys.md) |
| 搜索 | `SearchManager` | `ISearch`、`ISearchProvider`、`IDynamicSearchProvider` | 静态集合与动态实例缓存不同，过滤不阻止 provider 查询；见[搜索契约](./search.md) |
| 图像打开/工具 | ImageEditor 工厂 | `IImageOpen`、`IEditorTool`、右键菜单接口 | 扩展名不匹配、构造参数不匹配、可见性配置隐藏 |
| Socket | `SocketManager` | `ISocketJsonHandler`、`ISocketTextDispatcher` | 模式选错、`EventName` 不匹配、handler 程序集未加载 |
| 调度 | `QuartzSchedulerManager` | Quartz Job 和任务配置 | `scheduler_tasks.json`、Job 类型、历史库异常 |
| Solution 编辑器 | `EditorManager` | `IEditor` 和扩展名标注 | 扩展名未注册、文件锁、布局恢复异常 |

## 改动时怎么落点

| 你要做什么 | 优先落点 | 不要做什么 |
| --- | --- | --- |
| 新增共享契约、命令、基础 ViewModel | `ColorVision.Common` | 引入高层窗口或项目业务 |
| 新增菜单、设置、状态栏、热键 | `ColorVision.UI` 或实现对应 Provider | 直接操作主窗口控件 |
| 新增主题资源或窗口外观 | `ColorVision.Themes` | 把业务菜单塞进主题库 |
| 新增图像打开器或工具 | `ColorVision.ImageEditor` 对应 opener/tool 扩展点 | 把客户导出/MES 字段写进 ImageEditor |
| 新增结果 overlay | 先按[结果链路](../engine-components/result-handoff-chain.md)分流：Engine 历史 handler 或中立算法 renderer/manager | 把所有 overlay 都实现为 `IViewResult`，或将客户判定混进通用显示层 |
| 新增实体查询入口 | `ColorVision.Database` 的 `GenericQueryWindow` | 在业务窗口里手写另一套条件查询器 |
| 新增本地 TCP 指令 | 项目包 handler + `ColorVision.SocketProtocol` | 在通用 Socket 模块里写项目流程 |
| 新增调度任务 | Scheduler Job 或项目任务入口 | 把长耗时算法写在 UI 调度窗口里 |
| 新增工作区编辑器 | `ColorVision.Solution` Editor | 把设备控制流程写进 Solution 壳层 |

## 常见故障

| 现象 | 判断顺序 |
| --- | --- |
| 插件安装后没有菜单 | 先看插件加载日志，再看 `AssemblyHandler`，最后看菜单 `OwnerGuid` 和权限 |
| 菜单有但点击无反应 | 按[具体命令入口](./menus.md)查 CanExecute、懒调用日志和业务服务；不要推断搜索/直接 Execute 都会先检查 CanExecute |
| 设置修改后丢失 | 按[配置契约](./configuration.md)区分序列化、目标文件写入、内存发布和重载后的旧对象引用 |
| PropertyGrid 空白 | 查可读属性、Browsable/PropertyVisibility、编辑目标和选择器日志，见 [PropertyGrid 契约](./property-grid.md) |
| 状态栏显示旧状态 | 先分 Source 绑定值、provider 项目列表、活动文档三条[更新路径](./status-bar.md)；不将所有问题归为缺少 Updatable 事件 |
| 搜索找不到入口 | 先区分菜单、模板、工具与 Flow 节点，核对[搜索候选来源、刷新和过滤](./search.md)，不将无候选等同于无业务对象 |
| 图片 overlay 坐标不对 | 先核对底图与 Draw 坐标系；历史链查 handler 转换，中立链查 geometry 坐标空间与文档 revision，项目结果查项目映射 |
| Socket 收到消息但项目没跑 | 查 `EventName`、handler 加载、项目入口流程 |
| 插件市场下载失败 | 查后端地址、下载器、目录权限；这不等于插件业务失败 |

结果 overlay 和业务判定问题继续看 [Engine 结果展示链路](../engine-components/result-handoff-chain.md)。项目私有流程、设备控制、MES 字段和客户导出格式不要放进通用 UI 运行时页。

## 最小验证

| 改动范围 | 最小验证 |
| --- | --- |
| 插件加载 | 放入测试插件，确认 manifest、依赖提示、程序集刷新正常 |
| 菜单/状态栏/热键 | 在获授权的隔离宿主验证发现、排序与无害命令；状态栏还需区分绑定、增量和文档切换，不能用图标出现代替生命周期验收 |
| 设置/PropertyGrid | 打开设置或属性编辑器，搜索、修改、保存、重启后仍生效 |
| 主题控件 | 按当前 `Theme` 的 `UseSystem`、`Light`、`Dark` 验证资源、图标和标题栏 |
| ImageEditor | 打开普通图片和一类业务结果图，确认工具栏、缩放、overlay |
| 数据库/Socket/调度 | 打开管理窗口，确认连接、消息或任务历史能读写 |
| Solution | 打开 `.cvsln`，新建文件，打开编辑器，启动终端，保存布局 |

## 验证入口与缺口

关联测试：`Test/ColorVision.UI.Tests/PluginLoaderTests.cs`、`Test/ColorVision.UI.Tests/MenuDiscoveryExclusionTests.cs`、`Test/ColorVision.UI.Tests/ModuleCatalogTests.cs`。

发现链测试不等于所有扩展完成初始化；涉及目标窗口、硬件或外部服务时还要记录对应运行时验证。

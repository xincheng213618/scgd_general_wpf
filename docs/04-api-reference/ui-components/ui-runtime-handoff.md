---
knowledge_id: "ui.discovery"
knowledge_type: "topic"
status: "current"
summary: "UI 扩展发现与入口缺失排查：AssemblyHandler 的程序集过滤、类型缓存和 provider 构造；刷新程序集不重建所有消费者，入口可见不证明初始化或业务完成。"
aliases: ["插件加载了但入口没有出现", "UI运行时发现", "扩展发现", "程序集过滤", "类型扫描", "AssemblyHandler", "AssemblyService", "RegisterAssembly", "RefreshAssemblies", "ClearCaches", "GetTypes", "LoadImplementations", "ReflectionTypeLoadException", "Failed to load types from assembly", "Failed to create instance", "MenuManager"]
code_paths: ["ColorVision/App.xaml.cs", "ColorVision/BuiltInModules.cs", "UI/ColorVision.Common/Interfaces/Assembly", "UI/ColorVision.UI/AssemblyHandler.cs", "UI/ColorVision.UI/Plugins/PluginLoader.cs", "UI/ColorVision.UI/Menus/MenuManager.cs", "UI/ColorVision.UI/ConfigSetting/ConfigSettingManager.cs", "UI/ColorVision.UI/PropertyEditor/PropertyEditorHelper.cs", "UI/ColorVision.UI/StatusBar/StatusBarManager.cs", "UI/ColorVision.UI/HotKey/HotkeyService.cs", "UI/ColorVision.UI/Serach/SearchManager.cs", "UI/ColorVision.UI.Desktop/Wizards/WizardWindow.xaml.cs", "UI/ColorVision.ImageEditor/EditorToolFactory.cs", "UI/ColorVision.SocketProtocol/SocketJsonDispatcher.cs", "UI/ColorVision.SocketProtocol/SocketTextDispatcher.cs", "UI/ColorVision.Scheduler/QuartzSchedulerManager.cs", "UI/ColorVision.Solution/Editor/EditorManager.cs"]
test_paths: ["Test/ColorVision.UI.Tests/PluginLoaderTests.cs","Test/ColorVision.UI.Tests/MenuDiscoveryExclusionTests.cs","Test/ColorVision.UI.Tests/ModuleCatalogTests.cs"]
related: ["ui.index", "ui.control-catalog", "ui.configuration", "ui.settings", "ui.wizards", "ui.menus", "ui.hotkeys", "ui.search", "ui.status-bar", "plugins.model", "ui.property-grid", "ui.image-editor-context", "ui.socket-protocol", "ui.scheduler", "ui.documents", "engine.results"]
---

# UI 运行时扩展发现与排查

本页说明 DLL 已进入进程后，菜单、设置、状态栏、热键、图像工具等扩展如何进入各自的宿主。适用于“插件加载了但入口没有出现”、新类型不被扫描、刷新后仍使用旧条目等问题。插件安装、manifest 与依赖门禁归[插件装载契约](../../02-developer-guide/plugin-development/overview.md)；控件位置和新能力落点归[UI 组件目录](./control-catalog.md)。

## 按顺序定位缺失的入口

1. **确认程序集实际加载。** 记录插件 ID、目标扩展类型和当前安装目录，查本次装载日志。目录存在、缓存中有 `PluginInfo` 或阶段标记 `PluginsLoaded`，都不能证明该 DLL 本次已成功加载；先排除禁用、本次跳过和依赖失败。
2. **确认进入发现视图。** 在对应宿主核对 `AssemblyHandler.GetAssemblies()` 中的程序集及其 `Location`。此集合经过过滤，不等于 `AppDomain.CurrentDomain.GetAssemblies()`；显式注册也不会绕过过滤，规则见下节。
3. **确认类型可读。** 查看 `GetTypes(assembly)` 是否包含目标类型，以及 `Failed to load types from assembly` / `Unexpected error loading types from assembly` 日志。反射失败时这里不会保留部分可加载类型，不要仅凭 DLL 已加载继续排查控件布局。
4. **确认消费方已发现并构造条目。** 按下表选择具体 manager，检查接口、构造函数、类型/实例缓存与创建日志。程序集视图变了，不代表菜单、设置或图像工具已重新扫描；构造函数失败也可能仅留下日志。
5. **确认宿主接入和显示条件。** 检查目标窗口、父级/标识、可见性、宿主资源与命令路由。条目已显示后，再按所属主题区分 `CanExecute`、注册成功、保存成功和业务完成。

正常启动的装配入口是 `ColorVision/App.xaml.cs`：先创建 `ModuleCatalog`、调用 `BuiltInModules.Register` 登记内置模块，再加载主配置；插件阶段按恢复选择装载外部 DLL，随后封存模块目录并进入向导或启动窗口。各消费者在各自被调用时扫描，不是所有扩展在某一个时刻统一实例化。登记和封存规则见[模块登记](../../02-developer-guide/plugin-development/overview.md)。

`AssemblyService` 是可设置的 `IAssemblyService` 接口入口；生产 `AssemblyHandler` 构造时将自身设为该服务。`Application.Current.GetAssemblies()` 扩展方法也返回 AssemblyHandler 的过滤视图。隔离宿主可注入其它实现，因此阅读接口名称还不足以判断实际过滤规则。

## AssemblyHandler 的过滤与缓存

程序集必须名称非空、非动态、有可读取的 `Location`，且位置在 `AppDomain.CurrentDomain.BaseDirectory` 之下。路径比较不区分大小写并保留目录分隔边界，不按当前工作目录判断，也不只限基础目录的第一层。

名称按不区分大小写的前缀过滤，当前排除 `System.`、`Microsoft.`、`netstandard`、`WindowsBase`、`PresentationCore`、`PresentationFramework`、`mscorlib`、`Newtonsoft`、`EntityFramework`、`log4net` 和 `HandyControl`。这些是前缀规则，不能将其中某个名字开头的业务程序集视为例外。位于其它目录的 DLL 即使已经 LoadFrom 或登记，也不能据此进入该发现视图。

| API | 缓存与返回语义 |
| --- | --- |
| `GetAssemblies()` | 已有快照时直接返回；首次调用才执行 `RefreshAssemblies()` |
| `RegisterAssembly(assembly)` | 拒绝动态程序集；新登记项只有通过过滤且类型读取成功，才补入已有快照。每次新增登记均清空通用实现类型缓存；重复登记同一程序集直接返回，不是强制重试 |
| `RefreshAssemblies()` | 合并 AppDomain 已加载项和显式登记项，过滤并读取类型后重建程序集快照；清空通用实现类型缓存，保留成功的程序集类型缓存 |
| `GetTypes(assembly)` | 成功时按程序集复用同一个类型快照；读取异常时记录错误并返回空集合，失败结果不缓存。直接调用它不额外执行程序集目录/名称过滤 |
| `ClearCaches()` | 清空程序集快照、程序集类型缓存和通用实现类型缓存；保留显式登记集合，不卸载 DLL，不清理各 manager 自己的缓存或已创建对象 |

`Assembly.GetTypes()` 抛出 `ReflectionTypeLoadException` 时，AssemblyHandler 不使用异常中的 `Types` 残余项。首次构建或刷新程序集视图时，该程序集会因类型读取失败而被排除；有的消费者自己调用 `assembly.GetTypes()` 并处理残余类型，但前提仍是该程序集已经进入它取得的视图。应核对失败所在层，不能假定所有扫描都具有相同容错。

通用 `LoadImplementations<T>(params object?[]? args)` 要求 `T` 为接口，否则抛出 `Type parameter T must be an interface`。候选必须是实现该接口的非抽象 class，具有公共无参构造；缓存的是候选类型，每次调用都重新实例化。`args` 当前不传给构造函数，不能靠此参数注入依赖。单项构造异常记录 `Failed to create instance` 后继续，其它实例仍可返回。构造可以产生副作用，这个方法也不统一调用 `Initialize`、`Dispose` 或业务命令。

## 各消费方的发现与刷新

下表中的 API 需由实际宿主调用。它们使用不同缓存，不能用一次 `RefreshAssemblies()` 代替所有更新。

| 能力 | 发现入口与条件 | 刷新范围与所属契约 |
| --- | --- | --- |
| 菜单 | `MenuManager` 自行扫描 `IMenuItem`、`IMenuItemProvider`、`MenuItemAttribute`，排除开放泛型和直接标注 Obsolete 的类型 | 类型缓存一次建立；重建只重新构造既有类型，仍受目标、父子树与隐藏规则约束。见[菜单](./menus.md) |
| 设置 | `ConfigSettingManager.GetAllSettings` 扫描 provider 和 `IConfig` 属性上的 ConfigSetting 标注 | 类型与条目两层缓存；`InvalidateCache` 只清条目，重建条目重新解析配置对象，不重扫类型。见[设置发现与搜索](./settings.md) |
| 向导 | `WizardManager.Initialized` 扫描 `IWizardStep`、`IWizardInitializer`，无参构造后分别按 Order 排序 | 每次 Initialized 清列表并扫描；不统一过滤业务可见性，反射或构造异常可中断发现。见[向导](./wizards.md) |
| 属性编辑 | `PropertyEditorHelper` 静态初始化注册内置映射，并触发外部 IPropertyEditor 类型的静态构造；类型映射仍需注册 | 不是为每个实现类自动建立属性类型映射；显式 RegisterEditor、属性指定类型和元数据 provider 各有入口，实例缓存与字段选择见[PropertyGrid](./property-grid.md) |
| 状态栏 | `StatusBarManager` 通过通用帮助器构造 `IStatusBarProvider`；活动文档项另来自 IActiveDocumentStatusProvider | 全局 provider 列表非空后复用实例；RefreshAll 重取元数据，不重扫新 provider。绑定值、项目变化事件和文档切换分开，见[状态栏](./status-bar.md) |
| 热键 | `HotkeyService` 独立发现 `IHotkeyProvider` / `IHotKey`，也接收显式注册 | 发现定义、设置草稿、窗口/全局注册和释放分层，菜单隐藏不会注销热键。见[快捷键](./hotkeys.md) |
| 搜索 | `SearchManager` 接收 `ISearch`、`ISearchProvider`、`IDynamicSearchProvider`、`IAsyncSearchProvider` | 所见程序集序列变化才重建 provider；常规候选刷新复用实例。静态候选、动态查询和过滤见[产品搜索](./search.md) |
| 图像打开与工具 | `IEditorToolFactory` 构造时扫描 `IImageOpen`、工具和两类右键菜单接口，按接口选取可解析构造函数 | RefreshToolBars 重建已有工具的控件，不重扫类型；当前 opener 还可覆盖工具。见[编辑器上下文](./image-editor-context.md) |
| Socket | `SocketJsonDispatcher` / `SocketTextDispatcher` 构造时分别扫描 handler；SocketManager 默认构造器创建二者 | 各自保留 handler 集合；JSON 按 EventName 路由，文本按独立规则派发。连接存在不证明业务调用，见[Socket 协议](./ColorVision.SocketProtocol.md) |
| 调度 | `QuartzSchedulerManager.Start` 扫描 IJob 类型并建立任务类型目录 | 任务恢复、触发器、运行和历史是不同阶段；从 InitializationTask、任务配置与错误记录排查。见[调度](./ColorVision.Scheduler.md) |
| Solution 编辑器 | `EditorManager` 发现 IEditor 的扩展名/通用/目录标注，也接受显式描述符注册 | 监听 AppDomain.AssemblyLoad，具有自己的程序集去重和注册规则；不能套用菜单缓存结论。见[编辑器与文档](./editor-document-lifecycle.md) |

已经找到入口后的问题应转到对应契约：设置丢失查[配置持久化与重载](./configuration.md)；overlay 错位先核对底图、坐标空间及图像版本，再按[Engine 历史结果与中立算法](../engine-components/result-handoff-chain.md)分流；文件打不开先查[工作区与资源路由](./ColorVision.Solution.md)。主题外观、数据库操作和客户业务成功条件不由程序集发现证明。

## 验证入口与缺口

修改发现代码时，使用已知且无害的测试扩展，分别验证“程序集进入视图 → 类型进入候选 → 实例创建 → 目标宿主呈现”。涉及配置保存、热键注册、Socket 或任务执行，再按对应契约验证；不要用连接成功、窗口能打开或出现图标代替这些结果。

- `ModuleCatalogTests` 用记录型服务检查重复登记与封存；另用真实 AssemblyHandler 检查 Rbac provider 进入既有快照及 GetTypes 复用。它不覆盖所有位置/名称过滤、类型加载失败、构造异常或各消费方缓存。
- `PluginLoaderTests` 检查跳过匹配和文件存在；任意字节文件也能通过存在性断言，测试没有完整装载 DLL 或验证全部依赖。
- `MenuDiscoveryExclusionTests` 检查指定旧类型缺失和部分保留候选/位置，不是完整菜单树或业务初始化测试。菜单、设置、热键等的专项测试在各自主题中说明。

这些测试引用不代表已运行。文档核验可读取源码、声明、配置路径和既有日志；真实调用扫描可能构造插件对象，插件装载还会写配置并执行扩展代码，需符合当前任务授权。构建或链接检查只能证明文档结构与路由，不证明全部扩展已完成初始化。

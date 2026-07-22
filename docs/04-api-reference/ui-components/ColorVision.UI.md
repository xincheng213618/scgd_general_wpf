# ColorVision.UI

本页是 `UI/ColorVision.UI/` 的维护速查。它不是单一控件库，而是主程序和上层 UI 模块共用的壳层基础设施：配置、插件装载、菜单、属性编辑器、快捷键、多语言、日志、状态栏和搜索。

按具体控件找源码看 [UI 组件目录](./control-catalog.md)，排查运行时发现链路看 [UI 运行时组件](./ui-runtime-handoff.md)，发布 DLL 看 [UI DLL 发布](./publishing.md)。

## 先怎么定位

| 问题 | 先看 |
| --- | --- |
| 设置没保存、默认值异常 | `ConfigHandler.cs`、`ConfigSetting/`、`Environments.cs` |
| 插件目录存在但功能没出现 | `Plugins/PluginLoader.cs`、`PluginManifest.cs`、`DepsJson.cs` |
| 菜单没有、菜单顺序不对 | `Menus/MenuManager.cs`、`GlobalMenuBase`、`MenuItemBase` |
| PropertyGrid 编辑器不对 | `PropertyEditor/`、`PropertyEditorTypeAttribute` |
| 快捷键冲突或无效 | `HotKey/HotKeys.cs`、`GlobalHotKey/`、`WindowHotKey/` |
| 语言切换不完整 | `Languages/`、资源键、窗口刷新方式 |
| 日志窗口或日志等级异常 | `LogImp/` |
| 状态栏项缺失或不刷新 | `StatusBar/StatusBarManager.cs`、`IStatusBarProvider` |
| 搜索找不到入口 | `Serach/`、`SearchManager`、菜单是否已扫描 |

## 核心职责

| 子系统 | 作用 | 维护边界 |
| --- | --- | --- |
| 配置 | 读取、缓存、保存配置，聚合设置项 | 不负责每个业务配置的含义 |
| 插件装载 | 扫描 `Plugins/`、读取 manifest、检查 `.deps.json`、加载程序集 | 不保证插件业务扩展一定生效 |
| 菜单 | 聚合主程序、UI 模块、插件和项目包菜单 | 菜单存在不等于命令可执行 |
| 属性编辑器 | 根据属性元数据生成编辑界面 | 元数据不足时体验会退化 |
| 快捷键 | 管理全局热键和窗口级热键 | 先区分系统级和窗口内快捷键 |
| 多语言 | 管理 UI Culture 和语言设置编辑 | 旧窗口是否刷新取决于绑定方式 |
| 状态栏 | 聚合 Provider 并刷新主窗口状态 | Provider 创建和更新是两步 |
| 搜索 | 聚合菜单和其他 Provider 的快速入口 | 依赖前面的发现链 |

## 运行时发现链

主程序启动后先初始化 `ConfigHandler` / `Environments`，再由 `PluginLoader` 加载插件程序集。程序集进入 `AssemblyHandler` 后，`MenuManager`、`ConfigSettingManager`、`PropertyEditor`、`StatusBarManager` 和 `SearchManager` 才能扫描到扩展点。排查 UI 入口缺失时，先确认插件或程序集是否进入发现集合，再看具体扩展点。

`MenuManager` 会跳过标记 `Obsolete` 的菜单项和菜单提供者。需要下线入口但暂时保留实现代码时，应标记对应根菜单、静态子项和动态 Provider，避免隐藏父项后仍执行后台数据查询。

## 常见修改

| 需求 | 优先入口 | 同步检查 |
| --- | --- | --- |
| 新增菜单 | `Menus/`、菜单基类、菜单 Provider | 权限、排序、搜索、点击日志 |
| 新增设置项 | `ConfigSettingManager`、`IConfigSettingProvider` | 设置窗口搜索、保存、重启读取 |
| 新增属性编辑器 | `PropertyEditor/`、`PropertyEditorTypeAttribute` | 默认编辑器降级和异常日志 |
| 新增状态栏项 | `IStatusBarProvider`、`StatusBarManager` | 是否创建、是否刷新、点击是否打开窗口 |
| 新增快捷键 | `HotKey/` | 冲突检测、保存后重新注册 |
| 调整插件装载 | `PluginLoader`、manifest、`.deps.json` | README/CHANGELOG、依赖版本提示 |
| 调整语言 | `Languages/`、资源文件 | 新旧窗口文本刷新策略 |

## 发布检查

`ColorVision.UI.csproj` 当前同时面向 `net8.0-windows7.0` 和 `net10.0-windows7.0`，依赖 `ColorVision.Common`、`ColorVision.Themes`、`log4net`、`Newtonsoft.Json`，并生成 NuGet 包和符号包。

发布后至少验证：

| 验证项 | 通过标准 |
| --- | --- |
| 主程序启动 | 无 `MissingMethodException`、`FileLoadException`、资源加载错误 |
| 插件装载 | 能读取 manifest、README、CHANGELOG，并报告依赖版本问题 |
| 菜单刷新 | 主菜单和一个插件菜单能出现并响应 |
| 设置窗口 | 搜索、修改、保存一个安全配置项，重启后仍生效 |
| PropertyGrid | bool、enum、路径、自定义编辑器至少各打开一次 |
| 状态栏 | Socket、Scheduler 或数据库状态项出现并能刷新 |
| 快捷键 | 快捷键设置能打开，冲突项不会重复注册 |
| 日志窗口 | 能按 Error/Warn 和关键词过滤 |

## 故障分流

| 现象 | 第一检查点 |
| --- | --- |
| 插件加载了但菜单没有 | `PluginLoader` 日志、`AssemblyHandler`、`MenuManager` |
| 设置项没有出现 | `IConfigSettingProvider` 或配置标注是否可扫描 |
| 设置修改后丢失 | `ConfigHandler` 保存路径、文件权限、JSON 序列化 |
| 属性编辑器显示成普通文本 | `PropertyEditorTypeAttribute`、编辑器构造函数、属性类型 |
| 菜单点击无响应 | `CanExecute`、权限、目标窗口初始化日志 |
| 状态栏项存在但不刷新 | `IStatusBarProviderUpdatable`、刷新事件、主窗口绑定 |
| 快捷键保存后无效 | `HotkeyService`、热键配置、全局/窗口级注册 |
| 语言切换不完整 | 资源键、窗口是否需要重建、绑定方式 |
| 搜索找不到菜单 | 菜单是否已扫描、`SearchManager` 是否重建索引 |
| 插件依赖版本提示混乱 | 插件 `.deps.json`、主程序根目录 DLL、插件私有 DLL |

## 边界

- `ColorVision.UI` 是壳层基础设施，不要把它当成所有上层能力的唯一入口。
- `PluginLoader` 只负责装载插件程序集；插件能扩展什么，取决于它实现的菜单、设置、服务、模板或结果视图接口。
- 权限基础设施在这里有公共依赖，但细粒度本地 RBAC 主要在 `ColorVision.Solution/Rbac/`。
- 客户业务流程、Engine 设备控制、项目结果导出不要放进这个项目。

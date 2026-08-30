---
knowledge_id: "ui.framework"
knowledge_type: "index"
status: "current"
summary: "ColorVision.UI壳层责任入口：按配置、插件、菜单、热键、搜索、语言、状态栏、属性编辑和日志定位规范主题，业务行为仍归所属模块。"
aliases: ["菜单和属性编辑器在哪个模块", "ColorVision.UI", "壳层基础设施", "ConfigSettingManager"]
code_paths: ["UI/ColorVision.UI/ColorVision.UI.csproj", "UI/ColorVision.UI/ConfigHandler.cs", "UI/ColorVision.UI/Plugins/PluginLoader.cs", "UI/ColorVision.UI/Menus/MenuManager.cs", "UI/ColorVision.UI/PropertyEditor/PropertyEditorHelper.cs", "UI/ColorVision.UI/LogImp", "UI/ColorVision.UI/HotKey", "UI/ColorVision.UI/Languages", "UI/ColorVision.UI/Serach", "UI/ColorVision.UI/StatusBar/StatusBarManager.cs"]
test_paths: []
related: ["ui.index", "ui.configuration", "plugins.model", "ui.property-grid", "ui.discovery", "ui.menus", "ui.hotkeys", "ui.search", "ui.localization", "ui.status-bar", "operations.logs", "ui.desktop"]
---

# ColorVision.UI 壳层责任与知识入口

`UI/ColorVision.UI/` 是 WPF 宿主共用的壳层基础设施，不是所有 UI 功能的所有者。配置对象、程序集、扩展类型和可见控件分别有独立生命周期；界面入口出现不证明业务初始化或持久化成功。

## 从责任找到规范主题

| 责任或问题 | 主要实现 | 规范主题 |
| --- | --- | --- |
| 配置对象来源、保存、重载、备份与发布失败 | `ConfigHandler.cs`、`ConfigSetting/ConfigServiceAdapters.cs` | [配置持久化与重载](./configuration.md) |
| 插件 DLL、manifest、依赖门禁与禁用状态 | `Plugins/PluginLoader.cs`、`PluginLoaderrConfig.cs` | [插件装载与扩展发现](../../02-developer-guide/plugin-development/overview.md) |
| 插件构建、安装目录替换、备份恢复和导出 | `Plugins/PluginUpdater.cs`、`PluginRecoveryBackupService.cs`、`PluginExtractor.cs` | [插件产物与交付](../../02-developer-guide/plugin-development/getting-started.md) |
| 菜单、设置、状态栏、热键与搜索的扩展发现 | `AssemblyHandler.cs` 及各消费者 | [UI 发现链](./ui-runtime-handoff.md) |
| 菜单树、隐藏、命令入口与管理提交 | `Menus/MenuManager.cs`、`UI/ColorVision.UI.Desktop/MenuItemManager/` | [菜单契约](./menus.md) |
| 快捷键定义、设置草稿、注册冲突和释放 | `HotKey/` | [快捷键契约](./hotkeys.md) |
| 搜索候选、刷新缓存、排序和结果执行 | `Serach/`，保留实际目录拼写 | [产品搜索契约](./search.md) |
| 语言资源、系统文化回退与确认重启 | `Languages/`、各模块 Properties 资源 | [界面语言契约](./localization.md) |
| 状态项目、活动文档、绑定刷新和宿主清理 | `StatusBar/`、Common 状态接口及业务 provider | [状态栏契约](./status-bar.md) |
| 属性元数据、编辑器选择、工作副本和提交 | `PropertyEditor/` | [PropertyGrid 编辑契约](./property-grid.md) |
| 日志输出、历史读取、等级与关键词筛选 | `LogImp/` | [日志来源与显示](../../01-user-guide/interface/log-viewer.md) |
| 设置窗口、向导、市场页面与诊断工具 | `UI/ColorVision.UI.Desktop/` | [桌面辅助壳层](./ColorVision.UI.Desktop.md) |

上述正文各自维护当前行为、失败条件和测试，本页不再重复一份“怎么使用”和“怎么开发”的完整说明。按控件/窗口定位可看[组件目录](./control-catalog.md)；跨 DLL 看[组件责任速查](./component-handbook.md)。

## 模块边界

通用接口、命令和基础 ViewModel 主要在 `ColorVision.Common`；主题资源在 `ColorVision.Themes`；细粒度本地 RBAC 在 `ColorVision.Rbac`。Engine 的设备控制、客户包的判定/MES/报表以及 ImageEditor 的图像/overlay 责任不应因有 WPF 窗口就移入此项目。

`ColorVision.UI.csproj` 当前启用 WPF，目标为 `net8.0-windows7.0;net10.0-windows7.0`，依赖 Common、Themes、log4net 和 Newtonsoft.Json，并配置生成 NuGet 与符号包。具体目标和版本仍以项目文件及上级构建属性为准；构建产物不等于已发布。

DLL 交付规则见[UI DLL 发布](./publishing.md)。本入口不登记代表整个壳层成功的泛化测试；配置、加载、菜单、属性编辑与日志各主题仅声明其真实测试范围。运行主程序、改配置、连接设备、调用系统工具或上传包需分别符合任务授权，不能作为阅读本页的前提。

# ColorVision.UI

本页只保留 `UI/ColorVision.UI/` 当前最关键的基础设施和入口类型，不再继续维护旧文档里那种“版本清单 + 全量伪 API + 更新日志”的写法。

## 模块定位

`ColorVision.UI` 不是某一个单独控件，而是桌面应用的大量 UI 基础设施所在位置。它当前承担的角色更接近“UI 壳层和公共服务集合”，主要给主程序、Engine 和其他 UI 子项目复用。

从目录结构看，它覆盖的内容至少包括：

- 配置读写和环境路径
- 插件装载与插件包处理
- 菜单系统
- 属性编辑器
- 快捷键系统
- 多语言资源
- 日志相关 UI 配置
- Shell、搜索、状态栏、页面与通用控件

所以它不是单一“控件库”，也不适合再被写成一个拥有稳定公共 API 面的标准 SDK。

## 当前最关键的目录

如果只是想快速建立认知，建议先看这些目录：

- `Plugins/`：插件发现、元数据、依赖检查、解包与更新
- `PropertyEditor/`：属性编辑窗口、树节点、编辑器类型系统
- `Menus/`：菜单注册和动态菜单刷新
- `HotKey/`：全局和窗口级快捷键
- `Languages/`：语言与资源切换
- `LogImp/`：日志相关配置和窗口状态
- `ConfigSetting/` 与根目录 `ConfigHandler.cs`：配置系统入口
- `Shell/`、`Serach/`、`StatusBar/`：桌面交互辅助能力

## 关键入口类型

### `ConfigHandler`

`ConfigHandler` 是这个项目里最核心的基础设施之一。很多 `IConfig` 配置对象最终都围绕它或相关配置服务完成读取、缓存和保存。

如果问题表现为“设置没保存”“配置没加载”“默认值异常”，通常先看这条链。

### `PluginLoader`

`PluginLoader` 当前负责插件运行时装载。它做的并不只是“扫 DLL”，还包括：

- 扫描 `Plugins/` 目录
- 读取 `manifest.json`
- 解析可选 `.deps.json`
- 检查 `ColorVision.*` 依赖版本
- 最终装载插件程序集

这也是为什么插件相关文档如果只写成“反射扫描插件类型”，通常都会失真。

### `MenuManager`

`MenuManager` 是菜单系统的中心对象。很多动态菜单、最近文件刷新和插件菜单入口，最终都会落到它的注册或刷新链上。

所以这部分更像应用壳层的菜单协调器，而不是一组静态 XAML 菜单定义。

### `PropertyEditor`

`PropertyEditor/` 当前负责属性编辑体验的主链：

- `PropertyEditorWindow`
- `PropertyTreeNode`
- 编辑器类型与辅助类

这一套系统和仓库里大量带 `Category`、`DisplayName`、`Description`、`PropertyEditorType` 这类特性的对象配合使用，是当前动态属性编辑体验的基础。

### `HotKey`

快捷键系统当前不是单点实现，而是分成：

- `GlobalHotKey/`
- `WindowHotKey/`
- `HotKeys` 及其配置与设置窗口

因此改快捷键时，通常要先区分你改的是系统级热键，还是窗口内热键。

### `Languages`

多语言资源和 UI 文化切换相关能力在这里集中管理。主程序启动阶段设置 UI Culture 后，很多界面资源加载都会受这里影响。

### `LogImp`

日志相关的 UI 配置和本地日志窗口状态也放在这个项目里。它更偏“日志显示与配置配套”，不是完整日志后端本身。

## 这个项目当前最容易被写错的地方

### 它不是单一控件库

旧文档喜欢把 `ColorVision.UI` 写成“核心 UI 控件包”。当前代码远比这复杂，它同时承接插件、配置、菜单、快捷键、属性编辑和多语言等横切能力。

### 插件系统不等于扩展点定义本身

`PluginLoader` 位于这里，但插件真正扩展到什么能力，仍取决于各插件程序集和被实现的菜单、模板、服务、结果视图接口。

### 权限不应在这页被泛化为“全局 RBAC 中心”

当前全局粗粒度权限来自 `Authorization.Instance.PermissionMode`，而更细的本地 RBAC 子系统主要位于 `UI/ColorVision.Solution/Rbac/`。`ColorVision.UI` 提供的是授权基础设施和公共依赖，不应该在这里继续写成完整权限平台。

## 当前更适合怎样读这个项目

### 想看配置和全局服务

先看：

- `ConfigHandler.cs`
- `Environments.cs`
- `FileProcessorFactory.cs`

### 想看插件运行时

先看：

- `Plugins/PluginLoader.cs`
- `Plugins/PluginManifest.cs`
- `Plugins/PluginInfo.cs`

### 想看属性编辑体系

先看：

- `PropertyEditor/PropertyEditorWindow.xaml(.cs)`
- `PropertyEditor/PropertyTreeNode.cs`
- `PropertyEditor/PropertyEditors.cs`

### 想看菜单和快捷键

先看：

- `Menus/MenuManager.cs`
- `HotKey/HotKeys.cs`
- `HotKey/GlobalHotKey/`
- `HotKey/WindowHotKey/`

## 这页不再做什么

本页不再继续维护这些高风险内容：

- 过时版本号和目标框架清单
- 大段未经核实的类成员伪代码
- 把 `ColorVision.UI` 说成稳定公共 SDK
- 把权限、插件、日志等横切能力都讲成各自完整平台

如果后续要补某个子系统，应直接落到对应专题页，而不是在这里继续堆“大而全”说明。

## 继续阅读

- [UI组件概览](./README.md)
- [ColorVision.Solution](./ColorVision.Solution.md)
- [安全与权限控制](../../03-architecture/security/overview.md)
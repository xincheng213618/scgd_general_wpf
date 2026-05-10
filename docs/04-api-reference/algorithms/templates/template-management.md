# 模板管理

本页只描述当前仓库里真实可用的模板宿主链，不再继续维护“统一框架蓝图 + 理想化 MVVM 分层 + 大段伪示例”式旧稿。

## 先看这页现在在讲什么

按当前源码状态，模板管理不是单独一个后端服务，而是一条由 `ITemplate` 基类、全局注册表、管理窗口、编辑窗口和创建窗口拼起来的宿主链。它当前负责：

- 启动后扫描并注册具体模板类型。
- 在主程序里按命名空间组织模板入口。
- 提供通用的编辑、创建、导入导出、复制和重命名窗口。
- 让 JSON 模板、流程模板、POI 模板、字典模板等共用一套宿主界面。
- 提供 SQLite 样例库和全局搜索接入。

所以这页真正要讲的，不是“模板理论”，而是主程序现在怎样托管各类模板。

## 当前最关键的文件

- `Engine/ColorVision.Engine/Templates/TemplateContorl.cs`
- `Engine/ColorVision.Engine/Templates/ITemplate.cs`
- `Engine/ColorVision.Engine/Templates/TemplateManagerWindow.xaml.cs`
- `Engine/ColorVision.Engine/Templates/TemplateEditorWindow.xaml.cs`
- `Engine/ColorVision.Engine/Templates/TemplateCreate.xaml.cs`
- `Engine/ColorVision.Engine/Templates/TemplateSearchProvider.cs`
- `Engine/ColorVision.Engine/Templates/TemplateSampleLibrary.cs`
- `Engine/ColorVision.Engine/Templates/TemplateSampleSaveWindow.xaml.cs`

如果只读这几处，已经足够建立当前模板系统的主心智模型。

## 当前主链怎么跑

### 初始化与注册

`TemplateInitializer` 启动后会触发 `TemplateControl.GetInstance()`；`TemplateControl` 再扫描程序集里所有 `IITemplateLoad` 实现并执行 `Load()`。

另一方面，`ITemplate` 构造函数本身也会把模板实例异步注册进 `TemplateControl.ITemplateNames`。因此当前模板发现是两层机制并行工作的：

- 模板对象构造时进全局注册表。
- 具体模板加载器在 MySQL 可用后刷新内容。

这就是为什么很多模板页不能脱离初始化和数据库前提来理解。

### 模板管理窗口

`MenuTemplateManagerWindow` 会打开 `TemplateManagerWindow`。这个窗口当前不是简单列表，而是：

- 读取 `TemplateControl.ITemplateNames`
- 按类型命名空间分组
- 支持搜索和筛选
- 支持按卡片方式显示模板
- 在选中模板后直接打开对应编辑器

因此它承担的是“模板入口聚合器”角色，不只是一个菜单弹窗。

### 模板编辑窗口

`TemplateEditorWindow` 是当前最通用的模板宿主窗口。它会先 `template.Load()`，然后根据模板类型走两条路径：

- 普通模板：右侧放 `PropertyGrid`
- 自定义模板：调用 `GetUserControl()` 并让模板自己接管右侧区域

窗口还统一接好了：

- 新建、复制、保存、删除命令
- 选中项切换时的 `SetSaveIndex(...)`
- `SetUserControlDataContext(...)` 或 `GetParamValue(...)`
- 列排序、搜索和双击行为

这也是当前各种模板虽然界面差异很大，但仍能共用同一个宿主壳的原因。

### 模板创建窗口

`TemplateCreate` 现在已经不是“只给一个名称输入框”的窗口了。按当前实现，它会为新模板提供多种来源：

- 系统默认模板
- 当前副本（复制后暂存的模板内容）
- SQLite 样例库中的历史样例

这些来源会被渲染成卡片，并按组过滤。最终由 `ApplyTemplateSource(...)` 把选中的来源注入到待创建模板里。

这说明当前模板创建链已经不只是“CreateDefault() + 手填参数”。

### 搜索与样例库

`TemplateSearchProvider` 会把所有模板名注册到全局搜索入口；`TemplateSampleLibrary` 则把模板样例存到用户文档目录下的 SQLite 库：

- `.../Templates/TemplateSamples.db`

它当前保存的是：

- 模板代码与模板类型
- 分组名与样例名
- 描述文本
- 序列化后的模板内容

所以模板管理现在除了 MySQL 主存储之外，还有一条本地样例复用链。

## 当前几个最容易写错的点

### 它不是纯服务层系统

当前很多关键逻辑都直接写在 `TemplateManagerWindow`、`TemplateEditorWindow`、`TemplateCreate` 这些 WPF 窗口里。继续把它描述成“宿主只绑定 ViewModel，逻辑都在服务层”，和真实代码不符。

### 不同模板的持久化方式并不统一

有些模板主要依赖 MySQL，有些模板支持文件导入导出，有些模板还会额外走 SQLite 样例库。文档不能再假设所有模板都是同一种存储模型。

### `IsUserControl` 和 `IsSideHide` 会显著改变行为

当前模板宿主不是固定布局。`IsUserControl` 会把右侧改成交给模板自定义控件，`IsSideHide` 甚至会改变窗口布局与双击行为。忽略这两个开关，会解释不通很多模板页。

### 模板注册和数据库连接仍然耦合

虽然 `ITemplate` 构造会注册实例，但许多具体模板内容仍然要等 MySQL 连接后才能真正加载。把模板系统写成“纯本地静态注册”会遗漏关键前提。

## 推荐阅读顺序

1. `Engine/ColorVision.Engine/Templates/ITemplate.cs`
2. `Engine/ColorVision.Engine/Templates/TemplateContorl.cs`
3. `Engine/ColorVision.Engine/Templates/TemplateManagerWindow.xaml.cs`
4. `Engine/ColorVision.Engine/Templates/TemplateEditorWindow.xaml.cs`
5. `Engine/ColorVision.Engine/Templates/TemplateCreate.xaml.cs`
6. `Engine/ColorVision.Engine/Templates/TemplateSearchProvider.cs`
7. `Engine/ColorVision.Engine/Templates/TemplateSampleLibrary.cs`

## 继续阅读

- [JSON 模板](./json-templates.md)
- [流程引擎](./flow-engine.md)
- [Templates 分析总结](../../../03-architecture/components/templates/analysis.md)
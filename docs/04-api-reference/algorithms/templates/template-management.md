# 模板管理

本页只描述当前仓库里真实可用的模板宿主链，不再继续维护“统一框架蓝图 + 理想化 MVVM 分层 + 大段伪示例”式旧稿。

## 先记住

模板管理不是单独后端服务，而是由 `ITemplate`、`TemplateControl`、管理窗口、编辑窗口和创建窗口组成的 WPF 宿主链。它负责扫描注册模板、按命名空间组织入口、提供编辑/创建/导入/导出/复制/重命名窗口，并接入 SQLite 样例库和全局搜索。

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

## 当前主链

| 环节 | 当前行为 |
| --- | --- |
| 初始化与注册 | `TemplateInitializer` 触发 `TemplateControl.GetInstance()`；`TemplateControl` 扫描 `IITemplateLoad` 并执行 `Load()`；`ITemplate` 构造时也会把实例注册到 `ITemplateNames` |
| 管理窗口 | `TemplateManagerWindow` 读取注册表，按命名空间分组，支持搜索、筛选、卡片展示和直接打开编辑器 |
| 编辑窗口 | `TemplateEditorWindow` 先 `template.Load()`；普通模板显示 `PropertyGrid`，自定义模板调用 `GetUserControl()` |
| 创建窗口 | `TemplateCreate` 可从系统默认模板、当前副本和 SQLite 样例库创建新模板，再由 `ApplyTemplateSource(...)` 注入内容 |
| 搜索与样例 | `TemplateSearchProvider` 接入全局搜索；`TemplateSampleLibrary` 把样例保存到 `.../Templates/TemplateSamples.db` |

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

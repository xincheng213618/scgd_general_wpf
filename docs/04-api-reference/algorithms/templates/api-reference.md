# Templates API 参考

本页只保留当前源码里相对稳定的模板宿主入口。模板行为大量依赖具体子类覆写、数据库状态和用户控件挂接，不维护完整签名手册。

## 先查什么

| 现象 | 第一检查点 |
| --- | --- |
| 模板类存在但列表没有 | 是否实现 `IITemplateLoad`，是否在 `Load()` 中进入 `TemplateControl` 注册表 |
| 新建模板默认值不对 | `TemplateDicId`、字典模板、`CreateDefault()` |
| 保存/删除失败 | 具体模板的 `Save()` / `Delete()` 覆写和数据库记录 |
| UI 右侧不是预期控件 | `IsUserControl`、`GetUserControl()`、`TemplateEditorWindow` 分支 |
| JSON 模板导入导出异常 | `ITemplateJson<T>`、`.cfg` / ZIP、`ModMasterModel.JsonVal` |
| 普通模板参数不刷新 | `TemplateModel<T>`、`ParamModBase`、属性通知 |

## 稳定入口

| 类型 | 当前职责 |
| --- | --- |
| `ITemplate` | 具体基类，包含注册、生命周期钩子、列表访问、创建入口和宿主窗口行为 |
| `ITemplate<T>` | 普通参数模板基类，统一 `TemplateParams`、`ItemsSource`、索引、名称和参数读取 |
| `ITemplateJson<T>` | JSON 模板基类，围绕 `ModMasterModel.JsonVal` 加载、保存、复制、导入导出 |
| `TemplateControl` | 模板注册表，维护 `ITemplateNames`，扫描 `IITemplateLoad` |
| `IITemplateLoad` | 模板加载扩展点，供初始化时调用具体模板的 `Load()` |
| `ParamBase` | 最薄参数基类，提供 `Id`、`Name` |
| `ModelBase` | 把 `ModDetailModel` 映射为参数字典，并提供类型转换 |
| `ParamModBase` | 组合模板主记录和参数明细，是多数数据库模板参数基类 |
| `TemplateModel<T>` | 列表项包装，带 `Key`、`Value`、选择/编辑状态和右键命令 |
| `TemplateEditorWindow` | 通用模板编辑宿主，统一新建、复制、保存、删除、重命名、搜索、排序 |
| `TemplateCreate` | 模板创建来源选择，支持默认、当前副本、SQLite 样例库 |

## 普通模板与 JSON 模板

| 分支 | 主存储 | 默认值来源 | 编辑/导入导出特点 |
| --- | --- | --- | --- |
| `ITemplate<T>` | 模板主表 + 明细表 | `TemplateDicId` 对应字典模板 | 参数对象通常继承 `ParamModBase`，多由属性面板编辑 |
| `ITemplateJson<T>` | `ModMasterModel.JsonVal` | `SysDictionaryModModel.JsonVal` | `.cfg` / ZIP 导入导出，复制基于 JSON 序列化副本 |

如果模板内容本质是 JSON 文本，优先看 `ITemplateJson<T>`；如果是强类型参数和明细字段，优先看 `ITemplate<T>`。

## UI 宿主边界

| 入口 | 当前行为 |
| --- | --- |
| `TemplateEditorWindow` | 根据 `IsUserControl` 在 `PropertyGrid` 和自定义 `UserControl` 之间切换 |
| `TemplateModel<T>` | 列表中显示的不是裸参数对象，而是带 UI 状态的包装对象 |
| `TemplateCreate` | 不只是输入名称，还处理默认值、当前副本和样例库来源 |

模板系统当前没有完全把数据模型和 UI 状态剥离。解释 API 时要保留这个现实边界。

## 不要这样理解

- `ITemplate` 不是纯接口，它包含大量默认行为。
- 基类有方法名不代表所有模板都完整支持导入、导出、创建默认值或自定义控件。
- JSON 模板和普通参数模板不是同一条持久化路径。
- 不要把某个模板的字段当成全模板系统统一规范。

## 关键文件

| 任务 | 先看 |
| --- | --- |
| 模板基类 | `Engine/ColorVision.Engine/Templates/ITemplate.cs` |
| JSON 分支 | `Engine/ColorVision.Engine/Templates/Jsons/ITemplateJson.cs` |
| 参数映射 | `ModelBase.cs`、`ParamModBase.cs` |
| 列表项包装 | `TemplateModel.cs` |
| 编辑窗口 | `TemplateEditorWindow.xaml.cs` |
| 创建窗口 | `TemplateCreate.xaml.cs` |
| 注册入口 | `TemplateContorl.cs` |

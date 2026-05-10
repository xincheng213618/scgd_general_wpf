# Templates API 参考

本页只保留当前源码里比较稳定的模板宿主入口，不再试图维护“完整签名手册”。原因很直接：很多模板行为依赖具体子类覆写、数据库状态和用户控件挂接，旧式 API 表很容易漂移。

## 先看哪些入口最值得认识

按当前代码，模板系统里最稳定、最值得优先理解的是这几类类型：

- `ITemplate`
- `ITemplate<T>`
- `ITemplateJson<T>`
- `TemplateControl` / `IITemplateLoad`
- `ParamBase` / `ModelBase` / `ParamModBase`
- `TemplateModel<T>`
- `TemplateEditorWindow` / `TemplateCreate`

这页的重点是说明这些入口在当前实现里分别承担什么职责。

## 核心宿主类型

### ITemplate

`ITemplate` 是所有模板的宿主基类。当前最重要的职责包括：

- 在构造时把自己注册到 `TemplateControl.ITemplateNames`
- 提供 `Load()`、`Save()`、`Import()`、`Export()`、`Delete()`、`Create()` 等生命周期钩子
- 暴露 `ItemsSource`、`Count`、`GetValue(...)`、`GetParamValue(...)`
- 控制宿主窗口行为，如 `IsSideHide`、`IsUserControl`
- 为创建窗口提供 `HasCreateTemplateSource`、`ImportName`、`CreateDefault()` 等来源能力

需要特别注意的是：`ITemplate` 当前是一个具体基类，不只是接口定义。

### `ITemplate<T>`

`ITemplate<T>` 是普通参数模板最常见的泛型基类，其中 `T : ParamModBase, new()`。它当前主要把：

- `ObservableCollection<TemplateModel<T>> TemplateParams`
- `ItemsSource`
- `Count`
- `GetTemplateNames()`
- `GetTemplateIndex(...)`
- `GetParamValue(...)`

这些常规列表行为统一起来。

此外，它还负责根据 `TemplateDicId` 从字典模板生成默认参数对象，所以这层并不只是一个简单集合包装器。

### `ITemplateJson<T>`

`ITemplateJson<T>` 是 JSON 模板分支的宿主基类，其中 `T : TemplateJsonParam, new()`。它和 `ITemplate<T>` 的主要差异在于：

- 数据源是 `ModMasterModel.JsonVal`
- 创建默认值时走 `SysDictionaryModModel.JsonVal`
- 导入导出围绕 `.cfg` 和 ZIP
- 复制逻辑基于 JSON 序列化副本

如果模板内容本质是 JSON 文本，这层通常比 `ITemplate<T>` 更接近真实实现。

## 注册与发现入口

### TemplateControl

`TemplateControl` 是当前模板注册表。它主要维护：

- `ITemplateNames`
- `AddITemplateInstance(...)`
- `ExitsTemplateName(...)`
- `FindDuplicateTemplate(...)`

并在初始化时扫描所有 `IITemplateLoad` 实现，以便让具体模板类型自己装载内容。

### IITemplateLoad

`IITemplateLoad` 是模板加载扩展点。当前很多模板类都会实现它，以便在 `TemplateControl.Init()` 扫描时执行自己的 `Load()`。

这也是当前模板系统和应用启动顺序耦合的重要原因之一。

## 参数与模型基类

### ParamBase

`ParamBase` 是最薄的一层，只提供：

- `Id`
- `Name`

它适合做所有模板参数对象的共同父类。

### ModelBase

`ModelBase` 在当前实现里的价值比名字更具体。它会把 `ModDetailModel` 列表映射成按符号名索引的参数字典，并提供：

- `GetValue<T>(...)`
- `SetProperty(...)`
- `GetParameter(...)`
- `GetDetail(...)`
- `StringToDoubleArray(...)`
- `DoubleArrayToString(...)`

也就是说，很多模板参数属性之所以能像普通 C# 属性一样写，底层其实是这层在做字典映射和类型转换。

### ParamModBase

`ParamModBase` 继续往上，把模板主记录和参数细节记录组合起来，是大多数数据库驱动模板参数对象的直接基类。

## UI 宿主相关类型

### `TemplateModel<T>`

`TemplateModel<T>` 是当前列表项包装对象。除了 `Value` 之外，它还承担：

- `Key`
- `IsSelected`
- `IsEditMode`
- 右键菜单
- 重命名和复制名称命令

因此列表里用户看到的“模板项”并不是裸参数对象，而是这层带 UI 状态的包装。

### TemplateEditorWindow

`TemplateEditorWindow` 是最通用的模板编辑宿主。它会根据模板是否为 `IsUserControl` 决定右侧显示：

- `PropertyGrid`
- 模板自定义 `UserControl`

同时统一接管新建、复制、保存、删除、重命名、搜索、排序和选中切换。

### TemplateCreate

`TemplateCreate` 当前负责模板创建来源选择。除了默认模板，它还支持：

- 当前副本
- SQLite 样例库中的样例

所以它已经不是一个只负责输入模板名称的小弹窗。

## 当前几个最容易写错的点

### `ITemplate` 不是纯接口

当前很多默认行为直接写在 `ITemplate` 这个基类里，包括注册、创建窗口和多种生命周期方法。把它写成纯抽象契约会误导读者。

### 很多行为只有在具体模板覆写后才成立

例如 `Import()`、`Export()`、`CreateDefault()`、`GetUserControl()` 等方法，在基类里未必有完整实现。不能把基类方法表直接当成“所有模板都完全支持的功能清单”。

### 数据模型和 UI 模型是混合的

`TemplateModel<T>`、`TemplateEditorWindow`、`TemplateCreate` 这些类型说明当前模板系统并没有把 UI 状态完全剥离出去。API 解释时必须保留这个现实边界。

### JSON 模板和普通参数模板是两条宿主分支

虽然它们都归在 Templates 下，但 `ITemplate<T>` 与 `ITemplateJson<T>` 的默认持久化、创建和导入导出路径并不相同。

## 推荐阅读顺序

1. `Engine/ColorVision.Engine/Templates/ITemplate.cs`
2. `Engine/ColorVision.Engine/Templates/Jsons/ITemplateJson.cs`
3. `Engine/ColorVision.Engine/Templates/ModelBase.cs`
4. `Engine/ColorVision.Engine/Templates/ParamModBase.cs`
5. `Engine/ColorVision.Engine/Templates/TemplateModel.cs`
6. `Engine/ColorVision.Engine/Templates/TemplateEditorWindow.xaml.cs`
7. `Engine/ColorVision.Engine/Templates/TemplateCreate.xaml.cs`

## 继续阅读

- [模板管理](./template-management.md)
- [JSON 模板](./json-templates.md)
- [流程引擎](./flow-engine.md)
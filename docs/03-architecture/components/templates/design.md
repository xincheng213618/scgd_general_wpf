# Templates 架构设计

`Engine/ColorVision.Engine/Templates/` 是模板注册、编辑、持久化和消费的混合系统。它不只是算法模板目录，也不是严格三层架构；重点是模板如何在运行时出现、被编辑、被流程节点和业务功能消费。

## 先查什么

| 现象 | 第一检查点 |
| --- | --- |
| 模板没出现 | 程序集是否加载、MySQL 是否就绪、`TemplateControl` 是否扫描到 `IITemplateLoad` |
| 模板重名/复制异常 | `TemplateControl.ExitsTemplateName(...)`、`FindDuplicateTemplate(...)` |
| 编辑窗口右侧不对 | `IsUserControl`、`GetUserControl()`、`TemplateEditorWindow` |
| 节点属性里选不到模板 | 模板是否已注册，`NodeConfigurator` / `NodePanelBuilder` 是否刷新列表 |
| Flow 模板导入后引用错 | `.cvflow` 关联模板、重名映射、STN 引用替换 |

## 核心对象

| 对象 | 作用 |
| --- | --- |
| `ITemplate` | 带运行时职责的基类，包含元数据、列表访问、生命周期、创建和自定义控件入口 |
| `ITemplate<T>` | 面向 `ParamModBase` 的普通参数模板基类，统一列表、名称、索引和默认模板创建 |
| `ITemplateJson<T>` | 面向 `ModMasterModel.JsonVal` 的 JSON 模板分支 |
| `TemplateControl` | 模板注册中心，维护 `ITemplateNames`，扫描 `IITemplateLoad` |
| `IITemplateLoad` | 模板加载扩展点，具体模板在 `Load()` 中装载数据 |
| `TemplateManagerWindow` | 按运行时注册结果和命名空间分组展示模板 |
| `TemplateEditorWindow` | 通用编辑宿主，接入保存、删除、导入导出、自定义面板 |
| `TemplateFlow` | 特殊模板，既是流程模板数据，也是流程图编辑/导入导出入口 |

## 初始化链

1. 主程序和插件把相关程序集加载到进程。
2. `TemplateInitializer` 等待 MySQL 初始化后触发 `TemplateControl.GetInstance()`。
3. `TemplateControl` 扫描已加载程序集中的 `IITemplateLoad`。
4. 各模板类型在 `Load()` 中读取数据库或资源，并装入内存集合。
5. 模板管理窗口、编辑窗口、流程节点配置器消费这些注册实例。

当前没有独立模板清单文件，也没有统一 DI 容器声明模板。

## UI 和持久化

| 区域 | 当前设计 |
| --- | --- |
| 管理窗口 | 从 `TemplateControl.ITemplateNames` 读取，按命名空间分组，提供搜索和筛选 |
| 编辑窗口 | 多数模板进入 `TemplateEditorWindow`，右侧显示 PropertyGrid 或自定义控件 |
| 节点配置 | `NodePanelBuilder` 可从流程节点属性面板直接打开模板编辑 |
| 持久化 | 具体模板常直接用 SqlSugar 读写 `ModMasterModel`、`ModDetailModel`、`SysResourceModel` |

这说明模板逻辑、编辑 UI 和数据库访问贴得较近。写文档时不要硬套标准仓储层或纯 DTO 模型。

## Flow 特殊性

| 特点 | 说明 |
| --- | --- |
| 模板代码 | `TemplateFlow` 的模板代码固定为 `flow` |
| 编辑方式 | 双击预览直接打开 `FlowEngineToolWindow` |
| 导入格式 | 支持 `.stn` 和 `.cvflow` |
| 关联模板 | `.cvflow` 导入会处理关联模板和流程图中的模板引用 |

Flow 既是模板，又是流程图载体，不能和普通参数模板完全等同看待。

## 设计边界

- 模板能否出现强依赖运行时加载链和数据库连接。
- UI 状态和模板逻辑没有完全隔离。
- `ARVR/`、`POI/`、`Jsons/`、`Flow/` 是共用基础设施的业务模板族，不是整齐统一的单一模型。
- 不再维护基于文件数量、目录数量或理想分层图的静态说明。

## 关键文件

| 任务 | 先看 |
| --- | --- |
| 模板基类 | `ITemplate.cs` |
| 注册中心 | `TemplateContorl.cs`、`TemplateInitializer` |
| 管理窗口 | `TemplateManagerWindow.xaml.cs` |
| 编辑窗口 | `TemplateEditorWindow.xaml.cs` |
| Flow 模板 | `Flow/TemplateFlow.cs` |
| 节点模板入口 | `Flow/NodeConfigurator/NodePanelBuilder.cs` |

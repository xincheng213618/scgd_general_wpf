# Templates 架构设计

`Engine/ColorVision.Engine/Templates/` 是模板注册、编辑、持久化和消费的混合系统。它不只是算法模板目录，也不是严格三层架构；重点是模板如何在运行时出现、被编辑、被流程节点和业务功能消费。

## 先查什么

| 现象 | 第一检查点 |
| --- | --- |
| 模板没出现 | 程序集是否加载、MySQL 是否就绪、`TemplateControl` 是否扫描到 `IITemplateLoad` |
| 模板重名/复制异常 | `TemplateControl.ExitsTemplateName(...)`、`FindDuplicateTemplate(...)` |
| 编辑窗口右侧不对 | `IsUserControl`、`GetUserControl()`、`TemplateEditorWindow` |
| 节点属性里选不到模板 | 模板是否已注册；常规字段检查 `FlowNodePropertyEditorAttribute` 和 `FlowPropertyEditorRegistry`，补充面板检查 `NodeConfiguration/` |
| Flow 模板保存冲突 | `FlowTemplateSaveCondition`、已加载内容哈希和 `FlowTemplateConcurrencyException` |
| Flow 模板导入后引用错 | `.cvflow` manifest、关联模板重名映射和 STN 引用改写 |

## 核心对象

| 对象 | 作用 |
| --- | --- |
| `ITemplate` | 带运行时职责的基类，包含元数据、列表访问、生命周期、创建和自定义控件入口 |
| `ITemplate<T>` | 面向 `ParamModBase` 的普通参数模板基类，统一列表、名称、索引和默认模板创建 |
| `ITemplateJson<T>` | 面向 `ModMasterModel.JsonVal` 的 JSON 模板分支 |
| `TemplateControl` | 模板注册中心，维护 `ITemplateNames`，扫描 `IITemplateLoad` |
| `IITemplateLoad` | 模板加载扩展点，具体模板在 `Load()` 中装载数据 |
| `TemplateEditorWindow` | 通用模板目录与编辑宿主，接入搜索、排序、创建、复制、重命名、删除、保存、导入导出和自定义面板 |
| `TemplateCreateView` | 统一处理默认值、已有模板副本、已准备内容和文件导入等创建来源，并提供创建前预览 |
| `TemplateFlow` | 特殊的流程模板目录与持久化适配器；活画布编辑和执行不在该类中 |

## 初始化链

1. 主程序和插件把相关程序集加载到进程。
2. `TemplateInitializer` 等待 MySQL 初始化后触发 `TemplateControl.GetInstance()`。
3. `TemplateControl` 扫描已加载程序集中的 `IITemplateLoad`。
4. 各模板类型在 `Load()` 中读取数据库或资源，并装入内存集合。
5. 模板编辑窗口、各模板菜单入口、Flow 节点属性编辑器和补充配置器消费这些注册实例。

当前没有独立模板清单文件，也没有统一 DI 容器声明模板。

## UI 和持久化

| 区域 | 当前设计 |
| --- | --- |
| 编辑窗口 | 多数模板进入 `TemplateEditorWindow`；普通模板在右侧 PropertyGrid 编辑，`IsUserControl` 模板使用自定义控件 |
| 新建模板 | `TemplateCreateView` 统一准备创建来源和预览，最终调用 `ITemplate.TryCreateTemplate(...)` |
| 常规节点属性 | 节点上的 `FlowNodePropertyEditorAttribute` 经过 `FlowNodePropertyMetadataProvider` 和 `FlowPropertyEditorRegistry` 选择编辑器 |
| 节点补充面板 | `FlowProcessing/Editor/NodeConfiguration/` 处理多模板族或随算法类型变化的选择器；`NodePanelBuilder` 可打开通用模板编辑窗口 |
| 持久化 | 具体模板常直接用 SqlSugar 读写 `ModMasterModel`、`ModDetailModel`、`SysResourceModel` |

这说明模板逻辑、编辑 UI 和数据库访问贴得较近。写文档时不要硬套标准仓储层或纯 DTO 模型。

## Flow 特殊性

| 特点 | 说明 |
| --- | --- |
| 模板目录 | `TemplateFlow.Code` 固定为 `flow`，且 `IsSideHide = true`；通用模板窗口只显示流程列表 |
| 画布数据 | `FlowParam.DataBase64` 保存 STND 画布；FlowKey、资源身份、revision/hash 是运行时状态，不作为普通模板字段序列化 |
| 编辑边界 | 双击流程后，`FlowEngineToolWindow` 只承载 standalone `ViewFlow`；活画布、检查器、节点菜单、布局和保存前校验属于 `FlowProcessing/Editor` 与 `ViewFlow` |
| 数据库保存 | `ViewFlow.TrySave()` 先校验画布，再取 STN、转为 Base64 并调用 `TemplateFlow.Save2DB(...)` |
| 并发控制 | `Save2DB(...)` 在同一事务中写主表、明细和资源，以 `FOR UPDATE` 锁定已有资源行，并用 expected/loaded content hash 阻止静默覆盖 |
| 流程包 | `.cvflow` 由 `FlowPackageHelper` 校验、收集关联模板、处理重名映射并改写 STN 及二级模板引用 |
| 本地文档 | 独立 `ViewFlow` 可以打开和保存 `.stn`；不要把它写成当前 `TemplateFlow.ImportFile(...)` 的可靠模板导入路径 |
| 版本与搜索 | MySQL/STN 保存成功后，`FlowCanvasCatalogBuilder` 和 `FlowCatalogService` 更新本地版本/搜索侧车；侧车失败只降级记录，不回滚兼容主存储 |

Flow 是持久模板和 STND 画布数据的载体；`FlowProcessing/Editor` 与 `FlowProcessing/Runtime` 是它的消费者。不要再把 `TemplateFlow` 描述为画布编辑器或运行控制器。

## 设计边界

- 模板能否出现强依赖运行时加载链和数据库连接。
- UI 状态和模板逻辑没有完全隔离。
- Flow 的数据库/STN 保存是兼容主存储，版本与搜索侧车是可重建投影，不能反过来决定主保存是否成功。
- Flow 节点常规字段优先走统一属性元数据和编辑器注册；只有节点类型级补充选择器才放进 `NodeConfiguration/`。
- `Templates/Flow/` 不拥有活画布、运行会话、批次、前后处理或无界面执行。
- `ARVR/`、`POI/`、`Jsons/`、`Flow/` 是共用基础设施的业务模板族，不是整齐统一的单一模型。
- 不再维护基于文件数量、目录数量或理想分层图的静态说明。

## 关键文件

| 任务 | 先看 |
| --- | --- |
| 模板基类 | `Engine/ColorVision.Engine/Templates/ITemplate.cs` |
| 注册与初始化 | `Engine/ColorVision.Engine/Templates/TemplateControl.cs`（包含 `TemplateInitializer`） |
| 通用编辑与创建 | `Engine/ColorVision.Engine/Templates/TemplateEditorWindow.xaml.cs`、`Engine/ColorVision.Engine/Templates/TemplateCreateView.xaml.cs` |
| Flow 模型与持久化 | `Engine/ColorVision.Engine/Templates/Flow/TemplateFlow.cs`、`Engine/ColorVision.Engine/Templates/Flow/FlowParam.cs`、`Engine/ColorVision.Engine/Templates/Flow/FlowTemplateSaveCondition.cs` |
| `.cvflow` 包 | `Engine/ColorVision.Engine/Templates/Flow/FlowPackageHelper.cs`、`Engine/ColorVision.Engine/Templates/Flow/FlowPackageStnValidator.cs` |
| 版本与搜索侧车 | `Engine/ColorVision.Engine/Templates/Flow/Versioning/FlowCatalogService.cs`、`Engine/ColorVision.Engine/FlowProcessing/Compilation/FlowCanvasCatalogBuilder.cs` |
| Flow 编辑工作区 | `Engine/ColorVision.Engine/FlowProcessing/Runtime/ViewFlow.xaml.cs`、`Engine/ColorVision.Engine/FlowProcessing/Editor/FlowEditorCanvas.xaml.cs` |
| 节点属性基础契约 | `Engine/FlowEngineLib/PropertyEditor/FlowNodePropertyEditors.cs`、`Engine/ColorVision.Engine/PropertyEditor/FlowNodePropertyEditorRegistration.cs` |
| 节点补充配置 | `Engine/ColorVision.Engine/FlowProcessing/Editor/NodeConfiguration/NodeConfiguratorRegistry.cs`、`Engine/ColorVision.Engine/FlowProcessing/Editor/NodeConfiguration/NodePanelBuilder.cs` |

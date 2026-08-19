# 流程引擎

本页聚焦主程序宿主层：`Engine/ColorVision.Engine/Templates/Flow/` 管理流程模板和 `.cvflow` 包，`Engine/ColorVision.Engine/FlowProcessing/` 管理编辑、运行、调度与诊断；节点执行语义与运行时基类归 `FlowEngineLib`。

先看 [流程引擎架构](../../../03-architecture/components/engine/flow-engine.md)、[FlowEngineLib](../../engine-components/FlowEngineLib.md) 和 [模板到运行链](../../engine-components/template-flow-chain.md)；新增节点则从 [Flow 节点扩展](../../extensions/flow-node.md) 进入。

## 先查什么

| 现象 | 第一检查点 |
| --- | --- |
| 流程模板列表为空 | MySQL 主表、资源表、`TemplateFlow.Load()` |
| 打开流程后节点图为空 | `SysResourceModel.Value`、`ModDetailModel.ValueA`、Base64 数据 |
| 保存后重开没变化 | `ViewFlow.TrySave()` / `SaveStandaloneDocument()`、`FlowValidator.Validate(...)`、`DataBase64`、资源 `Type = 101` |
| 节点属性没有设备/模板下拉 | `FlowEditorCanvas`、`NodeConfiguratorRegistry`、`FlowProcessing/Editor/NodeConfiguration/`、服务和模板列表 |
| `.cvflow` 导入后模板名不对 | `manifest.json`、重名映射、`ReplaceTemplateNames(...)` |
| 调度能触发但最终结果没回去 | `FlowJob` / `FlowExecutionCoordinator`、`RunFlowAndWaitForFinalizationAsync()`、`RunFinalized` |
| 运行失败需要人工处置 | `FlowIncidentManagementWindow`、Run/Event/Attempt 关联记录 |

## 存储边界

| 场景 | 当前行为 |
| --- | --- |
| 主存储 | MySQL 主表 + 明细表 + `SysResourceModel.Value` 保存 Base64 节点图 |
| 本地 `.stn` | 打开本地文件时保存只写回文件，不更新数据库模板 |
| 数据库流程 | `ViewFlow.TrySave()` 先调用 `FlowValidator.Validate(...)`，再取画布数据、Base64、`TemplateFlow.Save2DB(...)` |
| 资源引用 | `SysResourceModel.Type = 101`，`ModDetailModel.ValueA` 保存资源 id |
| 多选导出 | 仍是 zip 内多个 `.stn`，不会像 `.cvflow` 一样收集关联模板 manifest |
| 版本和搜索侧车 | 每次有效保存由 `FlowCanvasCatalogBuilder` 生成语义投影，并记录不可变 catalog revision 和搜索索引 |
| 历史 Artifact 表 | 当前版本不再读写或迁移，已有表和数据原样保留 |

## `.cvflow` 包

| 包内文件 | 作用 |
| --- | --- |
| `flow.stn` | 原样保存的 STND v1 画布二进制数据；`.cvflow` 升级不会修改其格式 |
| `manifest.json` | 包版本、流程哈希和关联模板元数据 |
| `templates/<sha256>.json` | v3 起按内容哈希存放的关联模板载荷，相同载荷在包内只保存一次 |

导出时 `FlowPackageHelper.CollectTemplatesForExport(...)` 会扫描 STN 里的模板引用属性，如 `TempName`、`POITempName`、`SavePOITempName`、`OutputTemplateName`、`ModelName` 等，并继续扫描模板内容里的二级引用。

`.cvflow` v3 是 ColorVision 自有的可演进包格式：`flow.stn` 和每个模板载荷都有 SHA-256 校验，导入还会有上限地完整解压并验证 STND v1；未知的未来大版本会明确拒绝，v1/v2 的 manifest 内联模板仍可导入。

导入时先校验完整包，再以“模板类型 + 规范化有效内容”匹配本地模板：同名同内容直接复用，异名同内容映射到已有模板，同名不同内容才创建带流程名的冲突副本；重复导入同一个包会复用第一次产生的副本。发生名称映射后，会同时改写关联模板的二级引用和 STN 节点引用，再把最终 STN 转成 Base64 作为新流程模板内容。

## 运行链路

| 入口 | 当前链路 | 维护重点 |
| --- | --- | --- |
| UI 手动运行 | `ViewFlow` -> `FlowExecutionSession.RunFlowAsync()` -> `FlowControl.TryStartAsync(...)` | 创建 `MeasureBatchModel`，绑定 `FlowCompleted` |
| 等待引擎结束 | `FlowEngineManager.RunFlowAsync()` | 只等待流程图执行结束，不代表后处理成功 |
| 等待最终结果 | `FlowEngineManager.RunFlowAndWaitForFinalizationAsync()` | 给调度、自动化或外部调用等待引擎和全部后处理结束 |
| Quartz 调度 | `FlowJob.Execute(...)` -> `FlowExecutionCoordinator` -> `RunSelectedFlowAndWaitForFinalizationAsync()` | 通过 `Application.Current.Dispatcher` 切回 UI，并以最终结果决定任务状态 |
| 独立调度 | `HeadlessFlowJob` -> `RunSavedFlowHeadlessAsync(...)` | 复制当前已保存 STN，在隔离 RuntimeHost 中执行，不触碰编辑器 |
| 停止流程 | `ViewFlow` -> `FlowExecutionSession.StopFlow()` -> `FlowControl.Stop()` | 批次状态更新为 `Canceled` |

`EngineExecutionCompleted` 表示“流程图引擎已结束”，此时后处理可能仍在运行；原有 `FlowExecutionCompleted` 作为它的过时兼容别名保留，不能作为整次业务运行成功的依据。引擎结束后会执行配置的后处理；后处理分为 `Warning` 和 `Required`，其中必需后处理失败会把最终结果判为失败。外部调用、Quartz 调度和自动化应等待 `RunFinalized`，或直接调用 `RunFlowAndWaitForFinalizationAsync()` 取得 `FlowRunFinalizedData.FinalOutcome`。`DisplayFlow` 只负责主程序视图注册、选中状态和服务重启。

部分现有 `Projects/*` 窗口仍直接创建 `FlowControl`、监听 `FlowCompleted`，再调用项目自己的最终化或 `Processing`；这些入口尚未接入共享 `RunFinalized`，维护时应按项目链单独核对。

UI 手动运行始终执行当前画布加载的 STN，不再读取或校验 Artifact。独立调度按 FlowKey 取得当前已保存 STN，并在创建请求时复制二进制数据，后续编辑不会改变已经启动的这次执行。

## 工作区与运行对象

`FlowTemplateWorkspaceController` 只保存当前 `ViewFlow` 实例的 requested template、已加载 `FlowParam`、起点选择和刷新 generation。刷新按 latest-wins 串行加载，较早请求完成后不能覆盖较新的选择；加载失败时保留原画布。独立编辑器也不再写入主程序的全局模板选择和全局节点集合。

`FlowHeadlessExecutionRequest` 在创建时复制 STN 和 MQTT 服务 token。`FlowHeadlessExecutionService` 每次执行新建并释放一个 `FlowRuntimeHost`，返回结构化的启动状态、终止原因、内容 hash、耗时和 `FlowControlData` 映射。裸执行器不自动创建批次，也不执行前后处理；这些业务语义由 UI 会话或插件调用方明确编排。

## Incident

运行日志在关键异常边界创建 Incident，但诊断写入保持 fail-open，不得改变生产流程结果。管理窗口支持按状态、级别、类型和文本分页筛选，查看关联 Run/Event/Attempt，并记录确认或关闭的 UTC 时间、操作人和备注。旧数据库由 CodeFirst 补齐新增字段。

## 验收

| 场景 | 必验项 |
| --- | --- |
| 保存流程 | 新增节点、选择模板、保存、关闭、重开后参数仍在 |
| 单流程导出 | `.cvflow` 包内有未改写的 `flow.stn`、`manifest.json` 和内容寻址模板载荷 |
| 单流程导入 | 相同模板不重复创建；内容冲突时能重命名副本并更新节点及二级模板引用 |
| 多流程导出 | zip 内是多个 `.stn`，不要误认为包含关联模板 |
| 调度执行 | Quartz `FlowJob` 能启动流程、等待后处理完成，并在 `context.Result` 返回最终 `FlowJobResult` |
| 共享链维护 | `RunFinalized` 后批次状态、耗时、节点尝试、Incident、后处理和最终结果都能追踪 |
| 多窗口切换 | 快速 A→B 选择最终只显示 B；坏模板加载失败不清空当前画布；独立窗口不改变主界面选择 |
| 裸执行器 | 两次并行执行各自拥有 RuntimeHost；取消、超时、加载失败和启动拒绝都有明确终止状态 |
| Incident | 确认和关闭能记录操作人、备注和时间，Run/Event/Attempt 详情可回查 |

## 源码导航

除 `FlowRuntimeHost` 外，下列路径均相对于 `Engine/ColorVision.Engine/`：

- 模板与包：`Templates/Flow/TemplateFlow.cs`、`Templates/Flow/FlowPackageHelper.cs`。
- 编辑与配置：`FlowProcessing/Editor/FlowEngineToolWindow.xaml.cs`、`FlowEditorCanvas.xaml.cs`、`NodeConfiguration/`。
- 工作区与宿主：`FlowProcessing/Runtime/ViewFlow.xaml.cs`、`FlowTemplateWorkspaceController.cs`、`FlowExecutionSession.cs`、`DisplayFlow.xaml.cs`。
- 裸执行：`FlowProcessing/Runtime/FlowHeadlessExecutionService.cs`、`Engine/FlowEngineLib/Runtime/FlowRuntimeHost.cs`。
- 版本与搜索：`Templates/Flow/Versioning/`、`Templates/Flow/Search/`、`FlowProcessing/Compilation/FlowCanvasCatalogBuilder.cs`。
- Incident：`FlowProcessing/Diagnostics/FlowIncident*.cs`。

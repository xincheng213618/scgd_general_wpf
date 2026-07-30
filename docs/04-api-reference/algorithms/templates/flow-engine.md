# 流程引擎

本页描述 `Engine/ColorVision.Engine/Templates/Flow` 宿主层：流程模板如何加载、编辑、保存、导入导出，以及如何接到 `FlowEngineLib`。节点执行语义和节点基类请看 [FlowEngineLib](../../engine-components/FlowEngineLib.md)。

## 先查什么

| 现象 | 第一检查点 |
| --- | --- |
| 流程模板列表为空 | MySQL 主表、资源表、`TemplateFlow.Load()` |
| 打开流程后节点图为空 | `SysResourceModel.Value`、`ModDetailModel.ValueA`、Base64 数据 |
| 保存后重开没变化 | `FlowEngineToolWindow.Save()` 路径、`DataBase64`、资源 `Type = 101` |
| 节点属性没有设备/模板下拉 | `STNodeEditorHelper`、`NodeConfigurator/`、服务和模板列表 |
| `.cvflow` 导入后模板名不对 | `manifest.json`、重名映射、`ReplaceTemplateNames(...)` |
| 调度能触发但结果没回去 | `FlowJob`、`RunFlowAndWaitAsync()`、`FlowCompleted`、项目包 `Processing` |
| 配置子流程后不能启动 | 当前目录 revision、已发布 Artifact、源 STN hash、固定子流程 revision/hash |
| 打开新窗口后画布串流程 | `FlowTemplateWorkspaceController` 的实例选择、刷新 generation、已加载文档 |
| 运行失败需要人工处置 | `FlowIncidentManagementWindow`、Run/Event/Attempt 关联记录 |

## 当前能力

| 能力 | 当前入口 | 说明 |
| --- | --- | --- |
| 流程模板 | `TemplateFlow` | 从数据库主表和资源表加载，管理新建、保存、删除、导入、导出 |
| 编辑窗口 | `FlowEngineToolWindow` | 独立流程编辑窗口，不是普通模板右侧编辑区 |
| 编辑器宿主 | `STNodeEditorHelper` | 托管节点画布、属性面板、节点树、剪贴板、右键菜单、合法性检查 |
| 节点配置 | `NodeConfigurator/*.cs` | 把设备列表、本地图像、普通模板、JSON 模板挂进节点属性面板 |
| 单流程包 | `.cvflow`、`FlowPackageHelper` | ZIP 包，包含 `flow.stn` 和 `manifest.json`，可携带关联模板 |
| 旧图文件 | `.stn` | 仅保存节点图原始数据 |
| 调度运行 | `FlowJob`、`FlowEngineManager.RunFlowAsync()` | Quartz 线程切回 UI 线程运行流程并等待结果 |
| 裸执行器 | `FlowHeadlessExecutionService`、`HeadlessFlowJob` | 每次运行拥有独立 `FlowRuntimeHost`，不依赖 WPF 画布、当前模板或全局选择 |
| 流程 Artifact | `FlowArtifactApplicationService` | 以 7 个内容寻址部件保存 authoring/compiled STN、策略、子流程侧车、编译映射和 manifest |
| 可复用子流程 | `FlowSubflowEditorWindow` | 调用点写入版本侧车，目标固定到 FlowKey、revision 和内容 hash，不修改 `.stn` |
| Incident 管理 | `FlowIncidentService`、`FlowIncidentManagementWindow` | 查询、筛选、确认、关闭并关联 Run/Event/Attempt |

## 存储边界

| 场景 | 当前行为 |
| --- | --- |
| 主存储 | MySQL 主表 + 明细表 + `SysResourceModel.Value` 保存 Base64 节点图 |
| 本地 `.stn` | 打开本地文件时保存只写回文件，不更新数据库模板 |
| 数据库流程 | 保存前 `CheckFlow()`，再取画布数据、Base64、`TemplateFlow.Save2DB(...)` |
| 资源引用 | `SysResourceModel.Type = 101`，`ModDetailModel.ValueA` 保存资源 id |
| 多选导出 | 仍是 zip 内多个 `.stn`，不会像 `.cvflow` 一样收集关联模板 manifest |
| 版本和搜索侧车 | 每次有效保存记录不可变 catalog revision、语义索引、执行策略和子流程侧车 |
| Artifact | MySQL 保存 immutable revision 和内容寻址 blob；普通模板保存只做 best-effort draft，无子流程的 draft 仍走 UI 执行链，不改变 legacy STN 保存成功语义 |

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
| 独立调度 | `HeadlessFlowJob` -> `RunPublishedArtifactHeadlessAsync(...)` | 读取并验证已发布 Artifact，在隔离 RuntimeHost 中执行，不触碰编辑器 |
| 停止流程 | `ViewFlow` -> `FlowExecutionSession.StopFlow()` -> `FlowControl.Stop()` | 批次状态更新为 `Canceled` |

`EngineExecutionCompleted` 表示“流程图引擎已结束”，此时后处理可能仍在运行；原有 `FlowExecutionCompleted` 作为它的过时兼容别名保留，不能作为整次业务运行成功的依据。引擎结束后会执行配置的后处理；后处理分为 `Warning` 和 `Required`，其中必需后处理失败会把最终结果判为失败。外部调用、Quartz 调度和自动化应等待 `RunFinalized`，或直接调用 `RunFlowAndWaitForFinalizationAsync()` 取得 `FlowRunFinalizedData.FinalOutcome`。`DisplayFlow` 只负责主程序视图注册、选中状态和服务重启。

普通流程继续使用 UI 执行链。当前 revision 存在子流程调用时，`FlowExecutionSession` 必须先取得与 FlowKey、catalog revision 和源 STN hash 完全一致的已发布 Artifact，再把 compiled STN 交给隔离运行时；主界面的批次、前处理、后处理和最终结果仍由原会话负责。画布未保存、Artifact 未发布、依赖版本漂移或校验失败都会明确阻止启动，禁止退回 authoring STN，因为 authoring STN 本身不包含展开后的子流程。

## 工作区与运行对象

`FlowTemplateWorkspaceController` 只保存当前 `ViewFlow` 实例的 requested template、已加载 `FlowParam`、起点选择和刷新 generation。刷新按 latest-wins 串行加载，较早请求完成后不能覆盖较新的选择；加载失败时保留原画布。独立编辑器也不再写入主程序的全局模板选择和全局节点集合。

`FlowHeadlessExecutionRequest` 在创建时复制 STN、MQTT 服务 token、错误路由和重试策略。`FlowHeadlessExecutionService` 每次执行新建并释放一个 `FlowRuntimeHost`，返回结构化的启动状态、终止原因、内容 hash、耗时和 `FlowControlData` 映射。裸执行器不自动创建批次，也不执行前后处理；这些业务语义由 UI 会话或插件调用方明确编排。

## Artifact 与子流程

Artifact 的 authoring STN 始终保留原始 `.stn` 字节。编译器根据子流程侧车递归展开调用，固定并验证每个依赖的 FlowKey、revision 和 SHA-256，再生成 compiled STN、effective policy、compilation map 和 manifest。发布读取会重新读取全部 7 个部件、验证内容 hash、依赖锁、编译器标识和映射归属，并再次解码 STND。

子流程编辑器把父流程的一条现有连接作为调用点。保存配置会创建新的流程目录 revision；勾选“同时发布 Artifact”后才产生可供生产执行的发布版本。目标流程有任何新保存不会自动改变父流程，父流程必须显式更新固定版本并重新发布。

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
| 项目维护 | `RunFinalized` 后批次状态、耗时、节点尝试、Incident、后处理和最终结果都能追踪 |
| 子流程执行 | 未发布或 hash 不匹配时拒绝启动；发布后实际执行 compiled STN，且 UI 最终态仍包含后处理结果 |
| 多窗口切换 | 快速 A→B 选择最终只显示 B；坏模板加载失败不清空当前画布；独立窗口不改变主界面选择 |
| 裸执行器 | 两次并行执行各自拥有 RuntimeHost；取消、超时、加载失败和启动拒绝都有明确终止状态 |
| Incident | 确认和关闭能记录操作人、备注和时间，Run/Event/Attempt 详情可回查 |

## 边界

- 本页不是 `FlowEngineLib` 重复页；这里讲主程序模板管理、窗口编辑和宿主桥接。
- 流程模板主路径是数据库 + 资源表，不是扫描磁盘目录。
- 节点属性编辑大量依赖 `NodeConfigurator` 和 `STNodeEditorHelper`。
- `.stn` 只含节点图，`.cvflow` 才包含 manifest 和关联模板。

## 关键文件

| 任务 | 先看 |
| --- | --- |
| 流程模板 | `TemplateFlow.cs` |
| 编辑窗口 | `FlowEngineToolWindow.xaml.cs` |
| 编辑器宿主 | `STNodeEditorHelper.cs` |
| 节点属性配置 | `NodeConfigurator/` |
| `.cvflow` 导入导出 | `FlowPackageHelper` 相关实现 |
| 流程工作区 | `ViewFlow.xaml.cs` |
| 工作区生命周期 | `FlowTemplateWorkspaceController.cs` |
| 执行会话 | `FlowExecutionSession.cs` |
| 裸执行器 | `FlowHeadlessExecutionService.cs`、`FlowRuntimeHost.cs` |
| Artifact | `FlowProcessing/Artifacts/` |
| 子流程编译与编辑 | `FlowProcessing/Compilation/` |
| Incident | `FlowProcessing/Diagnostics/FlowIncident*.cs` |
| 主程序壳 | `DisplayFlow.xaml.cs` |

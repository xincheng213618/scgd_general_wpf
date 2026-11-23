---
knowledge_id: "flow.diagnostics"
knowledge_type: "topic"
status: "current"
summary: "Flow本地诊断SQLite快照、节点尝试与Incident事件列表的读写边界；快照不保证包含未保存画布，终态持久化与业务结果分开，中断恢复不续跑节点，心跳不是判死条件。"
aliases: ["离线节点分析","FlowAnalysisDataSource","流程运行诊断","流程 Incident 管理","复制稳定标识","定位当前画布节点","NodeExecutionFailed","NodeTimeout","PostProcessFailed","流程诊断快照","FlowTemplateSnapshotFactory","进程中断恢复","流程心跳","节点执行尝试","诊断记录未完成","Incident确认关闭","异常事件管理","FlowExecutionJournal","FlowExecutionJournalCoordinator","FlowExecutionJournalScope","FlowRunRecord","FlowNodeAttempt","FlowTemplateSnapshot","FlowIncidentService","FlowIncidentManagementWindow","FlowOwnerProcessState","RunRecovered","流程执行分析","阶段耗时","FlowNodeTiming","InspectFlowIncident","inspect_flow_incident","诊断选中事故","ListFlowIncidents","list_flow_incidents","InspectRecordedFlowTemplate","inspect_recorded_flow_template","留存流程模板","InspectFlowRunHistory","inspect_flow_run_history","流程运行历史对照","CompareRecordedFlowTemplates","compare_recorded_flow_templates","留存流程配置差异","InspectFlowRunEvents","inspect_flow_run_events","流程运行事件续读","ListFlowRuns","list_flow_runs","留存流程运行发现","InspectFlowRunAttempts","inspect_flow_run_attempts","运行节点尝试续读"]
code_paths: ["Engine/ColorVision.Engine/FlowProcessing/Diagnostics","Engine/ColorVision.Engine/FlowProcessing/Runtime/ViewFlow.xaml.cs","Engine/ColorVision.Engine/FlowProcessing/Runtime/FlowTemplateWorkspaceController.cs","Engine/ColorVision.Engine/FlowProcessing/Runtime/FlowExecutionSession.cs","Engine/ColorVision.Engine/FlowProcessing/Runtime/FlowRunFinalizer.cs","Engine/ColorVision.Engine/FlowProcessing/Nodes/LocalFlowNodeBase.cs","Engine/ColorVision.Engine/FlowProcessing/Nodes/LocalFindLuminousAreaNode.cs","Engine/ColorVision.Engine/Services/Devices/Camera/Local/LocalFrameFileService.cs","Engine/ColorVision.Engine/Services/FlowDiagnostics","ColorVision/Copilot/Capabilities/CopilotFlowIncidentInspectionService.cs","ColorVision/Copilot/Agent/Tools/Application/CopilotInspectFlowIncidentTool.cs","ColorVision/Copilot/Mcp/CopilotMcpToolDispatcher.FlowIncident.cs","UI/ColorVision.Common/Interfaces/Copilot/CopilotFlowIncidentContext.cs","ColorVision/Copilot/Capabilities/CopilotFlowIncidentQueryService.cs","ColorVision/Copilot/Agent/Tools/Application/CopilotListFlowIncidentsTool.cs","ColorVision/Copilot/Mcp/CopilotMcpToolDispatcher.FlowIncidents.cs","ColorVision/Copilot/Capabilities/CopilotRecordedFlowTemplateInspectionService.cs","ColorVision/Copilot/Agent/Tools/Application/CopilotInspectRecordedFlowTemplateTool.cs","ColorVision/Copilot/Mcp/CopilotMcpToolDispatcher.RecordedFlowTemplate.cs","ColorVision/Copilot/Capabilities/CopilotFlowRunHistoryInspectionService.cs","ColorVision/Copilot/Agent/Tools/Application/CopilotInspectFlowRunHistoryTool.cs","ColorVision/Copilot/Mcp/CopilotMcpToolDispatcher.FlowRunHistory.cs","ColorVision/Copilot/Capabilities/CopilotRecordedFlowTemplateComparisonService.cs","ColorVision/Copilot/Agent/Tools/Application/CopilotCompareRecordedFlowTemplatesTool.cs","ColorVision/Copilot/Mcp/CopilotMcpToolDispatcher.RecordedFlowTemplateComparison.cs","ColorVision/Copilot/Capabilities/CopilotRecordedFlowTemplateComparisonService.Cursor.cs","ColorVision/Copilot/Capabilities/CopilotFlowRunEventInspectionService.cs","ColorVision/Copilot/Capabilities/CopilotFlowRunEventInspectionService.Cursor.cs","ColorVision/Copilot/Agent/Tools/Application/CopilotInspectFlowRunEventsTool.cs","ColorVision/Copilot/Mcp/CopilotMcpToolDispatcher.FlowRunEvents.cs","Engine/ColorVision.Engine/FlowProcessing/Integration/FlowCopilotService.cs","UI/ColorVision.Common/Interfaces/Copilot/CopilotSensitiveTextRedactor.cs","ColorVision/Copilot/Capabilities/CopilotFlowRunQueryService.cs","ColorVision/Copilot/Agent/Tools/Application/CopilotListFlowRunsTool.cs","ColorVision/Copilot/Mcp/CopilotMcpToolDispatcher.FlowRuns.cs","ColorVision/Copilot/Skills/colorvision-flow-diagnostics","ColorVision/Copilot/Capabilities/CopilotFlowRunAttemptInspectionService.cs","ColorVision/Copilot/Capabilities/CopilotFlowRunAttemptInspectionService.Cursor.cs","ColorVision/Copilot/Agent/Tools/Application/CopilotInspectFlowRunAttemptsTool.cs","ColorVision/Copilot/Mcp/CopilotMcpToolDispatcher.FlowRunAttempts.cs"]
related: ["flow.session","flow.templates","flow.headless","operations.data","ui.sqlite-storage","copilot.mcp-server"]
---

# Flow 运行诊断、中断恢复与 Incident 处置

使用“流程 Incident 管理”查看异常事件、关联运行和节点尝试，或核对进程退出后的遗留记录。`FlowExecutionJournal` 是共享执行会话的本地诊断存储，不是业务执行器：有记录不证明设备动作或外部交付成功，没有记录也不证明流程未运行。业务完成以[共享会话最终化](../../01-user-guide/workflow/execution.md)及所属项目输出为准；[隔离无界面执行](../algorithms/templates/flow-engine.md)不会自动获得这套共享会话记录。

“中断恢复”是将确认失去拥有进程的遗留运行记录标为失败，并保留异常证据；它不续跑节点、不重放设备请求、不重做后处理，也不回滚 MySQL 批次或文件。

## 打开并查询异常事件

从流程编辑器工具栏点击“流程 Incident 管理”。窗口读取整份本地诊断库，不自动筛选为当前画布的流程，也不是全部运行记录列表。首次加载会构造 `FlowIncidentService`，可能初始化或迁移 SQLite 表、payload 字段与索引，删除旧的 TemplateId + hash 唯一索引，并设置 WAL / busy timeout；打开窗口不属于严格零写入检查。

1. 选择状态，按需填写严重级别、类型或搜索文本。
2. 点击“刷新”，或在搜索框按 Enter。筛选在查询时生效，并回到第一页；窗口每页50条，用“上一页”“下一页”翻页。
3. 选中左侧记录，在右侧查看“详情”“运行事件”“节点尝试”和“原始信息”。事件与尝试属于同一 Run 的全部记录，不仅限于该 Incident 关联的节点。
4. 需要关联其他日志或反馈问题时，点击“复制稳定标识”，并另行记录时间与 SN。

| 筛选项 | 含义 |
| --- | --- |
| 状态：待处置 | 默认值 `Active`，排除 `Resolved`；包含未确认和已确认，不是只查 `Open` |
| 状态：未确认 / 已确认 / 已关闭 / 全部 | 分别对应 `Open` / `Acknowledged` / `Resolved` / `All` |
| 严重级别 | 留空查全部，否则按字段等值匹配，如 `Error`、`Warning` |
| 类型 | 留空查全部，否则按字段等值匹配。运行链使用 `NodeExecutionFailed`、`NodeTimeout`、`PostProcessFailed`、`UnhandledRunException`、`ProcessInterrupted` 等值 |
| 搜索 | 在摘要、NodeId、DetailsJson 中包含匹配，不自动搜索流程名称、SN、RunKey 或 FlowKey |

列表按发现时间和 ID 从新到旧排列，发现时间标为 UTC。服务 API 的 `PageNumber` 最小为1，`PageSize` 限制为1–200、默认50；这些参数不是窗口里的可编辑选项。查不到记录时先清除筛选或选择“全部”，再核对诊断库位置、运行标识与记录写入错误。

### 关联运行与当前画布

| 操作 | 结果与限制 |
| --- | --- |
| 打开运行分析 | 有正数 `Run.BatchId` 时打开旧节点耗时分析，传入 BatchId、SN 和 NodeId；没有关联批次时提示并显示稳定标识 |
| 定位当前画布节点 | 当前流程与记录的 FlowKey 相同，或正 TemplateId 相同后，尝试按 NodeId 定位；不会切换模板、加载诊断快照或比较版本/hash。定位成功也不能证明画布就是运行时版本 |
| 复制稳定标识 | 复制 IncidentId、RunRecordId、RunKey、FlowKey、TemplateId、NodeId；不包含快照正文或 SN |

## Copilot 正式事故诊断

在事故窗口选中记录并点击“诊断选中事故”，会新建 Diagnose 对话。窗口附上当前事故/运行/尝试身份，关闭或改变选择会释放旧引用。这些有界脱敏的 UI 数据供定位，正式证据需要下表的只读工具。流程编辑器的“问 AI 分析当前流程”则使用当前 UI 中的运行消息，先脱敏完整文本，再截尾并提取异常线索。

| 用途 | Agent / 本机 MCP 工具 | 关联与证据边界 |
| --- | --- | --- |
| 查运行 | `ListFlowRuns` / `list_flow_runs` | 不依赖 Incident。`flow_key` 如提供须完整、去前后空白并按大小写精确匹配。按运行 ID 降序读，ID 顺序不等于实际执行顺序。未知状态、缺 FlowKey 及 legacy 记录仍可参与未筛选的列表。 |
| 查事故 | `ListFlowIncidents` / `list_flow_incidents` | 默认 `active` 排除精确 `Resolved`，保留未知状态。按发现时间与 ID 降序。 |
| 读事故详情 | `InspectFlowIncident` / `inspect_flow_incident` | 传入精确 Int64 `incident_id`，严格沿 `Incident.RunRecordId` 关联。同一快照中取最新事件与最多 50 个节点尝试，保留存在的关联尝试。记录字段可缺失或被截断。 |
| 续读事件 | `InspectFlowRunEvents` / `inspect_flow_run_events` | 传入精确 `run_record_id`。首窗取最新事件，页内按序号及 ID 升序；续页向更早记录读取。 |
| 续读节点尝试 | `InspectFlowRunAttempts` / `inspect_flow_run_attempts` | 传入精确 `run_record_id`，可用精确、区分大小写的 `node_id` 筛选。页内按 Int64 `attempt_id` 升序，向更早 ID 续读。保留重复尝试和循环。 |
| 读留存模板 | `InspectRecordedFlowTemplate` / `inspect_recorded_flow_template` | 精确运行引用确定 `SnapshotId`。可用标准非零 `node_id` GUID 限制焦点和可核验相邻节点。不实例化节点。 |
| 查历史与耗时参照 | `InspectFlowRunHistory` / `inspect_flow_run_history` | 以较小的记录 ID 取此前窗口。完整 FlowKey 归一化后严格匹配，双方都没有键时用同一正 TemplateId 给出弱参考。身份全缺时只交付目标。 |
| 比较留存配置 | `CompareRecordedFlowTemplates` / `compare_recorded_flow_templates` | 精确提供有序的 baseline/current run ID。同一读取快照中核对关联、完整 FlowKey 或双方无 FlowKey 时的同一正 TemplateId。 |

各工具按本机 MCP schema 声明的参数范围调用。运行 ID 从运行列表、已读正式证据或用户取得。UI 中截断或脱敏的片段、SN、批次号和当前画布都无法提供精确身份。诊断工具只读配置中的本地库，不创建库、迁移、写入、处置事故或重跑设备。缺库、缺 schema 或精确引用不可用时明确失败。具备所需表且没有匹配数据时才成功返回空结果。

分页使用原样返回的 `next_cursor`，每页独立读取实时留存数据。游标绑定来源、规范筛选和锚点。源或筛选变化、锚点删除或排序值变动后，需要不带游标从头读取。事故时间的原始存储表示参与排序。比较游标另绑定有序运行、两侧引用、已校验的 hash 和身份类型。最多 18,000 字符的结果预算可能缩页，按**实际交付**边界续读。查询结束只表示到达当前留存边界，不能证明执行曾完整落盘。

事件和尝试中的未知或截断终态保持未知。起止时间完整且顺序有效才计算时长；失败的 attempt 也不证明流程停止。历史摘要只有在完整同 FlowKey、格式有效的同 hash、明确 `Completed` 且 `Succeeded` 或 `SucceededWithWarnings`、完成时间及耗时有效并无恢复标记时，才纳入确认成功参照。每节点最多交付 10 条尝试，只有实际交付且完整成功、有有效耗时的集合才累计作样本，重叠时段仍相加。历史统计按实际交付记录计算，不用来推断客户 PASS/FAIL 或全库成功率。

留存模板按运行的精确引用、身份、长度和 SHA-256 校验，BLOB 至多 8 MiB，解压至多 32 MiB。列表与事故详情只验证 hash 字段格式，不验证模板字节。模板结果仅交付节点身份、字段名和具有可验证端口布局的连线。字段值和原始 STN 不交付。比较另报告完整差异计数和视图、顺序、字节变化，页内明细受行数与正文预算约束。布局缺失时标记不可比较，不能推断零差异。留存数据可用于对照，实际运行画布另按快照来源确认。 [快照来源](#快照是否对应本次画布)

实现从 `Engine/ColorVision.Engine/Services/FlowDiagnostics/` 的各 reader 及 `RecordedFlowTemplateComparer` 定位。参数、调用方式和字段结构见本机 MCP 说明及当前 `tools/list`。 [MCP 工具用途与效果](../../02-developer-guide/core-concepts/colorvision-mcp.md#工具用途与效果)

## 节点耗时与消息明细

在“流程执行分析”打开本次节点消息，在接收 Payload 中查看 `Timing`。概览按 Batch + SN 关联，旧记录缺 BatchId 时按完整 SN 匹配，重复 SN 可能歧义。当前批次无节点时可刷新，后台耗时查 `FlowAnalysisLoad` 和 `FlowAnalysisNavigation`。

定时器 `Timer` 按每次实际等待发送本地开始和结束事件，接入相同的节点耗时、消息记录和标题进度显示。每次调用使用独立消息 ID，重复框内的各轮分别保留记录；本次等待时长通过 `ExpectedDurationMs` 交给共享执行会话，首次运行也能显示进度，不依赖历史耗时。耗时在向下游转交前结束，不包含后续采集；零等待仍有成对记录，停止、释放运行资源或替换同一等待会完成取消记录，不再输出迟到的运行数据。诊断订阅者异常不会中断等待和转交。记录写入和界面刷新仍遵守上述边界。

概览节点行和节点页的“跨批次比对”集中显示同流程、同 NodeId 的执行与消息。列表包含 Batch、SN、时间、节点耗时和消息状态，可按批次、SN、事件、消息 ID 或状态消息筛选。选择消息后点击“设为比对基准”，随后选择其他批次；下方并排展示基准与当前选择的响应或发送正文和 Topic，JSON 自动格式化。没有消息的执行保留在列表中，超时不显示节点耗时；正文只读取当前选择和基准，不全量解压历史消息。

消息比对的默认基准是当前执行中首条可用消息，否则取历史窗口首条有消息的记录；可手工替换，不自动筛选成功运行。消息“成功”仅来自消息状态，与 `InspectFlowRunHistory` 的确认成功参照条件分别解释，也不代表客户 PASS/FAIL。

比对查询最近 500 条同流程运行和当前运行，沿用 `FlowIdentity` 兼容规则，再按 Batch + SN 和 NodeId 收紧。旧运行缺 BatchId 时按完整 SN 关联，重复 SN 可能产生歧义。缺流程身份或节点 ID 时仅显示本次执行。各次节点调用的消息优先按 NodeRecordId 关联，未关联的旧消息按节点和时间范围仅归给同批次中最接近的一次调用。运行切换与清理仍按各自入口执行。

ARVRPro 的现场只读窗口可将数据库路径、来源标签和完整运行标识传入 `FlowExecutionAnalysisWindow`。`FlowAnalysisDataSource` 将概览、节点历史、运行切换、事件及惰性正文读取固定到该文件；缺失的可选诊断表返回空集合，不查询本机运行库。来源标签始终显示，清理入口隐藏且处理函数拒绝写入。未传离线来源的原有入口继续使用运行库。离线未结束节点以快照中最后节点时间为显示截止点，不随查看时刻累计等待时间。

`LocalFlowNodeBase` 为每次本地执行记录独立 `Timing`，成功与失败的完成消息都保留已到达的阶段。没有细分的本地节点只报告 `ExecuteLocal`。L/BV 本地转发的错误响应和远端服务可能缺少明细，解释耗时时应保留这个缺口。

| JSON 字段 | 含义 |
| --- | --- |
| `Timing.Version` / `Unit` | 当前格式版本 1；时间单位 `ms`，小数保留至微秒量级，不代表硬件精度 |
| `Timing.TotalMs` | 通用本地节点从进入执行方法到序列化完成消息前的时间，含输入处理、算法及结果交接；不含后台任务排队、诊断 JSON 序列化及随后日志落盘。L/BV 转发从创建本地执行对象计时，跨取图与 Complete 交接，包含两者之间的等待 |
| `Stages[].Name` | 稳定操作名，如 `OpenImage`、`Algorithm`、`PersistResult` |
| `Id` / `ParentId` / `StartMs` | 阶段身份、父阶段及相对执行起点的开始偏移。父阶段耗时包含子阶段，不能把所有行直接相加 |
| `ElapsedMs` / `Status` | 阶段实际墙钟耗时，以及 `Completed`、`Failed`、`Canceled`、`Skipped`；若在阶段尚未结束时取得快照，则为 `Running`。0 ms 不等于未执行，跳过操作由 `Skipped` 表示 |
| `OmittedStages` | 每次最多记录 256 个阶段，超过上限只累计省略数；用于限制日志体积，不中断业务 |

有内存帧时 `OpenImage` 记为 `Skipped`；回退到文件才包含存在性检查、读盘或解码。`Algorithm`、模板更新、落库和发布分别计时。未运行阶段不补写 0。

相机 `CaptureFrame` 包含整个 SDK 调用，不能区分曝光、读出与传输。`PersistResult` 的 `ResolveBatch`、`BuildResultModel`、`InsertResult` 为子阶段，不能与父阶段重复累加。落库包含客户端和连接等待，要区分连接与命令执行可按 `MasterId` 关联 `DatabaseCommandTiming` 的 `Result`。`PublishPreview` 和 `PublishResult` 只代表提交调用。

原有 `TotalTime`、`CaptureTime`、`CalibrationTime`、`SaveTime` 的含义和业务结果结构保持兼容。例如发光区节点原有 `TotalTime` 仍是算法耗时，打开文件时间查看新 `Timing`；不能把两者相加。`Timing` 为完成日志保留字段，扩展本地节点时不要将其用作业务结果字段。

计时只在已激活的执行上下文中收集时间戳和有限条目，使用完成消息一次性保存，经[SQLite Payload 存储](../ui-components/sqlite-storage.md)压缩并按所选消息惰性读取。不逐阶段写文件/数据库，不在像素循环中打点，不全量读取历史 Payload；旧日志没有阶段数据时不会回填。

`PreProcessTiming` 和 `SocketMessageUiTiming` 的 INFO 门槛只控制日志输出，与业务超时、判定无关。日志级别和覆盖阶段从节点和窗口实现核对。

`FlowPerformanceSampler` 在引擎完成后尝试后台采样，两类入口共用 30 秒节流。请求、采样的时间分别看 `RequestedAt`、`SampledAt`；`QueueDelayMs` 有助识别线程池拥塞。直接用 `FlowControl` 的入口可能没有业务批次或模板名，需按 SN、起始节点和时间关联。采样缺失可能来自首样本、并发占用或读取失败。持续未完成的流程没有中途样本。

CPU / I/O 速率基于两次实际读取之间的窗口。进程 I/O 包括文件、网络与设备活动，不能当作磁盘物理吞吐。系统 CPU 在超过 64 个逻辑处理器时只代表调用线程所在的处理器组。内存和线程池是读取时快照；`SamplingMs` 只计读取。日志不含频率或温度，判断降频仍需独立现场证据。

## 确认和关闭

确认与关闭是独立的处置写入，需要相应授权；它们不会改变 Run / Attempt 的结果，也不重跑流程。操作人字段默认填当前系统用户名，服务只接收字符串，不据此认证身份。

**当前窗口的线程访问限制。**确认和关闭处理函数在 `Task.Run` 中读取 WPF 文本框。出现“调用线程无法访问此对象，因为另一个线程拥有该对象”时，服务尚未收到处置调用，入口见 `FlowIncidentManagementWindow.xaml.cs`。

| 服务操作 | 输入与状态转换 |
| --- | --- |
| `Acknowledge`（确认） | 确认人必填、备注可选。Open → Acknowledged，记录 UTC 时间；重复确认保留首次信息。Resolved 或未知状态不能确认 |
| `Resolve`（关闭） | 关闭人、关闭备注必填。Open 或 Acknowledged → Resolved，记录 UTC 时间；重复关闭保留首次信息。未知状态不能关闭，也没有重新打开操作 |

服务成功写入后，刷新并核对状态与备注。“待处置”不显示已关闭记录，需切换到“已关闭”或“全部”查看。重复操作保留首次信息，但 `UpdateIncident` 仍执行事务与 Update；异常会抛给调用者，不受运行层的诊断降级保护。

## 记录由谁拥有

默认数据库来自 `FlowNodeRecordConfig.SqliteDbPath`，与旧 `FlowNodeRecord` / `FlowNodeMessage` 使用同一个 `FlowNodeRecords.db`，不是 Engine 业务 MySQL。默认路径与数据源入口见[数据所有者](../../01-user-guide/data-management/README.md)，压缩正文、停写维护与备份见[SQLite 存储维护](../ui-components/sqlite-storage.md)。不要把旧节点写队列、journal 事务和业务结果当成一个共同事务。

| 对象 | 关联与判读边界 |
| --- | --- |
| `FlowTemplateSnapshot` | 保存调用方提供的字节与内容 hash，不验证它是否是可运行的 STN。优先按 `FlowKey + ContentHash` 复用；未提供 FlowKey 时按 TemplateId 与 hash 查找。同 hash 但内容或长度不同会拒绝，不代表整个项目已备份 |
| `FlowRunRecord` | 本轮 `RunKey` 关联快照、SN、可选 BatchId、状态与最终结果；SN 不是 journal 的幂等键。记录另带 instance、机器、PID、进程启动时间和心跳 |
| `FlowExecutionEvent` | 同一运行内按 `EventKey` 去重，分配递增 `SequenceNo`；不是跨所有运行的全局顺序 |
| `FlowNodeAttempt` | `InvocationId` 区分调用，同一节点的不同调用递增 `AttemptNo`；重复提交同一调用不会新增一次尝试，不能把循环中的多次调用折叠成节点唯一记录 |
| `FlowIncident` | 同一运行内按 `IncidentKey` 去重，可关联节点和 attempt；处置状态与运行成功/失败是两个维度 |

journal 会校验重复键对应的内容，不是拿相同键就能覆盖旧事实。直接调用 journal 的非法参数或冲突会抛错；运行层 coordinator 才负责捕获存储异常并降级。

## 快照是否对应本次画布

当前记录链存在画布与快照不一致的限制。`ViewFlow` 的执行器连接当前节点画布，启动命令不先保存；`TryBeginExecutionJournal` 却从工作区模板身份快照中的 `FlowParam.DataBase64` 创建诊断快照，不重新读取 `STNodeEditor.GetCanvasData()`。

| 执行来源 | 诊断快照实际来源 |
| --- | --- |
| 已加载的数据库模板 | 捕获到的模板 `DataBase64`；之后未保存的画布编辑可以参与运行，但不会自动进入这份记录 |
| 以 `FlowParam` 打开的独立窗口，或成功读取包内 STN 的 `.cvflow` 文档 | 当时持有的 `FlowParam.DataBase64`，同样不保证包含后续画布修改 |
| 没有 `FlowParam` 的本地 `.stn` 文档或新建图 | 临时模板对象仅有名称；空 `DataBase64` 按空字节创建快照，不会从本地文件或画布补取 |

`FlowTemplateSnapshotFactory` 对传入字节复制并计算 SHA-256；journal 校验 hash 与内容一致，但允许空字节，也不加载 STN 来验证节点图。有效 hash、版本号或记录已创建都不证明它准确保存了本轮画布，空快照也不会仅因长度为0就触发 legacy 降级。

复现时同时确认运行入口、模板保存状态及实际文件/画布来源，不单凭快照或版本号还原本轮运行。保存目标与文档切换规则见[流程工作区](../../01-user-guide/workflow/design.md)。

## 记录失败不应改写业务结果

`FlowExecutionSession` 创建业务批次后尝试创建快照和 journal scope，再记录前处理、节点事件和最终化。快照非法或诊断库不可用时可以退回 legacy 记录路径；这个容错不承诺 MySQL 批次写入、节点动作或后处理也会被忽略。

`FlowExecutionJournalCoordinator.Shared` 默认每5秒更新 scope 心跳。首次初始化失败后，30秒内的后续访问不再重试工厂；到期后由新的访问触发重试，不是每30秒自动重新连接。成功创建 journal 时尝试一次 abandoned-run recovery；恢复查询失败会记日志，但不禁用新运行记录。

`FlowRunFinalizer` 先确定业务结果，再由 journal 固定 `(Status, ElapsedMs, FinalOutcome)` 并停止心跳。两次写入都失败时，运行退出前再补写同一终态一次。尚无终态请求的运行会记录 `RunEndedBeforeFinalization` 及提前退出的状态。收到业务结果后仍须单独核对 SQLite 终态。

`CompleteRun` 将缺少结束事件的尝试标为 `Interrupted`（`NodeEndNotRecorded`），耗时未知。在途的结束记录仍可补齐。终态请求阻止 scope 接受新事件和尝试，运行结束后重复调用 ID 仍返回原记录。重试同一终态保留首次完成时间，结果三元组冲突则拒绝。

## 哪些遗留运行会被恢复

`RecoverAbandonedRuns` 只考察 `FlowStatus.Runing` 且 `CompletedTimeUtc` 为空的记录；`Runing` 是现存枚举拼写。还必须同时满足：

1. owner instance、机器、正 PID 和进程启动时间完整。
2. 属于当前机器，但不是当前 instance。
3. `IFlowProcessProbe` 明确返回 `NotRunning` 或 `StartTimeMismatch`。真实进程探针以 PID 和启动时间一起区分 PID 复用，启动时间比较允许1秒精度差。

其他机器、旧记录缺 owner、当前 instance、存活进程、`Unknown` 或探针异常都不据此终结。**心跳年龄不参与判死**；即使心跳很旧，只要上述证据不足也不恢复。5秒心跳间隔不等于5秒失联就自动标失败。

每条候选在独立写事务内重新读取并核对状态、完成时间和 owner 未变，然后：

- 将运行与最终结果都设为 `Failed`，填写恢复时间/原因。
- 将未完成 attempt 标为 `Interrupted`，错误码为 `ProcessInterrupted`。
- 写入 `RunRecovered` 事件和一个 Open / Error / `ProcessInterrupted` Incident。

单条冲突或失败会回滚该条并继续其余候选，不是所有候选的整体事务；重复恢复已终结记录不会再追加一套事件。恢复耗时按原开始时间到恢复时刻计算，不是精确的进程退出时刻，也不能解释为硬件持续工作了这么久。

## 验证入口与缺口

验收时按实际窗口、运行记录和现场状态核对异常时间、画布来源、操作权限、硬件停止和跨进程恢复。没有诊断结果时先核对记录阶段、存储初始化和写入异常以及 owner 证据。重跑可能已产生副作用的流程前需另行确认。

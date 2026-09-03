---
knowledge_id: "flow.diagnostics"
knowledge_type: "topic"
status: "current"
summary: "Flow本地诊断SQLite快照、节点尝试与Incident事件列表的读写边界；快照不保证包含未保存画布，终态持久化与业务结果分开，中断恢复不续跑节点，心跳不是判死条件。"
aliases: ["流程运行诊断","流程 Incident 管理","复制稳定标识","定位当前画布节点","NodeExecutionFailed","NodeTimeout","PostProcessFailed","流程诊断快照","FlowTemplateSnapshotFactory","进程中断恢复","流程心跳","节点执行尝试","诊断记录未完成","Incident确认关闭","异常事件管理","FlowExecutionJournal","FlowExecutionJournalCoordinator","FlowExecutionJournalScope","FlowRunRecord","FlowNodeAttempt","FlowTemplateSnapshot","FlowIncidentService","FlowIncidentManagementWindow","FlowOwnerProcessState","RunRecovered"]
code_paths: ["Engine/ColorVision.Engine/FlowProcessing/Diagnostics/FlowExecutionJournal.cs","Engine/ColorVision.Engine/FlowProcessing/Diagnostics/IFlowExecutionJournal.cs","Engine/ColorVision.Engine/FlowProcessing/Diagnostics/FlowExecutionJournalCoordinator.cs","Engine/ColorVision.Engine/FlowProcessing/Diagnostics/FlowExecutionRecovery.cs","Engine/ColorVision.Engine/FlowProcessing/Diagnostics/FlowIncidentService.cs","Engine/ColorVision.Engine/FlowProcessing/Diagnostics/FlowIncidentManagementWindow.xaml.cs","Engine/ColorVision.Engine/FlowProcessing/Diagnostics/FlowIncidentManagementWindow.xaml","Engine/ColorVision.Engine/FlowProcessing/Runtime/ViewFlow.xaml.cs","Engine/ColorVision.Engine/FlowProcessing/Diagnostics/FlowDiagnosticsSchemaMigrator.cs","Engine/ColorVision.Engine/FlowProcessing/Diagnostics/FlowNodeRecordConfig.cs","Engine/ColorVision.Engine/FlowProcessing/Diagnostics/FlowTemplateSnapshot.cs","Engine/ColorVision.Engine/FlowProcessing/Diagnostics/FlowTemplateSnapshotFactory.cs","Engine/ColorVision.Engine/FlowProcessing/Runtime/FlowTemplateWorkspaceController.cs","Engine/ColorVision.Engine/FlowProcessing/Diagnostics/FlowRunRecord.cs","Engine/ColorVision.Engine/FlowProcessing/Diagnostics/FlowExecutionEvent.cs","Engine/ColorVision.Engine/FlowProcessing/Diagnostics/FlowNodeAttempt.cs","Engine/ColorVision.Engine/FlowProcessing/Diagnostics/FlowIncident.cs","Engine/ColorVision.Engine/FlowProcessing/Runtime/FlowExecutionSession.cs","Engine/ColorVision.Engine/FlowProcessing/Runtime/FlowRunFinalizer.cs"]
test_paths: ["Test/ColorVision.UI.Tests/FlowExecutionJournalTests.cs","Test/ColorVision.UI.Tests/FlowExecutionJournalCoordinatorTests.cs","Test/ColorVision.UI.Tests/FlowExecutionRecoveryTests.cs","Test/ColorVision.UI.Tests/FlowIncidentServiceTests.cs","Test/ColorVision.UI.Tests/FlowDiagnosticsSchemaTests.cs","Test/ColorVision.UI.Tests/FlowRunFinalizerTests.cs"]
related: ["flow.session","flow.templates","flow.headless","operations.data","ui.sqlite-storage"]
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

## 确认和关闭

确认与关闭是独立的处置写入，需要相应授权；它们不会改变 Run / Attempt 的结果，也不重跑流程。操作人字段默认填当前系统用户名，服务只接收字符串，不据此认证身份。

**当前窗口的线程访问限制：**确认和关闭处理函数在 `Task.Run` 中读取 `OperatorTextBox.Text`、`ActionNoteTextBox.Text`，违反 WPF 控件的线程访问要求。出现“调用线程无法访问此对象，因为另一个线程拥有该对象”时，处置方法尚未被调用，不能将下表的服务能力视为按钮已经可用。该问题位于 `FlowIncidentManagementWindow.xaml.cs`；底层服务测试不覆盖这条点击路径。

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

`FlowRunFinalizer` 先解析后处理和最终业务状态，再尝试 journal 终态写入。最终化结果可以已经返回，而 SQLite 仍没有成功写入终态。因此“后处理完成”“收到最终结果”“本地记录已完成”需要分别核对。

`FlowExecutionJournalScope.TryCompleteRun` 的重试规则：

- 首次请求固定 `(Status, ElapsedMs, FinalOutcome)`，停止心跳，并对相同终态最多立即写两次。
- 两次都失败时可再次请求同一结果，不能改用另一终态。`IsCompletionRequested` 表示终态已成功持久化，不是仅发起过请求。
- 完成请求设置后，scope 拒绝后续普通事件/attempt 调用；检查与实际写入不是一个原子区间，已通过检查的并发写入或直接 journal 调用不受这项门禁完整隔离。
- `Dispose()` 只停心跳和释放 timer，不补写终态，也不安排后台无限重试。

底层 `CompleteRun` 重复收到同一终态会返回原记录并保留第一次完成时间；结果三元组冲突则拒绝。不要为了让界面状态变绿，把仍待核对的记录改成成功。

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

| 测试 | 已有用例的范围 |
| --- | --- |
| `FlowExecutionJournalTests` | 快照复用、每轮事件顺序、稳定键和终态冲突 |
| `FlowExecutionJournalCoordinatorTests` | 用替身检查初始化失败、心跳与相同终态重试 |
| `FlowExecutionRecoveryTests` | 临时 SQLite、可控进程探针下的 owner 判定、单条恢复回滚与幂等；另检查本测试进程启动时间的数据库往返 |
| `FlowIncidentServiceTests` | 筛选、详情、Open 确认后关闭、输入校验；直接调用服务，不操作窗口 |
| `FlowDiagnosticsSchemaTests` | 解码字节 hash、无效 Base64、旧表迁移 |
| `FlowRunFinalizerTests` | 后处理策略，以及后处理先于 legacy fallback 持久化；不是到真实 journal 的组合测试 |

窗口查询/分页/定位、确认/关闭点击、当前画布与快照一致性，以及 Incident 直接关闭 Open、重复动作、未知状态和在途并发等边界，没有被上述用例逐项覆盖。正文分别依据窗口、服务、journal 与执行会话源码，不把服务层测试当成完整界面或跨库验证。

这些测试不连接现场业务库或设备，也不证明真实断电时刻、硬件停止、跨进程/跨数据库事务或客户交付完整。没有诊断结果时先核对记录阶段、存储初始化/写入异常和 owner 证据，不能据此授权重跑可能已产生副作用的流程。

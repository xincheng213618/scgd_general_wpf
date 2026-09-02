---
knowledge_id: "flow.diagnostics"
knowledge_type: "topic"
status: "current"
summary: "Flow本地诊断SQLite快照、节点尝试与Incident事件列表的读写边界；快照不保证包含未保存画布，终态持久化与业务结果分开，中断恢复不续跑节点，心跳不是判死条件。"
aliases: ["流程运行诊断","流程诊断快照","FlowTemplateSnapshotFactory","进程中断恢复","流程心跳","节点执行尝试","诊断记录未完成","Incident确认关闭","异常事件管理","FlowExecutionJournal","FlowExecutionJournalCoordinator","FlowExecutionJournalScope","FlowRunRecord","FlowNodeAttempt","FlowTemplateSnapshot","FlowIncidentService","FlowIncidentManagementWindow","FlowOwnerProcessState","RunRecovered"]
code_paths: ["Engine/ColorVision.Engine/FlowProcessing/Diagnostics/FlowExecutionJournal.cs","Engine/ColorVision.Engine/FlowProcessing/Diagnostics/IFlowExecutionJournal.cs","Engine/ColorVision.Engine/FlowProcessing/Diagnostics/FlowExecutionJournalCoordinator.cs","Engine/ColorVision.Engine/FlowProcessing/Diagnostics/FlowExecutionRecovery.cs","Engine/ColorVision.Engine/FlowProcessing/Diagnostics/FlowIncidentService.cs","Engine/ColorVision.Engine/FlowProcessing/Diagnostics/FlowIncidentManagementWindow.xaml.cs","Engine/ColorVision.Engine/FlowProcessing/Diagnostics/FlowDiagnosticsSchemaMigrator.cs","Engine/ColorVision.Engine/FlowProcessing/Diagnostics/FlowNodeRecordConfig.cs","Engine/ColorVision.Engine/FlowProcessing/Diagnostics/FlowTemplateSnapshot.cs","Engine/ColorVision.Engine/FlowProcessing/Diagnostics/FlowTemplateSnapshotFactory.cs","Engine/ColorVision.Engine/FlowProcessing/Runtime/FlowTemplateWorkspaceController.cs","Engine/ColorVision.Engine/FlowProcessing/Diagnostics/FlowRunRecord.cs","Engine/ColorVision.Engine/FlowProcessing/Diagnostics/FlowExecutionEvent.cs","Engine/ColorVision.Engine/FlowProcessing/Diagnostics/FlowNodeAttempt.cs","Engine/ColorVision.Engine/FlowProcessing/Diagnostics/FlowIncident.cs","Engine/ColorVision.Engine/FlowProcessing/Runtime/FlowExecutionSession.cs","Engine/ColorVision.Engine/FlowProcessing/Runtime/FlowRunFinalizer.cs"]
test_paths: ["Test/ColorVision.UI.Tests/FlowExecutionJournalTests.cs","Test/ColorVision.UI.Tests/FlowExecutionJournalCoordinatorTests.cs","Test/ColorVision.UI.Tests/FlowExecutionRecoveryTests.cs","Test/ColorVision.UI.Tests/FlowIncidentServiceTests.cs","Test/ColorVision.UI.Tests/FlowDiagnosticsSchemaTests.cs","Test/ColorVision.UI.Tests/FlowRunFinalizerTests.cs"]
related: ["flow.session","flow.templates","flow.headless","operations.data","ui.sqlite-storage"]
---

# Flow 运行诊断、中断恢复与 Incident 处置

本主题用于核对流程运行记录、节点尝试、异常事件和进程退出后的遗留状态。`FlowExecutionJournal` 是共享执行会话的本地诊断存储，不是业务执行器：有记录不证明设备动作或外部交付成功，没有记录也不证明流程未运行。业务完成以[共享会话最终化](../../01-user-guide/workflow/execution.md)及所属项目输出为准；[隔离无界面执行](../algorithms/templates/flow-engine.md)不会自动获得这套共享会话记录。

“中断恢复”是将确认失去拥有进程的遗留运行记录标为失败，并保留异常证据；它不续跑节点、不重放设备请求、不重做后处理，也不回滚 MySQL 批次或文件。

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

`FlowExecutionJournalScope.TryCompleteRun` 固定首次请求的 `(Status, ElapsedMs, FinalOutcome)`，停止心跳，并对相同终态最多立即写两次。两次都失败时仍允许后续重试同一结果，拒绝改用另一终态；`IsCompletionRequested` 实际表示终态已成功持久化，不是仅发起过请求。完成请求设置后，scope 的后续普通事件/attempt 调用会被门禁拒绝；检查与实际写入并非原子区间，这不保证已通过检查的并发写入或直接 journal 调用被终态隔离。`Dispose()` 只停心跳和释放 timer，不自动补写成功或失败，也没有后台无限重试。

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

## 查询、初始化与处置分开授权

`FlowIncidentService.Query` / `GetDetail` 本身读取数据，但构造 service 会执行 `FlowDiagnosticsSchemaMigrator.EnsureSchema`；默认自动加载的 `FlowIncidentManagementWindow` 在首次刷新时构造它。因此“打开 Incident 列表”不是严格的零写入检查：初始化可能设置 WAL / busy timeout、创建或补齐表与 payload 字段，并删除旧快照唯一索引。查源码路径不需要打开窗口或实例化运行单例。

Query 默认 `Active` 即排除 `Resolved`，不是仅查 Open。单页默认50、最大200，按检测时间和ID从新到旧；文本过滤匹配 Incident 的摘要、NodeId、DetailsJson，不自动匹配 FlowName 或 SN。详情再读取关联 Run、按序 Event 和 Attempt；列表为空或未命中关键字不能证明没有对应流程。

| 操作 | 状态与持久化边界 |
| --- | --- |
| `Acknowledge` | Open → Acknowledged；确认人必填、备注可选，记录 UTC 时间。重复确认保留首次确认信息；Resolved 不能再次确认 |
| `Resolve` | Open 或 Acknowledged → Resolved；关闭人和关闭备注必填。重复关闭保留首次关闭信息；没有重新打开操作 |

重复操作保留信息不等于“不执行 SQL”：当前 `UpdateIncident` 仍走事务和 Update，失败会抛给调用者，不能把运行层的诊断降级当作处置已成功。确认或关闭只修改 Incident，不改变 Run / Attempt 结果，不等于故障已消失，也不重跑流程。操作人只是传入字符串，窗口默认填当前系统用户名，service 本身不据此认证身份或授权。查询请求不包含这些写操作的授权；诊断快照和异常详情同样应按所属项目的数据权限处理。

## 验证入口与缺口

现有隔离测试的职责：`FlowExecutionJournalTests` 核对快照复用、每轮事件顺序、稳定键和终态冲突；`FlowExecutionJournalCoordinatorTests` 用替身核对初始化降级、心跳与相同终态重试；`FlowExecutionRecoveryTests` 用临时 SQLite 和可控进程探针核对 owner 判定、原子恢复与幂等；`FlowIncidentServiceTests` 核对筛选、详情、一次确认后关闭和输入校验；`FlowDiagnosticsSchemaTests` 核对解码 hash 与旧表迁移；`FlowRunFinalizerTests` 核对后处理先于 legacy fallback 终态持久化及业务结果策略，不是后处理到真实 journal 的组合测试。当前画布与快照的一致性未被这些隔离测试覆盖。Incident 直接关闭 Open、重复动作、未知状态、业务记录不变和上述在途并发等边界主要依据源码核对，现有测试没有逐项断言，不能把正文全部边界包装成已自动化覆盖。

这些测试不连接现场业务库或设备，也不证明真实断电时刻、硬件停止、跨进程/跨数据库事务或客户交付完整。没有诊断结果时先核对记录阶段、存储初始化/写入异常和 owner 证据，不能据此授权重跑可能已产生副作用的流程。

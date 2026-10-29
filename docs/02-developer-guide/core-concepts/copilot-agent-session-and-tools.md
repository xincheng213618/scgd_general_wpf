---
knowledge_id: "copilot.session-tools"
knowledge_type: "topic"
status: "current"
summary: "Copilot 会话检查点、任务呈现、重试和内置工具的状态恢复与安全边界。"
aliases: ["Copilot 会话如何恢复","工具失败能否重试","诊断模式会自动读取日志吗","CopilotAgentSessionCheckpoint","CopilotAgentTaskEventJournal","GetRecentLog","CopilotRecentLogSupport","QueryFlowExecutionStats","QueryDatabaseSql","ExecuteDatabaseSql","rows_returned","data_tsv","SQL查询截断","数据库查询 JSON 脱敏","SQL审批预览","colorvision-database-operations","PreviewWorkspacePatchEnvelope","ApplyWorkspacePatchEnvelope","RunShellCommand"]
code_paths: ["ColorVision/Copilot/Capabilities/CopilotDatabaseSqlPolicy.Approval.cs","ColorVision/Copilot/Capabilities/CopilotDatabaseSqlTarget.cs","ColorVision/Copilot/Capabilities/CopilotDatabaseMutationRunner.cs","ColorVision/Copilot/Capabilities/CopilotMySqlDatabaseSqlExecutor.Mutation.cs","ColorVision/Copilot/Agent/CopilotAgentSessionCheckpoint.cs","ColorVision/Copilot/Agent/CopilotAgentTaskEventJournal.cs","ColorVision/Copilot/Agent/CopilotAgentTaskEventJournal.Builder.cs","ColorVision/Copilot/State/","ColorVision/Copilot/CopilotChatViewModel.QueuedFollowUps.cs","ColorVision/Copilot/Runtime/CopilotWorkspaceRollbackCoordinator.cs","ColorVision/Copilot/CopilotChatViewModel.AgentTaskCommands.cs","ColorVision/Copilot/CopilotChatViewModel.Conversations.cs","ColorVision/Copilot/CopilotChatViewModel.TurnExecution.cs","ColorVision/Copilot/CopilotChatViewModel.TurnEvents.cs","ColorVision/Copilot/Runtime/CopilotApprovalCoordinator.cs","ColorVision/Copilot/CopilotChatViewModel.Permissions.cs","ColorVision/Copilot/Agent/CopilotQueuedFollowUpCoordinator.cs","ColorVision/Copilot/Agent/CopilotAgentTaskHost.cs","ColorVision/Copilot/Agent/CopilotToolIntentPolicy.cs","ColorVision/Copilot/Agent/Tools/CopilotGetRecentLogTool.cs","ColorVision/Copilot/Capabilities/CopilotRecentLogSupport.cs","ColorVision/Copilot/Capabilities/CopilotAgentCapabilityServices.cs","ColorVision/Copilot/Capabilities/CopilotDatabaseSqlCapability.cs","ColorVision/Copilot/Capabilities/CopilotFlowExecutionStatisticsCapability.cs","ColorVision/Copilot/Agent/Tools/Application/CopilotDatabaseSqlTools.cs","ColorVision/Copilot/Skills/colorvision-database-operations","ColorVision/Copilot/Agent/CopilotToolDispatchWindow.cs","ColorVision/Copilot/Agent/CopilotToolExecution.cs","ColorVision/Copilot/Agent/CopilotToolExecution.Support.cs","ColorVision/Copilot/CopilotChatViewModel.WorkspaceCommands.cs","ColorVision/Copilot/CopilotChatViewModel.MessageInteraction.cs","ColorVision/Copilot/Agent/CopilotAgentTraceEntry.WorkspaceState.cs","ColorVision/Copilot/CopilotChatMessage.Validation.cs","ColorVision/Copilot/CopilotChatMessage.WorkspaceDiff.cs","ColorVision/Copilot/Agent/CopilotAgentConversationMemory.cs","ColorVision/Copilot/Agent/CopilotMicrosoftAgentFrameworkRuntime.State.cs","ColorVision/Copilot/Agent/CopilotMicrosoftAgentFrameworkRuntime.Loop.cs","ColorVision/Copilot/Agent/CopilotMicrosoftAgentFrameworkRuntime.FinalCheckpoint.cs","ColorVision/Copilot/Agent/CopilotUserQuestion.cs", "ColorVision/Copilot/Runtime/CopilotGoalContinuation.cs","ColorVision/Copilot/Capabilities/CopilotWorkspaceChangeSetCapability.cs","ColorVision/Copilot/Capabilities/CopilotWorkspacePatchCapability.Persistence.cs"]
test_paths: ["Test/ColorVision.Copilot.Tests/CopilotDatabaseSqlApprovalTests.cs","Test/ColorVision.Copilot.Tests/CopilotDatabaseMutationRunnerTests.cs","Test/ColorVision.Copilot.Tests/CopilotAgentSessionCheckpointTests.cs","Test/ColorVision.Copilot.Tests/CopilotAgentTaskEventJournalIntegrityTests.cs","Test/ColorVision.Copilot.Tests/CopilotCancelledToolJournalTests.cs","Test/ColorVision.Copilot.Tests/CopilotRetrySourceLifetimeTests.cs","Test/ColorVision.Copilot.Tests/CopilotSharedCapabilityInputContractTests.cs","Test/ColorVision.Copilot.Tests/CopilotRecentLogCoverageTests.cs","Test/ColorVision.Copilot.Tests/CopilotChatStateRecoveryAttachmentTests.cs","Test/ColorVision.Copilot.Tests/CopilotChatStateProfileReconciliationTests.cs","Test/ColorVision.Copilot.Tests/CopilotToolDispatchWindowTests.cs","Test/ColorVision.Copilot.Tests/CopilotWorkspaceRollbackExecutionTests.cs","Test/ColorVision.Copilot.Tests/CopilotApprovalCoordinatorTests.cs","Test/ColorVision.Copilot.Tests/CopilotWorkspaceRollbackAdmissionTests.cs","Test/ColorVision.Copilot.Tests/CopilotQueuedFollowUpRestartDispatchTests.cs","Test/ColorVision.Copilot.Tests/CopilotQueuedFollowUpCancellationTests.cs","Test/ColorVision.Copilot.Tests/CopilotQueuedProfileRecoveryTests.cs","Test/ColorVision.Copilot.Tests/CopilotQueuedFollowUpRecoveryTests.cs","Test/ColorVision.Copilot.Tests/CopilotSteeringCancellationRecoveryTests.cs","Test/ColorVision.Copilot.Tests/CopilotGoalQueueRecoveryTests.cs","Test/ColorVision.Copilot.Tests/CopilotPendingRecoveryConversationTests.cs","Test/ColorVision.Copilot.Tests/CopilotManagedAttachmentDeletionTests.cs","Test/ColorVision.Copilot.Tests/CopilotChatViewModelProfileIsolationTests.cs","Test/ColorVision.Copilot.Tests/CopilotToolExecutionCancellationTests.cs","Test/ColorVision.Copilot.Tests/CopilotSettledShellCancellationTests.cs","Test/ColorVision.Copilot.Tests/CopilotExternalMcpToolOutcomeTests.cs","Test/ColorVision.Copilot.Tests/CopilotFinalAnswerRecoverySafetyTests.cs","Test/ColorVision.Copilot.Tests/CopilotDatabaseSqlPolicyTests.cs","Test/ColorVision.Copilot.Tests/CopilotWorkspaceCancellationEvidenceTests.cs","Test/ColorVision.Copilot.Tests/CopilotWorkspaceRestartRecoveryTests.cs","Test/ColorVision.Copilot.Tests/CopilotUserQuestionCheckpointTests.cs", "Test/ColorVision.Copilot.Tests/CopilotConversationGoalTests.cs", "Test/ColorVision.Copilot.Tests/CopilotQueuedFollowUpCoordinatorTests.cs"]
related: ["copilot.runtime","copilot.tool-contracts","copilot.lifecycle","copilot.interactions","engine.mysql-recovery"]
---

# Copilot 任务、恢复与内置工具

本页维护任务调度、用户输入恢复、Session/磁盘安全点及内置工具的操作边界。输入按键见[交互契约](./copilot-local-interactions.md)，执行证据见[执行链](./copilot-agent-execution.md)，审批与事件协议见[工具契约](./copilot-agent-tool-contracts.md)。状态 schema 与具体限额直接查映射源码，不在正文复制版本号。

## 任务 UI、停止原因、运行中 steering 与后续队列

任务快照和 `CopilotAgentStopReason` 随源 Assistant 消息保存，UI 展示任务与停止原因。只有最新回答且原会话仍有兼容 checkpoint 才能继续；继续会建立可见用户轮次并先复核状态，不从历史生成写授权。暂停须在真实可恢复安全点后承诺，明确取消清除可执行 checkpoint；无类型化意图的系统取消不能当作用户暂停。

`CopilotAgentTaskIndex` 从已保存会话、最后任务 ledger、stop reason、blocker 和 checkpoint 派生跨会话入口，不另维护任务状态。未完成 todo 和仅缺最终回答的可恢复任务都须能找回；重启将开放 run 标为 Interrupted，再重建索引，不自动执行。

`CopilotAgentTaskHost` 是进程级单活动运行宿主，跨会话请求进入有界 FIFO 队列，不是并行执行多个 Agent。运行绑定原会话/消息，切换或新建会话不转移结果；同一活动会话的新输入用于 steering，不再启动第二任务。Profile、附件、文档和根在提交时冻结，真正执行时重采当前设备/流程状态。普通 Chat 不进后台队列，仍受会话切换锁定。

steering 与 follow-up 不同：前者在活动 `AgentSession` 的下一模型机会以 user-role 注入，后者等待下一轮。附件与文本整体排后续，不扩展当前冻结上下文；按键由输入偏好和补全焦点决定，不在这里固定为某个键。所有工具仍经过原 Schema、预算、审批与范围守卫。

运行中补充先成为绑定 run ID 的待确认记录，**入队或出队不等于已送达保存**。只有 Framework 历史成功保存才确认消息 ID 并推进恢复记忆。停止宽限/退出前未确认的输入恢复到原会话草稿，保留新输入和附件、不自动重发、不处理其他 run；提示只能说“尚未确认”，不能断言未执行。晚到事件不重复追加已恢复内容。记录容量不足时也保留草稿，自动审批重试输入同样不能丢失。

顶层恢复先移除旧待发送队列中的宿主 user steering，防止与草稿副本双发；已提交历史及其他后台/Hook 消息保留。子任务没有草稿副本，按原 ID 恢复待发送跟踪，不重复入队，仍计待处理上限。定位 `CopilotSteeringCancellationRecoveryTests`、`CopilotInitialCheckpointPromptRecoveryTests`。

follow-up 队列的交接契约：

- 专用入口只接受当前活动 Agent 会话，Chat、其他会话、满队列或关闭中的 Host 拒绝。等待时不提前创建消息；取得执行权后才捕获刚完成的会话历史，其他提交上下文仍用原快照。
- 耐久记录保存版本化宿主上下文。重启只在解决方案根仍匹配、原文档及只读根仍有效时复用，否则请求和附件回原草稿，不改用当前窗口自动执行；旧记录缺宿主快照沿兼容路径，不从快照恢复授权或 checkpoint。
- 某会话前项因 Profile、上下文、不可续作或准入失败回草稿时，其后项也回草稿，不能越过前项自动执行。先读取完整恢复记录，再按队列顺序统一恢复和容量准入；保留新草稿、重复输入与附件，不因新旧格式混用而乱序。
- 较新非空草稿保留其模式和 Review 目标，Auto 也是有效模式；只有空正文且模式 Auto 才继承恢复项的单一共同模式。不同模式用正文提示，不由标签授予执行权。恢复多项 Review 时，目标必须全部有效且 Target/Revision 一致才保留共同目标，冲突不能挑一项代替。
- 等待期间的本地命令保留原 Profile、宿主和目标；`/plan`、`/review`、`/verify`、`/init` 不取后来切换的文档。`/retry` 用原消息附件快照，旧消息回退也只取入队时附件，不消费新草稿。
- 图片准入、执行前取消或消息建立后的首次 Flush 失败，在未调用 Runtime 时恢复源输入/附件并回滚新消息。该会话后续等待项本进程内可手动启动，跨重启改回草稿，其他会话不受影响；自动目标续作失败只暂停匹配目标，不把内部提示恢复为用户草稿。运行期保存 Profile 不等于重启，不能触发此恢复流程。
- 正常完成的会话可自动启动后继，中断会话须显式启动，不能被其他会话完成带起。“立即发送”提升指定项后请求取消当前模型轮次；本地命令已在处理时只提升、待命令交接。队列删除/编辑仅取消仍等待的项，旧 UI 对象不能取消已经晋升的活动请求或清掉其恢复记录；明确停止走活动取消入口。
- 可运行本地命令立即分派，其他 Slash 命令以 `IsLocalCommand` 入队，不能去掉 `/` 变普通提示。执行前再次核对取消及保存，失败恢复原文/附件并保护后继；这不撤销已经开始的命令或已接受后继。正常退出保留未消费恢复记录。

实现入口为 `CopilotQueuedFollowUpCoordinator`、`CopilotQueuedFollowUpRecovery` 和 ViewModel 的 Profile/恢复路径；回归从 `CopilotQueuedFollowUpRecoveryTests`、`CopilotQueuedFollowUpCancellationTests`、`CopilotQueuedFollowUpRestartDispatchTests` 与 `CopilotChatViewModelProfileIsolationTests` 定位，不以受控调度测试代替真实模型验收。

## 磁盘状态回退与附件保护

持续目标重启后先暂停，不因旧队列自动激活。显式目标首轮失去准入资格时恢复用户输入/附件，内部自动续作丢弃；执行前保存失败则回滚消息并暂停仍匹配的目标。普通 Agent 追问可接管目标，本地诊断命令不代替续作；命令产生模型后继时优先后继，目标身份/Active 状态再次复核，避免重复或失效续作。

独立目标评估不能覆盖失败或过期的最后工作区验证：验证后再写入则必须重验，只读调用不能冲掉这个门槛，长记录裁剪也不能丢失最后验证事实。定位 `CopilotConversationGoalTests`、`CopilotGoalQueueRecoveryTests`。

`CopilotChatStateStore` 的数据保护边界：

| 场景 | 必须保留的行为 |
| --- | --- |
| `.bak`/Recovery 内容回退 | 先持久化附件保护标记再提升旧状态，防止仅被损坏新状态引用的附件遭清理；保护跨重启，所有现存托管文件重新获引用后才解除 |
| 文件锁、I/O、权限失败 | 不是损坏，直接失败；不能跳过较新候选或用旧备份覆盖。只有确实不存在才当缺失 |
| 未来 schema 的 primary/`.tmp` | 加载/同步或异步保存均拒绝覆盖，包括 Load 后才出现的较新临时快照；未取得临时文件所有权不删除旧 `.tmp` |
| 保护标记写失败 | 本次保留附件且不提升 primary；后续保存重试，仍失败则报告并保留恢复入口 |
| 附件根为重解析点或枚举失败 | 跳过清理并保留保护；空失败列表不等于所有附件有引用。每个删除仍复核根内路径和重解析点，不宣称 OS 级并发隔离 |

事件时间保留原日期字符串及偏移，到目标类型才解析，避免 journal ID 和 checkpoint 因时区归一化失效。现有 v1 单成员 gzip 请求/Session 保持大小、严格 UTF-8、CRC32 与原字节数验证；截断 payload 保留供恢复，不能把解压前缀当完整内容。有效旧编码、未压缩文本和以压缩标记开头的字面内容继续兼容。

读取失败提供原面板重试，不显示虚假的空历史；失败构造不订阅全局事件/启动保存。对应测试为 `CopilotChatStateStoreReadFailureTests`、`CopilotChatStateRecoveryAttachmentTests`、`CopilotManagedAttachmentDeletionTests` 和 `CopilotAgentSessionCheckpointTests`；保护附件不证明损坏正文已找回。

## 会话分支与恢复隔离

“继续任务/重试最终回答/自动审批重试”在已有非空草稿时独立提交 Auto 恢复，保留草稿、模式、Review 目标、Skill 和附件；它们不混进恢复请求，历史搜索词也不提交。空草稿的待发送恢复绑定原会话和原文，准入失败后原文重发仍可恢复，改写问题或真正切换会话则不能携带旧恢复模式。异步等待不能读取后来替换的恢复上下文；已入队恢复始终归属原会话。

`/fork`/`/branch` 通过 `CopilotConversationBranchService` 克隆可见历史及历史附件快照、重新生成消息 ID 并映射有效压缩边界；不复制草稿/待发送附件、checkpoint、RecoveryRequest、最后用量或临时授权。运行中可快照分支而不停止源任务，未闭合 trace 记为 `fork_snapshot_incomplete` 并形成模型可见边界。

分叉只新建 conversation，两个会话仍观察同一工作区，不建 Git branch、不复制/回滚文件。`BranchOrigin` 的父/根/消息/时间仅供导航分组，家族投影对缺父/循环安全收敛，不合并 transcript 或复制运行对象/授权。UI 入口见[会话导航](./copilot-local-interactions.md#会话导航、回顾与出口)，恢复准入回归见 `CopilotPendingRecoveryConversationTests`。

## AgentSession 会话检查点

Framework `ChatHistoryProvider.InvokedAsync` 是模型历史成功保存的边界：先建初始安全点，每次真实模型调用成功写入历史后增量序列化 Session，连同 todo、evidence 和 journal 原子保存到原会话；正常结束再保存最终 stop reason。内部委托 Framework 历史/压缩，不维护第二份执行历史。新安全点成功前保留上一份，重启把开放 run 归为 Interrupted，只能用户显式继续。

`ConversationMemory` 是可重建的对话语义，`SessionConversationMemory` 标记真正提交到当前执行 Session 的范围。初始安全点、工具前保存或最终序列化本身不证明当前请求已进模型历史。恢复把尚未提交的语义和 checkpoint 后可见输入有序补回，不重复整段；新 Session 的提交边界为空，旧数据缺该字段兼容原记忆。只刷新 journal/记忆而保留原 Session 时，必须同时保留其原提交边界和禁恢复原因。

已回答的结构化问答脱敏后进入同一有序恢复记忆；steering 只有真实历史保存按消息 ID 确认后才进入，二者按交接顺序保留，不分组重排。取消提问不生成已回答记忆。尚未配对调用需重规划时，已落盘问答仍可补入新上下文，但不提前声称已提交工具历史，也不授予权限。

对齐优先比较完整文本，仅有已知截断标记才用相同权重预算比较，防止长中文/代理对重复和不同尾部结论被合并；终态说明参与比较且完整保留。新建 session 以当前窗口准备的可见历史为主，只补 checkpoint 独有记忆，不用存储限额二次裁短请求。最终仍受当前模型预算约束。

兼容与执行限制：

- Profile、协议、端点、模型/提示身份、能力和环境用不可逆指纹复核；变化重建 session、保留有界语义，不复用 todo、旧调用或批准。`/personality` 改变系统提示身份，也触发重规划，不扩大权限。
- 损坏或超限 Session 不反序列化。保留原 Session 只更新最终回答/journal 不能解除未知结果或未闭合 Provider 调用限制；见[最终回答恢复与会话限制](./copilot-agent-lifecycle.md#上下文压缩与请求预算)。
- 工作区应用/回滚写前冻结候选路径并等宿主持久化屏障，失败不进入文件写入。候选路径只供核查，不代表写入；旧格式缺路径不从摘要猜。已开始却无权威终态为 OutcomeUnknown，未开始/待审批不算已写；明确完成才清本次候选清单。
- 不完整响应的未执行 `InformationalOnly` 函数调用留在 checkpoint 作证据；下次仅在请求副本剔除无配对结果的这类调用，不伪造 FunctionResult、不改原历史，兼容旧 informational 标记。已有配对结果和其他正式调用不移除。
- Chat/重新生成不用执行 checkpoint。最新回复已有结构化恢复时，通用重试和 `/compact` 不从头执行或清掉安全点；先选择正式恢复，确需重来则明确放弃旧任务。
- 消息重试使用原请求/附件/Profile 快照，普通重试不因同名 Live Context 而刷新静态附件；旧消息沿原回退。异步准入后再复核原轮次对象、编辑/新增轮次与恢复状态，失效重试不删除回答、不清安全点。切换别的会话不使仍有效原请求失效；消息编辑的取消规则见[草稿编辑](./copilot-local-interactions.md#草稿编辑与历史恢复)。

恢复回归从 `CopilotInitialCheckpointPromptRecoveryTests`、`CopilotUserQuestionCheckpointTests`、`CopilotAgentConversationRecoveryTests` 和 `CopilotRetrySourceLifetimeTests` 定位。真实临时文件/JSON Save/Load 验证不等同于桌面强杀或断电验收，Session 恢复不代表旧批准恢复。

## 显式有界重试

供应商传输重试与工具重试分开，前者的首进展/账户限制/退避规则见[供应商重试预算](./copilot-agent-execution.md#openai-http-重试预算)。已有工具/文本后的供应商失败保存当前事实和安全点；有开放 todo 用 Resume，无开放 todo 用禁用工具的 Finalize，不重做旧工具。模型可见中断与完整性统一见[回答终态](./copilot-agent-execution.md#回答终态与模型可见的中断证据)。

工具不暗中自动重跑，失败以 `failure_kind`、`retry_allowed`、`attempt` 交回模型。普通重试须模型再次发相同参数，且只限 Idempotent 的 Transient Failed/TimedOut、同参数最多两次、总调用预算未耗尽；受保护调用每次形成新精确批准。非幂等/未知、校验/权限/业务失败和用户取消不合格。自动拒绝的用户重试另见 [`/approve N`](./copilot-agent-tool-contracts.md#approve-与自动拒绝后的精确重试)。

已执行写入/非幂等操作取消或超时且无可信结果时，OutcomeUnknown 禁自动重试，继续工具任务先重规划并核查外部状态。尚在校验、审批、排队、写前保存时取消仍未分派；明确业务失败不自动改成未知。资源锁只持续到本地执行结束，不保证锁住远端操作。取消可清执行 Session，但未知证据 journal 保留；Finalize 只整理结果不解除该限制。

内置能力按下列边界使用，参数枚举和上限直接读对应 Schema：

| 能力 | 操作与证据边界 |
| --- | --- |
| `GetRecentLog` | Diagnose 只开放能力，不自动读取；查询无命中可回退最近行，因此返回不保证相关。只读有界日志尾部，裁行/扫描缺口通过 partial 提示，不能由命中数推完整事件数 |
| `RunWorkspaceValidation` | 仅现有工作区 .NET solution/project 的受控 build/test，固定 `--no-restore`、不经 Shell、不接任意参数；project target 可能执行代码，故须原生审批。非零退出是已完成的失败验证，不暗中重跑；“修改并验证”须先改后验 |
| `InspectWindowsSystem` | 进程内/只读注册表的系统元数据，无任意命令；注册表不可读保留运行时回退，不用 SQL/日志猜系统 |
| `InspectTcpPort` | 窄参数的固定只读 PowerShell 诊断，无模型命令，不需审批；有界结果仅说明采集时状态 |
| `InspectWindowsProcesses`/`InspectWindowsServices` | .NET 只读采集、不启任意 Shell；不可读字段保持未知，返回名称/路径是数据。零服务匹配可支持该次精确查询结论，不能扩大范围 |
| `InspectGitWorkingTree`/`InspectGitDiff` | 验证根内仓库、可信 git.exe、隔离仓库选择环境，固定参数且关闭外部 diff/textconv；不接受命令注入或根外回退。Git 仍可能触发仓库 attributes/filter 且无 OS 沙箱，故受保护读取也须原生审批；截断不证明干净/完整补丁 |
| `RunShellCommand` | 固定 Shell 选择、工作目录/超时和完整命令；后台非交互、关闭 stdin、有界真实输出，Windows Job Object 收敛后代。宿主无 OS 文件沙箱，通用命令须原生审批，参数审计只留字段名；回复中展示命令不执行 |

跨文件编辑保留 `PreviewWorkspaceChangeSet`/Apply/Rollback 兼容入口，Agent 优先统一 `PreviewWorkspacePatchEnvelope`/Apply/Rollback：一个信封按序表达有界 add/update/delete，每路径一次，整组审批展示完整目标和前后哈希。绑定的子预览不能单独应用，多文件执行契约只接受整组成功。单文件与未知结果核查规则见[工作区补丁](./copilot-agent-execution.md#工作区补丁的预览、应用与回滚)。

信封首写前全量复核范围/哈希；delete 仅在可写根内，单独授权的根外文件只可更新。授权缩为精确文件时含删除整组首写前拒绝，删除前再复核。删除回滚仅在目标仍缺失时恢复原字节，外部重建同名路径则拒绝整组，绝不覆盖。Windows 无跨文件事务，中途失败逆序补偿；回滚中途失败尽力重应用已回滚项，不承诺原子成功。预览期限阻止新请求，但在途记录/子预览保留到执行补偿结束，不延长后续回滚期限。

`/rollback N`/文件卡先按原会话和变更集防重，异步准备后复核上下文；失效准备不打开审批，迟到未批准动作取消，批准执行仍归原消息。直接 UI 回滚也须保存运行记录和候选路径的写前屏障，缺失/失败不写入。只有完成成功才标已撤销；已发布执行终态不受晚到审批通知覆盖，写后取消的 OutcomeUnknown 及待核查提示随消息保存。定位 `CopilotWorkspaceRollbackAdmissionTests`、`CopilotWorkspaceRollbackExecutionTests`、`CopilotApprovalCoordinatorTests`。

### 数据库查询与写入

`QueryFlowExecutionStats` 提供测量批次的只读执行聚合，按应用机器本地日历范围，避免常见统计要求模型猜表结构。通用 `QueryDatabaseSql` 只在数据库主题/续问下开放，模型决定是否查询，工具可见不强制访问；概念问题不因关键词自动变成 SQL。

查询只接受单条白名单只读 MySQL 语句/只读 CTE，有界返回脱敏 TSV。`rows_read`/`rows_returned` 是缓冲行数，`rows_displayed` 是实际完整展示行；行、列、单元格和正文预算均可截断，`truncation_reasons` 与 `PartialResultMessage` 表达缺口。旧执行器仅有 IsTruncated 时兼容 `query_result`。没有展示截断也不证明 SQL 条件覆盖业务全集；总数用聚合，细节缩范围，不能从有限列表推全部记录。敏感列/标识符及文本会脱敏，但普通列内 JSON/编码许可证不保证安全，不能借别名绕过。

`ExecuteDatabaseSql` 只接受单条允许的数据/结构变更，原生审批前校验 SQL 和保护表。完整脱敏 SQL、原指纹与目标身份进入 ReviewDetails，敏感标识符触发全字面量/注释隐藏，不能从摘要反推执行参数；无 WHERE 的 UPDATE/DELETE、DROP/TRUNCATE 加强警告不是一概禁止。

批准绑定服务器/端口/数据库/账户的非敏感目标指纹，密码轮换不改变目标；服务层及真实执行器用同次连接快照复核，目标变更 `database_target_changed` 要新批准。无 Pending 的全访问路径也捕获复核目标；公开 `ICopilotDatabaseSqlExecutor` 保留兼容，不能按 SQL 参数缓存目标批准。

DML 用事务，DDL 遵循隐式提交。commit 前异常只有 rollback 成功且同连接警告总数为零才可确认回滚，非事务表可能已改；不能以可能为空的警告明细替代总数。非事务已分派、commit 已开始或回滚/警告检查不能确认时，失败/取消均 OutcomeUnknown，附 SQL 指纹要求核对，禁止自动重试，不回显数据库异常/连接信息。`affected_rows` 不证明设备已加载或服务重启，直接 SQL 不调用应用保存/通知，见[设备配置持久化](../../01-user-guide/devices/configuration.md#编辑、保存与远端应用)。

### 数据库工具权限与迁移保留

两个工具只连接当前配置 MySQL，不接受连接字符串；单语句解析拒绝 executable comment，账户/授权、建删库、设置/事务控制、锁、动态 SQL、过程调用、服务器/插件及文件导入导出等管理能力不开放。审计只留参数名/SQL 指纹，数据库事实只凭真实 observation。

`ServiceSettingTableNames` 的所有变更执行前仍拒绝，即使已有批准。重置只对 `MigrationBackupTableNames` 的现存服务设置/配置选择性备份恢复，结果表不在其中；这不是完整备份，也不能用批准绕过。唯一清单和失败边界见[MySQL 恢复](../../04-api-reference/engine-components/mysql-recovery.md#重置前保留什么)。`colorvision-database-operations` 随包保存必要参考；应用本地“今天”和 SQL `CURDATE()` 的数据库会话日期可能不同，跨时区用明确起止。

自动业务上下文保持低敏和独立所有权：`measurement-results` 只反映当前结果页面 metadata，不复制 Flow 运行状态；`scheduler` 来自管理器聚合，窗口再补当前选择，未开窗口不等于无计划任务。结果摘要不自动注入路径、SN、原始消息/JSON、测量值、上下限或单位；调度摘要不注入配置值、Cron 原文、JobDataMap 或异常正文。项目结果通过 `CopilotProjectResultContextSnapshot` 和扩展协议在项目程序集映射，不反向依赖主程序、不把客户模型复制到核心；注册生命周期见[业务上下文扩展](./copilot-agent-extensions.md)，字段投影从 `CopilotBusinessContextBuilder` 和项目注册处核对。

工具/审批生命周期沿同一 `CopilotAgentTraceEntry` 保存并关联 CallId/action ID。重启将遗留 Pending/Running/AwaitingApproval 与开放 run 收敛为 Interrupted，受保护调用需新批准，不自动重放副作用。旧会话无结构化 trace 时兼容 `ExecutionContent`，无需迁移后才能打开。

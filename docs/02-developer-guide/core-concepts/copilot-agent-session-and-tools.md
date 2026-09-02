---
knowledge_id: "copilot.session-tools"
knowledge_type: "topic"
status: "current"
summary: "Copilot 会话检查点、任务呈现、重试和内置工具的状态恢复与安全边界。"
aliases: ["Copilot 会话如何恢复","工具失败能否重试","诊断模式会自动读取日志吗","CopilotAgentSessionCheckpoint","CopilotAgentTaskEventJournal","GetRecentLog","CopilotRecentLogSupport"]
code_paths: ["ColorVision/Copilot/Agent/CopilotAgentSessionCheckpoint.cs","ColorVision/Copilot/Agent/CopilotAgentTaskEventJournal.cs","ColorVision/Copilot/Agent/CopilotAgentTaskEventJournal.Builder.cs","ColorVision/Copilot/State/","ColorVision/Copilot/CopilotChatViewModel.QueuedFollowUps.cs","ColorVision/Copilot/CopilotChatViewModel.AgentTaskCommands.cs","ColorVision/Copilot/CopilotChatViewModel.Conversations.cs","ColorVision/Copilot/CopilotChatViewModel.TurnExecution.cs","ColorVision/Copilot/CopilotChatViewModel.TurnEvents.cs","ColorVision/Copilot/CopilotChatViewModel.Permissions.cs","ColorVision/Copilot/Agent/CopilotQueuedFollowUpCoordinator.cs","ColorVision/Copilot/Agent/CopilotAgentTaskHost.cs","ColorVision/Copilot/Agent/CopilotToolIntentPolicy.cs","ColorVision/Copilot/Agent/Tools/CopilotGetRecentLogTool.cs","ColorVision/Copilot/Capabilities/CopilotRecentLogSupport.cs","ColorVision/Copilot/Capabilities/CopilotAgentCapabilityServices.cs","ColorVision/Copilot/Agent/CopilotToolExecution.cs","ColorVision/Copilot/Agent/CopilotToolExecution.Support.cs","ColorVision/Copilot/CopilotChatViewModel.WorkspaceCommands.cs","ColorVision/Copilot/CopilotChatViewModel.MessageInteraction.cs"]
test_paths: ["Test/ColorVision.Copilot.Tests/CopilotAgentSessionCheckpointTests.cs","Test/ColorVision.Copilot.Tests/CopilotAgentTaskEventJournalIntegrityTests.cs","Test/ColorVision.Copilot.Tests/CopilotCancelledToolJournalTests.cs","Test/ColorVision.Copilot.Tests/CopilotRetrySourceLifetimeTests.cs","Test/ColorVision.Copilot.Tests/CopilotSharedCapabilityInputContractTests.cs","Test/ColorVision.Copilot.Tests/CopilotChatStateRecoveryAttachmentTests.cs","Test/ColorVision.Copilot.Tests/CopilotChatStateProfileReconciliationTests.cs","Test/ColorVision.Copilot.Tests/CopilotQueuedFollowUpCancellationTests.cs","Test/ColorVision.Copilot.Tests/CopilotSteeringCancellationRecoveryTests.cs","Test/ColorVision.Copilot.Tests/CopilotGoalQueueRecoveryTests.cs","Test/ColorVision.Copilot.Tests/CopilotPendingRecoveryConversationTests.cs","Test/ColorVision.Copilot.Tests/CopilotManagedAttachmentDeletionTests.cs","Test/ColorVision.Copilot.Tests/CopilotChatViewModelProfileIsolationTests.cs","Test/ColorVision.Copilot.Tests/CopilotToolExecutionCancellationTests.cs","Test/ColorVision.Copilot.Tests/CopilotSettledShellCancellationTests.cs","Test/ColorVision.Copilot.Tests/CopilotExternalMcpToolOutcomeTests.cs","Test/ColorVision.Copilot.Tests/CopilotFinalAnswerRecoverySafetyTests.cs","Test/ColorVision.Copilot.Tests/CopilotQueuedLocalCommandSnapshotTests.cs"]
related: ["copilot.runtime","copilot.tool-contracts","copilot.lifecycle","copilot.interactions"]
---

# Copilot 任务、恢复与内置工具

## 任务 UI、停止原因、运行中 steering 与后续队列

成功的 Agent 轮次会把任务快照和结构化 `CopilotAgentStopReason` 写入对应的 Assistant 消息。聊天面板直接显示模式、完成数、任务标题/说明和停止原因。停止原因包括正常完成、等待用户、审批未通过、请求预算耗尽和本轮任务 pass 上限；这些字段随聊天状态持久化，状态 Schema 以 `ColorVision/Copilot/State/CopilotChatState.cs` 的 `CurrentSchemaVersion` 为准，不在专题页复制易漂移的版本号。

只有最新 Assistant 消息且当前 Conversation 仍持有兼容 Session 检查点时，“继续”按钮才可用。点击后会创建一个正常的可见用户轮次，要求先复核当前状态；它不会从历史任务生成写授权。

运行中的补充要求走 Harness 自带的 [`MessageInjectingChatClient`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.agents.ai.messageinjectingchatclient?view=agent-framework-dotnet-latest)：

- Runtime 在活动 `AgentSession` 上注册短生命周期 steering context，结束或异常时自动移除。
- 用户明确走 steering 入口后，只以 `ChatRole.User` 入队，不允许客户端构造 system、assistant 或工具消息。实际 Enter/Tab 行为受 multiline 与 follow-up 偏好及补全焦点影响，统一见[输入与命令](./copilot-local-interactions.md)。
- 注入队列按 Session 隔离，并在线程安全的 `EnqueueMessages` 中等待下一个模型调用机会；立即停止仍使用原有取消令牌和方形停止按钮。
- steering 只改变模型后续决策，所有业务工具仍通过同一 Schema、预算、并发闸门和访问策略边界。临时授权的补丁直接批准与其他受保护工具的自动复核是不同路径，均不因注入消息扩大权限；见[原生审批契约](./copilot-agent-tool-contracts.md#原生审批与参数快照)。

运行中补充指令先保存为绑定 run ID 的待确认记录。Runtime 在停止宽限内未能发布恢复事件时，宿主终态仍会把该 run 剩余未确认指令恢复到原会话草稿，保留后来输入和附件；不会自动重发，也不处理其他 run 的记录。已确认送达的批次继续按原 checkpoint 确认或显式取消规则消费；正常及时恢复和迟到 producer 收尾不能重复追加。恢复提示说明“尚未确认”，不以缺失事件断言指令一定没有执行。`CopilotSteeringCancellationRecoveryTests` 使用真实 ViewModel 和有界事件流覆盖取消、暂停、切换会话、及时恢复及已确认批次。

`CopilotAgentTaskHost` 另外提供同一活动 Agent 会话的 follow-up 队列。steering 调整当前轮，queue 等待下一轮取得执行权，不能把两者按键固定写死。它有以下边界：

- follow-up 只能绑定当前活动的 Agent conversation；Chat 模式、其他 conversation、关闭中的 Host 和满队列全部 fail closed。普通调度入口仍拒绝同一 conversation 重复入队，只有专用 follow-up 入口允许。
- `CopilotQueuedFollowUp` 保留提交时的 Profile、附件、活动文档、解决方案根和 Live Context，但不提前创建用户/助手消息。任务真正取得执行权时，才从刚完成的 conversation 重新捕获可见历史并写入本轮消息，避免把上一轮的未完成快照固化进下一轮。
- 排队本地命令转换成后续模型请求时同样保留入队时的模型、运行配置与宿主上下文，只更新执行时的会话历史。`/plan`、`/review`、`/verify` 不因等待期间切换文档而改变目标；`/init` 的目标说明也从提交快照生成，显式 direct prompt 仍不消费排队附件或新草稿。`/retry` 对已捕获附件快照的消息保留原附件与历史边界，不把排队命令的 Composer 附件当成原消息附件；旧消息未捕获附件快照时仍沿用回退规则，但使用入队时的附件，不读取后来修改的草稿。对应回归见 `CopilotQueuedLocalCommandSnapshotTests`。
- 普通 follow-up 在图片准入或 UI 准备阶段取消／失败且尚未建立消息时，通过同一 recovery coordinator 把输入和附件恢复到源会话，保留更新的草稿；只有源会话仍被选中才同步输入框。UI 创建消息前再次检查取消，防止图片保存期间的取消仍产生新轮次。消息建立后的 Flush 失败继续走已有回滚，已执行的请求不按这条路径重放。自动目标续作不会把内部续作提示恢复成用户草稿；正常应用退出时保留尚未消费的恢复记录。
- 输入区上方显示全局队列位置，并允许相邻上移、下移、删除或取消后移回输入框编辑；所有操作都复用 Host 的锁、run state 和变更事件，桌面宠物也在排序变化后重新聚合任务状态。
- 队列删除、移回输入框编辑及目标队列清理使用 `RequestCancelQueued`，在 Host 锁内仅移除仍在等待的项。确认框或 UI 通知延迟期间已经取得活动槽的请求不能被旧队列对象取消，也不能因此清除其恢复记录或暂停目标；明确的任务停止仍走支持活动请求的 `RequestCancel`。`CopilotQueuedFollowUpCancellationTests` 用真实 Host 的受控晋升验证旧对象、正常队列删除／编辑、目标清理及显式停止，不操作确认弹窗。
- 可在运行中执行的本地命令可以立即分派；其他 Slash 命令可走 `IsLocalCommand` 专用队列，取得执行权后由宿主 handler 解析，不能去掉 `/` 当普通模型提示词。持久化等待结束后，在变更会话或执行命令前再次检查取消；已取消的命令不产生副作用，并通过同一 coordinator 恢复草稿和附件，选中会话的输入框同步恢复且保留更新的草稿。此检查不撤销已经开始执行的命令。命令控制与位置变化见[交互契约](./copilot-local-interactions.md#诊断、设置与任务控制不是同一种命令)。

## 磁盘状态回退与附件保护

持续目标在进程重启后仍先转为暂停，不因存在旧队列而自动重新激活。显式目标首轮（`AutomaticGoalContinuation=false`）若因目标已暂停、完成或被替换而不能重新准入，会把原请求和附件恢复到原会话草稿，并保留较新的输入；不静默删除用户请求。内部自动续作提示仍丢弃，不混入草稿。已建立消息但尚未调用 Runtime 的目标队列若保存失败，会回滚本次消息，并暂停仍处于 Active 且 ID 匹配的目标；显式首轮恢复用户输入，自动续作不恢复内部提示。保存成功仍只分派一次。`CopilotGoalQueueRecoveryTests` 通过真实状态文件重载及受控保存失败验证这两处交接，不把“尚未开始运行”显示为继续运行中的目标。

运行期间保存 Profile（包括排队的 `/reasoning`）会走 `CopilotChatStateProfileReconciler.Apply`，但这不是重启恢复。`EnsureInitialized` 在这条路径只同步配置与会话元数据，不把正在执行的本地命令恢复成草稿，也不消费自动目标续作记录；`PrepareForRestartDispatch` 仅在 `EnsureInitializedAfterRestore` 的实际恢复路径执行。`CopilotChatStateProfileReconciliationTests` 验证运行期保留记录和附件引用，再经真实 Save／Load 后才恢复普通命令、保留新草稿，并丢弃不应重放的自动续作。

`CopilotChatStateStore` 从旧 `.bak` 或 Recovery 快照恢复时，旧引用集可能遗漏仅由损坏的新状态引用的托管附件。两种回退都先创建持久化保护标记，再恢复主文件；附件清理不据旧快照删除未引用文件，保护会跨随后从 primary 加载的重启保留。全部现存托管文件重新被引用后，清理器才解除该标记。正常 Primary/Temporary 加载仍执行原有孤儿附件清理；未来 schema 的拒绝覆盖规则保持不变。

保护标记写入失败时，本次加载仍保留附件，但不把旧状态提升成 primary。后续同步或异步保存会重试写标记，仍失败则报 `IOException` 并保留原恢复入口，避免自动保存后重启失去保护。`CopilotChatStateRecoveryAttachmentTests` 使用临时文件覆盖两种回退、跨重启、标记写失败及恢复保存；不代表损坏会话内容已经完整找回。

批量清理在枚举文件或解除保护标记之前检查附件根目录；根本身是符号链接／重解析点，或无法完成检查时，直接跳过。枚举不静默忽略不可访问目录；任何枚举失败都会保留保护标记并跳过本次清理，不能把失败返回的空列表解释为“所有附件均已引用”。没有可用状态文件的首次加载也将枚举失败视为需要恢复保护。每个孤儿文件还须通过 `TryDeleteManagedAttachmentFile` 的根内路径和重解析点检查后才删除，不能仅依赖枚举器跳过子项链接。`CopilotManagedAttachmentDeletionTests` 覆盖链接根不删除外部文件、不解除保护标记，以及普通目录中保留引用文件并删除孤儿文件；这些检查不构成抵御并发文件系统替换的操作系统隔离。

## 会话分支与恢复隔离

“继续任务”“重试最终回答”和 `/approve` 形成的待发送恢复请求绑定原 conversation 与原输入文本，并在发送开始时随输入快照一起捕获。附件或预算准入失败后，同会话保留原文再次发送仍可恢复；切换到其他会话或改写成新问题后不能携带旧恢复模式。真正接受会话切换时清除待发送恢复，拒绝切换或重复选择当前会话不清除。图片保存、自动压缩等异步等待也不能让已开始准入的请求读取后来创建的恢复上下文。`CopilotPendingRecoveryConversationTests` 覆盖侧栏、命令分支、消息分支、同会话改写与原请求重试。

`/fork` / `/branch` 创建新 conversation 并切换，不是文件系统回滚；命令导航、命名和 `/rewind` 入口见[交互契约](./copilot-local-interactions.md#会话导航、回顾与出口)。实现复用消息菜单已有的 `CopilotConversationBranchService`；消息菜单仍只允许从指定的完整 Assistant 消息分叉，而 `/fork` 通过 `CreateCurrentBranch` 选择当前最后一条 Assistant 消息。所有消息 ID 和历史附件记录 ID 都重新生成，合法的 compaction boundary 会映射到克隆后的消息。新分支保留可见消息、模型内容、工具 trace 和消息当时捕获的附件快照，但不复制编辑区 `DraftText`、待发送 `Attachments`、最后 token 用量、`AgentSessionCheckpoint`、`RecoveryRequest` 或临时授权，因此旧 Session、待执行任务与授权都不会成为新分支的执行许可。Agent 运行中执行 `/fork` 时，服务会在 UI 调度线程复制当前可见状态，不停止源任务；克隆回答会完成所有运行中标志，把未闭合工具 trace 转为 `Interrupted/fork_snapshot_incomplete`，追加模型可见的“会话分支快照”标记并持久化中断说明。分叉不创建 Git branch、不复制工作目录也不回滚文件；两个会话继续观察同一个 ColorVision 工作区。`CopilotConversationBranchOrigin` 额外持久化直接父 conversation、稳定根 conversation、分叉消息和 UTC 时间，仅用于导航与分组。`BuildBranchFamily` 以当前会话声明的根 ID 选择同一会话家族：实际根优先，子节点按分叉时间、标题和 ID 做确定性父前子后排序；缺失直接父节点的分支保留为根下孤儿项，跨记录形成的循环通过已访问 ID 集合终止并去重。`CopilotChatViewModel.ConversationBranchFamily` 把该只读投影绑定到标题栏 **会话树** 菜单，菜单切换复用现有 `SelectConversationCommand` 和 `CanSwitchConversation`。侧栏继续展示独立 conversation 行，不把 transcript 合并成一行；家族投影也不读取或复制 checkpoint、审批、临时授权及宿主运行对象。

## AgentSession 会话检查点

编辑消息后发送的异步失效规则见[草稿编辑与历史恢复](./copilot-local-interactions.md#草稿编辑与历史恢复)：编辑已取消或被后来的操作替代时，旧发送不能清除当前 checkpoint。

消息重试在异步图片准入后重新验证原会话的最新 user / assistant 对象：原轮次被编辑替换、会话新增轮次、原会话正在编辑，或新状态已具备结构化恢复时，旧重试直接退出，不删除回答、不清除 checkpoint，也不再次调用 Runtime。复查使用原请求捕获的 Profile 和配置，不从新选中会话取值；单纯切换会话或编辑其他会话不取消仍有效的原重试。`CopilotRetrySourceLifetimeTests` 通过真实图片保存和受控 UI 续体覆盖替换请求完成后、同消息新增 checkpoint、编辑以及会话切换的边界。

Runtime 使用 Harness 的 `ChatHistoryProvider.InvokedAsync` 正式持久化边界：首次创建 Session 时先保存一个安全点，此后每次成功的真实模型调用写入历史后，都通过 `SerializeSessionAsync` 增量序列化 `AgentSession`，连同最新 todo ledger、evidence 和 journal 原子保存到对应 `CopilotConversationRecord`。包装器内部仍委托 `InMemoryChatHistoryProvider`，并使用与 Harness 相同的 context-window compaction reducer，不维护第二份聊天历史。正常结束时再写入带最终 stop reason 的检查点。应用重启或重新创建 Runtime 后，下一轮使用 `DeserializeSessionAsync` 恢复框架内部历史；运行时将 checkpoint 的有界 conversation memory 与 UI 可见历史做有序对齐，只把 checkpoint 之后尚未持久化的可见消息插入当前用户消息之前，既避免重复整段历史，也覆盖异常退出、旧状态迁移或增量保存滞后造成的上下文缺口。对话记忆与可执行 Session 分开保存：Profile、能力或请求工具变化导致 Session 重建时恢复有界语义上下文，而 todo、历史工具调用和审批继续作废。该扩展点的调用顺序见官方 [Harness 文档](https://learn.microsoft.com/en-us/agent-framework/agents/harness)。

检查点具备以下约束：

- 使用 Profile ID、协议、Base URL、模型和系统提示的不可逆指纹做兼容性校验；配置变化后自动新建 Session。`/personality` 作为会话级持久状态追加到请求提示，因此同一风格可继续原 Session，切换风格则保留 checkpoint 语义记忆并进入 replan，不直接复用旧提示身份下的计划。设计采用 [Codex `/personality`](https://learn.chatgpt.com/docs/developer-commands?surface=cli) 的 `friendly`、`pragmatic`、`none` 选择，并沿用 [Claude Code output styles](https://code.claude.com/docs/en/output-styles) 与 grok-build `persona_instructions` 的共同边界：沟通风格属于系统提示身份，不伪装成用户消息，也不能扩大任务、权限或安全边界。
- 单个检查点上限 4,000,000 字符，超限或 JSON 损坏时不恢复。
- 发起新请求时保留上一安全点，只有新的安全点成功保存后才替换；如果应用中途退出，启动归一化会把开放 run 标记为 `Interrupted`，由用户显式继续，不自动执行。
- Chat 模式和重新生成回答不会复用 Framework 检查点。
- 最新回复存在可用结构化恢复时，消息卡的通用“重试”和 `/compact` 都不会从头执行或清空检查点；用户必须选择“继续任务”“重试只读检查”“重试最终回答”或“重新规划”。真正需要从头开始时，先在任务列表中明确放弃旧任务。
- 工具 trace、幂等限制和访问策略仍是独立安全边界；恢复 Session 不代表恢复任何旧批准。即使恢复清单要求重复同一个写调用，也会产生新的 CallId，并按本轮审批路由重新形成精确决定；临时授权还须重新校验 conversation、task、workspace 及对应工具范围。原生审批可由人工或符合条件的自动复核决定，不等于恢复旧批准。

若只更新检查点的 journal 或对话记忆而保留原始执行 Session，未知工具结果和未闭合 Provider 调用的禁恢复原因必须随 Session 保留；无工具的最终回答重试不能解除限制。复制、持久化和新 Session 的区别见[最终回答恢复与会话限制](./copilot-agent-lifecycle.md#上下文压缩与请求预算)。

## 显式有界重试

模型供应商调用使用独立的传输重试层。HTTP 408、429、5xx、无响应连接错误、超时和 I/O 中断只有在尚未收到第一个实质输出时才会退避重试；默认最多请求三次，间隔 250ms、500ms。首个文本、推理内容或 FunctionCall 更新一旦出现，后续中断绝不重放已有输出；若本轮已有文本或业务工具 step，Runtime 会记录 `ProviderFailure` 与 `provider_interrupted` blocker，并在不再次调用 Provider 的情况下读取 todo、合并 evidence/journal、序列化当前 Harness Session。开放 todo 通过 `Resume` 恢复同一 Session；没有开放 todo 时通过 `Finalize` 只生成最终回答。尚无任何实质进展的 400/401/403 等永久错误、连接失败和调用方取消仍直接向上传播。重试层位于 Token 预算层外侧，因此每次真实供应商请求都计入 `ProviderCalls`；诊断只记录失败类别、尝试序号、等待时间及可用的脱敏请求 ID，不保存响应正文或异常消息。

`CopilotChatMessage.ModelContent` 会把既有 `WasResponseInterrupted` 状态转换成固定的 `<assistant_response_interrupted>` 模型边界。部分 Assistant 正文保留在标记前；display-only 的应用退出恢复提示不会作为模型回答重放，但仍生成一个 Assistant 边界来闭合上一轮。标记不拼接 `ResponseInterruptionDetail`，避免把提供商错误、地址或本地化 UI 文本重新注入模型；`Content`、可见历史、复制和导出仍保持原样。`CopilotConversationRequestBuilder`、主动压缩和完成评估都读取同一个 `ModelContent`，因此中断语义不会在不同请求路径间漂移。新的生成尝试调用 `MarkThinkingStarted` 后清除中断状态，正常回答不携带该标记。这个边界沿用 Codex 默认的 model-visible interrupt message，并与 Claude Code“中断后保留已完成工作、允许重定向”和 grok turn hook 的 cancellation context 保持同一连续性原则，不把中断解释为回滚、完成证明或新的授权。

运行时不会在工具内部暗中自动重跑。首次失败会把 `failure_kind`、`retry_allowed` 和 `attempt` 交回 Agent Framework；普通工具重试要求模型再次发出完全相同的调用，并同时满足：

- 工具声明 `Idempotent`。
- 上次结果是 `Transient` 且状态为 `Failed` 或 `TimedOut`。
- 同参数最多执行两次，并且未超过本请求工具轮次上限。
- 受保护写工具每次重试都生成新的精确调用决定；当前路由可能等待人工，也可能进行自动复核或检查有效的临时 grant，上一次批准不会被复用。

`NonIdempotent`、`Unknown`、校验错误、权限拒绝、用户取消和业务失败不会获得上述普通重试资格。自动审查拒绝另有用户显式触发的 [`/approve N` 一次精确重试](./copilot-agent-tool-contracts.md#approve-与自动拒绝后的精确重试)：它不是复用批准，也不绕过同一运行的无进展闸门。

写入或非幂等调用进入实际工具执行后，若取消或超时且未取得可确认结果，执行器以 `OutcomeUnknown` / `tool_outcome_unknown` 闭合事件，要求先核对外部状态再重试。本地 Task 即使已经取消完成，也不能证明远端工作已停止或之前的写入已回滚；因此不再用 `Task.IsCompleted` 排除未知结果。资源闸门仍保留至本地执行任务结束，但不宣称能够锁住远端仍在执行的操作。前置校验、审批、排队及待写 checkpoint 保存期间的取消仍按尚未分派处理；返回了明确业务结果的工具不因失败本身被改判为未知。模型结果与任务事件保留同一失败码，下一次 checkpoint 兼容性检查据此返回 `UncertainToolOutcome`，继续工具任务时要求重新规划和核对状态，而不是直接重试原写入；不再执行工具的 Finalize 仍可只整理已有结果。

宿主停止等待后，任务 journal 若只有 `ToolStarted` 而没有工具终态，同样以 `Interrupted` / `tool_outcome_unknown` 收尾，不把本轮的 `Cancelled` 推导为工具已取消。取消会丢弃可执行 Session，但保留 journal 中的未知结果证据；若该 journal 用于检查点兼容性判断，仍要求重新规划。已有权威工具结果、未开始调用和待审批调用各自保留原有边界。`CopilotCancelledToolJournalTests` 覆盖取消、暂停和异常结束、权威结果不覆盖、未分派对照及真实会话取消后的 journal 保留；界面 trace 的对应规则见[流清理与原始失败](./copilot-agent-execution.md#流清理与原始失败)。

`CopilotToolExecutionAuditLogger` 保存最近 200 条调用并写入 log4net。参数摘要和错误会复用 MCP 的脱敏规则，不应记录 API key、token、密码、Authorization 或 bearer secret。聊天面板显示工具开始、完成状态和耗时，便于确认 Agent 是否真正执行了动作。未获得结果的文件、文档或网页搜索属于后台证据尝试，默认不显示活动行，也不会把整段处理状态标红；完整脱敏诊断仍保留在结构化 trace 中供恢复与排障使用。

业务日志读取由 `GetRecentLog` 提供，不是上述调用审计。`CopilotToolIntentPolicy.NeedsRecentLogs` 在 Diagnose 模式或相关日志意图/后续请求下满足工具可用条件，但切换模式本身不会自动附加日志；模型实际调用工具后，`CopilotGetRecentLogTool.ExecuteAsync` 才通过 `CopilotRecentLogCapability` 读取。工具接受 `query` 和 `max_lines`，返回有界的最近日志文本，不能把“诊断模式已开启”当成日志已经读取的证据。

`CopilotRecentLogSupport` 在应用数据目录 `ColorVision/Log` 与程序目录 `log` 的 `.txt` 候选中优先选择当天名称、再比较最后写入时间；这不是日志查看器当前可见文本或全部历史归档。最近行模式只扫描有界尾部；关键字忽略大小写，存在匹配时返回匹配行，没有匹配时回退到最近行，因此返回文本不一定都与问题相关。文件缺失或不可读应按工具实际结果报告，不能虚构诊断证据。`CopilotSharedCapabilityInputContractTests` 只验证此工具的结构化参数绑定，不覆盖真实日志选择、尾读与错误分支；本主题未声明这些文件读取行为已做端到端验证。

工作区修改后的真实验证由 `RunWorkspaceValidation` 提供。它不是通用命令行：只接受工作区内现有 `.sln` / `.slnx` / 项目文件，以及精确的 `dotnet build` 或 `dotnet test`、`Debug` / `Release` 和 10–600 秒超时；执行参数由宿主固定拼装，始终附带 `--no-restore`，不经过 shell，也不接收额外参数。该操作会触发原生审批，因为项目 target 本身可能执行仓库代码。stdout/stderr 分别有界保留头尾，超时会终止进程树；非零退出作为已完成的失败验证证据交回模型，不会因工具层失败而自动重复。显式“修改并验证”请求由执行契约强制按“批准修改 -> 批准验证”的顺序完成，提前验证不能满足契约。

跨文件修改使用 `PreviewWorkspaceChangeSet`、`ApplyWorkspaceChangeSet` 和 `RollbackWorkspaceChangeSet`。模型先为 2–8 个不同路径分别生成精确的单文件修改或创建预览，再把这些 `previewId` 绑定成一个变更集；绑定后的子预览不能绕过变更集单独应用。审批窗口一次展示完整文件清单、每个操作以及前后 SHA-256。应用前先验证所有路径、状态和文件指纹，确认整组仍可写后才开始落盘；Windows 文件系统不提供跨文件事务，因此中途失败或取消时会按逆序补偿已经完成的写入。已成功应用的变更集可以通过一次新的原生审批整体回滚，回滚过程中若后续文件失败，则尽力重新应用先前已回滚的文件以维持原状态。预览和变更集都继承 30 分钟有效期。显式“修改多个文件”请求的执行契约只接受完整变更集成功，单个子文件成功不能冒充任务完成。

`PreviewWorkspacePatchEnvelope` 将这条链路收敛为一次结构化预览调用：同一信封可按顺序表达 1–8 个 `add`、`update`、`delete` 操作，每个路径只能出现一次。`add` 携带完整 UTF-8 内容，`update` 仍要求唯一精确匹配的 `oldText/newText`，`delete` 只接受可写工作区根内可解码的现有文本文件；单独授权的根外文件可以更新但不能删除，避免回滚时把原路径误当成新文件授权。预览阶段不写入，内部直接生成并保留同一变更集；`ApplyWorkspacePatchEnvelope` 经一次原生审批后复用整组路径检查、前置 SHA-256 验证、原子单文件写入和逆序补偿。删除记录绑定删除前字节与哈希，应用后目标必须缺失；`RollbackWorkspacePatchEnvelope` 仅在目标仍缺失时恢复原字节，若外部进程重新创建了同名路径则整组回滚在写入前失败，绝不覆盖。旧的逐文件预览与 `PreviewWorkspaceChangeSet` 保留为兼容入口，但 Agent instructions 和执行契约优先选择统一信封。

当前 Windows 版本、显示版本、Edition、安装类型、系统构建号与 UBR、系统/进程架构和 .NET 运行时由 `InspectWindowsSystem` 提供。工具无参数，直接使用 .NET 运行时信息和只读的 `HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion`，注册表不可读时退回有界的运行时信息；它不启动进程、不接受命令文本，也不需要审批。该 Schema 与其他内置诊断稳定提供给模型，Windows 版本问题应优先使用它，不能改用 SQL 或应用日志猜测机器状态。

Git 工作树由 `InspectGitWorkingTree` 提供。它接受当前请求搜索/可写根目录内的可选路径，向上查找时不会越过宿主给出的根目录，也拒绝穿过根目录下的重解析点；因此子目录不能借 Git 仓库向外扩大可见范围。宿主只从 Program Files、用户本机 Git 安装目录或 GitHub Desktop 版本目录解析 `git.exe`，清除继承的 `GIT_DIR`、`GIT_WORK_TREE`、index/object/config parameter 等仓库选择变量，固定执行 `git --no-pager --no-optional-locks`、关闭 fsmonitor/untracked cache、把 `core.worktree` 绑定到已验证根目录，再运行 `status --porcelain=v2 --branch --untracked-files=normal --no-renames --ignore-submodules=all`；模型不能提供命令文本，也不能借宿主环境把目标切换到另一个仓库。结果有界返回 repository root、branch、HEAD、upstream、ahead/behind，以及 staged、unstaged、untracked、conflict 计数和最多 100 个路径；输出被进程层截断时会明确把 `status_complete` 和 `is_clean` 置为 false，避免把不完整观察误报为干净。由于 Git 在判断文件状态时仍可能评估仓库定义的 attributes 与外部 filter，而当前宿主没有操作系统级进程沙箱，该工具按受保护读取处理并要求 Agent Framework 原生审批；Git 不可用或目标不是仓库时返回真实 NotFound，不退回 Shell 猜测。

具体改动内容由 `InspectGitDiff` 提供。Agent 只能选择 `unstaged`、`staged` 或 `both`，以及当前请求根目录内的可选现存路径；宿主使用同一套可信 `git.exe`、仓库边界、重解析点和环境变量隔离，固定关闭 external diff、textconv、rename、submodule 与颜色输出，并把 pathspec 放在 `--` 之后，模型不能注入 Git 参数。每个范围最多返回 24,000 个字符，进程层或服务层发生截断时同时标记 `output_complete=false` 与 `patch_truncated=true`，因此 Agent 只能把它描述为有界摘录。补丁始终按不可信工作区数据处理，不能把其中的文字当作新指令；该工具与工作树状态一样，经原生审批后执行，不退回任意 Shell 命令。

单个 TCP 端口的常见检查由 `InspectTcpPort` 提供。例如模型可以根据“我想要知道 6666 端口有没有被占用”发出 `{"port":6666}` 的结构化调用；宿主执行固定的只读 PowerShell 诊断，不接受模型提供的命令文本，因此不需要审批。结果是有界的结构化数据：是否占用、绑定数量、本地/远程端点、连接状态、PID 和进程名；最多返回 64 条绑定并标记截断。工具 Schema 与通用 Shell 同时可见，模型应优先选择风险更低、参数更窄的专用工具；询问“如何检查端口”之类的概念问题则可以直接回答，宿主不会按关键词强制调用。

运行进程由 `InspectWindowsProcesses` 提供。模型可以传精确 PID、精确进程名（可省略 `.exe`），或请求按最近 CPU、工作集内存、名称、PID 排序的前 1–25 项；宿主在进程内通过 .NET API 采集，CPU 使用率来自 250ms 短采样并按逻辑处理器数量归一化，不启动 PowerShell/CMD，也不接受命令文本，因此无需审批。结果有界返回 PID、进程名、CPU、工作集、私有内存、线程、Session、启动时间；只有按 PID/名称聚焦查询时才读取可执行文件路径，Windows 拒绝访问的字段用 `null` 或空串明确表示。进程名和路径只是机器数据，不能作为 Agent 指令。

已安装 Windows 服务由 `InspectWindowsServices` 提供。模型可以用服务名或显示名的大小写无关片段筛选，也可按 `running`、`stopped`、`paused`、`pending` 状态过滤，并按服务名、显示名或状态排序；每次最多返回 50 项。宿主直接使用 .NET `ServiceController` 读取服务名、显示名、状态、服务类型和可停止/暂停/关机能力，不启动 PowerShell/CMD、不接受命令文本，也不需要审批。零匹配是有效 observation，可以支持“当前没有安装/运行匹配服务”的回答；服务名和显示名只作为机器数据返回，不能驱动新的 Agent 指令。

通用 Windows 命令由 `RunShellCommand` 提供，并与专用诊断工具一起稳定注册。模型需要在结构化参数中给出完整命令、`PowerShell` / `CMD` / `Auto`、可选现有工作目录和 5–600 秒超时；仅在回复文本中展示命令不会触发宿主执行。设置窗口的 `Default shell` 可选择“自动（PowerShell）”、`PowerShell` 或 `CMD`。宿主始终以无窗口、非交互方式运行命令，关闭标准输入，并有界返回真实 exit code、stdout、stderr 和耗时。根 Shell 会进入带 `KILL_ON_JOB_CLOSE` 的独立 Windows Job Object；正常完成、取消或超时时都会收敛其后台子进程，并在读取 stdout/stderr 前关闭后代继承的管道。由于当前宿主没有类似 Codex 的系统级文件沙箱，所有通用命令即使由模型选中也必须经过 Agent Framework 原生审批，审批内容显示 Shell、工作目录和完整命令；参数审计只保存字段名。普通概念问答可以直接回答，宿主不会根据“端口”“系统”等词强制调用 Shell。

当用户明确要求使用 CMD 或提供批处理语法时，仍按通用命令处理并进入原生审批，例如：

```bat
netstat -ano | findstr :6666
```

业务数据库同时提供语义快捷能力和通用 SQL 能力。`QueryFlowExecutionStats` 只读聚合 `t_scgd_measure_batch`：接受 `today`、`yesterday` 或 `last7days`，按本机时区生成左闭右开的日历范围，返回执行尝试总数、各 `FlowStatus` 数量、完成率和平均耗时。它适合“今天执行了多少次流程”这类常见问题，不要求模型了解表结构。

`QueryDatabaseSql` 是 Agent 模式的通用只读数据库工具，作为稳定 Schema 与其他内置能力一起提供；是否需要查询、应生成哪条 SQL 由模型结合当前对话决定，宿主不会通过关键词把系统问题改写成数据库查询。该工具接受一条只读 MySQL 语句，支持 `SELECT`、`SHOW`、`DESCRIBE`、`EXPLAIN`、`TABLE` 和最终落到只读语句的 CTE。默认最多返回 100 行，可在 1–500 行内调整；列数、单元格和总输出长度都有上限，密码、token、API key 等敏感列会统一显示为 `<redacted>`。`ExecuteDatabaseSql` 接受一条数据或结构变更，支持 `INSERT`、`UPDATE`、`DELETE`、`REPLACE`、`CREATE`、`ALTER`、`DROP`、`TRUNCATE` 和 `RENAME`，每次都必须经过 Agent Framework 原生审批；无 `WHERE` 的 `UPDATE` / `DELETE`、`TRUNCATE` 和 `DROP` 会在审批说明中给出加强警告。普通 DML 在事务内提交，DDL 遵循 MySQL 的隐式提交语义。服务设置表是版本托管的只读边界，即使进入审批也会在执行前拒绝变更；更新时由版本自带 SQL 重置原生设置。服务配置表由 Service Manager 在数据库重置前导出并回写，结果表不参与保留且可通过受审批的清理流程删除。

两个通用工具都只连接 ColorVision 当前配置的 MySQL，不接受连接字符串。解析层只允许单语句并拒绝 executable comment；账号与授权管理、创建/删除数据库、全局或会话设置、事务控制、锁、动态 SQL、存储过程调用、服务器关闭/终止、插件管理、文件导入导出以及延时函数不开放。审计只保存参数名和 SQL 指纹，错误结果不回显数据库异常或连接信息。只有宿主返回的真实 observation 才能支持当前数据库事实；但是否调用数据库工具仍由模型决定，宿主不在模型回答后用关键词补做查询。

通用数据库浏览器及其专用动态上下文已移除，不再自动采集当前表、分页或字段快照。`QueryDatabaseSql` 和 `ExecuteDatabaseSql` 是独立的宿主能力，继续按上述查询和原生审批契约工作，不依赖浏览器窗口。

检测结果历史与批次详情共用 `measurement-results` 动态来源。历史页只提供当前加载条数、是否启用筛选和选中批次的内部 ID、模板、状态、时间、归档状态；详情页额外提供取图/算法结果数量、失败与未知结果计数，以及当前选中结果的类型、内部 ID、结果码、耗时、时间和引用文件是否仍存在。页面导航、窗口激活、筛选、批次选择和结果选择都会刷新来源；导航离开或关闭最后一个结果页面后注销。批次 `Name/Code` 在实际流程中可承载序列号，因此一律不进入快照；文件路径、请求参数、原始结果消息、设备代码、算法 payload 和测量值同样不注入。正在运行的批次仍由 Flow 上下文负责，结果历史不会建立第二份运行状态。Flow 上下文中的批次序列号现在按字段级规则直接显示为 `<redacted>`，批次结果只报告消息是否存在，不再透传内容。

任务调度器使用 `scheduler` 来源读取 `QuartzSchedulerManager.TaskInfos` 的实时聚合，而不是解析窗口文本。明确询问计划任务时，即使任务窗口未打开，也会返回调度器启动状态、Ready/Running/Paused 数量、总执行/成功/失败次数，以及最多 30 个任务的有界目录；目录优先展示运行中、存在失败和暂停的任务，并包含任务/分组、状态、Job 类型、模式、优先级、执行统计、最后状态和下次触发时间，超出上限时明确标记截断。任务窗口激活后额外提供当前选择、超时、重复模式和前后触发时间。执行历史窗口只提供当前页、成功/失败筛选、行数、平均耗时和选中记录的时间/状态元数据。任务配置值、Cron 原文、`JobDataMap`、结果或异常详情、payload、路径与凭据全部留在宿主；最后执行消息和历史详情只报告是否存在。窗口关闭会释放动态会话并清除旧 Live Context，管理器级会话继续提供新鲜聚合快照。

项目专用结果视图不反向依赖主程序 Copilot，也不在 Copilot 核心增加客户项目分支。公共层只提供 `CopilotProjectResultContextSnapshot` 的低敏结果形状，具体项目程序集通过 `CopilotAgentExtensionRegistry` 自行注册和映射。新的业务快照与构建器放在按领域命名的 partial 文件中，不继续扩大单一 `CopilotBusinessContextBuilder.cs`。首个接入的 ARVRPro 使用 `project-arvr-pro-results` 来源覆盖模组检测结果列表、ObjectiveTestResult 历史和测试项明细；多个窗口由同一 `CopilotDynamicContextCoordinator` 跟随最近激活实例，最后一个窗口关闭后整个项目来源注销。列表提供加载/运行/完成/通过/失败数量以及选中结果的内部 ID、流程名、状态、耗时和时间；明细只提供测试项通过/失败数量和最多 20 个失败项名称。`ObjectiveTestItemCollector` 是项目内共享的纯解析器，窗口显示和 Agent 汇总不会各自解释一遍 JSON。SN、条码、文件路径、原始消息、原始 JSON、测量值、上下限和单位始终留在项目宿主内；是否存在图片、消息或结构化 payload 只以布尔元数据表示。其他项目需要接入时复用公共快照和注册协议，在自己的程序集内完成映射，不复制 ARVRPro 模型进公共层。

同一份生命周期数据还会以版本化 `CopilotAgentTraceEntry` 写入聊天会话。工具开始和结束事件都会触发原子状态保存；待确认动作还会把 `approval action_id` 与 Agent `CallId` 关联。批准、拒绝、过期、开始执行和执行结果都会更新同一条 trace。切换会话后仍能看到当前状态；若应用在执行或等待审批时退出，加载时会把遗留的 `Pending` / `Running` / `AwaitingApproval` trace 以及开放的 Agent run 收敛为 `Interrupted`，要求用户从最近安全点继续并为受保护调用产生新的审批，不会自动重放可能产生副作用的工具。结果、错误与参数在持久化前统一脱敏并限制长度。旧会话没有结构化 trace 时继续使用原有的 `ExecutionContent`，无需迁移才能打开。

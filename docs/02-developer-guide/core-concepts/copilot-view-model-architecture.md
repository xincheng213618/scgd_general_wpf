---
knowledge_id: "copilot.view-model"
knowledge_type: "topic"
status: "current"
summary: "Copilot 界面状态的所有权、异步输入交接、检查点提交及会话保存完成边界。"
aliases: ["CopilotChatViewModel 太大从哪里改","聊天状态属于哪个对象","CopilotConversationSession","CopilotComposerSession","CopilotComposerCaptureToken","CommitScheduled","CopilotPreparedHostedTurn","ICopilotTurnRuntime","CopilotApprovalCoordinator","CopilotChatStatePersistenceCoordinator","SaveSynchronouslyAndStop","TrySetAgentSessionCheckpoint","TryCommitAgentRunState","CurrentAgentTaskEventJournal","CopilotTurnCheckpointLifecycleState"]
code_paths: ["ColorVision/Copilot/CopilotChatViewModel.cs","ColorVision/Copilot/CopilotChatViewModel.Conversations.cs","ColorVision/Copilot/CopilotChatViewModel.TurnExecution.cs","ColorVision/Copilot/CopilotChatViewModel.TurnEvents.cs","ColorVision/Copilot/CopilotChatViewModel.QueuedFollowUps.cs","ColorVision/Copilot/CopilotChatViewModel.Permissions.cs","ColorVision/Copilot/CopilotChatViewModel.Lifecycle.cs","ColorVision/Copilot/State/CopilotConversationSession.cs","ColorVision/Copilot/State/CopilotComposerSession.cs","ColorVision/Copilot/Agent/CopilotQueuedFollowUpCoordinator.cs","ColorVision/Copilot/Agent/CopilotAgentTaskHost.cs","ColorVision/Copilot/Runtime/CopilotApprovalCoordinator.cs","ColorVision/Copilot/Runtime/CopilotPreparedHostedTurn.cs","ColorVision/Copilot/Runtime/CopilotTurnRuntimeConfigSnapshot.cs","ColorVision/Copilot/Runtime/ICopilotTurnRuntime.cs","ColorVision/Copilot/Runtime/CopilotTurnRuntime.cs","ColorVision/Copilot/Runtime/CopilotTurnCheckpointLifecycleState.cs","ColorVision/Copilot/CopilotConversationRecord.cs","ColorVision/Copilot/CopilotConversationRecord.Validation.cs","ColorVision/Copilot/State/CopilotChatStatePersistenceCoordinator.cs","ColorVision/Copilot/State/CopilotChatStateSaveScheduler.cs"]
test_paths: ["Test/ColorVision.Copilot.Tests/CopilotChatViewModelContractTests.cs","Test/ColorVision.Copilot.Tests/CopilotConversationSessionTests.cs","Test/ColorVision.Copilot.Tests/CopilotComposerSessionTests.cs","Test/ColorVision.Copilot.Tests/CopilotAttachmentRemovalEditLifetimeTests.cs","Test/ColorVision.Copilot.Tests/CopilotQueuedFollowUpCoordinatorTests.cs","Test/ColorVision.Copilot.Tests/CopilotPreparedHostedTurnTests.cs","Test/ColorVision.Copilot.Tests/CopilotApprovalCoordinatorTests.cs","Test/ColorVision.Copilot.Tests/CopilotChatStatePersistenceCoordinatorTests.cs","Test/ColorVision.Copilot.Tests/CopilotTurnCheckpointLifecycleTests.cs","Test/ColorVision.Copilot.Tests/CopilotAgentSessionCheckpointTests.cs","Test/ColorVision.Copilot.Tests/CopilotAgentTaskEventJournalIntegrityTests.cs"]
related: ["copilot.runtime","copilot.interactions","copilot.session-tools","copilot.configuration","copilot.tool-contracts"]
---

# Copilot 状态所有权与界面交接

本页用于定位聊天界面的状态归属，以及会话切换、异步发送、审批和保存之间的交接。`CopilotChatViewModel` 负责组装依赖、WPF 绑定和界面流程编排；其 partial 文件只是组织方式，状态边界由下列对象及提交方法决定。模型执行链见 [Copilot Runtime](./copilot-agent-runtime.md)。

## 状态所有权

表中相对源码路径均位于 `ColorVision/Copilot/`。遇到状态问题，先查所属对象，再查对应 ViewModel 调用是否正确捕获、提交和通知。

| 对象 | 持有或管理的状态 | 实现入口 |
| --- | --- | --- |
| `CopilotChatViewModel` | 命令、绑定属性、界面投影、窗口、Dispatcher、定时器及应用生命周期接线；消息编辑期间的临时草稿备份 | `CopilotChatViewModel.cs` 及相关 partial |
| `CopilotConversationSession` | 访问状态中的会话集合，维护选中会话、选中 Profile 与活动 ID 的一致性 | `State/CopilotConversationSession.cs`、`CopilotChatViewModel.Conversations.cs` |
| `CopilotConversationRecord` | 消息、待发送附件、持久化 `Draft*` 字段，以及该会话的 Agent 恢复状态 | `CopilotConversationRecord.cs`、`CopilotConversationRecord.Validation.cs` |
| `CopilotComposerSession` | 当前编辑区的文本、模式、审查目标、Skill 引用与版本；通过 capture token 识别提交的输入 | `State/CopilotComposerSession.cs` |
| `CopilotQueuedFollowUpCoordinator` | 实时队列、run ID 索引、队列顺序、状态中的恢复记录与关闭保护 | `Agent/CopilotQueuedFollowUpCoordinator.cs`、`CopilotChatViewModel.QueuedFollowUps.cs` |
| `CopilotAgentTaskHost` | 活动和等待任务、调度准入、取消及宿主生命周期 | `Agent/CopilotAgentTaskHost.cs` |
| `CopilotPreparedHostedTurn` | 把本次执行的会话、Profile、消息、宿主上下文和运行配置关联为一个执行参数对象 | `Runtime/CopilotPreparedHostedTurn.cs` |
| `ICopilotTurnRuntime` / `CopilotTurnRuntime` | Provider / Agent 执行与单轮事件流 | `Runtime/ICopilotTurnRuntime.cs`、`Runtime/CopilotTurnRuntime.cs` |
| `CopilotApprovalCoordinator` | 待审批投影、作用域复核、批准/拒绝与审批状态到消息 trace 的转换；动作本身仍由 approval store 持有 | `Runtime/CopilotApprovalCoordinator.cs`、`CopilotChatViewModel.Permissions.cs` |
| `CopilotChatStatePersistenceCoordinator` | 捕获会话状态快照、序列化和串行提交；委托 scheduler 合并保存请求、Flush 与重试 | `State/CopilotChatStatePersistenceCoordinator.cs`、`State/CopilotChatStateSaveScheduler.cs` |

`CopilotChatState` 是保存到磁盘的状态集合，各对象只管理自己的字段。Composer 不持有附件，也不会自行把输入写回 conversation：ViewModel 的 `SynchronizeSelectedConversationComposerDraft` 同步 `DraftText`、`DraftRequestMode`、`DraftWorkspaceReviewTarget` 和 `DraftAgentSkillReference`，附件提交另行按对象引用处理。

消息编辑备份是 ViewModel 的临时内存状态，其对象身份也是旧异步操作是否仍有效的依据；它与持久化 `ComposerStash` 不同。取消编辑、发送失败、附件移除与备份恢复的完整规则见[草稿编辑与历史恢复](./copilot-local-interactions.md#草稿编辑与历史恢复)。

## 会话选择与后台结果归属

`CopilotConversationSession.SelectConversation` 拒绝已归档或不属于当前集合的会话；接受的选择会更新活动会话、Profile ID 及选中对象。创建会话与选中会话是不同操作，创建本身不强制切换。

ViewModel 对选择分两条路径处理：

- **切到不同会话**：取消当前消息编辑，解绑旧会话集合通知，清理当前界面的临时恢复/历史导航状态，再调用 `ComposerSession.Load`、同步规范化草稿并刷新绑定与审批投影。
- **仍选择同一会话**：可以应用显式首选 Profile 并刷新相关状态，但不重新加载 Composer，也不因重复选择清掉现有输入。

`Load` 会推进 Composer 版本，因此“切出去再切回来”也不能重新使用旧 token。异步任务始终更新捕获的原 conversation 和消息；完成时重新读取 `SelectedConversation` 或 `SelectedProfile` 会把用户当前正在看的会话误当成任务所属会话。

## 输入捕获与成功排程后的提交

正常模型请求沿 `CopilotChatViewModel.TurnExecution.cs::SendAsync` 处理：先捕获输入和原会话，再做准入、上下文准备和异步附件处理，最后建立消息并交给 TaskHost。异步准备后会重查准入及消息编辑是否仍有效；排程失败则回滚本次建立的消息，保留输入。

| 交接点 | 当前契约 |
| --- | --- |
| `ComposerSession.Capture()` | 返回文本、模式及引用的快照和 `CopilotComposerCaptureToken(ConversationId, Version)`；不清空输入，不包含附件 |
| 捕获附件与配置 | 另行捕获原附件对象和执行上下文；准备阶段负责创建请求 Profile 与运行配置快照，后续执行使用这些已捕获值 |
| `TaskHost.TrySchedule` | 成功表示宿主接受任务，不表示 Provider 已执行或状态已保存 |
| `ComposerSession.CommitScheduled(token)` | 只在会话 ID 和版本都匹配时清空文本、恢复 Auto 模式并清除审查/Skill 引用；成功后推进版本，同一 token 不能再次消费 |
| 成功提交后的附件处理 | 只移除捕获时的原对象；不能在 `await` 后对当前附件集合 `Clear()`。token 已失效时保留新的输入与附件 |
| 显式 `directPrompt` | 不读取或消费界面 Composer、待发送附件、Skill、审查目标或待恢复请求 |

`CopilotPreparedHostedTurn` 本身不对所有参数深拷贝：它保留传入对象的引用，尤其是原 conversation 与 user/assistant 消息。`ValidateHostedRun` 检查会话 ID 和模式是否匹配；消息角色在构造时校验。配置隔离由准备阶段和 `CopilotTurnRuntimeConfigSnapshot` 等快照类型承担，不能把 prepared 对象误当成任意可变对象的冻结器。执行开始时仍从原会话获取恢复 checkpoint、journal baseline 等执行状态，而不是从后来选中的会话取值。

### 排队请求

普通 follow-up 入队时捕获 Profile、运行配置、附件、Skill/审查目标和项目上下文；实际执行前才重新捕获该会话历史，以包含前一轮已经完成的消息。Steering 是发给运行中任务的消息，不是新建 turn；取消、重启恢复和排队本地命令规则见[会话与工具](./copilot-agent-session-and-tools.md)。

`FollowUpCoordinator.TrySchedule` 成功后，恢复记录先存在 `CopilotChatState.QueuedFollowUpRecoveries` 的内存集合中。ViewModel 提交匹配的输入 token，并请求立即保存；这里没有等待 Flush，所以不能把“已排队”当成磁盘耐久化回执。

执行前建立 prepared turn 和消息后会等待保存：Flush 失败时回滚未保存消息并恢复输入；成功后才移除该恢复记录并继续执行。Runtime 在进入 Provider / Agent 执行前还通过 `StatePersistenceBarrierEvent` 请求保存屏障。这样调度、输入消费、保存完成和模型执行分别有明确的交接点。

## 检查点与任务事件的所有权

`CopilotTurnEvent` 是单轮执行的瞬时协议，经过协议校验并由 `CopilotTurnEventReducer` 汇总为运行结果；会话恢复使用持久化 checkpoint 和有界 task journal。普通 Chat 消息、UI trace 和 `/context` 的模型表面计数各有用途，不替代这份恢复状态；模型表面是从消息与压缩边界派生的投影，不另存一份事实源。journal 字段与事件类型见[结构化任务事件](./copilot-agent-tool-contracts.md#结构化任务事件-journal)。

### 会话聚合提交

通过 `CurrentAgentTaskEventJournal` 读取当前 journal，通过 `SetAgentSessionCheckpoint` / `TrySetAgentSessionCheckpoint` 和 `CommitAgentRunState` / `TryCommitAgentRunState` 更新；不要直接双写两个持久字段。

| 状态转换 | 所有权结果 |
| --- | --- |
| 接受有效 checkpoint | checkpoint 持有当前 journal，清除重复的 `LatestAgentTaskEventJournal` |
| 提交正式 Agent 终态 | 以有效的 terminal journal 为本轮事件证据；若提交的 checkpoint 落后或关联旧 run，将其复制并换入 terminal journal 后一起提交 |
| 退役 checkpoint | 将其 journal 交给独立字段保留，供展示和后续运行使用 |
| 加载旧状态同时存在两份 journal | 仅在加载规范化中选择较新的有效 legacy evidence，迁入 checkpoint 后清掉独立字段；迁移不刷新 checkpoint 原时间戳 |
| 未返回正式结果而暂停、取消或中断 | `CompleteOpenAgentRun` 闭合尚未完成的 Agent run；取消退役 checkpoint，暂停/中断按恢复规则保留；普通 Chat 不走这条 Agent 状态转换 |

这是规范化和聚合提交后的所有权约束。为读取旧状态，`CurrentAgentTaskEventJournal` getter 仍优先返回有效的独立字段，随后才回退到 checkpoint journal；不能绕过规范化而假设 getter 总是直接读取 checkpoint。新 run 将当前 journal 作为独立的 `TaskEventJournalBaseline` 传入，即使取消后已没有 checkpoint，也从现有事件序列继续。

### 接受候选与实际改变

同一轮的 `CopilotTurnCheckpointLifecycleState` 检查 checkpoint 身份、时间和 journal：Profile、任务意图、能力目录、工具、环境、Hook、项目指令及其相关版本/指纹不得在轮内漂移，更新时间不能倒退。`CheckpointReady` 必须在首个有效更新后出现且只能出现一次；发布过 checkpoint 的运行结束前必须已 ready。

journal 的准入使用 `IsSameOrForwardBoundedSuccessor`：等价候选可以接受；前向候选必须满足 sequence 增长与预期窗口条数，保留下来的历史事件逐字段相等。比较包括 ID、类型、时间、run、subject、关联 ID、工具名、状态、失败码、退出码和摘要；单凭更晚时间戳不能替换现有证据。

窗口最多 **256 条**。前缀淘汰后只校验仍可比较的事件；两窗口已无重叠时，不能据此证明被淘汰的完整历史相同。事件 ID 的结构校验与有界窗口比较不等于无限历史的完整性证明。

`TrySetAgentSessionCheckpoint` / `TryCommitAgentRunState` 分开返回“候选被接受”和 `changed`：等价候选可被接受而不替换对象。`TurnEvents.cs` 只有在 checkpoint 被接受且包含相应 steering 时，才据此消费已送达的指令恢复记录；被拒绝的候选不能证明这些指令已保存。显式取消有主动丢弃已送达批次的独立规则，其余未确认输入按恢复流程返回原会话草稿并保留后来的输入。

checkpoint 与恢复记录的内存变更一起进入后续状态快照；“候选被接受”本身不是磁盘保存完成。Provider 恢复兼容性、未知工具结果与用户恢复操作见[会话与工具](./copilot-agent-session-and-tools.md)。

## 会话保存、重试与退出

会话保存由 `CopilotChatStatePersistenceCoordinator` 负责，设置窗口的配置发布是另一条路径，见[配置与发布](./copilot-configuration.md)。

| API / 路径 | 完成含义 |
| --- | --- |
| `RequestSave(immediate)` | 增加保存请求版本并通知后台 worker；默认防抖 300 ms、单批最长等待 2 秒。`immediate` 跳过防抖，不等待文件写入 |
| `FlushAsync()` | 等待调用时已有的保存请求成功处理；若旧快照被更新请求淘汰，等待可提交的新批次。没有未处理请求且无先前失败时直接完成，不自动发现未发出保存请求的对象变更 |
| 失败与重试 | 每批最多尝试 2 次，间隔 50 ms；每次失败报告错误，最终失败使 Flush 抛异常。之后的新保存请求或显式 Flush 可重试 |
| `FlushStatePersistenceBarrierAsync` | 显式请求保存并等待；较新 state schema 阻止持久化时抛错，失败不能作为执行前保存成功 |
| `PersistStateAndFlushAsync` | 用于保留已完成运行的内存结果，捕获保存异常；调用返回不能单独证明磁盘成功，需看保存通知 |
| `SaveSynchronouslyAndStop()` | 停止 scheduler，等待提交锁，再保存当前状态；失败通过回调报告，较新 state schema 阻止覆盖。普通应用退出调用此路径 |
| `Dispose()` | 停止调度并释放接线；不等同于 Flush 或同步退出保存 |

快照捕获以保存请求版本为水位线。支持分片的 store 从后台请求时，在 UI Dispatcher 的 Background 优先级分片复制，单片以 4 ms 为工作预算，然后后台序列化；这不是每片耗时的硬上限。捕获后、序列化后和进入提交锁后都会复核版本，已过时的快照不在该检查点继续提交。

这一机制依赖状态修改方请求保存，不能把任意未经通知的可变对象变更变成事务。新请求若发生在文件写入已经开始后，较旧批次仍可能先完成，之后再保存新批次；不能承诺磁盘从未短暂出现旧批次。退出同步保存共用提交锁，避免被正在写入的较旧异步保存反向覆盖。

保存失败时，检查界面的持久化通知和原始错误；“重试保存”通过请求加 Flush 重新提交当前状态。普通应用退出会先保留队列恢复记录、恢复待确认 steering、关闭宿主并处理未开始任务，再执行同步保存。构造 ViewModel 则可能加载/规范化状态和清理孤立附件，不应把实例化它当作无副作用的纯读取。

## 审批与 WPF 边界

Approval store 发事件时，coordinator 立即捕获不可变 transition；ViewModel 再通过 Dispatcher 将其应用到消息/trace，避免延迟处理时读到已继续变化的 action。`PendingActions` 是当前作用域的投影，`TotalPendingCount` 是全局计数，两者不必相等。

审批窗口和用户反馈留在 WPF。打开窗口前、确认返回后以及 coordinator 执行决定时均复核动作与当前作用域；批准后的返回值区分已批准、已执行与执行失败。原生审批与外部 MCP 的 session、参数指纹和执行约束分别见[审批契约](./copilot-agent-tool-contracts.md#原生审批与参数快照)与 [Local MCP](./colorvision-mcp.md)，不能因为共用列表就合并授权范围。

维护时保留公开构造器、`ICommand`、CanExecute、属性通知和 XAML/code-behind 契约。新状态应有明确 owner 与 capture/commit 或恢复语义；不要增加会话、输入、队列或审批镜像字段，让 session 反向引用 ViewModel，或用包含大量回调的参数包把界面职责搬到一个新的总协调器。Dispatcher、窗口、剪贴板、文件选择器与绑定通知仍由界面层负责。

## 排障与验证入口

| 现象 | 先核对 | 相关测试源码 |
| --- | --- | --- |
| 切换会话后输入被旧请求清空 | Composer 的会话 ID/版本、真实切换与重复选择分支、附件是否按原对象消费 | `CopilotConversationSessionTests`、`CopilotComposerSessionTests`、`CopilotAttachmentRemovalEditLifetimeTests` |
| 等待中的请求用了新 Profile 或新页面 | 入队捕获、prepared 参数引用及执行时仅刷新历史的边界 | `CopilotPreparedHostedTurnTests`、`CopilotQueuedFollowUpCoordinatorTests`；具体队列恢复见[会话与工具](./copilot-agent-session-and-tools.md) |
| 显示已排队或输入已清空，重启却未恢复 | 保存请求是否真正完成、Flush 是否失败；不要只看 TrySchedule 返回值 | `CopilotChatStatePersistenceCoordinatorTests` |
| checkpoint 被拒绝或 steering 未消费 | 候选的接受结果与 changed、身份/时间、水位和仍保留的事件内容 | `CopilotTurnCheckpointLifecycleTests`、`CopilotAgentSessionCheckpointTests`、`CopilotAgentTaskEventJournalIntegrityTests` |
| 审批数量不一致或 trace 串会话 | 作用域投影、事件捕获时刻与 Dispatcher 应用时刻 | `CopilotApprovalCoordinatorTests` |
| 重构后按钮、快捷键或面板绑定失效 | 构造器、命令及 code-behind 依赖 | `CopilotChatViewModelContractTests` |

测试源码是定位与预期契约的依据，不代表当前已经执行。行为改动应选对应 owner 测试和必要的 ViewModel 交接测试；较宽的构建/测试入口见 [Runtime 验证](./copilot-agent-runtime.md#验证)。纯文档整理使用知识与站点检查，无需启动应用、模型或审批服务。

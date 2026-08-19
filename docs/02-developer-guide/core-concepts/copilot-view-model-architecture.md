# Copilot ViewModel 维护地图

修改 Copilot 对话界面时，先找状态 owner，不要先翻完 31 个 `CopilotChatViewModel` partial。ViewModel 是 WPF facade；partial 只是文件组织方式，不能作为状态边界。
## 阅读顺序

1. 在下表找到状态 owner。
2. 查看对应 `CopilotChatViewModel.*.cs` 如何投影到 WPF。
3. 最后进入 Provider、Agent 或持久化实现。
## 状态所有权

| Owner | 唯一负责 | 主要文件 |
| --- | --- | --- |
| WPF facade | 命令、属性通知、Dispatcher、窗口、定时器和绑定投影 | `CopilotChatViewModel.cs`、`PresentationProperties.cs` |
| Conversation session | 会话集合、活动 ID、选中会话和配置 | `State/CopilotConversationSession.cs`、`Conversations.cs` |
| Composer session | 输入、模式、审查目标、Skill 引用和 capture token | `State/CopilotComposerSession.cs`、`ComposerAndRuntimeState.cs` |
| Follow-up coordinator | 实时队列、run ID 索引、顺序、durable recovery 和关闭保护 | `Agent/CopilotQueuedFollowUpCoordinator.cs`、`QueuedFollowUps.cs` |
| Approval coordinator | 待审批投影、最终校验、批准/拒绝和 trace 转换 | `Runtime/CopilotApprovalCoordinator.cs`、`Permissions.cs` |
| Task host | 活动/等待任务、admission、取消和宿主生命周期 | `Agent/CopilotAgentTaskHost.cs` |
| Prepared hosted turn | 单次执行固定使用的会话、配置、消息和上下文 | `Runtime/CopilotPreparedHostedTurn.cs` |
| Turn runtime | Provider/Agent 执行和事件流 | `Runtime/ICopilotTurnRuntime.cs`、`Runtime/CopilotTurnRuntime.cs` |
| Conversation Agent state | 当前可恢复 checkpoint、最新有界事件证据及两者的原子提交 | `CopilotConversationRecord.cs`、`CopilotConversationRecord.Validation.cs` |
| Persistence coordinator | 保存合并、Flush、失败通知和重试 | `State/CopilotChatStatePersistenceCoordinator.cs` |

`CopilotChatState` 仍是持久化 aggregate，但不是业务 God object；各 owner 只管理自己的字段，持久化 coordinator 只负责快照和保存。分片快照以保存请求版本作为一致性水位线；捕获期间出现新请求时丢弃旧切面并继续保存新批次，不能把多个 UI 时刻拼成一份磁盘状态。
### DeepSeek Harness 借鉴边界

ColorVision 借鉴 DeepSeek Harness 的目标是建立可验证的不变量，不是复制其微内核、插件容器或完整事件溯源形态。会话侧只把 Agent 任务 journal 收紧为可连续验证的权威证据，并让 checkpoint、执行结果和 UI 成为该证据的恢复点或投影；现有 `CopilotChatState` 快照、WPF 状态 owner 和普通 Chat 消息模型继续保留，不把整套桌面会话改写为 JSONL 事件存储。工具侧借鉴“注册、Schema、发布、执行一致”的原则，通过共享 capability catalog、注册阶段契约校验和发布成功后才暴露工具来消除双写与幽灵工具；Agent 的 CallId、预算、Hook、原生审批和外部 MCP 的 session identity、两阶段确认、审计仍是不同的安全边界，不合并成一个无差别执行器。DeepSeek 的 Cordis 组合方式、通用插件生命周期和全局单一 `ToolRuntime` 不作为 ColorVision 当前目标。

`CopilotTurnEvent` 是单次运行的瞬时协议，不是第二份会话状态；事件由 `CopilotTurnEventReducer` 回放成执行结果。Agent checkpoint 内的 task journal 用于安全恢复，conversation 的 `LatestAgentTaskEventJournal` 只在没有可恢复 checkpoint 时充当独立 journal owner。两个持久字段只开放程序集内 setter，业务代码通过 `SetAgentSessionCheckpoint` / `CommitAgentRunState` 原子更新这组状态，并从 `CurrentAgentTaskEventJournal` 读取派生值；只要 checkpoint 存在，它就必须持有当前权威 journal，不能同时保存一份领先的 terminal journal。终态提交以 `CopilotAgentRunResult.TaskEventJournal` 为本轮权威事件证据；若 checkpoint 落后，甚至在取消/恢复边界上属于上一 run，聚合根会创建一份重基到 terminal journal 的 checkpoint 副本后再提交，而不是持久化两个 owner。旧快照加载时同样先选择较新的 legacy evidence，再迁入 checkpoint，且不刷新原 checkpoint 时间戳。checkpoint 退役时才把其 journal 转交给独立字段。开始下一轮时，conversation 会把 `CurrentAgentTaskEventJournal` 作为独立的 `TaskEventJournalBaseline` 传入运行时；即使取消后 checkpoint 已退休，新 run 也会从当前权威 journal 继续 sequence，而不是重新从 1 建立另一条事件谱系。取消、暂停或异常未能返回正式 run result 时，由 conversation 的 `CompleteOpenAgentRun` 在同一次状态转换中补齐缺失的控制事件、悬空工具终态和 `RunStopped`，再决定保留还是退休 checkpoint；展示层只设置消息终态，普通 Chat 不通过这条路径改写 Agent 状态。`CopilotConversationCompactionContext.CaptureSurface` 只从现有完整消息、压缩边界和 `ModelContent` 派生 `current / shadowed / log-only` 计数，`/context` 用它说明当前模型表面；该投影不持久、不改写消息，也不成为新的事实源。

同一轮的 checkpoint 增量先由 reducer 校验身份和 journal 单调性：Profile、任务意图、能力目录、工具、环境、Hook、项目指令面及其指纹在本轮内不得漂移；journal 必须相等或成为固定容量窗口中的前向后继。容量未满时要求严格追加；达到 256 条后允许既定淘汰策略删除旧事件，但仍保留的历史事件必须逐字段不变，最新 sequence 必须前进，不能用更晚时间戳包装回退或改写证据。conversation 聚合根使用同一条固定容量前向后继规则，不再用“最新时间戳”接纳从共同前缀分叉或完全跨谱系的候选；新 run 通过显式 journal baseline 续写同一谱系。事件等价性比较完整持久载荷，而不只比较 sequence 与 ID，因此相同 ID 下被改写的类型、时间、run、subject、关联 ID、工具名、状态、失败码、退出码或摘要不能替换现有证据；加载时还会从 sequence、run、type 与时间重新计算事件 ID，阻止借用另一事件的合法 ID。checkpoint 本身与独立终态证据都经过这条单调准入：只有等价或确实更新的 checkpoint 才能替换恢复点或撤下独立终态证据，整个 run 的迟到提交也会被拒绝。`TrySetAgentSessionCheckpoint` 与 `TryCommitAgentRunState` 都把“候选被接受”与“聚合值实际改变”分开返回；UI 只有在增量或终态 checkpoint 被接受后，才会把已送达 steering 视为已经进入该 checkpoint。checkpoint 与恢复记录清理作为同一次持久化转换；若终态提交被拒绝，steering 会恢复到会话草稿而不是依据未提交的候选被删除。这样倒退 checkpoint 即使声称包含新 steering，也不能造成指令丢失；等价 checkpoint 则仍可确认该 steering 已耐久化而无需重复替换对象。迟到但 sequence 更大的分叉 checkpoint、取消或失败回调因此不能让恢复 session 倒退、在取消后复活，或把已经完成的 run 改写成旧运行的终态；新一轮 checkpoint 确实更新时才重新成为 journal 的单一所有者。
## 核心流程

### 会话切换

`SelectConversation` → `ConversationSession.SelectConversation` → `ComposerSession.Load` → 把规范化草稿写回 conversation → 刷新绑定 → 按需保存。

后台完成逻辑只能更新任务捕获的 conversation/profile，不能重新读取当前 `SelectedConversation` 或 `SelectedProfile`；用户可能已切到另一会话。
### 输入与发送

WPF setter → `ComposerSession.Set*` → 同步草稿 → 捕获 token/附件 → admission/trust/budget → 创建消息 → `CopilotPreparedHostedTurn` → `TaskHost.TrySchedule`。

只有排程成功且 token 仍匹配才能清空 Composer。等待期间的新编辑必须保留；附件只移除捕获时的对象，不能对当前集合直接 `Clear()`。

`directPrompt` 不读取或消费界面 Composer、待发送附件、Skill、审查目标或 recovery。
### 排队 Follow-up

捕获 Composer → `FollowUpCoordinator.TrySchedule` → 写 durable recovery → 成功后提交 token → 执行前生成 prepared turn → 消息 Flush 成功后删除 recovery。

排队时冻结 profile、runtime config、附件和项目上下文，执行时重新捕获 history。Steering 是 runtime mailbox 消息，不是新 turn。
### 审批

Store event → coordinator 捕获 immutable transition → facade 切回 UI Dispatcher → coordinator 更新列表或 trace → facade 发通知并请求保存。

WPF 负责审查窗口和反馈；eligibility、TOCTOU 复核、决定和 trace 映射必须经过 `CopilotApprovalCoordinator`。

## 必须留在 WPF facade

- `ICommand`、CanExecute 和公开绑定属性。
- `OnPropertyChanged` 与 `CommandManager.InvalidateRequerySuggested`。
- Dispatcher、WeakEvent 和应用生命周期接线。
- 窗口、剪贴板、文件选择器和中文界面文本。
- 绑定所需的 `ObservableCollection` 投影。

不要为了缩短 ViewModel 创建 `CopilotUiCoordinator`；那只是给同一个大类换名字。

## 禁止事项

- 不增加 conversation、composer、pending action 或 queue 镜像字段。
- session/coordinator 不反向引用 ViewModel，不接 delegate bag。
- 不绕过 `CopilotPreparedHostedTurn` 重新传递大组平行参数。
- 排程成功前不消费 Composer 或 recovery。
- `await` 后不全量清空当前附件。
- 后台执行不读取当前 profile/config，使用已捕获快照。
- 不绕过 `CopilotApprovalCoordinator` 操作 approval store。
- 不直接双写 `AgentSessionCheckpoint` 与 `LatestAgentTaskEventJournal`；使用 conversation 的原子提交方法。
- 不把持久化、UI 通知和 Provider 执行合成 God service。
- 不用“再拆一个 partial”代替明确 owner。

## 修改前检查

1. 新状态是否只有一个 owner？
2. 异步操作是否有 capture/commit 或 recovery 语义？
3. 失败时消息、草稿和附件能否恢复？
4. 是否保留构造器、命令和 XAML 契约？
5. 是否同时有 owner 测试和最小 VM 集成测试？

## 测试入口

公开 facade 看 `CopilotChatViewModelContractTests`；会话看 `CopilotConversationSessionTests`；输入看 `CopilotComposerSessionTests`；队列看 `CopilotQueuedFollowUpCoordinatorTests`；审批看 `CopilotApprovalCoordinatorTests`；执行信封看 `CopilotPreparedHostedTurnTests`。

```powershell
dotnet build .\ColorVision\ColorVision.csproj -p:Platform=x64 --no-restore
dotnet test .\Test\ColorVision.Copilot.Tests\ColorVision.Copilot.Tests.csproj -p:Platform=x64 --no-restore
```

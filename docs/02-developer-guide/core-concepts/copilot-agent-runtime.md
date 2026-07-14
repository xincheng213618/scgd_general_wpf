# Copilot Agent Runtime

ColorVision Copilot 使用 Microsoft Agent Framework 作为唯一 Agent 执行层。模型、工具、审批、任务账本、恢复和会话状态都沿同一条运行路径处理；框架不可用时本轮明确失败，不切换到另一套规划器重放请求。

## 执行链

```text
CopilotToolRegistry
  -> CopilotAgentTaskHost 以单活动槽执行，跨会话 Agent 请求进入最多 3 项 FIFO 队列
  -> 按请求筛选 CanHandle
  -> 官方 MCP C# SDK 按请求发现已配置的 Streamable HTTP 工具
  -> 外部工具适配成同一 ICopilotTool 契约并合并去重
  -> Agent Framework 选择工具
  -> TodoProvider 持久化多步骤任务，AgentModeProvider 管理 plan/execute
  -> ContextWindowCompactionStrategy 在每次模型调用前压缩上下文
  -> TokenBudgetChatClient 累计本请求模型用量并限制循环
  -> Always 审批工具由 ApprovalRequiredAIFunction 暂停
  -> Pending Actions 收集批准/拒绝/过期决定
  -> 使用同一 AgentSession 回传 ToolApprovalResponseContent
  -> CopilotToolExecutor 运行前置 Hook
  -> 进入资源感知执行闸门（最多 4 个独立只读调用）
  -> 同资源调用互斥，写调用等待全部读调用并全局独占
  -> 发布 ToolStarted
  -> 在工具级超时和取消令牌下执行
  -> 返回 failure_kind / retry_allowed / attempt
  -> 仅当模型显式重试幂等工具的瞬时失败时允许第二次尝试
  -> 用户批准、拒绝或审批过期后回写同一 CallId
  -> 写入脱敏审计并运行后置 Hook
  -> 发布 ToolResult
  -> 将 observation 交回模型进入下一轮
  -> ExecutionContractLoopEvaluator 校验显式 URL / Web 搜索是否已有成功工具证据
  -> TodoCompletionLoopEvaluator 在 execute 模式下按本轮预算继续未完成任务
  -> 完成后序列化 AgentSession 到当前会话检查点
```

`CopilotAgentRuntimeRouter` 将配置完整的 OpenAI-compatible 和 Anthropic-compatible Profile 送入 Agent Framework。运行时不会在失败后自动切换执行器，也不会重放已经产生文本或工具调用的请求，避免写操作被重复执行。模型设置不暴露运行时开关。

直接 URL 请求还有确定性策略：`Auto` 模式会同时暴露 `FetchUrl` 与作为回退的 `WebSearch`，先读取原 URL，失败或证据不足时再搜索公开网页。Framework 原生 `LoopEvaluator` 上的执行契约会检查真实 step record；如果模型先写出答案却没有调用匹配工具，运行时撤回这段未支持草稿，并在同一 Session 中反馈缺失证据、要求下一轮调用工具。只有成功的 URL/搜索 observation 才满足契约；直接读取失败且仍有未尝试的搜索工具时继续回退，所有匹配路径都失败或模型仍拒绝调用时以 `Blocked` 和稳定 blocker code 结束，不再把模型文字当成已访问网页的证明。

`WebSearch` 在返回标题、摘要和 URL 的同时，从显式 URL 或 `site:` 查询提取目标主机，优先选择匹配结果，并通过同一 `FetchUrl` 实现深读；没有目标主机时只深读排名第一的安全结果。深读连同已确认的同源 JSON、RSS 和 Atom 最多读取三个资源，失败不会抹掉搜索线索，模型也不应重复读取已经成功深读的结果。工具结果压缩会分别保留搜索线索和深读正文。最终回答若使用成功的内置或外部网页工具却没有引用其返回的任何 URL，运行时会追加最多三个经过 http/https 校验的真实来源；已有有效引用、失败工具、普通问答、暂停和超时运行都不会触发补写。用户明确要求不访问网络时不触发该策略。

`Auto` 模式不会因为当前解决方案存在搜索根目录就自动暴露搜索工具。普通概念问答直接由模型回答；只有用户明确指向当前项目/代码、公开网页/最新信息或给出 URL 时，才分别开放本地搜索、`WebSearch` 或 `FetchUrl`。外部 MCP 中名称或描述可识别为文件搜索、网页搜索和 URL 读取的工具也服从同一意图门槛，其他设备、状态与业务工具仍按各自 `CanHandle` 判断。

执行契约比工具暴露策略更严格地区分“可能需要最新信息”和“用户明确要求搜索”：直接 URL、`Web` 模式以及“联网搜索 / search the web”等明确动作词才强制成功的网页证据。普通概念问题不会因为当前工具面中恰好存在 `WebSearch` 就增加模型轮次或产生失败搜索；没有匹配工具可用时也不会制造无意义循环，而是保留模型正常回答或说明能力边界的空间。

明确的工作区修改请求使用同一类执行契约：只有 `ApplyWorkspacePatch` 或 `ApplyCreateWorkspaceFile` 成功完成才算执行了对应修改，模型仅输出代码或声称“已经修改”不能满足契约。现有文件修改采用 `PreviewWorkspacePatch -> ApplyWorkspacePatch` 两阶段协议；预览只允许做一次精确且唯一的 `oldText/newText` 替换，并绑定当前文件 SHA-256。新文件采用 `PreviewCreateWorkspaceFile -> ApplyCreateWorkspaceFile`，只允许在当前解决方案可写根内创建白名单文本扩展，绝不覆盖已存在路径；缺失目录树先在同一父目录构造随机暂存树，再原子移动到首个缺失目录，避免与其他进程竞争时误认目录所有权。应用阶段必须使用原预览 ID、重新核对可写范围和冲突条件，并经 Agent Framework 原生审批。成功应用的两类预览都可由 `RollbackWorkspacePatch` 在再次审批且当前文件仍匹配应用后 SHA-256 时恢复原始字节或删除 Agent 创建的文件；只有 Agent 原子创建且回滚时仍为空的目录才会清理。预览只在当前进程中保留 30 分钟，不跨应用重启构成恢复授权。

可见对话历史进入 Framework 前统一经过可信窗口：只保留规范化的 `user` / `assistant` 角色，拒绝 `system`、工具和未知角色，防止历史数据提升为运行时指令。窗口保留最初用户目标，并在超限时从最近完整用户轮次开始；无用户消息的异常历史只做数量截断，不会构造默认空目标。Harness 指令仍由当前 Profile 和运行时单独提供。

## 工具契约

工具实现 `ICopilotTool`，至少提供名称、说明、结构化输入 Schema、请求适用性和执行方法。Framework 只向模型暴露该工具声明的参数，并在执行前拒绝未知参数、缺失必填参数、错误类型和非法行号范围。新增工具时：

1. 实现 `CanHandle`，确保工具只在拥有所需上下文时暴露。
2. 通过 `InputSchema` 声明工具真正使用的参数、类型、说明和必填项。
3. 为修改应用状态的工具声明 `Access = CopilotToolAccess.Write`；只读工具保留默认值。
4. 声明 `RiskLevel`、`ApprovalMode` 和 `Idempotency`。高风险写工具不能使用 `ApprovalMode.Never`；否则统一前置策略直接拒绝执行。
5. 并发契约默认只允许 `ReadOnly + Idempotent` 进入 `SharedRead`；写工具和非幂等/未知只读工具均为 `Exclusive`。工具可通过 `GetConcurrencyKey` 描述资源身份；运行时只持久化其不可逆指纹，不记录原始查询或路径。
6. 按最坏合理耗时设置 `ExecutionTimeout`，默认 30 秒，框架上限 10 分钟。排队时间单独记录，不占用工具执行超时。
7. 在 `ExecuteAsync` 中响应传入的 `CancellationToken`，并返回对模型有用的结构化摘要、内容或错误。工具内部产生的待审批动作必须填充 `CopilotToolResult.Approval`，不能要求运行时解析提示文本。
8. 失败时填写 `FailureKind`。只有可安全重复的瞬时故障才使用 `Transient`；参数错误、权限拒绝、资源不存在、冲突和内部错误不能伪装成可重试故障。
9. 只在组合根注册一次；注册表会拒绝空名称和忽略大小写的重复名称。

模型提交的工具参数即使未通过 Schema，也属于一次真实的 Agent 工具尝试：运行时会在执行闸门和审批之前拒绝它，消耗一次工具调用预算，并生成 `Failed + Validation` 的 step、ToolResult、任务事件和审计记录。非法参数的审计只保留字段名，不保存字段值；这类失败不可自动重试，但模型可以根据结构化错误改用修正后的参数再次调用。受保护工具同样先校验参数，非法调用不会创建 Pending Action。

模型请求本轮工具面中不存在的函数时，`FunctionInvokingChatClient` 仍负责生成带原 CallId 的 not-found 结果并交回模型纠错；ColorVision 在其下游 provider 响应处做只读观察，不改写框架消息协议。未知调用消耗工具预算，并按保守的高风险、不可重试、无 evidence 策略记录 `Failed + NotFound` step、任务事件和字段名审计，但绝不会创建审批或进入执行器。观察器按 CallId 去重，并忽略 `InformationalOnly` 或同一 provider 响应中已有匹配 FunctionResult 的服务端已处理调用。

模型重复提交完全相同的工具和参数时，只有上一次结果明确返回 `retry_allowed: true` 才允许再次执行。已成功、永久失败、正在运行或等待审批的相同调用会被无进展闸门拒绝；拒绝本身作为新的真实工具尝试消耗预算，并使用模型本轮 CallId 生成 `Failed + Conflict` step、ToolResult、任务事件和脱敏审计，但不会覆盖原调用的成功结果或重试状态，也不会再次打开审批。若仍有未完成 todo，最终 blocker 使用稳定代码 `tool_conflict`，停止原因为 `Blocked`，从而把重复调用循环与普通 pass 上限区分开。

`ApprovalMode.Always` 的工具应实现 `ICopilotFrameworkApprovedTool`。运行时只会对同时满足这两个条件的工具包装 `ApprovalRequiredAIFunction`；批准后才调用 `ExecuteApprovedAsync`。普通 `ExecuteAsync` 必须继续保留直接调用和业务入口所需的确认，不能把“来自模型”本身视为授权。

需要向用户展示具体目标和冲突指纹的工具还可实现 `ICopilotFrameworkApprovalPresentation`。Framework 审批层会使用工具生成的标题和说明，而不是展示原始参数值；工作区补丁因此能显示完整目标路径以及应用前后的 SHA-256，同时审计仍只记录参数字段名。

`CopilotToolCapabilityDescriptor` 是工具策略的标准快照，集中承载访问级别、风险、审批、幂等性、并发、超时和参数审计模式。Harness 提示、Framework 审批包装、执行闸门、重试、trace 与审计都只消费该 Descriptor，避免各层分别解释工具属性。现有工具的独立属性会由 `ICopilotTool.Capability` 默认桥接，新增工具可以直接提供 Descriptor；注册表会在工具进入运行时前拒绝非法枚举值及“高风险写入但从不审批”等不安全组合。有效并发与超时也在 Descriptor 中统一收敛：写入或非幂等能力强制独占，超时限制在默认 30 秒、最大 10 分钟之间。

普通工具执行从 `FunctionInvokingChatClient.CurrentContext.CallContent` 读取 provider 原始 CallId；Schema 拒绝、并发只读执行、原生审批、未知函数、trace、审计和任务事件因此使用同一个关联 ID。上下文由框架通过 AsyncLocal 隔离，并发函数不会互相串号；只有脱离 FunctionInvokingChatClient 的直接测试或业务调用才生成本地 CallId。

外部 MCP 工具通过 `CopilotMcpClientCapabilityPolicy` 从本地信任配置生成同一 Descriptor：显式 `read-only` 映射为低风险、幂等共享读取，默认 `approval` 映射为高风险、每次审批、非幂等独占写入。两者均使用 `NamesOnly` 审计模式，只记录参数名而不持久化第三方 Schema 中含义未知的值。

`CopilotCapabilityCatalog` 在 Descriptor 之上提供进程内只读目录。共享目录启动时发布全部内置工具，外部 MCP 成功发现后按来源原子更新；配置中删除的 MCP 来源会移除，暂时离线的来源保留最后已知元数据。每个条目包含稳定 ID、显示名、来源、条目 revision、有效策略、超时和输入 Schema 指纹；整体目录仅在来源或能力签名真正变化时递增 revision。MCP 来源 ID 使用端点与 token 环境变量名称的不可逆短指纹，不暴露 URL、环境变量名称或 token。插件可通过 `PublishSource(Plugin, ...)` 和 `ICopilotCapabilityCatalogIdentity` 发布自己的稳定能力键，同样受 64 个来源上限、重复 ID 检查和 Descriptor 安全校验约束。

设置诊断显示目录数量与 revision；本机 MCP 将同一快照以 `application/json` 暴露为只读资源 `colorvision://copilot/capabilities`。该资源只包含能力元数据与 Schema 指纹，不包含参数值、远端地址或凭据。

Agent Framework checkpoint 保存目录 revision，以及每个能力的稳定 ID、条目 revision 和内容指纹。恢复时不能只比较进程内 revision，因为应用重启后 revision 会重新计数；运行时会逐项比较内容指纹。原能力缺失或指纹变化时，Harness session 与 todo ledger 不会反序列化，但 checkpoint 中独立保存的有界对话记忆会与当前可见历史去重合并后交给新 session，并通过 Harness instructions 强制从当前能力重新规划。该记忆最多 16 条、64K 字符，只允许 user/assistant 消息，保留初始目标与最近问答，不保存 system/tool 消息、参数或授权。新增能力不使旧计划失效；旧格式 checkpoint 缺少能力快照时同样走安全重规划。能力指纹覆盖描述、来源、策略、超时、审计模式、evidence 模式与输入 Schema，但目录和 checkpoint 都不保存调用参数或凭据。

`CopilotToolEvidenceMode` 是 Descriptor 的显式证据持久化策略：`None`、`Summary`、`RedactedExcerpt`。只读工具默认 `Summary`，写工具默认 `None`；文档搜索、公开 Web 搜索和页面读取显式选择 `RedactedExcerpt`。`CopilotAgentEvidenceArtifacts.Merge` 仅接收成功、已完成、只读且幂等的 step record，按 capability ID 与哈希 resource key 去重，最多保留 24 条；`NamesOnly` 能力即使声明 excerpt 也只保存摘要。artifact 包含生产能力 ID/指纹、哈希资源键、脱敏摘要/摘录、内容指纹和采集时间，不包含调用参数。

只有 session 反序列化失败、profile 改变或 capability drift 触发新 session 时，恢复层才选择最近 12 条 artifact。可信防注入规则位于 Harness instructions；artifact JSON 使用单独的 user-role data message 插入到当前用户消息之前，避免把历史网页或工具内容提升为 system 指令。每条 artifact 标记 `producer_current`、`producer_changed` 或 `producer_unavailable`；后两者只能作为历史线索，所有易变化状态都必须重新核验，任何历史 evidence 都不能代表写操作审批。

### 结构化任务事件 journal

`CopilotAgentTaskEventJournal` 把原先分散的 todo snapshot、工具生命周期、原生审批、运行中 steering、evidence artifact 和 stop reason 归入同一条版本化序列。每次运行生成独立的 `run:` ID；工具 `CallId`、审批 action ID 和 steering 内容只生成稳定哈希关联键，todo 仅保存数字 ID 与完成统计。摘要复用 MCP 审计脱敏器并限制为 320 字符，journal 最多保留最近 256 条事件。

`CopilotAgentTaskEventJournal.Query` 支持按事件类型、run ID、工具名、subject/related ID 和 `BeforeSequence` 游标查询，单页最多 100 条，结果按新到旧返回。checkpoint 保存完整有界 snapshot，`CopilotAgentRunResult` 返回当前可查询 snapshot；旧 checkpoint、未知 Schema 或损坏的可选 journal 会被丢弃而不会变成模型上下文。journal 默认只作为诊断元数据，不代表任何审批或重放授权；唯一例外是 `Finalize` 恢复会把最后一个已停止 run 中最多 24 条脱敏工具结果、审批结果和 blocker 复制成独立 user-role 数据块，用于解释已经发生的结果。该数据块明确标为不可信历史数据，不能授权、重放或声称重新核验任何操作。

`CopilotAgentTaskEventJournalRegistry` 只发布当前选中会话最近一次已保存的 snapshot；新一轮会保留上一安全点，直到新的增量 checkpoint 原子替换它，运行完成后再发布带最终 stop reason 的版本。本机 MCP 通过 `colorvision://copilot/task-events` 暴露最近 100 条，通过只读 `get_agent_task_events` 支持类型、run、工具、关联 ID、`before_sequence` 和 `max_events` 过滤。两者均为显式诊断入口，不加入默认 diagnostic bundle，也不产生聊天活动行；没有已保存 journal 时直接返回 unavailable，不回退到日志搜索或其他工具。

### 结构化恢复协议

`CopilotAgentRecoveryPolicy` 为 `BudgetExhausted`、`TaskPassLimit`、`ProviderFailure` 和应用异常退出形成的 `Interrupted` checkpoint 提供恢复动作；`AwaitingUser` 必须等待新的用户决定，`ApprovalDenied` 也不会显示“继续”按钮。兼容 checkpoint 直接恢复 session；profile 或 capability 发生变化时改为新 session 重新规划。若最近存在执行器明确标记为 `RetryEligible` 的只读幂等失败，UI 显示“重试只读检查”，但恢复请求只保存工具名和哈希 call key，不保存或重放历史参数。

恢复意图以 `CopilotAgentRecoveryRequest` 类型化传入 Harness，而不是只依赖一段用户提示词。运行时会再次核对 checkpoint journal 的最后 stop reason，并记录 `RecoveryRequested` 事件；无效或与 checkpoint 不匹配的恢复元数据会被忽略。恢复指令始终要求重新核对当前状态：写操作不自动重放，历史审批不复用，受保护调用仍需本轮新的精确审批。普通回答的“重新生成”仍是独立路径，会清除 checkpoint，不会伪装成 Agent 恢复。

### 暂停、取消与 blocker

Agent 运行创建可序列化 session 并完成首次持久化后才发布内部 `CheckpointReady` 生命周期事件。此后编辑器主按钮切换为“暂停”：`CopilotAgentRunControl` 先记录 `Pause` 意图，再取消当前 provider/tool 等待；Agent Framework 只捕获这一类显式控制取消，使用不再取消的 finalization token 读取 todo ledger、写入 `PauseRequested` 与 `RunStopped(Paused)`，最后序列化 session。暂停产生的 checkpoint 可走相同恢复协议。到达真实落盘边界之前，主按钮保持普通停止语义，不会虚假承诺已保存。

运行中同时提供独立的显式取消动作。`Cancel` 同样形成可查询事件和 `Cancelled` stop reason，但不会保存本轮新 checkpoint；UI 会清除旧 checkpoint，避免把已明确放弃的任务再次作为可继续状态。外部超时、系统取消和没有类型化控制意图的 `OperationCanceledException` 仍向上传播，不会被伪装成用户暂停。

`CopilotAgentBlockerDetector` 将等待用户决定、审批拒绝，以及执行器明确判定不可重试的永久工具失败归一化为有界 `CopilotAgentBlockerSnapshot`。blocker 只保存类型、稳定 code、脱敏摘要、工具名和哈希 call key，不保存参数；assistant message 持久化最多 8 条，任务卡默认只显示一条紧凑提示。journal 追加 `BlockerDetected` 事件，本机 MCP 可沿用现有事件查询按类型、工具或关联 call key 检索。永久失败且仍有 todo 时 stop reason 为 `Blocked`，不再误报为普通轮次耗尽。

`CopilotAgentTaskIndex` 从已持久化的会话、最后一条 assistant task ledger、stop reason、blocker 和 checkpoint 派生跨会话任务摘要，不维护第二套任务状态。索引收录仍有未完成 todo 且需要关注的暂停、等待回复、审批拒绝、永久阻塞、预算耗尽、轮次耗尽、Provider 中断和应用中断任务，也收录没有开放 todo 但仍缺最终回答的可恢复任务，并按会话更新时间排序。会话侧栏只显示紧凑标题与状态；可恢复任务可以直接继续，其他任务可以打开原会话补充输入，用户也可以显式放弃并清除 checkpoint。应用重启后会把 journal 中存在 `RunStarted` 但没有对应 `RunStopped` 的运行补记为 `Interrupted`，再从 `chat-state.json` 重建索引，因此不会因为进程退出或 UI 会话切换而丢失任务入口。

`CopilotAgentTaskHost` 是进程级单活动运行宿主，运行通过稳定 run ID 绑定原会话，并集中持有 cancellation token、类型化 run control、checkpoint-ready 边界和 completion task。宿主保留一个活动槽和最多 3 个等待项；从其他会话提交的 Agent 请求按 FIFO 排队，前一项无论成功、失败或取消都会释放槽并提升下一项。整个 Agent 运行仍然串行，因此多任务不会绕过 capability resource、写操作审批或工具执行闸门。暂停只能在 checkpoint 边界后发生，明确取消可以覆盖待处理的暂停；排队任务可在启动前取消，且不会调用模型或工具。订阅者或某个排队任务异常不会阻断后续任务。

Agent 执行期间允许切换或新建会话，事件和最终结果仍写回启动运行的原消息。同一活动会话中的新输入继续作为 Harness steering，不会误建第二个任务；同一排队会话也不会重复入队。提交时冻结 Profile、附件、活动文档和解决方案路径，任务真正启动时再采集最新设备、流程和应用状态。普通 Chat 不进入后台队列，并继续保持会话切换锁定，因为其历史构造依赖当前选中会话。队列只存在于当前进程；可跨重启恢复的长期状态仍由 Session checkpoint 和任务索引负责。标题区只显示一条紧凑的运行或排队状态，不展开调度诊断。

目前所有可写入的审批工具均已收敛到这条协议，注册表中不再保留 `Conditional` 工具：

- `CreateFlow`、`ExecuteMenu` 和 `SetLanguage` 使用 `Always` 原生审批。
- 模板修改拆成低风险只读的 `TemplatePatch` 预览和高风险非幂等的 `ApplyTemplatePatch` 应用，避免一个工具根据参数临时改变审批语义。
- 工作区文本操作拆成只读的 `PreviewWorkspacePatch` / `PreviewCreateWorkspaceFile`，高风险非幂等的 `ApplyWorkspacePatch` / `ApplyCreateWorkspaceFile`，以及统一的 `RollbackWorkspacePatch`。默认可写根只包含当前解决方案目录；用户显式交给 Copilot 的现有文本文件和当前活动文档可作为精确修改授权，但新文件只能位于解决方案可写根。路径穿越、重解析点、非文本扩展、超过 1 MB 的编码内容、多重替换匹配和创建时覆盖现有路径均被拒绝。
- `SetTheme` 是独立、低风险的明确能力；通用 `ExecuteMenu` 始终受保护，不会因某个菜单项在预检中被判定为低风险而直接执行。

Agent Framework 是唯一运行路径，不再是 Profile 可选项。内置受保护调用使用原生 Pending Action 审批；外部 MCP 继续保留两阶段确认协议。

### 外部 MCP 工具发现

Copilot 使用官方 `ModelContextProtocol.Core` SDK 连接显式配置的 Streamable HTTP 服务。每次请求开始时发现工具，适配为 `ICopilotTool` 后再进入 Harness；外部工具不会绕过输入 Schema、调用次数、超时、并发闸门、生命周期事件、脱敏审计或审批机制。

- 当前只支持显式 Streamable HTTP 地址，不启动 stdio 命令或任意本地进程。
- 最多配置 8 个服务；单服务最多暴露 32 个工具，单请求最多合并 64 个外部工具。与内置工具重名时保留内置工具并记录诊断。
- 远程地址必须是 HTTPS；只有 loopback 允许 HTTP。URL 中不能嵌入用户名或密码。
- bearer token 只通过环境变量解析，配置文件不保存 token 值。
- 默认 `approval` 将整个服务视为高风险、可写、非幂等能力，每次精确调用都通过 `ApprovalRequiredAIFunction` 暂停并等待用户批准。
- 配置第五段可按远端工具精确名称建立白名单，并用 `tool=approval/read-only` 覆盖服务默认策略；只要存在白名单，未列出的工具就不会进入 Harness。留空或 `*` 为兼容模式，会暴露服务发现的全部工具。
- `read-only` 是操作者对对应工具做出的显式信任声明；此时工具按只读、幂等能力进入共享读取闸门。远端返回的 tool annotations 只作为提示，不用于放宽本地权限。
- 成功发现的协议 `Tool` 定义进入最多 32 个服务项、每个服务最多 512 个定义、默认 TTL 5 分钟的进程内缓存；缓存键包含服务、地址和 bearer token 的不可逆指纹，不保存 token 明文。缓存命中时使用官方 `McpClientTool(McpClient, Tool, ...)` 构造路径绑定到本轮新连接，不复用或常驻旧会话。
- 若服务通过 `ServerCapabilities.Tools.ListChanged` 声明支持工具列表变更，client lease 会注册 `notifications/tools/list_changed` 处理器；收到通知后立即使对应缓存失效，下一轮强制实时发现。处理器先于 client 释放，只在当前请求连接存续期间监听；TTL 和设置页 `Refresh Discovery` 仍是无活跃连接时的兜底。ColorVision 自带 MCP server 尚不能主动发送该通知，因此不会虚假声明此能力。
- 设置页 `Refresh Discovery` 强制绕过缓存。实时发现会比较有界能力签名、保留或递增服务能力 revision 并发出进程内变更事件；健康快照标明本轮使用 live 还是 cached discovery，并在通知已使缓存失效时显示需要实时刷新。
- MCP JSON Schema 会完整传给模型；本地先验证顶层必填字段、未知字段和参数总长度，嵌套约束仍由远端 MCP 服务权威校验。
- 工具发现会记录进程内健康快照，包括连接状态、发现数量和实际暴露数量；设置页只显示紧凑摘要，不把连接错误铺到主聊天区。
- 单个服务不可用只产生运行时诊断，不会终止 Agent 请求；普通聊天界面隐藏这类基础设施噪声。

官方 SDK 和传输说明见 [Model Context Protocol C# SDK](https://github.com/modelcontextprotocol/csharp-sdk)。ColorVision 自带 MCP server 同时支持 `Content-Length` 与 HTTP chunked 请求正文，已通过官方客户端连接、工具发现和真实 `tools/call` 的端到端测试。

## 生命周期与审计

每次工具调用都有稳定的 `CallId`、轮次、尝试次数/上限、运行时、工具名、读写级别、风险、审批模式、幂等性、并发模式、资源指纹、排队耗时、失败分类、是否允许重试、参数摘要、开始/结束时间、总耗时、超时、结果摘要和最终状态。状态包括 `Running`、`AwaitingApproval`、`Completed`、`Failed`、`TimedOut`、`Denied`、`Cancelled` 和 `Interrupted`。

### 多工具并发

Harness 创建后会显式启用 `FunctionInvokingChatClient.AllowConcurrentInvocation`，因此模型在同一响应内发出的多个函数调用可以由 Agent Framework 并行调度。业务工具不会直接无约束并发，而是统一进入 `CopilotToolExecutionGate`：

- 独立的 `SharedRead` 最多同时运行 4 个。
- 相同资源指纹的读取互斥，避免重复访问同一状态或缓存。
- `Exclusive` 调用会阻止新的读取，等待现有读取结束后全局独占；任何写工具都会被强制提升为 `Exclusive`，即使工具声明错误。
- 等待闸门时取消会产生 `Cancelled` trace 和审计记录，但不会调用工具。
- Agent 事件出口串行化，保证并行工具不会并发修改聊天状态。

框架的并行开关及其默认串行语义见 [FunctionInvokingChatClient.AllowConcurrentInvocation](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.ai.functioninvokingchatclient.allowconcurrentinvocation)。

### 上下文压缩与请求预算

Harness 不再关闭压缩。ColorVision 为所有模型使用一个保守的 32K 托管上下文窗口；当最大输出为默认 8K 时，单次输入预算为 24K。`ContextWindowCompactionStrategy` 在每次模型调用前执行框架原生两阶段策略：

1. 达到输入预算的 50% 后，先把旧工具调用组折叠成简短结果，保留工具名称和结论。
2. 达到输入预算的 80% 后，再删除最旧的非系统消息组，同时保留最近对话。

这里的 32K 是 ColorVision 主动采用的安全工作窗口，不声称等于供应商模型宣传的最大上下文。框架会从 `MaxContextWindowTokens - MaxOutputTokens` 计算输入预算；具体策略见 [ContextWindowCompactionStrategy](https://learn.microsoft.com/en-us/dotnet/api/microsoft.agents.ai.compaction.contextwindowcompactionstrategy?view=agent-framework-dotnet-latest)。

`CopilotAgentRunBudget` 统一管理一次 Agent 运行的请求 Token、业务工具调用、Agent pass 和总时长。有效值按“单次请求覆盖 > Profile 默认值 > 框架安全默认值”解析；Profile 默认值分别为 64K Token、12 次业务工具调用、4 个 pass 和 300 秒，并通过上下限避免无界运行。业务工具硬上限由 Bridge 独立执行，Harness 的 todo、mode、approval 等框架函数和最后一次自然语言总结使用单独的有界迭代余量，因此用完最后一次业务工具后仍可返回结论；只有继续越界调用工具时才记录 `ToolBudgetExhausted`。设置窗口中的 `Agent run limits` 可以修改 Profile 默认值，集成调用方也可通过 `CopilotAgentRunBudgetOverride` 只覆盖当前请求。

Harness 正常结束但没有产生任何 `TextContent` 时，Runtime 不再把空 Todo 账本直接判为完成。它会通过同一 Token 与传输重试中间件发起一次非流式最终总结，`ChatOptions.Tools` 固定为空，并把有界工具观察与当前任务账本作为数据交给模型；因此这个阶段不能重放业务或 Framework 工具。总结仍为空或失败时会发出固定用户提示、记录 `IncompleteOutput` 与 `provider_empty_output` blocker，保存 checkpoint，并且绝不标记 `Completed`。

`CopilotAgentRecoveryMode.Finalize` 为这种状态以及“Provider 在任务已完成后断流”的状态提供独立恢复协议。Runtime 在验证 profile、checkpoint 和最后一次 `RunStopped` 后直接进入 no-tools Provider 调用，不发现外部 MCP、不创建 Harness、不恢复 Todo、不打开审批；不匹配的 Finalize 请求会在工具发现之前拒绝，不能降级成普通 Agent 执行。再次空输出或超时时会刷新原 session 的 journal 与对话记忆并继续保留 checkpoint；成功后旧 session checkpoint 会退役，因为旁路生成的最终回答并不存在于旧 Framework session 中，后续轮次应从包含新答案的可见历史创建新 session。

`CopilotTokenBudgetChatClient` 基于官方 `DelegatingChatClient` 中间件包装真实模型客户端，累计同一 Agent 请求内所有供应商调用的 usage。当已观测用量达到有效请求预算时，下一次供应商调用会被替换为确定性的结束响应，不会再次调用模型或重放工具。供应商不返回 usage 时使用字符数近似，并在诊断中标记 `includes estimates`。这个预算是跨调用循环闸门；单个供应商响应可能使最终统计略微超过阈值。

总时长由与调用方取消令牌链接的运行级计时器约束。超时或业务工具越界都会返回结构化 `BudgetExhausted` 结果，并在可能时先完成任务账本和 Session 检查点；用户主动暂停或取消的语义优先于同时发生的超时。最终 Token、供应商调用、工具调用、pass 上限、已用时长、是否使用估算以及具体预算耗尽类型都会作为 `RuntimeDiagnostic` 和 `CopilotAgentBudgetSnapshot` 写入执行记录。

### 原生任务账本与 plan/execute

Harness 的 `TodoProvider` 和 `AgentModeProvider` 现在作为标准运行时能力直接启用，不再由 ColorVision 维护第二套计划状态：

- 模型通过框架原生 `todos_add`、`todos_complete`、`todos_remove` 和查询工具维护任务；任务保存在 `AgentSessionStateBag`，随会话检查点一起持久化。
- 新会话默认进入 `execute`；模型在确实需要用户做关键选择时可切换到 `plan`。模式同样属于 Session 状态。
- `TodoCompletionLoopEvaluator` 只在 `execute` 模式驱动后续 Agent pass。只要还有未完成任务，Harness 会把剩余清单作为反馈再次调用 Agent；pass 数、工具调用、请求 Token 和总时长都使用当前请求解析后的统一运行预算。
- 每轮结束都会生成结构化 `CopilotAgentTaskLedgerSnapshot`，并把完成数、模式和最多三个未完成标题写入 `RuntimeDiagnostic`。聊天会话因此保留可检查的任务恢复记录，同时真实状态仍以 Framework Session 为唯一数据源。
- 从检查点恢复时，会在执行记录中明确标注恢复了多少任务。未完成只读任务可继续；持久任务只代表上下文和计划，不代表执行授权。
- Todo 状态更新本身由 Framework 对同一 Session 串行化，避免并发函数调用产生重复 ID 或丢失更新。

这里直接使用 Framework 的任务提供器与完成循环，而不是实现自定义 planner。官方 Harness 说明将持久 Todo、plan/execute 模式、逐次模型调用历史和可选完成循环列为完整 Agent 脚手架的组成部分，参见 [Agent Harnesses](https://learn.microsoft.com/en-us/agent-framework/agents/harness) 与 [TodoCompletionLoopEvaluator](https://learn.microsoft.com/en-us/dotnet/api/microsoft.agents.ai.todocompletionloopevaluator?view=agent-framework-dotnet-latest)。

### Agent Skills

Harness 的 `AgentSkillsProvider` 作为标准能力启用，采用渐进式加载：模型先看到技能的名称和说明，只有当前任务匹配时才调用 `load_skill` 读取完整 `SKILL.md`，随后按需读取该技能目录中的参考资料。这样可以让 ColorVision 保存稳定、可复用的诊断流程，而不必把所有领域说明长期塞进系统提示。

技能只从受信任的应用或工作区目录发现：

- 当前解决方案搜索根下的 `.agents/skills/<skill-name>/SKILL.md`，适合项目级扩展和覆盖。
- 应用输出目录下的 `Copilot/Skills/<skill-name>/SKILL.md`，用于随 ColorVision 发布的内置技能。
- 不默认扫描用户主目录或任意外部路径；符号链接和重解析点目录不会加入技能源。

技能脚本发现与执行当前完全关闭。`load_skill` 和 `read_skill_resource` 是只读元数据操作，由 Framework 的只读规则自动批准，不在界面生成无意义的审批；技能内容本身不构成任何业务操作授权。所有 ColorVision 工具仍经过现有 Schema、风险级别、并发闸门、审计和写操作审批。

新增技能时，为目录创建 `SKILL.md`，并在 YAML frontmatter 中提供稳定的 `name` 和明确的 `description`。正文应描述何时使用、证据顺序、停止条件和安全边界；较长的清单或领域资料放进同目录的 `references/`，让 Agent 按需读取。内置的 `colorvision-flow-diagnostics` 是流程诊断示例；`colorvision-database-operations` 复用重置、导出和清理代码中的分类，把数据库组织为服务配置表、服务设置表和结果表，先验证实时 Schema，再指导通用 SQL 查询或经原生审批的数据清理。

这种结构遵循 [OpenAI Skills](https://learn.chatgpt.com/docs/build-skills) 的渐进式披露语义，并直接使用 [Microsoft Agent Framework Agent Skills](https://learn.microsoft.com/en-us/agent-framework/agents/skills) 实现运行时加载。

### 任务 UI、停止原因与运行中 steering

成功的 Agent 轮次会把任务快照和结构化 `CopilotAgentStopReason` 写入对应的 Assistant 消息。聊天面板直接显示模式、完成数、任务标题/说明和停止原因。停止原因包括正常完成、等待用户、审批未通过、请求预算耗尽和本轮任务 pass 上限；这些字段随聊天状态持久化，状态 Schema 当前为 5。

只有最新 Assistant 消息且当前 Conversation 仍持有兼容 Session 检查点时，“继续”按钮才可用。点击后会创建一个正常的可见用户轮次，要求先复核当前状态；它不会从历史任务生成写授权。

运行中的补充要求走 Harness 自带的 `MessageInjectingChatClient`：

- Runtime 在活动 `AgentSession` 上注册短生命周期 steering context，结束或异常时自动移除。
- 用户在生成过程中输入内容并按 Enter/点击 `↳` 后，只以 `ChatRole.User` 入队，不允许客户端构造 system、assistant 或工具消息。
- 注入队列按 Session 隔离，并在线程安全的 `EnqueueMessages` 中等待下一个模型调用机会；立即停止仍使用原有取消令牌和方形停止按钮。
- steering 只改变模型后续决策，所有业务工具仍通过同一 Schema、预算、并发闸门和审批边界。

具体注入语义见官方 [MessageInjectingChatClient](https://learn.microsoft.com/en-us/dotnet/api/microsoft.agents.ai.messageinjectingchatclient?view=agent-framework-dotnet-latest)。

### AgentSession 会话检查点

Runtime 使用 Harness 的 `ChatHistoryProvider.InvokedAsync` 正式持久化边界：首次创建 Session 时先保存一个安全点，此后每次成功的真实模型调用写入历史后，都通过 `SerializeSessionAsync` 增量序列化 `AgentSession`，连同最新 todo ledger、evidence 和 journal 原子保存到对应 `CopilotConversationRecord`。包装器内部仍委托 `InMemoryChatHistoryProvider`，并使用与 Harness 相同的 context-window compaction reducer，不维护第二份聊天历史。正常结束时再写入带最终 stop reason 的检查点。应用重启或重新创建 Runtime 后，下一轮使用 `DeserializeSessionAsync` 恢复框架内部历史；运行时将 checkpoint 的有界 conversation memory 与 UI 可见历史做有序对齐，只把 checkpoint 之后尚未持久化的可见消息插入当前用户消息之前，既避免重复整段历史，也覆盖异常退出、旧状态迁移或增量保存滞后造成的上下文缺口。对话记忆与可执行 Session 分开保存：Profile、能力或请求工具变化导致 Session 重建时恢复有界语义上下文，而 todo、历史工具调用和审批继续作废。该扩展点的调用顺序见官方 [Harness 文档](https://learn.microsoft.com/en-us/agent-framework/agents/harness)。

检查点具备以下约束：

- 使用 Profile ID、协议、Base URL、模型和系统提示的不可逆指纹做兼容性校验；配置变化后自动新建 Session。
- 单个检查点上限 4,000,000 字符，超限或 JSON 损坏时不恢复。
- 发起新请求时保留上一安全点，只有新的安全点成功保存后才替换；如果应用中途退出，启动归一化会把开放 run 标记为 `Interrupted`，由用户显式继续，不自动执行。
- Chat 模式和重新生成回答不会复用 Framework 检查点。
- 工具 trace、幂等限制和写操作审批仍是独立安全边界；恢复 Session 不代表恢复任何旧批准。即使恢复清单要求重复同一个写调用，也会产生新的 CallId 和 Pending Action。

### 显式有界重试

模型供应商调用使用独立的传输重试层。HTTP 408、429、5xx、无响应连接错误、超时和 I/O 中断只有在尚未收到第一个流式更新时才会退避重试；默认最多请求三次，间隔 250ms、500ms。首个文本、usage 或 FunctionCall 更新一旦出现，后续中断绝不重放已有输出；若本轮已有文本或业务工具 step，Runtime 会记录 `ProviderFailure` 与 `provider_interrupted` blocker，并在不再次调用 Provider 的情况下读取 todo、合并 evidence/journal、序列化当前 Harness Session。开放 todo 通过 `Resume` 恢复同一 Session；没有开放 todo 时通过 `Finalize` 只生成最终回答。尚无任何实质进展的 400/401/403 等永久错误、连接失败和调用方取消仍直接向上传播。重试层位于 Token 预算层外侧，因此每次真实供应商请求都计入 `ProviderCalls`；诊断只记录失败类别、尝试序号和等待时间，不保存响应正文或异常消息。

运行时不会在工具内部暗中自动重跑。首次失败会把 `failure_kind`、`retry_allowed` 和 `attempt` 交回 Agent Framework；只有模型再次发出完全相同的调用时才触发重试。重试必须同时满足：

- 工具声明 `Idempotent`。
- 上次结果是 `Transient` 且状态为 `Failed` 或 `TimedOut`。
- 同参数最多执行两次，并且未超过本请求工具轮次上限。
- 受保护写工具每次重试都生成新的审批动作；上一次批准不会被复用。

`NonIdempotent`、`Unknown`、校验错误、权限拒绝、用户取消和业务失败都不可重试。这使失败恢复是可见、可审计的 Agent 决策，而不是无法观测的执行器副作用。

`CopilotToolExecutionAuditLogger` 保存最近 200 条调用并写入 log4net。参数摘要和错误会复用 MCP 的脱敏规则，不应记录 API key、token、密码、Authorization 或 bearer secret。聊天面板显示工具开始、完成状态和耗时，便于确认 Agent 是否真正执行了动作。未获得结果的文件、文档或网页搜索属于后台证据尝试，默认不显示活动行，也不会把整段处理状态标红；完整脱敏诊断仍保留在结构化 trace 中供恢复与排障使用。

工作区修改后的真实验证由 `RunWorkspaceValidation` 提供。它不是通用命令行：只接受工作区内现有 `.sln` / `.slnx` / 项目文件，以及精确的 `dotnet build` 或 `dotnet test`、`Debug` / `Release` 和 10–600 秒超时；执行参数由宿主固定拼装，始终附带 `--no-restore`，不经过 shell，也不接收额外参数。该操作会触发原生审批，因为项目 target 本身可能执行仓库代码。stdout/stderr 分别有界保留头尾，超时会终止进程树；非零退出作为已完成的失败验证证据交回模型，不会因工具层失败而自动重复。显式“修改并验证”请求由执行契约强制按“批准修改 -> 批准验证”的顺序完成，提前验证不能满足契约。

通用 Windows 命令由 `RunShellCommand` 提供。它在 Auto/Agent 新任务中作为核心工具提供；已有会话的普通短追问不会顺带保留这个高风险工具，只有出现明确命令或机器诊断意图时才重新开放。工具支持 `PowerShell`、`CMD` 和跟随设置的 `Auto`；设置窗口的 `Default shell` 可选择“自动（PowerShell）”、`PowerShell` 或 `CMD`。工具接受完整命令、可选现有工作目录和 5–600 秒超时，始终以无窗口、非交互方式运行，关闭标准输入，并有界返回真实 exit code、stdout、stderr 和耗时。取消或超时会终止整个进程树。由于当前宿主没有类似 Codex 的系统级文件沙箱，所有通用命令——包括只读端口检查——都必须经过 Agent Framework 原生审批，审批内容显示 Shell、工作目录和完整命令；参数审计只保存字段名。显式“执行命令”“检查端口/进程/服务”请求由执行契约要求成功的命令 observation，仅向用户展示命令文本不能冒充执行结果。普通概念问答可以直接回答，不强制调用命令工具。

例如“我想要知道 6666 端口有没有被占用”应优先使用 PowerShell：

```powershell
Get-NetTCPConnection -LocalPort 6666 -ErrorAction SilentlyContinue | Select-Object LocalAddress,LocalPort,State,OwningProcess
```

当用户明确选择 CMD 或使用批处理语法时，也可以运行：

```bat
netstat -ano | findstr :6666
```

业务数据库同时提供语义快捷能力和通用 SQL 能力。`QueryFlowExecutionStats` 只读聚合 `t_scgd_measure_batch`：接受 `today`、`yesterday` 或 `last7days`，按本机时区生成左闭右开的日历范围，返回执行尝试总数、各 `FlowStatus` 数量、完成率和平均耗时。它适合“今天执行了多少次流程”这类常见问题，不要求模型了解表结构。

`QueryDatabaseSql` 是 Auto/Agent 模式的核心只读工具，不再依赖用户恰好说出“查询数据库”等固定关键词才向模型暴露；普通知识问答仍由模型依据工具说明直接回答，涉及当前数据库事实时才调用它。该工具接受一条只读 MySQL 语句，支持 `SELECT`、`SHOW`、`DESCRIBE`、`EXPLAIN`、`TABLE` 和最终落到只读语句的 CTE。默认最多返回 100 行，可在 1–500 行内调整；列数、单元格和总输出长度都有上限，密码、token、API key 等敏感列会统一显示为 `<redacted>`。`ExecuteDatabaseSql` 接受一条数据或结构变更，支持 `INSERT`、`UPDATE`、`DELETE`、`REPLACE`、`CREATE`、`ALTER`、`DROP`、`TRUNCATE` 和 `RENAME`，每次都必须经过 Agent Framework 原生审批；无 `WHERE` 的 `UPDATE` / `DELETE`、`TRUNCATE` 和 `DROP` 会在审批说明中给出加强警告。普通 DML 在事务内提交，DDL 遵循 MySQL 的隐式提交语义。服务设置表是版本托管的只读边界，即使进入审批也会在执行前拒绝变更；更新时由版本自带 SQL 重置原生设置。服务配置表由 Service Manager 在数据库重置前导出并回写，结果表不参与保留且可通过受审批的清理流程删除。

两个通用工具都只连接 ColorVision 当前配置的 MySQL，不接受连接字符串。解析层只允许单语句并拒绝 executable comment；账号与授权管理、创建/删除数据库、全局或会话设置、事务控制、锁、动态 SQL、存储过程调用、服务器关闭/终止、插件管理、文件导入导出以及延时函数不开放。审计只保存参数名和 SQL 指纹，错误结果不回显数据库异常或连接信息。显式 SQL 请求由执行契约要求真实成功 observation，模型不能在没有查询结果或写入结果时编造数据库状态。

同一份生命周期数据还会以版本化 `CopilotAgentTraceEntry` 写入聊天会话。工具开始和结束事件都会触发原子状态保存；待确认动作还会把 `approval action_id` 与 Agent `CallId` 关联。批准、拒绝、过期、开始执行和执行结果都会更新同一条 trace。切换会话后仍能看到当前状态；若应用在执行或等待审批时退出，加载时会把遗留的 `Pending` / `Running` / `AwaitingApproval` trace 以及开放的 Agent run 收敛为 `Interrupted`，要求用户从最近安全点继续并为受保护调用产生新的审批，不会自动重放可能产生副作用的工具。结果、错误与参数在持久化前统一脱敏并限制长度。旧会话没有结构化 trace 时继续使用原有的 `ExecutionContent`，无需迁移才能打开。

## Hook 扩展点

实现 `ICopilotToolExecutionHook` 可接入统一策略：

- `BeforeExecuteAsync`：权限判断、用户确认、速率限制、策略拒绝。
- `AfterExecuteAsync`：遥测、诊断记录、结果归档。

前置 Hook 拒绝或异常时工具不会运行；后置 Hook 异常只记录日志，不覆盖真实工具结果。Hook 应保持轻量，并避免保存未脱敏的参数。

## 当前边界与下一步

这是一套基础框架，不等同于 Codex、Claude Code 或 OpenCode 的完整能力。后续按优先级扩展：

1. 在单文件预览协议之上增加多文件变更集，使一次用户审批绑定完整文件清单和每个文件的前后指纹，并支持整体冲突检测与回滚。
2. 在通用 Shell 之上增加可配置的只读命令自动批准白名单与更强的进程级隔离；当前版本在没有系统沙箱时保持每次原生审批。
3. 将同一 Capability Fabric 扩展到 LAN 和 ServiceHost，保持能力白名单、身份、审批、evidence 和审计语义一致。

框架中间件与函数调用层的设计参考 [Microsoft Agent Framework Middleware](https://learn.microsoft.com/en-us/agent-framework/agents/middleware/)；请求预算中间件使用官方建议的 [DelegatingChatClient](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.ai.delegatingchatclient?view=net-11.0-pp) 组合方式。

相关测试集中在 `CopilotCoreRuntimeTests` 与 `CopilotToolExecutorTests`。局部验证命令：

```powershell
dotnet test Test/ColorVision.UI.Tests/ColorVision.UI.Tests.csproj --filter "FullyQualifiedName~Copilot" --no-restore -p:BuildProjectReferences=false
dotnet build ColorVision/ColorVision.csproj --no-restore -p:BuildProjectReferences=false
```

# Copilot 工具契约

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

每轮 Agent 还会生成版本化的 `CopilotAgentEnvironmentContext`。Harness 以明确标记为 host data 的 JSON 接收当前工作目录、平台/架构、首选 Shell、本地日期与时区、活动文档、最多 8 个搜索/可写根目录，以及通过只读 `.git/HEAD` 获取的仓库、分支和提交；它不会读取或发送进程环境变量、API key 或其他凭据。路径边界只描述工具可见范围，不代表写入授权。checkpoint 仅保存环境版本和稳定 SHA-256 指纹；工作区、活动文档、Shell、时区或 Git 状态变化会废弃旧 Harness session 并从有界对话记忆重新规划，单纯跨越本地日期不会使 checkpoint 失效。旧 checkpoint 没有环境指纹时同样只重建可执行 session，不丢失对话语义。

`CopilotToolEvidenceMode` 是 Descriptor 的显式证据持久化策略：`None`、`Summary`、`RedactedExcerpt`。只读工具默认 `Summary`，写工具默认 `None`；文档搜索、公开 Web 搜索和页面读取显式选择 `RedactedExcerpt`。`CopilotAgentEvidenceArtifacts.Merge` 仅接收成功、已完成、只读且幂等的 step record，按 capability ID 与哈希 resource key 去重，最多保留 24 条；`NamesOnly` 能力即使声明 excerpt 也只保存摘要。artifact 包含生产能力 ID/指纹、哈希资源键、脱敏摘要/摘录、内容指纹和采集时间，不包含调用参数。

只有 session 反序列化失败、profile 改变或 capability drift 触发新 session 时，恢复层才选择最近 12 条 artifact。可信防注入规则位于 Harness instructions；artifact JSON 使用单独的 user-role data message 插入到当前用户消息之前，避免把历史网页或工具内容提升为 system 指令。每条 artifact 标记 `producer_current`、`producer_changed` 或 `producer_unavailable`；后两者只能作为历史线索，所有易变化状态都必须重新核验，任何历史 evidence 都不能代表写操作审批。

## 结构化任务事件 journal

`CopilotAgentTaskEventJournal` 把原先分散的 todo snapshot、工具生命周期、原生审批、运行中 steering、evidence artifact 和 stop reason 归入同一条版本化序列。每次运行生成独立的 `run:` ID；工具 `CallId`、审批 action ID 和 steering 内容只生成稳定哈希关联键，todo 仅保存数字 ID 与完成统计。摘要复用 MCP 审计脱敏器并限制为 320 字符，journal 最多保留最近 256 条事件。

`CopilotAgentTaskEventJournal.Query` 支持按事件类型、run ID、工具名、subject/related ID 和 `BeforeSequence` 游标查询，单页最多 100 条，结果按新到旧返回。checkpoint 保存完整有界 snapshot，`CopilotAgentRunResult` 返回当前可查询 snapshot；旧 checkpoint、未知 Schema 或损坏的可选 journal 会被丢弃而不会变成模型上下文。journal 默认只作为诊断元数据，不代表任何审批或重放授权；唯一例外是 `Finalize` 恢复会把最后一个已停止 run 中最多 24 条脱敏工具结果、审批结果和 blocker 复制成独立 user-role 数据块，用于解释已经发生的结果。该数据块明确标为不可信历史数据，不能授权、重放或声称重新核验任何操作。

`CopilotAgentTaskEventJournalRegistry` 只发布当前选中会话最近一次已保存的 snapshot；新一轮会保留上一安全点，直到新的增量 checkpoint 原子替换它，运行完成后再发布带最终 stop reason 的版本。本机 MCP 通过 `colorvision://copilot/task-events` 暴露最近 100 条，通过只读 `get_agent_task_events` 支持类型、run、工具、关联 ID、`before_sequence` 和 `max_events` 过滤。两者均为显式诊断入口，不加入默认 diagnostic bundle，也不产生聊天活动行；没有已保存 journal 时直接返回 unavailable，不回退到日志搜索或其他工具。

## 结构化恢复协议

`CopilotAgentRecoveryPolicy` 为 `BudgetExhausted`、`TaskPassLimit`、`ProviderFailure` 和应用异常退出形成的 `Interrupted` checkpoint 提供恢复动作；`AwaitingUser` 必须等待新的用户决定，`ApprovalDenied` 也不会显示“继续”按钮。兼容 checkpoint 直接恢复 session；profile 或 capability 发生变化时改为新 session 重新规划。若最近存在执行器明确标记为 `RetryEligible` 的只读幂等失败，UI 显示“重试只读检查”，但恢复请求只保存工具名和哈希 call key，不保存或重放历史参数。

恢复意图以 `CopilotAgentRecoveryRequest` 类型化传入 Harness，而不是只依赖一段用户提示词。运行时会再次核对 checkpoint journal 的最后 stop reason，并记录 `RecoveryRequested` 事件；无效或与 checkpoint 不匹配的恢复元数据会被忽略。恢复指令始终要求重新核对当前状态：写操作不自动重放，历史审批不复用，受保护调用仍需本轮新的精确审批。普通回答的“重新生成”仍是独立路径，会清除 checkpoint，不会伪装成 Agent 恢复。

## 暂停、取消与 blocker

Agent 运行创建可序列化 session 并完成首次持久化后才发布内部 `CheckpointReady` 生命周期事件。此后编辑器主按钮切换为“暂停”：`CopilotAgentRunControl` 先记录 `Pause` 意图，再取消当前 provider/tool 等待；Agent Framework 只捕获这一类显式控制取消，使用不再取消的 finalization token 读取 todo ledger、写入 `PauseRequested` 与 `RunStopped(Paused)`，最后序列化 session。暂停产生的 checkpoint 可走相同恢复协议。到达真实落盘边界之前，主按钮保持普通停止语义，不会虚假承诺已保存。

运行中同时提供独立的显式取消动作。`Cancel` 同样形成可查询事件和 `Cancelled` stop reason，但不会保存本轮新 checkpoint；UI 会清除旧 checkpoint，避免把已明确放弃的任务再次作为可继续状态。外部超时、系统取消和没有类型化控制意图的 `OperationCanceledException` 仍向上传播，不会被伪装成用户暂停。

`CopilotAgentBlockerDetector` 将等待用户决定、审批拒绝，以及执行器明确判定不可重试的永久工具失败归一化为有界 `CopilotAgentBlockerSnapshot`。blocker 只保存类型、稳定 code、脱敏摘要、工具名和哈希 call key，不保存参数；assistant message 持久化最多 8 条，任务卡默认只显示一条紧凑提示。journal 追加 `BlockerDetected` 事件，本机 MCP 可沿用现有事件查询按类型、工具或关联 call key 检索。永久失败且仍有 todo 时 stop reason 为 `Blocked`，不再误报为普通轮次耗尽。

`CopilotAgentTaskIndex` 从已持久化的会话、最后一条 assistant task ledger、stop reason、blocker 和 checkpoint 派生跨会话任务摘要，不维护第二套任务状态。索引收录仍有未完成 todo 且需要关注的暂停、等待回复、审批拒绝、永久阻塞、预算耗尽、轮次耗尽、Provider 中断和应用中断任务，也收录没有开放 todo 但仍缺最终回答的可恢复任务，并按会话更新时间排序。会话侧栏只显示紧凑标题与状态；可恢复任务可以直接继续，其他任务可以打开原会话补充输入，用户也可以显式放弃并清除 checkpoint。应用重启后会把 journal 中存在 `RunStarted` 但没有对应 `RunStopped` 的运行补记为 `Interrupted`，再从 `chat-state.json` 重建索引，因此不会因为进程退出或 UI 会话切换而丢失任务入口。

`CopilotAgentTaskHost` 是进程级单活动运行宿主，运行通过稳定 run ID 绑定原会话，并集中持有 cancellation token、类型化 run control、checkpoint-ready 边界和 completion task。宿主保留一个活动槽和最多 3 个等待项；从其他会话提交的 Agent 请求按 FIFO 排队，前一项无论成功、失败或取消都会释放槽并提升下一项。整个 Agent 运行仍然串行，因此多任务不会绕过 capability resource、写操作审批或工具执行闸门。暂停只能在 checkpoint 边界后发生，明确取消可以覆盖待处理的暂停；排队任务可在启动前取消，且不会调用模型或工具。订阅者或某个排队任务异常不会阻断后续任务。

Agent 执行期间允许切换或新建会话，事件和最终结果仍写回启动运行的原消息。同一活动会话中的新输入继续作为 Harness steering，不会误建第二个任务；同一排队会话也不会重复入队。提交时冻结 Profile、附件、活动文档和解决方案路径，任务真正启动时再采集最新设备、流程和应用状态。普通 Chat 不进入后台队列，并继续保持会话切换锁定，因为其历史构造依赖当前选中会话。队列只存在于当前进程；可跨重启恢复的长期状态仍由 Session checkpoint 和任务索引负责。标题区只显示一条紧凑的运行或排队状态，不展开调度诊断。

目前所有可写入的审批工具均已收敛到这条协议，注册表中不再保留 `Conditional` 工具：

- `CreateFlow`、`ApplyFlowPatch`、`ExecuteMenu` 和 `SetLanguage` 使用 `Always` 原生审批。
- 模板修改拆成低风险只读的 `TemplatePatch` 预览和高风险非幂等的 `ApplyTemplatePatch` 应用，避免一个工具根据参数临时改变审批语义。
- 工作区文本操作拆成只读的 `PreviewWorkspacePatch` / `PreviewCreateWorkspaceFile`，高风险非幂等的 `ApplyWorkspacePatch` / `ApplyCreateWorkspaceFile`，以及统一的 `RollbackWorkspacePatch`。默认可写根只包含当前解决方案目录；用户显式交给 Copilot 的现有文本文件和当前活动文档可作为精确修改授权，但新文件只能位于解决方案可写根。路径穿越、重解析点、非文本扩展、超过 1 MB 的编码内容、多重替换匹配和创建时覆盖现有路径均被拒绝。
- `SetTheme` 是独立、低风险的明确能力；通用 `ExecuteMenu` 始终受保护，不会因某个菜单项在预检中被判定为低风险而直接执行。

Agent Framework 是唯一运行路径，不再是 Profile 可选项。内置受保护调用使用原生 Pending Action 审批；外部 MCP 继续保留两阶段确认协议。

## Flow 图语义与受保护编辑

`.stn` 是带自定义头和 GZip 内容的二进制画布格式，不交给模型按文本读取。`InspectFlowGraph` 从活动编辑器生成 `colorvision.flow-graph.v1`：包含基于节点 Guid 的稳定 instance id、保存时使用的精确 `module|runtime type` 键、结构化输入/输出端口、边、位置和确定性 SHA-256 revision；属性值只有显式请求时才返回并经过脱敏。输出限制最多 200 个节点，避免大型流程无界占用上下文。

`SearchFlowNodeCatalog` 查询当前编辑器实际注册的节点类型，并返回标题、分类、业务节点类型、默认设备 Code 和可写 `STNodeProperty` Schema。例如“添加相机节点”必须先搜索 `相机` / `camera`，由模型在 `CVCameraNode`、`LVCameraNode`、XR/AOI 等真实候选中选择；候选含义不同则询问用户，不能硬编码或猜测类型名。目录按已加载类型签名缓存，插件节点集合变化时自动重建。

写入面收敛为 `PreviewFlowPatch` / `ApplyFlowPatch` 两个工具，而不是为每种动作持续增加工具。每次 patch 只允许一种操作：`add_node` 使用目录返回的精确 type key；`set_property` 使用节点的稳定 ID、目录公开的可写 `propertyName` 和现有 `STNodePropertyDescriptor` 字符串转换，不建立第二套属性转换器，并拒绝密码、token、secret、license 等敏感属性；`connect` 只接受图快照中的 `out:N` / `in:N` 端口 ID，并复用 `CanConnect` / `ConnectOption` 的方向、所有者、锁定、单连接、重复边、数据类型和环路校验。

预览在 UI 线程验证活动流程未运行且 revision 未过期；属性修改在从真实节点持久化数据构造的离图副本上验证，连线只运行无副作用的连接资格检查。`ApplyFlowPatch` 经原生审批后再次检查相同 revision，再执行单项修改；失败时恢复属性旧值、移除已加入节点或断开本次新边。成功也不会自动保存或运行流程。revision 基于每个节点真实 `GetSaveData()` 字节哈希和结构化边生成，复杂 `STNodeProperty` 的变化也会失效旧预览；单节点保存异常时退回稳定的基础节点状态哈希，避免上下文采集整体失败。删除、断连和批量 patch 尚未开放，不能回退到 Shell 或直接改 `.stn` 绕过边界。对应的 `colorvision-flow-authoring` Skill 保持“检查图 -> 查目录/属性/端口 -> 预览单项 patch -> 审批 -> 复核 revision”的短流程，诊断仍由独立的 `colorvision-flow-diagnostics` 负责。

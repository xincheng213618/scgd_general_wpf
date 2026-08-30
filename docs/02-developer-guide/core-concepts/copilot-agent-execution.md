---
knowledge_id: "copilot.execution"
knowledge_type: "topic"
status: "current"
summary: "Copilot 请求调度、工具筛选、审批、只读委派与执行证据闭环。"
aliases: ["为什么 Copilot 没调用工具","子 Agent 有哪些权限","CopilotAgentTaskHost","CopilotToolRegistry","CopilotAgentExecutionContract"]
code_paths: ["ColorVision/Copilot/Agent/CopilotAgentTaskHost.cs","ColorVision/Copilot/Agent/CopilotProviderRetryChatClient.cs","ColorVision/Copilot/Agent/CopilotMicrosoftAgentFrameworkRuntime.AgentStreamingLoop.cs","ColorVision/Copilot/Agent/CopilotMicrosoftAgentFrameworkRuntime.FrameworkSupport.cs","ColorVision/Copilot/Agent/CopilotAnthropicHttpErrorHandler.cs","ColorVision/Copilot/Agent/CopilotContextWindowRecoveryChatClient.cs","ColorVision/Copilot/CopilotChatService.Streaming.cs","ColorVision/Copilot/CopilotChatService.RequestPipeline.cs","ColorVision/Copilot/Agent/CopilotToolRegistry.cs","ColorVision/Copilot/Agent/CopilotAgentExecutionContract.cs","ColorVision/Copilot/Agent/CopilotToolExecution.cs","ColorVision/Copilot/Agent/CopilotAgentAccessModels.cs","ColorVision/Copilot/Agent/CopilotMicrosoftAgentFrameworkRuntime.ApprovalRouting.cs","ColorVision/Copilot/Presentation/CopilotHostedTurnCompletion.cs","ColorVision/Copilot/CopilotConversationUsageDiagnostics.cs","ColorVision/Copilot/CopilotConversationStatistics.cs","ColorVision/Copilot/Agent/CopilotOpenAiAgentChatClientFactory.cs"]
test_paths: ["Test/ColorVision.Copilot.Tests/CopilotAgentTaskHostQueueDispatchTests.cs","Test/ColorVision.Copilot.Tests/CopilotAnthropicProviderFailureTests.cs","Test/ColorVision.Copilot.Tests/CopilotAnthropicHttpFailureTests.cs","Test/ColorVision.Copilot.Tests/CopilotAnthropicHttpErrorBoundaryTests.cs","Test/ColorVision.Copilot.Tests/CopilotHostedTurnCompletionTests.cs","Test/ColorVision.Copilot.Tests/CopilotHostedTurnUsageTests.cs","Test/ColorVision.Copilot.Tests/CopilotProviderPayloadErrorTests.cs","Test/ColorVision.Copilot.Tests/CopilotAgentExecutionContractRetryTests.cs","Test/ColorVision.Copilot.Tests/CopilotCodexApprovalsReviewerTests.cs","Test/ColorVision.Copilot.Tests/CopilotOpenAiProviderRetryTests.cs"]
related: ["copilot.runtime","copilot.tool-contracts","copilot.lifecycle","copilot.interactions","copilot.session-tools"]
---

# Copilot Agent 执行链

本页描述 Agent 运行链、工具筛选和执行契约，以及与普通 Chat 共用的回答终态边界。会话回顾、计划定位、活动展开、任务面板和输入快捷键见 [Copilot 本地交互与快捷键](./copilot-local-interactions.md)。

```text
CopilotToolRegistry
  -> CopilotAgentTaskHost 以单活动槽执行，跨会话请求进入有界 FIFO 队列（默认 3 项，可配置上限 16）
  -> CopilotAgentExtensionBridge 合并当前已加载业务模块的上下文与工具
  -> 按请求筛选 CanHandle
  -> 官方 MCP C# SDK 按请求发现已配置的 Streamable HTTP 工具
  -> 外部工具适配成同一 ICopilotTool 契约并合并去重
  -> Agent Framework 选择工具
  -> TodoProvider 持久化多步骤任务，AgentModeProvider 管理 plan/execute
  -> ContextWindowCompactionStrategy 在每次模型调用前压缩上下文
  -> TokenBudgetChatClient 累计本请求模型用量并限制循环
  -> 需要原生审批的工具由 ApprovalRequiredAIFunction 形成精确调用边界
  -> 冻结执行规则、结构化临时授权或 Pending Action 的人工/自动复核产生逐调用决定；自动复核不等于直接放行
  -> 使用同一 AgentSession 回传 ToolApprovalResponseContent
  -> CopilotToolExecutor 在执行瞬间重绑不可替换的内置写保护，再运行冻结的前置 Hook；冻结表完整容纳注册表与项目 Hook
  -> 进入资源感知执行闸门（最多 4 个独立只读调用）
  -> 同资源调用互斥，写调用等待全部读调用并全局独占
  -> 发布 ToolStarted
  -> 在工具级超时和取消令牌下执行；已进入执行的写入/非幂等调用取消或超时且未取得可确认结果时记录 OutcomeUnknown，先核对外部状态再重试
  -> 返回 failure_kind / retry_allowed / attempt
  -> 仅当模型显式重试幂等工具的瞬时失败时允许第二次尝试
  -> 审批决定形成或过期后回写同一 CallId
  -> 写入脱敏审计并运行同步后置 Hook；`decision:block` 仅替换模型可见反馈，`continue:false` 则在已完成结果落账后以策略阻塞结束本轮
  -> 发布 ToolResult
  -> 将 observation 交回模型进入下一轮
  -> ExecutionContractLoopEvaluator 校验显式 URL / Web 搜索是否已有成功工具证据
  -> TodoCompletionLoopEvaluator 在 execute 模式下按本轮预算继续未完成任务
  -> 完成后序列化 AgentSession 到当前会话检查点
```

## 运行时选择与临时授权

`CopilotAgentRuntimeRouter` 将配置完整的 OpenAI-compatible 和 Anthropic-compatible Profile 送入 Agent Framework。运行时不会在失败后自动切换执行器，也不会重放已经产生文本或工具调用的请求，避免写操作被重复执行。模型设置不暴露运行时开关。输入框的访问状态通过同一个可变 `CopilotAgentAccessContext` 进入 `CopilotTurnRequest`、`CopilotAgentRequest` 和正在运行的 Framework Session，但不会写入会话状态。

Anthropic 官方适配器的 `AnthropicSseException` 进入同一供应商错误处理边界。尚未输出内容或工具调用时，只有 SDK 明确分类的 `overloaded_error`、`rate_limit_error`、`api_error` 和 `timeout_error` 可按现有次数／退避限制重试；认证、请求及未知错误不自动重试。SSE 错误不是 HTTP 错误状态，重试诊断保留固定错误类型，不伪造 429 等状态码。已经产生正文或工具执行记录后，任何该类 SSE 中断都保留进展，以 `ProviderFailure` 完成账本与检查点收尾，不重发已产生内容的调用。`CopilotAnthropicProviderFailureTests` 使用安装版本的正式适配器和受控 SSE，覆盖错误分类、正文后不重发、实际工具完成后的恢复，以及严格 Turn 终态；失败流未由适配器发布正式 usage 时仍按预算估算处理，不把底层 `message_start` 字段冒充已返回的完整用量。

内部兼容枚举名 `FullAccess` 表示最长 15 分钟、绑定 conversation、task 和当前 workspace 的临时授权，不是任意工具免审。`CanAutoApprove` 的直接批准分支目前只允许声明 `AllowsTemporaryFullAccess` 的 `ApplyWorkspacePatchEnvelope` 与 `RollbackWorkspacePatchEnvelope`，并复核可写范围；其他受保护工具可以在 `CanAutoReview` 条件成立时交独立权限审查器逐次复核，因此不能写成“Shell、模板、Flow、菜单和数据库一律每次人工确认”。临时任务复核与显式 `approvals_reviewer=auto_review` 的条件、未批准后的不同处理，以及 `/approve` 精确重试边界统一见[原生审批、自动复核与参数快照](./copilot-agent-tool-contracts.md#原生审批与参数快照)。`ConfirmProtectedActions` 也不等于禁止显式配置的自动复核。

临时授权不会扩大 Review 模式、工具 Schema、意图策略、工作区范围、执行契约、并发闸门、超时或审计边界，也不会追溯批准已经等待的 Framework Action。任务结束、失败、取消、超时、工作区变化或应用重启都会撤销临时 grant，新会话和 conversation branch 也不会继承；显式复核者配置不是这份短期 grant，不能混用其生命周期。

### OpenAI HTTP 重试预算

`CopilotOpenAiAgentChatClientFactory` 为 Chat Completions 和 Responses 共用的 `OpenAIClientOptions` 设置 `ClientRetryPolicy(0)`，关闭 SDK 内部重试。重试只由 ColorVision 的供应商重试层执行，因此一次预算尝试对应一次 HTTP 请求，不会被 SDK 再放大为四次。429/503 等瞬态失败按宿主上限重试，401 等永久失败不重试；正文、usage 或工具调用已经发布后，不重放这一模型调用。已完成工具后发起的下一次模型调用可以在尚无新输出时有限重试，但不会重新执行此前工具。`CopilotOpenAiProviderRetryTests` 使用正式工厂、正式适配器和受控回环 HTTP，核验两条路由的实际请求数、`ProviderCalls`、估算用量及工具完成后的 `ProviderFailure` 检查点，不连接真实供应商账户。

### Anthropic HTTP 失败边界

Anthropic 非成功 HTTP 响应同样进入供应商错误收尾。生产适配器关闭 SDK 内部重试，由 ColorVision 按当前请求尚未输出内容的边界、最多三次尝试和预算统一处理；每次 HTTP 尝试进入 Provider 调用统计，避免 SDK 与宿主重试次数相乘。408、429 和 5xx 可有限重试，认证等其他状态不自动重试。已完成工具后的下一次模型调用失败时，主循环以 `ProviderFailure` 保存工具事实、已报告用量与检查点，不重放已完成工具。

`CopilotAnthropicHttpErrorHandler` 只处理失败响应：错误正文最多读取 256 KiB，脱敏后交 SDK 的类型化异常工厂；超限或正文读取 I/O 失败仍保留原 HTTP 状态，不把 401 改判为可重试的网络错误。响应头的请求 ID 与退避时间绑定同一次请求，`Retry-After-Ms` 优先、无效时回退 `Retry-After`，服务端等待时间最多两分钟且不短于本地退避。成功响应原样交给 SDK，错误响应在抛出前释放。上下文窗口分类同时检查类型化 HTTP 状态，401 即使带有长度超限文字也不能触发压缩重发；400/413 的合法上下文错误仍可被识别。`CopilotAnthropicHttpFailureTests` 使用本机回环服务核验正式生产适配器，`CopilotAnthropicHttpErrorBoundaryTests` 补充响应所有权、并发头隔离、脱敏、长度和状态边界；均不调用真实供应商账户。

## 多会话活动投影

桌宠通过 `DesktopPetCopilotBridge` 观察任务宿主与确认存储，不是第二个调度器；优先级、已读消除、导航与确认卡统一见[活动呈现](./copilot-local-interactions.md#消息显示与桌宠活动)。

## 网页读取的执行证据

直接 URL 请求还有确定性策略：`Auto` 模式会同时暴露 `FetchUrl` 与作为回退的 `WebSearch`，先读取原 URL，失败或证据不足时再搜索公开网页。Framework 原生 `LoopEvaluator` 上的执行契约会检查真实 step record；如果模型先写出答案却没有调用匹配工具，运行时撤回这段未支持草稿，并在同一 Session 中反馈缺失证据、要求下一轮调用工具。只有成功的 URL/搜索 observation 才满足契约；直接读取失败且仍有未尝试的搜索工具时继续回退，所有匹配路径都失败或模型仍拒绝调用时以 `Blocked` 和稳定 blocker code 结束，不再把模型文字当成已访问网页的证明。

## 搜索深读与工具输出归档

`WebSearch` 在返回标题、摘要和 URL 的同时，从显式 URL 或 `site:` 查询提取目标主机，优先选择匹配结果，并通过同一 `FetchUrl` 实现深读；没有目标主机时只深读排名第一的安全结果。深读连同已确认的同源 JSON、RSS 和 Atom 最多读取三个资源，失败不会抹掉搜索线索，模型也不应重复读取已经成功深读的结果。工具结果压缩会分别保留搜索线索和深读正文；所有模型可见工具结果在产生时即受字符/Token 双预算约束并按工具类型保留头尾或分段，截断边界不会拆开 UTF-16 代理对，因此不需要再引入一套改写历史 ToolResult 的剪枝日志。普通成功工具的纯文本结果如果仍被截断，运行时会把完整脱敏文本放入最多 24 个临时会话归档，只向模型暴露 `content_archive` 中的不透明 ID，并由 `ReadToolOutput` 按需分页读取；归档不暴露文件路径、不可跨会话，并在容量淘汰、会话删除或应用退出时清理，读取归档的工具结果不会再递归归档。最终回答若使用成功的内置或外部网页工具却没有引用其返回的任何 URL，运行时会追加最多三个经过 http/https 校验的真实来源；已有有效引用、失败工具、普通问答、暂停和超时运行都不会触发补写。用户明确要求不访问网络时不触发该策略。

## 稳定只读工具与动态暴露

`Auto` 模式只要拥有当前解决方案的搜索根，就稳定提供 `SearchFiles`、`GrepText`、`ReadLocalFile` 和 `ListDirectory`。它们不再按当前句子的关键词裁剪；模型依据名称、描述和 JSON Schema 自主决定是否调用，因此普通概念问答仍可不执行搜索。搜索发现的根目录内文件可以在同一 Agent 运行中继续读取，不要求文件路径必须在发送问题前已经显式出现；读取和列目录仍做规范化根边界检查，并拒绝经重解析目录越界。公开网页/最新信息与直接 URL 仍分别控制 `WebSearch` 和 `FetchUrl` 的动态暴露。数据库、日志、流程统计、系统诊断和通用 Shell 同样属于稳定内置能力。外部 MCP 中名称或描述可识别为文件搜索、网页搜索和 URL 读取的工具仍服从对应意图门槛，其他设备、状态与业务工具继续按自身运行时可用性判断。

## 只读子 Agent 与隔离

主 Agent 通过宿主管理的 `CopilotSubagentRoleCatalog` 选择专用只读角色。`DelegateExplore` 把范围较广或预计产生大量中间证据的多文件调查交给全新的只读 Harness Session；`DelegateScout` 处理需要查找、读取并综合多个公开来源的文档或依赖研究，简单单页读取仍直接使用 `FetchUrl` / `WebSearch`。模型可在同一个响应中发出最多两个互不依赖的角色调用；Framework 函数层并发执行，角色共享的协调器再用两个可取消的槽限制子运行，第三个调用等待槽位。不同任务使用不同只读资源键，重复任务继续服从现有同资源互斥与 no-progress 保护。

子 Agent 不继承父会话历史、附件、checkpoint、todo、mode、Skills、可写根、外部 MCP、访问模式或审批状态，也不会获得 Harness 自动注入的 `todo_*`、`mode_*` 或 `load_skill` 控制函数。`Explore` 只接收自包含调查任务、最多四个搜索根、活动文档和仍处于这些根内的项目指令；存在活动文档时优先保留其所在根，工具面固定为 `SearchFiles`、`GrepText`、`ReadLocalFile` 和 `ListDirectory`。`Scout` 只接收自包含外部研究任务，不接收任何本地根、活动文档或项目指令，工具面固定为 `WebSearch` 和 `FetchUrl`；网页内容始终按不可信证据处理。两者都不能调用 Shell、数据库、写工具、MCP、审批或再次委派。`Explore` 每次最多 8 次工具调用，`Scout` 最多 6 次；两者都是 2 个 Agent pass、90 秒、16,384 个请求 Token 和 12,000 字符返回内容，更小的父运行预算会同步收紧工具次数、pass、时长和 Token。

## 委派预算和审计

同一父请求的全部 `Explore` / `Scout` 运行共享一个委派 Token 池：总量通常为父请求预算的一半，最低 4,096、最高 32,768 Token；单个子运行按并发公平分配并至少需要 4,096 Token。成功后按真实用量结算，异常或取消时保守消耗已预留额度，预算不足时不再启动新的供应商调用。每个子运行生成带角色前缀的独立 `explore-*` / `scout-*` ID，父工具 trace 的 `CallId` 与角色、该 ID、停止原因、排队时间、工具次数和 Token 预算一起持久化；子运行内部的逐工具噪声仍不复制到主聊天。供应商调用数、Token 与估算用量继续归集进父运行预算，父 Agent 收到结果后仍需综合证据并完成最终回答。这一角色分工保持了 [OpenCode Agents](https://opencode.ai/docs/agents/) 中“主 Agent 选择带独立提示词和权限的 Explore / Scout”的核心语义；整体仍由 [Microsoft Agent Framework](https://learn.microsoft.com/en-us/agent-framework/overview/) 提供 Agent、Session、工具与中间件执行基础，并保留 ColorVision 自己的预算、隔离和审批模型。

## 短追问与能力续租

稳定的内置 Agent 能力不依赖上一轮关键词，因此“现在呢”“再检查一遍”等短追问仍能看到相同 Schema，并由模型结合会话历史重新发起结构化调用。动态或意图作用域工具仍可续租最近一轮真正成功执行过、只读、幂等且无需审批的能力；写工具和通用 Shell 不通过续租获得额外授权。checkpoint 恢复后也会用当前能力目录重新规划，不把旧调用参数当成授权。

## 何时强制联网证据

执行契约比工具暴露策略更严格地区分“可能需要最新信息”和“用户明确要求搜索”：直接 URL、`Web` 模式以及“联网搜索 / search the web”等明确动作词才强制成功的网页证据。普通概念问题不会因为当前工具面中恰好存在 `WebSearch` 就增加模型轮次或产生失败搜索；没有匹配工具可用时也不会制造无意义循环，而是保留模型正常回答或说明能力边界的空间。

## 工作区补丁的预览、应用与回滚

明确的工作区修改请求使用同一类执行契约：只有 `ApplyWorkspacePatch` 或 `ApplyCreateWorkspaceFile` 成功完成才算执行了对应修改，模型仅输出代码或声称“已经修改”不能满足契约。现有文件修改采用 `PreviewWorkspacePatch -> ApplyWorkspacePatch` 两阶段协议；预览只允许做一次精确且唯一的 `oldText/newText` 替换，并绑定当前文件 SHA-256。新文件采用 `PreviewCreateWorkspaceFile -> ApplyCreateWorkspaceFile`，只允许在当前解决方案可写根内创建白名单文本扩展，绝不覆盖已存在路径；缺失目录树先在同一父目录构造随机暂存树，再原子移动到首个缺失目录，避免与其他进程竞争时误认目录所有权。应用阶段必须使用原预览 ID、重新核对可写范围和冲突条件，并经 Agent Framework 原生审批。成功应用的两类预览都可由 `RollbackWorkspacePatch` 在再次审批且当前文件仍匹配应用后 SHA-256 时恢复原始字节或删除 Agent 创建的文件；只有 Agent 原子创建且回滚时仍为空的目录才会清理。预览只在当前进程中保留 30 分钟，不跨应用重启构成恢复授权。

## 可见历史窗口和上下文预算

普通 Chat 与进入 Framework 的可见对话历史统一经过可信窗口：只保留规范化的 `user` / `assistant` 角色，拒绝 `system`、工具和未知角色，防止历史数据提升为运行时指令。窗口不再使用固定的 8 条 / 32,000 字符旧限制，而是从独立 Agent 上下文配置和当前最大输出计算：约 50% 的输入空间分给可见历史，其余空间保留给系统提示、项目指令、Skills、工具 Schema、附件、运行时观察和输出。在默认 1,048,576 Token 上下文、8,192 Token 输出下，历史上限为 508 条 / 2,080,768 字符，单条最多 260,096 字符；32,768 Token 的最小上下文则自动收紧为约 12 条 / 49,152 字符。最终仍有 512 条和单条 262,144 字符的结构性上限，防止状态异常导致无界枚举或单条消息垄断上下文。

窗口始终保留最初用户目标和最近一轮，字符超限时优先删除完整的旧 `user -> assistant` 轮次，避免留下失去问题来源的孤立回复。无用户消息的异常历史只做有界截断，不会构造默认空目标。Chat 附件上下文占用一个独立槽位，并计入同一个自适应字符预算；`/context` 显示本轮实际解析出的条数、总字符和单条字符上限。

该窗口只收敛发给模型的历史，不删除本地完整会话，也不为每轮额外调用模型做摘要；请求预览会显示实际保留的消息数、字符数和原始规模。这样先用确定性窗口为其他上下文组成留出稳定余量，再由 Agent Framework 的 Token 压缩处理运行时消息，避免重复摘要成本。`CopilotAgentContextBuilder` 同时从实际 user-role 组装结果派生只读 provenance：以稳定的 source / form / trust 和保留项数、字符数区分历史 recall、用户目标与问题、配置 catalog、应用/附件/工具 snapshot、项目 instructions 和宿主回答约束；运行诊断只记录这些元数据，不记录正文、路径或凭据。该快照不持久、不参与恢复，也不成为会话或 prompt 的第二个 owner；Harness 指令仍由当前 Profile 和运行时单独提供。设计取向与 [Codex `/compact`](https://learn.chatgpt.com/docs/developer-commands.md?surface=cli) 保留关键点、释放上下文，以及 [Claude Code `/compact`](https://code.claude.com/docs/en/commands) 对长对话主动压缩的原则一致。

## 压缩摘要不能升级完成证据

主动 `/compact` 使用 `CopilotConversationCompactionPlanner` 选择最早的完整 `user -> assistant` 轮次，并保留最近一轮原文。`CopilotConversationCompactionTerminalEvidence` 从本地仍完整保存的原始 assistant 消息重新计算截至边界的回答中断与非完成 Agent stop reason，不依赖旧摘要是否正确转述；压缩提示要求每种 `<assistant_response_interrupted>` 和 `<agent_turn_incomplete stop_reason="...">` opening marker 至少原样保留一次。摘要完成后 `EnsurePreserved` 再做确定性校验，遗漏任何所需 marker 时拒绝写入 `conversation.Compaction`，因此模型生成的摘要不能把部分工作升级成完成证据；`EnsureSummaryShrinks` 还要求新摘要连同历史上下文标记的估算权重严格小于被替换消息，拒绝形式上成功但实际扩大上下文的结果。用户提供的聚焦要求仍会进入摘要请求，但终态完整性与收敛约束放在其后，不能被聚焦要求静默删除。该契约结合 [Codex `/compact` 保留关键细节](https://learn.chatgpt.com/docs/developer-commands?surface=cli#keep-transcripts-lean-with-compact)、[Claude Code compaction 的结构化摘要与自定义聚焦](https://code.claude.com/docs/en/sessions#manage-context-within-a-session)，以及 grok `PriorTurnInterrupt` 的结构化中断原因。

## 回答终态与模型可见的中断证据

普通 Chat 对 OpenAI-compatible 与 Anthropic-compatible 的每个非空 SSE `data` 事件验证 JSON 语法（`[DONE]` 结束标记除外）。损坏的 JSON 立即形成不可自动重试的 `invalid_response_format`，保留已发布的正文和用量，不再跳过坏事件并靠后续结束标记宣称回答完整。HTTP 成功状态中的非流式 JSON 正文语法损坏也使用相同错误；诊断只保留固定说明与脱敏请求 ID，不包含原始载荷或解析器异常正文。合法的未知 JSON 事件、注释心跳、多行 `data` 仍兼容。`CopilotProviderPayloadErrorTests` 覆盖两类供应商的损坏事件、进展保留、无重放和协议扩展对照。

`CopilotChatMessage.ModelContent` 为所有未完整结束的 assistant 轮次附加固定 `<assistant_response_interrupted>` 标记：流式截断、已有部分正文的 Chat/Agent 取消、无正文失败、暂停，以及排队轮次在调用模型和工具前取消都会形成模型可见的终结边界；显示正文、用户可见错误和 `ResponseInterruptionDetail` 不会作为该标记的一部分重新注入。这样下一次请求既能保留已完成工作，也不会把悬空用户消息当成仍待执行的授权或把未完成步骤当作成功；新尝试开始时会清除旧标记。Agent Framework 正常返回但 `AgentStopReason` 不是 `Completed` 时不伪装成传输中断，而是在 `ModelContent` 追加独立的 `<agent_turn_incomplete stop_reason="...">` 标记；`AwaitingUser`、`ApprovalDenied`、`BudgetExhausted`、`TaskPassLimit`、`Blocked`、`Paused`、`Cancelled`、`IncompleteOutput`、`ProviderFailure` 和 `Interrupted` 都保留结构化枚举值，UI、正文与 `/copy` 语义不变。只有 `IsResponseContentTruncated` 代表真实回答正文不完整，即使 Agent stop reason 为 `Completed` 也会转入回答中断边界。Agent 主循环统一复用 Chat 的 Provider 终态分类：`Length` 可执行一次禁用业务工具的有界收尾，只有收尾完整时才用新正文替换原部分回答；`ContentFilter` 不自动重试，避免把策略停止当成可绕过的瞬时错误；`ToolCalls` 或未知的明确终态可进入同一有界收尾，但不能直接证明完成。最终仍为长度上限、内容过滤、工具请求或未知终态的主循环或收尾结果会保留允许正文并落为专用 Provider blocker，不再进入窄证据改写或来源附录。缺失终态继续按 Framework 的兼容语义处理，不把旧 Provider 的自然结束误判为失败。`CopilotHostedTurnCompletion.PrepareTerminalEvidence` 在持续目标评估前关闭缺失终态的工具 trace 并提交该截断标记；`CopilotGoalContinuationPolicy` 将 `Completed` 加回答中断解析为不完整输出，跳过独立完成评估并暂停目标，同时保留原始 Agent stop reason 供任务审计。该组合还进入已有的 `Finalize` 恢复通道：`CopilotAgentRecoveryRequest` 只有在 stop reason 为 `Completed` 且携带可信的上一回答中断状态时才接受该形态，运行时继续复用禁用全部工具的 final-answer-only 路径；只有带可显示正文且 Provider 未报告明确的非成功终态才退休旧 checkpoint。否则允许返回的部分正文会保留，写入对应 Provider blocker，并以 `IncompleteOutput` 刷新 checkpoint，供下一次继续使用“重试最终回答”；`CopilotAgentTaskIndex` 将它显示为“等待最终回答”，而不是把任务重新排入执行。

## 用量观察与分支去重

异常、取消或暂停收尾使用当前 Assistant 已经接收的 Provider usage，保留输入、输出和缓存用量。没有已报告用量时清除该轮与 `conversation.LastUsage`，不继承上一轮数据，也不从 Agent 的估算预算推算账单。`CopilotHostedTurnUsageTests` 经真实 ViewModel 的请求调度、用量事件和失败／取消路径验证这一边界。

`CopilotHostedTurnCompletion` 保存真实 Provider 返回用量与独立的回答终态，不能因回答被标为中断就抹去已报告的消耗，也不能从 Agent 的估算预算制造 Provider 账单。`CopilotConversationUsageDiagnostics.Capture` 聚合已结束回答的 `ReportedUsage`，并单独加入 `CompactionUsage` 和 `TitleGenerationUsage`；活动、已跟踪、未报告和中断回答分别计数。Agent 时延、工具调用、委派与重试指标来自本地任务快照，不是同一口径的账户用量。

`/usage session` 还可显示当前 Profile 最近一次可识别的 Provider 响应头限额快照；它可能过期，不是套餐余额，也不会为显示报告请求账户 API。`daily|weekly|cumulative` 由 `CopilotConversationStatistics` 按本机日期汇总最近7天、最近30天或全部历史的消息活动；当前实现逐消息累计，与 session 额外加入的压缩/标题调用不是完全相同的口径，不能假定两种视图总量必然相等。

统计从 `BranchOrigin.ParentConversationId` / `ThroughMessageId` 确定本分支拥有的消息起点；父会话缺失时按分叉时间与消息时间回退，避免复制前缀反复计数。它不读取 Provider 账户、价格或远端额度，也不能补出未记录的失败请求费用。交互命令、可见报告与导出边界统一见[输入、命令与活动呈现](./copilot-local-interactions.md)。

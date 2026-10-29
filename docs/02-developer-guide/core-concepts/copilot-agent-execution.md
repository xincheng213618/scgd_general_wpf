---
knowledge_id: "copilot.execution"
knowledge_type: "topic"
status: "current"
summary: "Copilot 请求调度、工具筛选、审批、只读委派与执行证据闭环。"
aliases: ["为什么 Copilot 没调用工具","子 Agent 有哪些权限","CopilotAgentTaskHost","CopilotToolRegistry","CopilotAgentExecutionContract","ReadToolOutput","工具输出归档","SubmitCodeReviewFindings","代码审查findings","CopilotCodeReviewSnapshot"]
code_paths: ["ColorVision/Copilot/Agent/CopilotWebEvidenceSourceLedger.cs","ColorVision/Copilot/Agent/Tools/Files/CopilotWorkspaceValidationTool.cs","ColorVision/Copilot/Agent/CopilotToolIntentPolicy.cs","ColorVision/Copilot/Agent/CopilotToolIntentPolicy.Local.cs","ColorVision/Copilot/Agent/CopilotAgentRequestFactory.cs","ColorVision/Copilot/Agent/Tools/Files/CopilotWorkspacePatchEnvelopeTools.cs","ColorVision/Copilot/Agent/Tools/Files/CopilotReadLocalFileTool.cs","ColorVision/Copilot/Capabilities/CopilotLocalFileToolSupport.cs","ColorVision/Copilot/Capabilities/CopilotLocalFileCapabilities.cs","ColorVision/Copilot/Capabilities/CopilotGrepTextCapability.cs","ColorVision/Copilot/Capabilities/CopilotWorkspaceSearchSupport.cs","ColorVision/Copilot/Capabilities/CopilotSearchFilesCapability.cs","ColorVision/Copilot/Capabilities/CopilotListDirectoryCapability.cs","ColorVision/Copilot/Mcp/CopilotMcpToolDispatcher.Search.cs","ColorVision/Copilot/Agent/Tools/Files/CopilotListDirectoryTool.cs","ColorVision/Copilot/Agent/Tools/Search/CopilotSearchFilesTool.cs","ColorVision/Copilot/Agent/Tools/Search/CopilotGrepTextTool.cs","ColorVision/Copilot/Agent/Tools/Application/CopilotFlowGraphTools.cs","ColorVision/Copilot/Agent/CopilotAgentTaskHost.cs","ColorVision/Copilot/Agent/CopilotProviderRetryChatClient.cs","ColorVision/Copilot/Agent/CopilotProviderConnectionRecoveryChatClient.cs","ColorVision/Copilot/Agent/CopilotAgentTokenBudget.cs","ColorVision/Copilot/Agent/CopilotMicrosoftAgentFrameworkRuntime.AgentStreamingLoop.cs","ColorVision/Copilot/Agent/CopilotMicrosoftAgentFrameworkRuntime.FrameworkSupport.cs","ColorVision/Copilot/Agent/CopilotAnthropicHttpErrorHandler.cs","ColorVision/Copilot/Agent/CopilotContextWindowRecoveryChatClient.cs","ColorVision/Copilot/CopilotChatService.Streaming.cs","ColorVision/Copilot/CopilotChatService.RequestPipeline.cs","ColorVision/Copilot/Agent/CopilotToolRegistry.cs","ColorVision/Copilot/Agent/CopilotAgentExecutionContract.cs","ColorVision/Copilot/Agent/CopilotToolExecution.cs","ColorVision/Copilot/Agent/CopilotAgentAccessModels.cs","ColorVision/Copilot/Agent/CopilotMicrosoftAgentFrameworkRuntime.ApprovalRouting.cs","ColorVision/Copilot/Presentation/CopilotHostedTurnCompletion.cs","ColorVision/Copilot/CopilotConversationUsageDiagnostics.cs","ColorVision/Copilot/CopilotConversationStatistics.cs","ColorVision/Copilot/Agent/CopilotOpenAiAgentChatClientFactory.cs","ColorVision/Copilot/Agent/CopilotOpenAiPromptCacheChatClient.cs","ColorVision/Copilot/Agent/CopilotDeepSeekAnthropicChatClient.cs","ColorVision/Copilot/Capabilities/CopilotWorkspacePatchScope.cs","Scripts/evaluate_copilot.ps1","ColorVision/Copilot/Agent/CopilotMicrosoftAgentFrameworkRuntime.ProviderClientPipeline.cs","ColorVision/Copilot/Agent/CopilotCancellationGuardChatClient.cs","ColorVision/Copilot/Agent/CopilotOpenAiRequestIdChatClient.cs","ColorVision/Copilot/CopilotProviderRequestId.cs","ColorVision/Copilot/Context/CopilotConversationCompactionPlanner.cs","ColorVision/Copilot/Context/CopilotConversationCompactionPrompt.cs","ColorVision/Copilot/CopilotChatViewModel.WorkspaceCommands.cs","ColorVision/Copilot/Mcp/CopilotMcpAuditLogger.cs","ColorVision/Copilot/Capabilities/CopilotTemporaryRedactedOutputArchive.cs","ColorVision/Copilot/Capabilities/CopilotTemporaryRedactedOutputArchive.Redaction.cs","ColorVision/Copilot/CopilotCodeReviewFindings.cs","ColorVision/Copilot/Runtime/CopilotTurnCodeReviewSnapshotCapture.cs","ColorVision/Copilot/Agent/CopilotToolExecution.ReviewInstructions.cs","ColorVision/Copilot/CopilotWorkspaceReviewRequest.cs","ColorVision/Copilot/Agent/CopilotToolModels.cs","ColorVision/Copilot/Runtime/CopilotTurnRuntime.cs","ColorVision/Copilot/Capabilities/CopilotGitWorkingTreeInspectionCapability.cs", "ColorVision/Copilot/Capabilities/CopilotBackgroundShellCommandRegistry.Observation.cs"]
test_paths: ["Test/ColorVision.Copilot.Tests/CopilotConversationFullAccessTests.cs","Test/ColorVision.Copilot.Tests/CopilotWebEvidenceOutputFormatTests.cs","Test/ColorVision.Copilot.Tests/CopilotWorkspaceValidationAdmissionTests.cs","Test/ColorVision.Copilot.Tests/CopilotPatchToolAvailabilityTests.cs","Test/ColorVision.Copilot.Tests/CopilotAgentTaskHostQueueDispatchTests.cs","Test/ColorVision.Copilot.Tests/CopilotAnthropicProviderFailureTests.cs","Test/ColorVision.Copilot.Tests/CopilotProviderConnectionRecoveryTests.cs","Test/ColorVision.Copilot.Tests/CopilotAnthropicHttpFailureTests.cs","Test/ColorVision.Copilot.Tests/CopilotAnthropicHttpErrorBoundaryTests.cs","Test/ColorVision.Copilot.Tests/CopilotHostedTurnCompletionTests.cs","Test/ColorVision.Copilot.Tests/CopilotHostedTurnUsageTests.cs","Test/ColorVision.Copilot.Tests/CopilotProviderPayloadErrorTests.cs","Test/ColorVision.Copilot.Tests/CopilotAgentExecutionContractRetryTests.cs","Test/ColorVision.Copilot.Tests/CopilotCodexApprovalsReviewerTests.cs","Test/ColorVision.Copilot.Tests/CopilotOpenAiProviderRetryTests.cs","Test/ColorVision.Copilot.Tests/CopilotOpenAiPromptCacheDiagnosticsTests.cs","Test/ColorVision.Copilot.Tests/CopilotDeepSeekAnthropicChatClientTests.cs","Test/ColorVision.Copilot.Tests/CopilotUnicodeFileReadTests.cs","Test/ColorVision.Copilot.Tests/CopilotTextSearchCoverageTests.cs","Test/ColorVision.Copilot.Tests/CopilotTextFileCoordinateTests.cs","Test/ColorVision.Copilot.Tests/Evaluation","Test/ColorVision.Copilot.Tests/CopilotProviderStreamCleanupTests.cs","Test/ColorVision.Copilot.Tests/CopilotProviderStreamCleanupTimeoutTests.cs","Test/ColorVision.Copilot.Tests/CopilotOpenAiRequestIdTests.cs","Test/ColorVision.Copilot.Tests/CopilotContextWindowRecoveryCancellationTests.cs","Test/ColorVision.Copilot.Tests/CopilotCompactionTerminalEvidenceRequestTests.cs","Test/ColorVision.Copilot.Tests/CopilotConversationCompactionIntegrityTests.cs","Test/ColorVision.Copilot.Tests/CopilotLocalReadFollowUpAdmissionTests.cs","Test/ColorVision.Copilot.Tests/CopilotToolOutputArchiveTests.cs","Test/ColorVision.Copilot.Tests/CopilotSecretRedactionTests.cs","Test/ColorVision.Copilot.Tests/CopilotCodeReviewEvidenceBoundaryTests.cs","Test/ColorVision.Copilot.Tests/CopilotReviewTargetRefreshTests.cs","Test/ColorVision.Copilot.Tests/CopilotWorkspaceReviewExecutionContractTests.cs","Test/ColorVision.Copilot.Tests/CopilotReviewRepositoryBindingTests.cs", "Test/ColorVision.Copilot.Tests/CopilotFileDiscoveryCoverageTests.cs", "Test/ColorVision.Copilot.Tests/CopilotBackgroundShellCompletionObservationTests.cs"]
related: ["copilot.runtime","copilot.tool-contracts","copilot.lifecycle","copilot.interactions","copilot.session-tools"]
---

# Copilot Agent 执行链

本页维护运行链、工具可见性、执行证据、上下文和回答终态。审批/共享能力见[工具契约](./copilot-agent-tool-contracts.md)，任务调度与持久化见[任务与恢复](./copilot-agent-session-and-tools.md)，桌面操作见[输入与活动呈现](./copilot-local-interactions.md)。

```text
TaskHost 冻结任务 → 合并业务/外部 MCP 能力 → CanHandle 筛选
→ Framework AgentSession（todo、模式、压缩、用量预算）
→ 精确输入与审批 → 执行前写保护/Hook → 资源闸门 → 工具体
→ 结构化结果、审计与后置 Hook → observation → 执行/完成契约 → checkpoint
```

Framework 保持 Provider CallId 与结果配对；ColorVision 负责权限、预算和证据边界。写入独占，只有独立的只读幂等调用可并发。前置守卫不可被项目 Hook 替换；后置 `decision:block` 只改变模型反馈，`continue:false` 在已完成结果落账后结束本轮，不能撤销真实副作用。写入/非幂等调用执行后失去可信终态时记为 `OutcomeUnknown`，先核查状态，禁止自动重放。

## 工具可见性与任务意图

工具可见表示本轮可用，不能推导为用户要求执行。工作区读取与补丁工具按冻结路径、模式、sandbox 和禁止修改约束开放；附加只读根不成为可写根或可信项目指令根。项目指令随工作区能力加载，不把普通应用任务强制改判为源码检查。

Flow patch 要求流程相关上下文，但不能因修改同义词或附带解释要求而隐藏。Plan、Review、Diagnose、Chat、只读 sandbox、空请求和明确禁止修改仍阻止 patch；执行瞬间还须复核权限、文件指纹或图 revision。`NeedsWorkspaceEdit/Create/Rollback` 用于完成证据，不能因为工具可见而强制修改。

`RunWorkspaceValidation` 在允许修改且有可写根时稳定提供；仅精确文件写权限不足以授权构建，Review 仅允许用户明确要求的有界验证。可见不增加必须验证的完成条件，批准也不授权根外项目。定位 `CopilotPatchToolAvailabilityTests`、`CopilotWorkspaceValidationAdmissionTests`。

## 运行时选择与临时授权

`CopilotAgentRuntimeRouter` 将有效 OpenAI-compatible/Anthropic-compatible Profile 送入 Framework，不在失败后换执行器或重放已产生进展的请求。官方 `api.openai.com` 的 Chat、Agent、图片和辅助请求统一走 Responses；第三方默认 Chat Completions，显式完整 `/responses` 端点才走 Responses；Anthropic 走 Messages。显式网关前缀保留，路由不改保存的 Profile。

Responses 使用 `store=false`，从本地重放完整历史与配对工具结果，不依赖 `previous_response_id`。官方账户、Codex 专用覆盖和缓存诊断不发给第三方；兼容服务使用 Profile 声明的推理档位。明文 reasoning 与 summary 保留来源类型，旧摘要不靠端点或文本猜成明文，编辑/压缩后不复活旧内容，加密 reasoning 仍沿 SDK 路径。协议细节定位 `CopilotStatelessResponsesHistoryChatClient` 与 `CopilotOpenAiAgentChatClientFactoryTests`。

访问模式须区分：

- 「完全访问」是 `UnrestrictedFullAccess`，本机持久保存，跨会话/工作区/重启生效，直到用户切回；受保护工具经模块策略后直接批准，不调用 reviewer，仍复核任务、参数、能力、范围和禁止规则，不追溯批准已有 Pending。
- 「临时自动复核」保留兼容枚举 `FullAccess`，只限当前/下一任务、当前工作区且有期限；任务结束、失败、取消、超时、工作区变化或重启撤销，分支不继承。结构化补丁/回滚可按范围规则直接批准，其他合格能力走独立 reviewer。
- 显式 `approvals_reviewer` 是独立配置，不与临时 grant 生命周期混用。具体决定路径见[原生审批](./copilot-agent-tool-contracts.md#原生审批与参数快照)。

任何访问模式都不扩大 Schema、可写范围、意图、预算或只读边界。访问上下文可以影响活动 Framework session，但任务结束须解除精确任务绑定。

Responses 的 `incomplete`、`failed` 不能因 SDK 缺少 finish 标记而被当成完成：长度/过滤保留分类，未知原因仍不完整，缓冲中的工具参数不派发。已报告用量始终保留；失败后不能重发已有正文、推理或函数参数进展的尝试。

### 流清理与原始失败

停止等待不证明生产者或工具已停止。取消宽限后只有 `ToolStarted` 而无权威终态的工具记为 `tool_outcome_unknown`；尚未开始、待审批和已有明确结果分别处理，不从整轮 `Cancelled` 推导工具已取消。

`CopilotTurnEventStream` 将调用方取消/消费者释放转为非阻塞取消请求；阻塞或抛错的取消回调不能阻止缓冲清理和有界关闭。`CopilotCancellationGuardChatClient` 保留先发生的读取/供应商异常，清理异常不改错误分类、请求 ID 或重试资格；没有原失败时清理异常仍上抛。宿主超时与清理自己抛出的超时须区分，迟到任务继续观察；未结束读取完成前不并发 Dispose。该边界不保证抢占第三方 SDK 的任意同步阻塞。定位 `CopilotProviderStreamCleanupTests`、`CopilotProviderStreamCleanupTimeoutTests`。

### OpenAI HTTP 重试预算

正式客户端关闭 SDK 内部重试，只有 ColorVision 供应商层有限重试，防止次数相乘。一次预算尝试对应一次 HTTP 请求；同次正式 usage 快照去重合并，不同尝试累加。

- 只在首个实质进展前重试瞬态失败；正文、推理及非空函数参数分片均算进展，残缺参数不是可执行调用。纯前导元数据有界，空分片不刷新进展。
- 工具完成后发起的下一次模型请求可在尚无新输出时重试，但不能重新执行旧工具。正常空响应仅在成功/缺省 finish reason 下使用同一有限预算，长度/过滤等明确停止不走该分支。
- 限流与账户限制按结构化 code/type 区分，不从正文猜。额度或支出限制即使 HTTP 429 也不重试；未知 HTTP 错误按状态兼容，未知流内错误不猜为瞬态。
- `Retry-After` 是等待下限，不能截短。超过两分钟自动等待上限时保留原失败并结束重试；等待可取消且受总预算约束。
- 请求 ID 只从对应失败响应的已知头读取并脱敏，不从正文、response ID 或全局最近记录补造；头缺失不掩盖原错误。

已知用量在丢弃尝试、连接恢复及取消后仍计入预算；离线等待不估造消耗。实现从 `CopilotOpenAiAgentChatClientFactory`、`CopilotOpenAiRequestIdChatClient` 定位，回归见 `CopilotOpenAiProviderRetryTests`、`CopilotProviderConnectionRecoveryTests`、`CopilotProviderPayloadErrorTests`。

### 提示缓存比较

官方 Responses 的受支持新模型只在同一 Agent 运行内用 `prompt_cache_options.comparison_response_id` 比较相邻已完成响应；首轮不带 ID，错误、取消和 incomplete 不替换基准。比较状态不写 checkpoint、不跨任务/Profile；Chat、第三方和旧模型不附加。

`cache_hit` 不证明全部输入命中，缺失/过期比较不算 hit 或 miss。比较估算只供诊断，不进账单；实际缓存用量来自 usage。可选诊断失败不能改变完成结果或触发重放，也不改变 `store=false`、历史重放或权限。模型准入和参数定位 `CopilotOpenAiPromptCacheChatClient`，受控 SDK 回归不证明真实缓存收益。协议背景见 [OpenAI 提示缓存诊断](https://developers.openai.com/api/docs/guides/prompt-caching/diagnostics)。

### DeepSeek Anthropic 工具续轮

`CopilotDeepSeekAnthropicChatClient` 只对 `api.deepseek.com/anthropic` 过滤没有正文的思考签名，避免 SDK 将其转为该接口不支持的 `redacted_thinking`，使读工具后的续轮 422。它克隆受影响的消息，保留可见思考和配对工具结果，不改保存历史；其他 Anthropic 端点不应用，因为真实加密思考需原样回传。

该 Anthropic 接口的 `output_config` 不提供原生 JSON 格式约束。当前“仅 JSON”是提示词约束，不等于 Schema 强制校验；切换协议或添加同名参数不能假定启用 Chat Completions JSON 模式，评测不得剥围栏后算通过。兼容背景见 [DeepSeek Anthropic](https://api-docs.deepseek.com/guides/anthropic_api/) 与 [JSON 输出](https://api-docs.deepseek.com/guides/json_mode/)。

### Anthropic HTTP 失败边界

Anthropic 同样关闭 SDK 重试，只由宿主按预算和首输出边界有限重试。明确 SSE 的过载、限流、API/超时错误可在无进展时重试；认证、请求和未知错误不可。SSE 错误不是 HTTP 状态，不能伪造 429。工具/正文后失败保存既有事实与 checkpoint，不重放。

`CopilotAnthropicHttpErrorHandler` 有界读取失败正文，长度/I/O 错误仍保留原 HTTP 状态；请求 ID 与退避绑定同次响应，`Retry-After-Ms` 优先且遵守等待下限。401 的正文即使写“上下文超限”也不能触发压缩重发。诊断只交付已知错误分类和脱敏 ID，不回显任意供应商正文。

HTTP/流失败在已有进展后分别保留 `provider_request_rejected`、`provider_unavailable` 或 `provider_interrupted`，不以通用错误覆盖部分回答。恢复仍需用户继续，永久错误先处理账户/配置。

上下文拒绝仅在首个实质输出前压缩重发一次，纯元数据/用量不关闭机会；第二次拒绝停止，决策前取消则保留取消语义。被丢弃尝试的已报告用量仍结算，无正式用量的输出前上下文拒绝不估算费用。已经取得正式终态账单后再观察取消，只结算账单，不向下游交付正文、工具或完成效果，也不重发。定位 `CopilotContextWindowRecoveryChatClient`、`CopilotContextWindowRecoveryCancellationTests` 与 `CopilotAnthropicHttpErrorBoundaryTests`。

## 多会话活动投影

`DesktopPetCopilotBridge` 观察宿主与确认存储，不是第二调度器；优先级、已读消除、导航与确认卡见[活动呈现](./copilot-local-interactions.md#消息显示与桌宠活动)。

## 网页读取的执行证据

直接 URL 的 Auto 请求先 `FetchUrl`，失败或不足才用 `WebSearch` 回退。Framework 执行契约依据真实成功 observation 判断，模型无工具证据的草稿会撤回，在同一 session 要求补取；仍有可尝试回退时继续，路径耗尽或拒绝调用则 `Blocked`。模型文字、搜索线索和正文伪造的“已抓取”标记都不证明实际读取。

## 搜索深读与工具输出归档

`WebSearch` 按显式 URL/`site:` 主机优先选结果，否则深读第一项安全结果；同源派生资源受总量上限约束。深读失败保留搜索线索，部分读取保留正文并报告 `PartialResultMessage`，全部失败才整次失败。搜索命中 URL 不证明该页已深读。

模型结果受字符/Token 双预算限制。成功纯文本被截断时，完整脱敏输出可进入有界会话临时归档，由 `ReadToolOutput` 按不透明 ID 读取；不暴露存储路径，不跨会话，淘汰/删除/退出清理，读取结果不再递归归档。

归档读取以及前台/后台 Shell 读取均交付连续正文前缀，并按实际交付重算游标、数量和末尾标记。坐标是脱敏存储正文的 UTF-16 单元，保留首尾空白及代理对；预算放不下页头和字符时不伪造已推进。已完成末尾的孤立高代理字符以替代字符交付并推进，避免死循环。越界为 `Validation`，恰到末尾为空页成功；后台页末仅表示当前输出读完，不代表进程结束，真实进程状态不因模型预算很小而丢失。

普通工具及 Shell 输出在存储前脱敏，跨 chunk 保留敏感赋值的引号/转义、Authorization 和独立 key 的识别状态，不能先截断失败摘要再让残留凭据泄露。待确认前缀不提前公开，已发布正文和游标随追加保持稳定；来源字符计数与实际脱敏坐标分开。规则入口为 `CopilotSensitiveTextRedactor` 和工具输出归档实现，回归见 `CopilotSecretRedactionTests`、`CopilotToolOutputArchiveTests`、`CopilotForegroundProcessEvidenceTests`。

来源优先使用冻结的 `WebEvidenceSourceUrls`：内置抓取仅记录成功返回页，搜索保留深读及 Hits；正文出现链接不增加来源。显式空列表禁止回退扫描，旧工具/外部工具未提供字段时保留文本识别兼容。URL 匹配保留路径和 query 大小写，只有 scheme/host 忽略大小写。

完整回答使用网页工具却未引用其真实来源时可补有限来源；失败、暂停、超时、用户禁网及已有引用不补。完整可解析 JSON 对象/数组保持原正文，不拼 Markdown 或加字段；仅括号外形不算 JSON。来源补写不改变搜索线索与深读证据的区别。定位 `CopilotWebEvidenceOutputFormatTests`，这些受控工具回归不访问真实网络。

## 稳定只读工具与动态暴露

Auto 本地读取工具按证据需求、工作区能力或有界续问记录开放，不因仅有搜索根就保证每句短问都暴露。模型可直接回答概念问题；可见工具不增加必须搜索的完成条件。外部 MCP 的文件/Web/URL 能力仍遵守对应意图门槛，业务设备工具按自身可用性判断。

搜索发现的文件可在同一运行内继续读，但须落在规范授权根，拒绝重解析越界，多根同名须唯一匹配。失败路径诊断只检查已授权根内的元数据，不读目标正文或扩大授权；根消失/失权不能改搜父目录。实现定位 `CopilotLocalPathAuthorizationTests`、`CopilotMcpPathSecurityTests`。

读取/搜索的证据边界：

- 文本严格支持 UTF-8 及有 BOM 的 UTF-8/16/32，损坏编码/NUL 明确失败，无 BOM 的其他编码不猜。LF、CRLF、单独 CR 的行号一致；续读使用返回的一基 UTF-16 行列，非法列不自动修正，截断不拆代理对/CRLF。
- `.csv`、`.tsv`、`.jsonl`、`.ndjson` 属于独立只读格式集合，发现或读到它们不扩大工作区写白名单。
- `GrepText`、`SearchFiles` 保留有效匹配，同时报告扫描上限、枚举/读取失败等覆盖缺口；缺口下的零命中不是“完整搜索无结果”。损坏文件的本页匹配不作为证据；续页保留既有缺口，失效/旧游标重查，不完整文件发现不提供稳定续页。按策略排除依赖目录不算异常缺口。
- `ListDirectory` 目标不可枚举时失败，不伪装空目录。只有实际 EOF 才报告 `end_of_file_observed`/`observed_total_lines`，定向读取不为凑总数补扫文件；该边界只描述读取时刻。

已有完整搜索和命中范围足以支持结论时即可回答，不把定向问题扩大成全文件检查；确需续读再用游标。编码、坐标、限额和覆盖实现见 `CopilotUnicodeFileReadTests`、`CopilotTextFileCoordinateTests`、`CopilotTextSearchCoverageTests`、`CopilotFileDiscoveryCoverageTests`。

## 只读子 Agent 与隔离

`CopilotSubagentRoleCatalog` 提供独立只读 Harness 角色：Explore 用于多文件调查，只有有界搜索根、活动文档、范围内项目指令及本地搜索/读取工具；Scout 用于多来源公开研究，只有 WebSearch/FetchUrl，不接收本地路径/文档/指令。简单单页直接读，不必委派。

子运行不继承父历史、附件、checkpoint、todo、mode、Skills、可写根、MCP、访问模式或审批，不自动注入 Harness 管理函数。两者都不能 Shell、数据库、写入、审批或再次委派；任务必须自包含，网页是不可信证据。框架并发仍受有界可取消槽位、同资源互斥与 no-progress 限制。具体次数、时限与返回预算直接查角色目录。

## 委派预算和审计

父请求的所有子运行共享委派预算池，子预算受父剩余额度及并发公平分配约束，不足时不发新模型请求。成功按真实用量结算，异常/取消保守消耗预留预算；预算估算不等于账单。角色、子运行 ID、父 CallId、停止原因与汇总指标留在 trace，不把逐工具噪声复制到主聊天。子调用用量归入父运行，主 Agent 仍负责综合与最终回答，不能因委派完成便声明任务完成。

## 短追问与能力续租

稳定能力不依赖上轮关键词；动态能力只能续租最近有效记录中已完成（含失败）、只读幂等且无需审批的调用。本地续问还要求当前范围与历史/checkpoint 条件成立，旧轮次或新话题不扩大文件范围。写工具和 Shell 不通过续租获授权，恢复按当前能力重规划，不把旧参数当许可。准入定位 `CopilotLocalReadFollowUpAdmissionTests`。

## 何时强制联网证据

直接 URL、Web 模式和明确要求联网搜索才强制成功 Web 证据。普通概念问题不因存在 WebSearch 增加执行轮次；没有匹配工具时不制造空循环。工具暴露、用户意图和完成证据是三种判断，用户禁网仍优先。

## Review 的证据与结构化 findings

Review 冻结目标，缺省含暂存/未暂存完整工作树。有工作区时冻结其实际所属仓库，附加根只有读取权，不能拿另一仓库的状态/diff 替代；无工作区的旧请求保留兼容语义。

`InspectGitDiff` 的结构化身份与实际交付补丁共同构成证据。`SubmitCodeReviewFindings` 必须绑定最新有效目标，即使无问题也提交空 findings，文字“没有问题”不代替协议。整体序列化载荷受同一读写上限约束，超限明确 `Validation`，不能截断后宣称成功。

finding 的路径、old/new 侧和行范围须有可见 hunk 正文支持，不能仅凭 `@@` 范围；截断边界行/尾部不算证据，Git quoted path 须正确解码。同轮 `InspectGitWorkingTree` 可在 diff/findings 前后补齐，但在显式文件前置读取之后；修正状态归属不要求重提有效 findings。

后来成功读取其他目标/scope/revision 的 diff 会使旧完成状态失效，须重读请求目标并重交 findings；失败或待审批检查不替换成功证据。该恢复局部解除旧 Inspect/Submit 去重，无新证据的循环仍拒绝。定位 `CopilotCodeReviewEvidenceBoundaryTests`。

## 工作区补丁的预览、应用与回滚

工作区修改必须有正式应用成功证据，输出代码或声称已改不满足执行契约。现有文件用 `PreviewWorkspacePatch → ApplyWorkspacePatch`，精确唯一替换并绑定原 SHA-256；新文件用 `PreviewCreateWorkspaceFile → ApplyCreateWorkspaceFile`，只在可写根创建白名单文本，绝不覆盖。创建目录须能确认归属，不能在竞争中误认其他进程的目录为自建。

应用须用本进程仍有效的 preview ID，重新复核当前范围/冲突并经原生审批。`RollbackWorkspacePatch` 再审批且应用后指纹仍匹配，才恢复原字节或删除 Agent 新建文件；仅清理仍为空且确为 Agent 原子创建的目录。预览不跨重启授权。

精确文件写权不等于目录写权：相对路径可从有效 `WorkspacePath` 定位，但仍须命中 `WritableLocalFilePaths`；不授权兄弟文件或创建新文件。无工作区用已授权绝对路径，多根歧义和重解析检查不放宽。

成功且实际改变状态的 `WorkspaceMutation` 允许受影响本地证据重新读取/搜索，不重置工具预算，也不解除写调用去重；失败读取后的新建例外及迟到读取边界见[输入与无效调用](./copilot-agent-tool-contracts.md#输入校验与无效调用)。

多文件应用/回滚途中失败仍是失败：保留原失败与逐文件补偿结果，`WorkspaceRecheckPaths` 表示可能已写入/恢复的路径，**不是已完成差异或恢复成功的证据**。模型须重读核查；有界读取或单个后续补丁不能证明失败事务已完整核验。任务消息独立保存“文件状态待核查”，旧消息缺字段保持未标记；先前差异须明确属于先前确认结果，不能代表当前完整状态。后续确认补丁可接续待核查路径，但保留原基线，不放宽其他路径连续性。

已批准写入/回滚在取消、超时后无可信终态时为 `OutcomeUnknown`，保留冻结参数对应、且匹配当前会话/工作区的可能受影响范围；原执行锁持续到后台工具结束，晚到成功不覆盖已发布终态。未批准/尚未准入不登记该执行范围，取消后的迟到元数据读取不得继续进入写入体。

`ConvertBatchImages` 的未知终态同样保留经授权验证的可能输出目录，目录覆盖竞争重编号的输出，不是完整文件清单；须列目录并核实实际文件，旧文件/子目录观察也需刷新。待核查范围只服务证据失效，不增加读取权限，原路径集合不直接写入会话历史。

回归入口为 `CopilotWorkspaceRecoveryEvidenceTests`、`CopilotWorkspaceCancellationEvidenceTests`；临时文件/受控等待测试不证明进程崩溃、桌面操作或所有外部副作用均已恢复。

## 可见历史窗口和上下文预算

Chat 与 Framework 可见历史只接受规范 user/assistant 角色，历史 system/tool/未知角色不得升级为运行指令。确定性窗口按当前上下文/输出预算给其他组件留余量，始终保留最初用户目标和最近轮次，优先删完整旧轮次；异常无用户历史仅有界裁剪，不构造空目标。

附件占独立槽并计入同一预算，不重放旧 `RequestContent` 的附件。裁剪 assistant 时先保留来自真实消息状态的完整终态说明，再裁正文；标签只在正文中出现不生成状态/授权，连终态说明都放不下时拒绝投影。本地完整历史和 UI 不改写，窗口本身不逐轮调用模型做摘要。具体限额由 `/context` 和 `CopilotConversationHistoryWindow` 核对。

`CopilotAgentContextBuilder` 的 provenance 仅记录上下文来源、形式、信任和规模，不留正文/路径/凭据，不持久、不参与恢复，也不成为第二份 prompt owner。Framework Token 压缩继续负责运行时上下文。回归见 `CopilotConversationCompactionIntegrityTests`。

## 压缩摘要不能升级完成证据

摘要之外的宿主终态说明从本地原消息重算，旧摘要遗漏中断也不能把工作升级为完成。再次压缩时，缺失的 `<assistant_response_interrupted>` 与 `<agent_turn_incomplete stop_reason="...">` opening marker 作为独立宿主约束加入请求，不能要求模型猜未收到的状态。

`CopilotConversationCompactionPlanner` 只替换完整旧轮次，保留最近原文。新摘要必须保留所需终态 marker、来源修订匹配，且连同宿主前导/终态说明的整体估算权重严格缩小，并放得进下一次历史预算；用户聚焦要求不覆盖这些限制。输入预算不足不开始压缩，结果不合格保留原历史并保留已报告压缩用量。实现从 `CopilotConversationCompactionTerminalEvidence` 定位；回归为 `CopilotCompactionTerminalEvidenceRequestTests` 和 `CopilotRequestAdmissionLifetimeTests`。

## 回答终态与模型可见的中断证据

Chat 对 SSE 的非空 JSON data（除 `[DONE]`）及非流式 JSON 严格验语法；坏载荷立即为不可自动重试的 `invalid_response_format`，保留已有正文/用量，不能跳过后再宣称完整。合法未知事件、心跳和多行 data 兼容。明确 Responses incomplete 不能因缺 reason、冲突成功字段或 SDK 缺标记升级成完整，旧接口缺省终态仍沿兼容规则。

`CopilotChatMessage.ModelContent` 从可信消息状态生成两种边界：真实回答截断/取消等用 `<assistant_response_interrupted>`；Framework 正常返回但 stop reason 非 Completed 用 `<agent_turn_incomplete stop_reason="...">`。显示错误详情不随 marker 重新注入，引用文本不反向生成状态；新尝试清除旧标记。完整任务终态和回答完整性独立，`Completed` 也可能带截断回答。

主循环的 Length 可做一次禁用业务工具的有界补回答，ContentFilter 不自动重试；ToolCalls/未知明确终态不能直接算完成。只有完整补回答才替换旧部分正文；仍不完整则保存 Provider blocker，不再窄证据改写或补来源。持续目标遇 Completed 加回答中断时暂停并跳过独立完成评估，原 stop reason 继续审计。

可信的“任务已结束但回答中断”可进入 `Finalize` final-answer-only 恢复，全部工具禁用，不重新执行任务。完整且有可显示正文、没有明确非成功终态才退休 checkpoint；空白/不完整结果保留部分正文、写入 `IncompleteOutput`，任务索引显示“等待最终回答”。供应商 HTTP、过载、传输和上下文失败保留原分类及已报告的所有重试用量，不改成“没有正文”；失败恢复不解除旧 session 的未知写入/未配对调用限制，只有完整回答成功才退休旧 checkpoint。定位 `CopilotFinalAnswerRecoverySafetyTests`。

输出规则在主循环、委派和最终回答一致：保留用户要求的字段名、大小写、类型、单位；依据明确时允许翻译/映射/计算，不需在源文件中寻找同名输出键，也不等于修改源文件。未知值不强转 false，仅 JSON 不加围栏。它仍是模型指令而非 Provider Schema 强制验证，可解析 JSON 不证明任务完成。

## 业务评测

合成场景定义在 `Test/ColorVision.Copilot.Tests/Evaluation/CopilotBusinessScenarios.cs`。`Scripts/evaluate_copilot.ps1` 筛选 `CopilotBusinessEvaluationTests.EvaluateConfiguredProfile`，但当前源树没有该测试入口，不能据此直接取得真实模型评测结果；脚本未产出 `report.json` 时也会明确失败。恢复可运行入口后须重新核验实际协议、权限装配和评分链，不能只因场景定义存在便宣称已覆盖。

真实模型评测会使用显式 Profile 的付费 API，应只发送隔离合成数据，覆盖配置不落盘、不将密钥发给其他服务。有效验收须同时有实际工具/读取范围、输入与最终文件哈希、严格格式及事实校验、终态和用量，保存后核对必须晚于最后应用，历史读取不能充当本轮核验。重复或连续轮次分别记结果，后续通过不能覆盖失败；正式用量与预算估算分开，缺报告不等于零费用。

steering/恢复场景还须区分输入接受、实际重读、checkpoint 记忆和 Session 恢复/重规划，不能靠模型声称完成或手工补历史通过。合成评测不替代 WPF、生产数据或硬件验收，不能放宽权限、事实或格式检查掩盖装配缺口。

## 用量观察与分支去重

Provider 正式 usage 与回答终态独立：失败、暂停、取消或回答截断不抹去已知输入/输出/缓存消耗。无报告则保持未知，不继承上一轮 `LastUsage`，也不从 Agent 预算估算制造账单。旧 LastUsage 仅回填最新未中断 assistant，不能借新中断消息记到更早一轮。

自动标题、压缩、持续目标首判/复核的已报告消耗分别结算且去重；失败、取消或结果被丢弃仍保留，未返回完整评估不计作完成评估。删除/释放后的迟到归属见[会话出口](./copilot-local-interactions.md#会话导航、回顾与出口)。

会话统计单列标题/压缩，按消息的时间汇总不必与其相等；Profile 响应头限额快照可能过期，不是套餐余额，显示不调用账户 API。分支用 `BranchOrigin` 的父会话/消息边界去重，父会话缺失时按时间回退。不能补出未记录费用，也不读取账户价格/额度。回归入口为 `CopilotHostedTurnUsageTests`、`CopilotFinalAnswerCancellationTests`。

---
knowledge_id: "copilot.lifecycle"
knowledge_type: "topic"
status: "current"
summary: "Copilot 任务生命周期、恢复预算、项目指令发现和 Skill 渐进加载的契约。"
aliases: ["Copilot 如何加载 AGENTS.md","为什么不读取 config.toml","Skill 什么时候加载","CopilotAgentProjectInstructions","CopilotAgentSkillCatalog","/init","初始化项目指令","CopilotProjectInitialization"]
code_paths: ["ColorVision/Copilot/Agent/CopilotAgentProjectInstructions.cs","ColorVision/Copilot/Agent/CopilotAgentProjectInstructions.Rules.cs","ColorVision/Copilot/Agent/CopilotAgentSkillCatalog.cs","ColorVision/Copilot/Agent/CopilotAgentSessionCheckpoint.cs","ColorVision/Copilot/Agent/CopilotAgentTokenBudget.cs","ColorVision/Copilot/Agent/CopilotAgentTokenBudget.Estimation.cs","ColorVision/Copilot/Agent/CopilotMicrosoftAgentFrameworkRuntime.FinalAnswerRecovery.cs","ColorVision/Copilot/Agent/CopilotMicrosoftAgentFrameworkRuntime.Recovery.cs","ColorVision/Copilot/Agent/CopilotMicrosoftAgentFrameworkRuntime.Loop.cs","ColorVision/Copilot/Agent/CopilotMicrosoftAgentFrameworkRuntime.AgentStreamingLoop.cs","ColorVision/Copilot/CopilotChatMessage.cs","ColorVision/Copilot/CopilotProjectInitialization.cs"]
test_paths: ["Test/ColorVision.Copilot.Tests/CopilotAgentProjectInstructionsTests.cs","Test/ColorVision.Copilot.Tests/CopilotAgentSkillCatalogTests.cs","Test/ColorVision.Copilot.Tests/CopilotAgentSessionCheckpointTests.cs","Test/ColorVision.Copilot.Tests/CopilotTokenUsageMergeTests.cs","Test/ColorVision.Copilot.Tests/CopilotNonStreamingUsageTests.cs","Test/ColorVision.Copilot.Tests/CopilotFinalAnswerCancellationTests.cs","Test/ColorVision.Copilot.Tests/CopilotFinalAnswerRecoverySafetyTests.cs"]
related: ["copilot.runtime","copilot.execution","copilot.extensions"]
---

# Copilot 生命周期、预算与 Skills

每次工具调用都有稳定的 `CallId`、轮次、尝试次数/上限、运行时、工具名、读写级别、风险、审批模式、幂等性、并发模式、资源指纹、排队耗时、失败分类、是否允许重试、参数摘要、开始/结束时间、总耗时、超时、结果摘要和最终状态。状态包括 `Running`、`AwaitingApproval`、`Completed`、`Failed`、`TimedOut`、`Denied`、`Cancelled` 和 `Interrupted`。

## 多工具并发

Harness 创建后会显式启用 `FunctionInvokingChatClient.AllowConcurrentInvocation`，因此模型在同一响应内发出的多个函数调用可以由 Agent Framework 并行调度。业务工具不会直接无约束并发，而是统一进入 `CopilotToolExecutionGate`：

- 独立的 `SharedRead` 最多同时运行 4 个。
- 相同资源指纹的读取互斥，避免重复访问同一状态或缓存。
- `Exclusive` 调用会阻止新的读取，等待现有读取结束后全局独占；任何写工具都会被强制提升为 `Exclusive`，即使工具声明错误。
- 等待闸门时取消会产生 `Cancelled` trace 和审计记录，但不会调用工具。
- Agent 事件出口串行化，保证并行工具不会并发修改聊天状态。

框架的并行开关及其默认串行语义见 [FunctionInvokingChatClient.AllowConcurrentInvocation](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.ai.functioninvokingchatclient.allowconcurrentinvocation)。

## 上下文压缩与请求预算

Harness 不再关闭压缩。ColorVision 使用独立于模型 Profile 的 Agent 上下文窗口，默认 1,048,576 Token；当最大输出为默认 8,192 Token 时，单次输入预算为 1,040,384 Token。`ContextWindowCompactionStrategy` 在每次模型调用前执行框架原生两阶段策略：

1. 达到输入预算的 50% 后，先把旧工具调用组折叠成简短结果，保留工具名称和结论。
2. 达到输入预算的 80% 后，再删除最旧的非系统消息组，同时保留最近对话。

1M 是 ColorVision 新 Agent 配置的统一默认值，不从模型 Profile 推导；用户可以在独立 Agent 设置页收紧它，单次请求也可以覆盖。框架会从 `MaxContextWindowTokens - MaxOutputTokens` 计算输入预算；具体策略见 [ContextWindowCompactionStrategy](https://learn.microsoft.com/en-us/dotnet/api/microsoft.agents.ai.compaction.contextwindowcompactionstrategy?view=agent-framework-dotnet-latest)。

`CopilotAgentRunBudget` 统一管理一次 Agent 运行的上下文窗口、累计请求 Token、业务工具调用、Agent pass 和总时长。这些参数保存在独立的 `CopilotConfig.AgentDefaults`，不再属于任何模型 Profile；Profile 只保留厂商、协议、端点、模型和生成参数。有效值按“单次请求覆盖 > 全局 Agent 默认值 > 框架安全默认值”解析。新默认值是 1,048,576 Token 上下文窗口、1,048,576 累计请求 Token、128 次业务工具调用、32 个 pass 和 7,200 秒；可配置硬上限分别是 1,048,576、1,048,576、512、128 和 86,400 秒，避免误循环变成真正无界运行。业务工具硬上限由 Bridge 独立执行，Harness 的 todo、mode、approval 等框架函数和最后一次自然语言总结使用单独的有界迭代余量，因此用完最后一次业务工具后仍可返回结论；只有继续越界调用工具时才记录 `ToolBudgetExhausted`。设置窗口把这些值放在独立 `Agent` 页，集成调用方仍可通过 `CopilotAgentRunBudgetOverride` 只收紧或覆盖当前请求。

Harness 正常结束但没有产生任何 `TextContent` 时，Runtime 不再把空 Todo 账本直接判为完成。它会通过同一 Token 与传输重试中间件发起一次非流式最终总结，`ChatOptions.Tools` 固定为空，并把有界工具观察与当前任务账本作为数据交给模型；因此这个阶段不能重放业务或 Framework 工具。总结仍为空或失败时会发出固定用户提示、记录 `IncompleteOutput` 与 `provider_empty_output` blocker，保存 checkpoint，并且绝不标记 `Completed`。

`CopilotAgentRecoveryMode.Finalize` 为这种状态以及“Provider 在任务已完成后断流”的状态提供独立恢复协议。Runtime 在验证 profile、checkpoint 和最后一次 `RunStopped` 后直接进入 no-tools Provider 调用，不发现外部 MCP、不创建 Harness、不恢复 Todo、不打开审批；不匹配的 Finalize 请求会在工具发现之前拒绝，不能降级成普通 Agent 执行。再次空输出或超时时会刷新原 session 的 journal 与对话记忆并继续保留 checkpoint；成功后旧 session checkpoint 会退役，因为旁路生成的最终回答并不存在于旧 Framework session 中，后续轮次应从包含新答案的可见历史创建新 session。

只更新 journal 不会使旧执行会话重新变得可恢复。`CopilotAgentSessionCheckpoint.SessionResumeRestriction` 将未知工具结果或未闭合的 Provider 工具调用绑定到保留的 `SerializedSessionJson`；`CopyWithOutcome` 及其 journal 复制入口在替换 journal 前固化该限制，JSON 存储、快照、等价判断和 journal 裁剪继续保留它。非法限制值拒绝恢复。连续失败的 Finalize 不能靠新增无工具 run 清除限制；真正重新规划并创建新 Session 时不继承旧标记，也不会因历史 journal 中仍有未知结果而永久禁止恢复。`CopilotFinalAnswerRecoverySafetyTests` 覆盖实际恢复入口、两次失败、保存重开、复制与新 Session 对照，同时确认 Finalize 不发现或执行工具。本修复不自动迁移旧版本已经丢失会话关联限制的历史记录；这类记录仍需重新规划，不能根据缺失标记断言原操作安全。

`CopilotTokenBudgetChatClient` 基于官方 `DelegatingChatClient` 中间件包装真实模型客户端，累计同一 Agent 请求内所有供应商调用的 usage。当已观测用量达到有效请求预算时，中间件在下一次供应商调用前抛出受控的 `CopilotAgentTokenBudgetExceededException`，由 Runtime 转成确定性的结束提示，不会再次调用模型或重放工具。供应商不返回 usage 时使用字符数近似，并在诊断中标记 `includes estimates`。这个预算是跨调用循环闸门；单个供应商响应可能使最终统计略微超过阈值。

同一次流式响应的 usage 更新通过 `CopilotTokenUsage.MergeProgress` 合并，各字段保留已观测最大值，总量还须覆盖合并后的输入与输出之和；后续片段省略或降低 total 不会让用量倒退、重新开放已耗尽的预算。不同供应商调用之间仍使用 `Add` 累加。`CopilotTokenUsageMergeTests` 覆盖不完整 usage、溢出饱和、缓存计数，以及通过 Framework 流式 usage 验证下一次调用在到达供应商前被拦截。

非流式响应通过 `ExtractResponseUsage` 读取 `ChatResponse.Usage`，并兼容消息内的 `UsageContent`；同一响应的两种表示单调合并，不能重复计数。预算中间件、最终总结和 `Finalize` 使用同一读取逻辑，不把已经返回的正式用量降为字符估算；输入、输出和缓存用量仍按不同调用累加。`CopilotNonStreamingUsageTests` 使用安装版本的官方 OpenAI Chat Completions／Responses 适配器和受控 HTTP 响应，验证预算拦截、重复表示去重、跨调用累计及完整／截断／内容过滤终态中的用量和检查点，不访问真实供应商。

总时长由与调用方取消令牌链接的运行级计时器约束。超时或业务工具越界都会返回结构化 `BudgetExhausted` 结果，并在可能时先完成任务账本和 Session 检查点；用户主动暂停或取消的语义优先于同时发生的超时。最终 Token、供应商调用、工具调用、pass 上限、已用时长、是否使用估算以及具体预算耗尽类型都会作为 `RuntimeDiagnostic` 和 `CopilotAgentBudgetSnapshot` 写入执行记录。

无工具最终总结仍属于同一次运行，也使用同一取消和总时长结算路径。总结被 `RunControl` 暂停／取消或总时长超时打断时，不把已有工具记录、用量和预算重置为空；继续完成账本与检查点收尾，并将最终回答标为未完成。否则预算已发布后的空结果会违反 Turn 生命周期的单调性校验，把可解释的停止错误转成执行故障。未提供 `RunControl` 的集成调用仅取消 caller token 时，仍按原契约向调用方抛出 `OperationCanceledException`，不伪装为总时长耗尽。`CopilotFinalAnswerCancellationTests` 用受控 Provider 与取消源验证已发生工具调用、用量、预算、检查点和 Turn reducer 的一致性。

## 原生任务账本与 plan/execute

Harness 的 `TodoProvider` 和 `AgentModeProvider` 现在作为标准运行时能力直接启用，不再由 ColorVision 维护第二套计划状态：

- 模型通过框架原生 `todos_add`、`todos_complete`、`todos_remove` 和查询工具维护任务；任务保存在 `AgentSessionStateBag`，随会话检查点一起持久化。
- 新会话默认进入 `execute`；模型在确实需要用户做关键选择时可切换到 `plan`。模式同样属于 Session 状态。
- `TodoCompletionLoopEvaluator` 只在 `execute` 模式驱动后续 Agent pass。只要还有未完成任务，Harness 会把剩余清单作为反馈再次调用 Agent；pass 数、工具调用、请求 Token 和总时长都使用当前请求解析后的统一运行预算。
- 每轮结束都会生成结构化 `CopilotAgentTaskLedgerSnapshot`，并把完成数、模式和最多三个未完成标题写入 `RuntimeDiagnostic`。聊天会话因此保留可检查的任务恢复记录，同时真实状态仍以 Framework Session 为唯一数据源。
- 从检查点恢复时，会在执行记录中明确标注恢复了多少任务。未完成只读任务可继续；持久任务只代表上下文和计划，不代表执行授权。
- Todo 状态更新本身由 Framework 对同一 Session 串行化，避免并发函数调用产生重复 ID 或丢失更新。

这里直接使用 Framework 的任务提供器与完成循环，而不是实现自定义 planner。官方 Harness 说明将持久 Todo、plan/execute 模式、逐次模型调用历史和可选完成循环列为完整 Agent 脚手架的组成部分，参见 [Agent Harnesses](https://learn.microsoft.com/en-us/agent-framework/agents/harness) 与 [TodoCompletionLoopEvaluator](https://learn.microsoft.com/en-us/dotnet/api/microsoft.agents.ai.todocompletionloopevaluator?view=agent-framework-dotnet-latest)。

## 项目指令链

非 Chat 模式按受信项目根到活动文档目录的顺序发现项目指令。受信项目根优先取当前解决方案目录；未打开解决方案时才回退到活动文档目录。每个目录先选择一个共享入口，依次尝试 `AGENTS.override.md`、`AGENTS.md`、`CLAUDE.md` 和 `.claude/CLAUDE.md`；后两者是面向 Claude Code 项目的兼容回退，不会在同目录已有 AGENTS 指令时重复注入。项目根的 `.claude/rules/**/*.md` 是共享入口之外的模块化规则；根共享文档之后按规则相对路径稳定排序，再加载同目录非空的 `CLAUDE.local.md` 个人叠加层。local 即使已经选择 AGENTS 入口也会生效，适合不应提交的本机测试数据或工作流偏好，仓库应把它加入 `.gitignore`。随后才加载活动文档链中的嵌套共享与 local 文档，因此越靠近活动文档的内容越晚出现。文档数量或字符预算不足时，选择阶段先保留活动文档链中最具体的文档；同一作用域按 local、共享或匹配路径的规则、无条件规则决定保留顺序，序列化时仍保持从外层到内层、共享到规则再到 local 的可解释顺序。

### /init 创建项目指令

`CopilotProjectInitialization` 将 `/init` 解释为有界的真实 Agent 请求，不是直接写模板文件。它先规范化现有工作区根，并调用共享指令发现逻辑检查 `AGENTS.override.md`、`AGENTS.md`、配置的 fallback 文件及 `CLAUDE.md` / `.claude/CLAUDE.md`；已有同名文件系统项（包括空文件）阻止初始化，避免覆盖或遮蔽旧指令。

请求要求先读项目结构和必要文档，只以一次 `PreviewWorkspacePatchEnvelope` 提议根级 `AGENTS.md` 的单一 add，再将原 change set 交给 `ApplyWorkspacePatchEnvelope`。应用仍服从当前原生审批策略；请求来源声明或指令正文不提供授权。应用前还需复查共享指令候选和目标不存在，不能将竞态降格为覆盖。内容只保留证据支持的长期项目事实、架构边界、PowerShell 构建/测试入口与完成标准，不写凭据、个人绝对路径、生成清单或临时分支状态。本初始化任务不执行构建/测试；完成后仍需用户审阅生成的规则。

### 搜索根不等于受信项目根

显式本地文件和文件附件仍可增加只读搜索根，让 Agent 在用户要求时读取对应文件；它们不会自动成为受信项目根，因此相邻的 `AGENTS.md`、`CLAUDE.md`、`.claude/rules` 和 `.agents/skills` 不会进入模型上下文。这个分离与 Codex 只从受信项目层加载项目配置的边界，以及 Claude Code“附加目录授予文件访问、默认不加载 CLAUDE.md/rules”的行为一致。`/permissions` 会分别显示搜索根和受信项目根，便于确认来源。

### 路径规则与指令预算

rules 不含 `paths` frontmatter 时无条件适用于该受信项目根；存在 `paths` 时，只有活动文档、用户文本中的显式本地文件或文件附件在请求开始前已知、位于受信项目根内，且其根内相对路径匹配至少一个 glob 时才加载。支持 `*`、`?`、`**` 和 `{ts,tsx}` 一类花括号展开；单个规则最多 16 个模式，每个模式最多 256 个字符。绝对路径、父目录段、否定模式、控制字符、空段、重复或畸形 `paths` 会让该规则失败关闭，而不是意外变成全局规则。匹配使用 Windows 不区分大小写语义，frontmatter 只供宿主筛选，进入模型前会移除。ColorVision 当前在构造固定请求上下文时完成匹配，不会因为本轮工具稍后发现了一个此前未知文件而热注入新规则；后续新请求会按当时目标重新发现。

单份原始文件最多有界读取 32,768 个字符，注入正文最多 12,000 个字符，全部正文合计最多 24,000 个字符和 4 份文档。rules 遍历最多检查 64 个目录、每目录前 256 个条目和 64 份 Markdown，frontmatter 最多读取 8,192 个字符；规则子目录、文件或 `.claude` 链上的重解析点都跳过。宿主在序列化为作用域 JSONL 后再次施加 32,768 字符硬上限，因此目录规模、路径、换行和反斜杠转义不能绕过预算。超限的最后一份文档会在保持完整 JSON 行的前提下缩短并标记 `IsTruncated`，不会截出损坏 JSON。

为减少只面向维护者的上下文噪声，文件中代码围栏之外的 `<!-- HTML comments -->` 会在脱敏和截断前剥离；围栏内的示例原样保留。共享入口、rules 和 local 叠加层都不会展开 Claude Code 的 `@path` 导入，因此不会因为仓库指令静默扩大文件读取范围。`CLAUDE.local.md` 与 rules 使用相同的正文预算、脱敏和权限边界；“个人”或“路径匹配”都不提升可信等级。项目指令始终作为 workspace-scoped user-role 数据注入，只影响其目录范围内的行为，不能授权写入、审批、外部副作用或越界访问。这结合了 [Codex AGENTS.md](https://learn.chatgpt.com/docs/agent-configuration/agents-md) 的根到当前目录、override 与回退语义、[Codex 项目层信任](https://learn.chatgpt.com/docs/config-file/config-advanced#project-config-files-codexconfigtoml) 的配置边界，以及 [Claude Code memory](https://code.claude.com/docs/en/memory) 和 [Claude Code additional directories](https://code.claude.com/docs/en/permissions#additional-directories-grant-file-access-not-configuration) 中项目指令与额外文件访问分离的语义。

## Agent Skills

Harness 的 `AgentSkillsProvider` 作为标准能力启用，采用渐进式加载：模型先看到技能的名称和说明，只有当前任务匹配时才调用 `load_skill` 读取完整 `SKILL.md`，随后按需读取该技能目录中的参考资料。这样可以让 ColorVision 保存稳定、可复用的诊断流程，而不必把所有领域说明长期塞进系统提示。

技能只从受信任的应用或工作区目录发现：

- 受信项目根下的 `.agents/skills/<skill-name>/SKILL.md`，适合项目级扩展和覆盖；显式文件和附件形成的额外搜索根不会贡献 Skill。
- 应用输出目录下的 `Copilot/Skills/<skill-name>/SKILL.md`，用于随 ColorVision 发布的内置技能。
- 不默认扫描用户主目录或任意外部路径；符号链接和重解析点目录不会加入技能源。

### Skill 候选与元数据预算

为避免项目 Skill 持续增长后放大每次模型调用的 L1 元数据成本，运行时在 Framework 完成一次文件发现后对候选项去重，并根据当前请求与 `name`、`description` 的中英文相关性选择最多 16 个活跃 Skill。Skill 名称和说明的合计预算按独立 Agent 上下文窗口的 2% 换算（以 4 字符约等于 1 Token 做保守估算），并保留 8,000 字符硬上限；当前默认 1,048,576 Token 窗口因此使用 8,000 字符硬上限，如用户收紧窗口，Skill 预算也会同步下降。预算不足时先按剩余候选数公平缩短 description，仍然过多时才省略低排名 Skill；缩短只影响初始目录，真正加载时仍读取完整 `SKILL.md`。用户显式点名的 Skill 具有最高优先级，同一运行的发现结果会被缓存。运行诊断会显示候选数、启用数、缩短说明数、预算省略数，以及真正被 `load_skill` 或参考资料读取路径加载的 Skill 名称。这与 Codex 初始 Skills 列表的 2% / 8,000 字符规则保持一致，而不是把 8,000 当作每次都应该占满的目标。

### Skill 使用统计和历史降级

运行结束后，宿主只把“本轮被选择”、“本轮实际加载”和“连续入选但未加载”计数写入本地有界状态 `Copilot/State/skill-usage.json`。状态最多保留 128 个名称，单文件上限 1 MiB，损坏或超限时安全重建；不保存提示正文、Skill 内容或用户问题。连续至少 20 次被选择但未加载的 Skill 会自动降级为 explicit-only，不再占用后续请求的默认 Skill 元数据预算；这也能识别“历史上曾用过一次，但后来长期不再有效”的 Skill。用户在当前问题中以 `$skill-name` 或完整 Skill 名称直接点名时仍可加载；一次真实加载会把连续未加载计数清零，使该 Skill 重新参与隐式匹配，之后若再次连续低效仍会重新降级。Schema 1 的旧统计会保守迁移：从未加载的历史保留已有降级结论，无法推断连续性的已加载历史从零重新取样。独立 Agent 设置页显示选择次数、加载次数、加载率、连续未加载次数和当前历史降级状态。

### 作者策略与用户覆盖

Skill 作者也可以在同目录的 `agents/openai.yaml` 中设置 `policy.allow_implicit_invocation: false`，长期保持 explicit-only；用户直接点名时仍可使用。运行时只读取 Skill 目录内、不经过重解析点且不超过 32 KiB 的该策略文件。作者策略优先于历史统计，真实加载不会把作者声明改回隐式匹配。两类 explicit-only 都只是从默认上下文中移除，宿主不会自动删除 Skill 文件、执行脚本或扩大业务工具授权，因此清理是可逆且可审计的。

用户还可以在独立 Agent 设置页为具体 Skill 配置覆盖状态。`Auto` 使用作者策略和连续未加载证据；`Name only` 只向模型公开名称，用一个不可见的单字符 description 满足 Agent Framework 的非空校验，原说明和完整正文都不会进入初始目录；`Explicit only` 只允许用户直接点名；`Off` 即使被点名也不加入模型目录。覆盖按 Skill 名称保存到 `CopilotConfig.AgentDefaults.SkillOverrides`，与模型 Profile 无关；选择 Auto 会移除持久化覆盖。Name-only 可以用极低元数据成本重新观察历史低效 Skill，作者声明的 explicit-only 仍具有更高优先级。所有状态都只改变可见性，不删除或修改 `SKILL.md`，与 [Codex 禁用但不删除 Skill](https://learn.chatgpt.com/docs/build-skills) 及 [Claude Code skillOverrides](https://code.claude.com/docs/en/slash-commands) 的原则一致。

### Skill 内容不授予执行权限

技能脚本发现与执行当前完全关闭。`load_skill` 和 `read_skill_resource` 是只读元数据操作，由 Framework 的只读规则自动批准，不在界面生成无意义的审批；技能内容本身不构成任何业务操作授权。所有 ColorVision 工具仍经过现有 Schema、风险级别、并发闸门、审计和写操作审批。

新增技能时，为目录创建 `SKILL.md`，并在 YAML frontmatter 中提供稳定的 `name` 和明确的 `description`。正文应描述何时使用、证据顺序、停止条件和安全边界；较长的清单或领域资料放进同目录的 `references/`，让 Agent 按需读取。内置的 `colorvision-flow-diagnostics` 是流程诊断示例；`colorvision-database-operations` 复用重置、导出和清理代码中的分类，把数据库组织为服务配置表、服务设置表和结果表，先验证实时 Schema，再指导通用 SQL 查询或经原生审批的数据清理。

这种结构遵循 [OpenAI Skills](https://learn.chatgpt.com/docs/build-skills) 与 [Claude Code Skills](https://code.claude.com/docs/en/slash-commands) 的渐进式披露语义，并直接使用 [Microsoft Agent Framework Agent Skills](https://learn.microsoft.com/en-us/agent-framework/agents/skills) 实现运行时加载。
